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
        public DownloadRequest(
            Uri baseUri,
            string relativePath,
            string localPath,
            string expectedSha256 = null,
            string fallbackRelativePath = null,
            string fallbackLocalPath = null,
            string fallbackExpectedSha256 = null)
        {
            BaseUri = baseUri;
            RelativePath = NormalizeRelativePath(relativePath);
            LocalPath = Path.GetFullPath(localPath);
            ExpectedSha256 = NormalizeHash(expectedSha256);
            FallbackRelativePath = NormalizeRelativePath(fallbackRelativePath);
            FallbackLocalPath = string.IsNullOrWhiteSpace(fallbackLocalPath) ? string.Empty : Path.GetFullPath(fallbackLocalPath);
            FallbackExpectedSha256 = NormalizeHash(fallbackExpectedSha256);
        }

        public Uri BaseUri { get; }
        public string RelativePath { get; }
        public string LocalPath { get; }
        public string ExpectedSha256 { get; }
        public string FallbackRelativePath { get; }
        public string FallbackLocalPath { get; }
        public string FallbackExpectedSha256 { get; }
        public bool HasFallback => !string.IsNullOrWhiteSpace(FallbackRelativePath) && !string.IsNullOrWhiteSpace(FallbackLocalPath);

        public string DisplayName =>
            string.IsNullOrWhiteSpace(RelativePath)
                ? Path.GetFileName(LocalPath)
                : RelativePath;

        public bool TryGetFallbackRequest(out DownloadRequest request)
        {
            request = null;
            if (!HasFallback)
                return false;

            request = new DownloadRequest(BaseUri, FallbackRelativePath, FallbackLocalPath, FallbackExpectedSha256);
            return true;
        }

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

        private sealed class DownloadException : Exception
        {
            public DownloadException(Uri fileUri, long responseCode, string error)
                : base($"Failed to download {fileUri}: {error}")
            {
                ResponseCode = responseCode;
            }

            public long ResponseCode { get; }
        }

        public static void DownloadAsync(
            IEnumerable<DownloadRequest> requests,
            string label,
            Action<DownloadBatchResult> onSuccess = null,
            Action<DownloadBatchResult> onFailure = null)
        {
            DownloadAsyncInternal(requests, label, onSuccess, onFailure).Forget();
        }

        private static async UniTask DownloadAsyncInternal(
            IEnumerable<DownloadRequest> requests,
            string label,
            Action<DownloadBatchResult> onSuccess,
            Action<DownloadBatchResult> onFailure)
        {
            DownloadBatchResult result;
            try
            {
                //result = new DownloadBatchResult(0, 0, new List<DownloadFailure>());
                result = await DownloadAsyncCore(requests, label);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                result = new DownloadBatchResult(0, 0, new[] { new DownloadFailure(string.Empty, ex.Message) });
            }

            try
            {
                if (result.Success)
                    onSuccess?.Invoke(result);
                else
                    onFailure?.Invoke(result);
            }
            catch (Exception callbackEx)
            {
                Debug.LogException(callbackEx);
            }
        }

        private static async UniTask<DownloadBatchResult> DownloadAsyncCore(IEnumerable<DownloadRequest> requests, string label)
        {
            await _batchGate.WaitAsync();
            try
            {
                var cleanedRequests = await UniTask.RunOnThreadPool(() => NormalizeRequests(requests));
                if (cleanedRequests.Count == 0)
                {
                    PublishProgress(DownloadProgressState.Hidden());
                    return new DownloadBatchResult(0, 0, Array.Empty<DownloadFailure>());
                }

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
                var normalizedRequest = new DownloadRequest(
                    request.BaseUri,
                    request.RelativePath,
                    localPath,
                    request.ExpectedSha256,
                    request.FallbackRelativePath,
                    request.FallbackLocalPath,
                    request.FallbackExpectedSha256);

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
                try
                {
                    await DownloadOneCoreAsync(request);
                    return;
                }
                catch (Exception firstFailure)
                {
                    if (TryBuildFallbackRequest(request, firstFailure, out var fallbackRequest))
                    {
                        TryDelete(request.LocalPath);

                        try
                        {
                            await DownloadOneCoreAsync(fallbackRequest);
                            return;
                        }
                        catch (Exception fallbackFailure)
                        {
                            request = fallbackRequest;
                            TryDelete(request.LocalPath);
                            firstFailure = fallbackFailure;
                        }
                    }

                    TryDelete(request.LocalPath);

                    lock (failures)
                    {
                        failures.Add(new DownloadFailure(request.LocalPath, firstFailure.Message));
                    }

                    Debug.LogWarning(firstFailure);
                }
            }
            finally
            {
                onFinished?.Invoke();
                _downloadGate.Release();
            }
        }

        private static async UniTask DownloadOneCoreAsync(DownloadRequest request)
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

                await www.SendWebRequest();
                /*while (!www.isDone)
                {
                    await UniTask.Yield();
                }*/

                if (www.result != UnityWebRequest.Result.Success)
                    throw new DownloadException(fileUri, www.responseCode, www.error);

                if (!string.IsNullOrWhiteSpace(request.ExpectedSha256)
                    && !HashMatches(request.LocalPath, request.ExpectedSha256))
                {
                    throw new Exception($"Downloaded file hash mismatch for {fileUri}");
                }
            }
        }

        private static bool TryBuildFallbackRequest(DownloadRequest request, Exception failure, out DownloadRequest fallbackRequest)
        {
            fallbackRequest = null;
            if (request == null || failure == null)
                return false;

            if (!request.RelativePath.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                return false;

            if (failure is not DownloadException downloadException || downloadException.ResponseCode != 404)
                return false;

            return request.TryGetFallbackRequest(out fallbackRequest);
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
