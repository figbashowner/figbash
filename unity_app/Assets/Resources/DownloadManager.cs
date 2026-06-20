using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets
{
    public sealed class DownloadRequest
    {
        public DownloadRequest(Uri baseUri, string relativePath, string localPath, string expectedSha256 = null)
        {
            BaseUri = baseUri;
            RelativePath = NormalizeRelativePath(relativePath);
            LocalPath = Path.GetFullPath(localPath);
            ExpectedSha256 = NormalizeHash(expectedSha256);
        }

        public Uri BaseUri { get; }
        public string RelativePath { get; }
        public string LocalPath { get; }
        public string ExpectedSha256 { get; }

        public string DisplayName =>
            string.IsNullOrWhiteSpace(RelativePath)
                ? Path.GetFileName(LocalPath)
                : RelativePath;

        private static string NormalizeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.Replace('\\', '/').TrimStart('/');
        }

        private static string NormalizeHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;

            return hash.Trim().ToLowerInvariant();
        }
    }

    public sealed class DownloadFailure
    {
        public DownloadFailure(string localPath, string error)
        {
            LocalPath = localPath;
            Error = error;
        }

        public string LocalPath { get; }
        public string Error { get; }
    }

    public sealed class DownloadBatchResult
    {
        public DownloadBatchResult(int requestedCount, int processedCount, IReadOnlyList<DownloadFailure> failures)
        {
            RequestedCount = requestedCount;
            ProcessedCount = processedCount;
            Failures = failures ?? Array.Empty<DownloadFailure>();
        }

        public int RequestedCount { get; }
        public int ProcessedCount { get; }
        public IReadOnlyList<DownloadFailure> Failures { get; }
        public bool Success => Failures.Count == 0;
    }

    public readonly struct DownloadProgressState
    {
        public DownloadProgressState(string label, int completed, int total)
        {
            Label = label ?? string.Empty;
            Completed = completed;
            Total = total;
        }

        public string Label { get; }
        public int Completed { get; }
        public int Total { get; }
        public bool Visible => Total > 0;

        public static DownloadProgressState Hidden() => new DownloadProgressState(string.Empty, 0, 0);
    }

    internal static class DownloadManager
    {
        private static readonly SemaphoreSlim _batchGate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _downloadGate = new SemaphoreSlim(2, 2);

        public static event Action<DownloadProgressState> ProgressChanged;

        public static async UniTask<DownloadBatchResult> DownloadAsync(IEnumerable<DownloadRequest> requests, string label)
        {
            var cleanedRequests = NormalizeRequests(requests);
            if (cleanedRequests.Count == 0)
            {
                PublishProgress(DownloadProgressState.Hidden());
                return new DownloadBatchResult(0, 0, Array.Empty<DownloadFailure>());
            }

            await _batchGate.WaitAsync();
            try
            {
                var completed = 0;
                var failures = new List<DownloadFailure>();
                var progressLabel = string.IsNullOrWhiteSpace(label) ? "Downloading" : label;

                PublishProgress(new DownloadProgressState(progressLabel, completed, cleanedRequests.Count));

                var tasks = cleanedRequests
                    .Select(request => DownloadOneAsync(request, failures, () =>
                    {
                        completed++;
                        PublishProgress(new DownloadProgressState(progressLabel, completed, cleanedRequests.Count));
                    }))
                    .ToArray();

                await UniTask.WhenAll(tasks);
                PublishProgress(DownloadProgressState.Hidden());
                return new DownloadBatchResult(cleanedRequests.Count, completed, failures);
            }
            finally
            {
                _batchGate.Release();
            }
        }

        private static List<DownloadRequest> NormalizeRequests(IEnumerable<DownloadRequest> requests)
        {
            var normalized = new List<DownloadRequest>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (requests == null)
                return normalized;

            foreach (var request in requests)
            {
                if (request == null || string.IsNullOrWhiteSpace(request.LocalPath))
                    continue;

                var localPath = Path.GetFullPath(request.LocalPath);
                var normalizedRequest = new DownloadRequest(request.BaseUri, request.RelativePath, localPath, request.ExpectedSha256);

                if (File.Exists(localPath))
                {
                    if (string.IsNullOrWhiteSpace(normalizedRequest.ExpectedSha256)
                        || HashMatches(localPath, normalizedRequest.ExpectedSha256))
                    {
                        continue;
                    }
                }

                if (!seen.Add(localPath))
                    continue;

                normalized.Add(normalizedRequest);
            }

            return normalized;
        }

        private static async UniTask DownloadOneAsync(DownloadRequest request, List<DownloadFailure> failures, Action onFinished)
        {
            await _downloadGate.WaitAsync();
            try
            {
                if (File.Exists(request.LocalPath))
                {
                    if (string.IsNullOrWhiteSpace(request.ExpectedSha256) || HashMatches(request.LocalPath, request.ExpectedSha256))
                        return;

                    TryDelete(request.LocalPath);
                }

                if (request.BaseUri == null)
                    throw new InvalidOperationException($"No source URI is available for {request.LocalPath}");

                var localDirectory = Path.GetDirectoryName(request.LocalPath);
                if (!string.IsNullOrWhiteSpace(localDirectory))
                    Directory.CreateDirectory(localDirectory);

                var fileUri = new Uri(request.BaseUri, request.RelativePath);
                using (var www = UnityWebRequest.Get(fileUri.AbsoluteUri))
                {
                    var downloadHandler = new DownloadHandlerFile(request.LocalPath);
                    downloadHandler.removeFileOnAbort = true;
                    www.downloadHandler = downloadHandler;

                    www.SendWebRequest();
                    while (!www.isDone)
                    {
                        await UniTask.Yield();
                    }

                    if (www.result != UnityWebRequest.Result.Success)
                        throw new Exception($"Failed to download {fileUri}: {www.error}");

                    if (!string.IsNullOrWhiteSpace(request.ExpectedSha256)
                        && !HashMatches(request.LocalPath, request.ExpectedSha256))
                    {
                        throw new Exception($"Downloaded file hash mismatch for {fileUri}");
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(request.LocalPath))
                        File.Delete(request.LocalPath);
                }
                catch
                {
                    // Ignore cleanup failures.
                }

                lock (failures)
                {
                    failures.Add(new DownloadFailure(request.LocalPath, ex.Message));
                }

                Debug.LogWarning(ex);
            }
            finally
            {
                onFinished?.Invoke();
                _downloadGate.Release();
            }
        }

        private static bool HashMatches(string localPath, string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(localPath) || string.IsNullOrWhiteSpace(expectedSha256) || !File.Exists(localPath))
                return false;

            try
            {
                var actualSha256 = ComputeSha256Hex(localPath);
                return string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeSha256Hex(string localPath)
        {
            using (var stream = File.OpenRead(localPath))
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void TryDelete(string localPath)
        {
            try
            {
                if (File.Exists(localPath))
                    File.Delete(localPath);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        private static void PublishProgress(DownloadProgressState state)
        {
            ProgressChanged?.Invoke(state);
        }
    }
}
