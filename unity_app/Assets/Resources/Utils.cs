using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets
{
    internal class Utils
    {
        public static string GetUiSidecarName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (fileName.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                return System.IO.Path.GetFileName(fileName);

            if (!fileName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
                return null;

            return System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(fileName), ".ui.stl");
        }

        public static string GetUiSidecarPath(string stlPath)
        {
            if (string.IsNullOrWhiteSpace(stlPath))
                return null;

            if (stlPath.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                return stlPath;

            return System.IO.Path.ChangeExtension(stlPath, ".ui.stl");
        }

        public static string MakePortablePath(string path, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return path.Trim();

            var normalizedRoot = NormalizePath(rootPath);
            if (!string.IsNullOrWhiteSpace(normalizedRoot))
            {
                var relativePath = TryMakeRelativePath(normalizedRoot, normalizedPath);
                if (!string.IsNullOrWhiteSpace(relativePath))
                    return relativePath;
            }

            return normalizedPath;
        }

        public static string ResolvePortablePath(string path, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var trimmedPath = path.Trim();
            if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    return uri.AbsoluteUri;

                if (uri.IsFile)
                    trimmedPath = uri.LocalPath;
            }

            var normalizedRoot = NormalizePath(rootPath);
            if (string.IsNullOrWhiteSpace(normalizedRoot))
                return NormalizePath(trimmedPath);

            if (!System.IO.Path.IsPathRooted(trimmedPath))
                return NormalizePath(System.IO.Path.Combine(normalizedRoot, trimmedPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

            var normalizedPath = NormalizePath(trimmedPath);
            if (System.IO.File.Exists(normalizedPath) || System.IO.Directory.Exists(normalizedPath))
                return normalizedPath;

            var remappedPath = TryRemapLegacyPath(normalizedPath, normalizedRoot);
            return string.IsNullOrWhiteSpace(remappedPath) ? normalizedPath : remappedPath;
        }

        public static string MakePortableRepositorySource(string source, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(source))
                return source;

            var trimmedSource = source.Trim();
            if (Uri.TryCreate(trimmedSource, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    return uri.AbsoluteUri.TrimEnd('/');

                if (uri.IsFile)
                    trimmedSource = uri.LocalPath;
            }

            return MakePortablePath(trimmedSource, rootPath);
        }

        public static string ResolvePortableRepositorySource(string source, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(source))
                return source;

            var trimmedSource = source.Trim();
            if (Uri.TryCreate(trimmedSource, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    return uri.AbsoluteUri.TrimEnd('/');

                if (uri.IsFile)
                    trimmedSource = uri.LocalPath;
            }

            return ResolvePortablePath(trimmedSource, rootPath);
        }

        private static string TryMakeRelativePath(string rootPath, string path)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var normalizedRoot = NormalizePath(rootPath).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalizedRoot) || string.IsNullOrWhiteSpace(normalizedPath))
                return string.Empty;

            var rootPrefix = normalizedRoot + System.IO.Path.DirectorySeparatorChar;
            if (!string.Equals(normalizedRoot, normalizedPath, StringComparison.OrdinalIgnoreCase)
                && !normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var relativePath = System.IO.Path.GetRelativePath(normalizedRoot, normalizedPath);
            if (relativePath == ".")
                return System.IO.Path.GetFileName(normalizedPath);

            return relativePath;
        }

        private static string TryRemapLegacyPath(string path, string rootPath)
        {
            var pathSegments = GetPathSegments(path);
            var rootSegments = GetPathSegments(rootPath);
            if (pathSegments.Count == 0 || rootSegments.Count == 0)
                return string.Empty;

            var suffixLength = Math.Min(3, rootSegments.Count);
            var rootSuffix = rootSegments.Skip(rootSegments.Count - suffixLength).ToArray();
            if (rootSuffix.Length == 0)
                return string.Empty;

            for (var i = 0; i <= pathSegments.Count - suffixLength; i++)
            {
                var matches = true;
                for (var j = 0; j < suffixLength; j++)
                {
                    if (!string.Equals(pathSegments[i + j], rootSuffix[j], StringComparison.OrdinalIgnoreCase))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                    continue;

                var remainder = pathSegments.Skip(i + suffixLength).ToArray();
                if (remainder.Length == 0)
                    return rootPath;

                return System.IO.Path.Combine(new[] { rootPath }.Concat(remainder).ToArray());
            }

            return string.Empty;
        }

        private static List<string> GetPathSegments(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return new List<string>();

            var pathRoot = System.IO.Path.GetPathRoot(normalizedPath);
            if (!string.IsNullOrWhiteSpace(pathRoot) && normalizedPath.StartsWith(pathRoot, StringComparison.OrdinalIgnoreCase))
                normalizedPath = normalizedPath.Substring(pathRoot.Length);

            return normalizedPath
                .Split(new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return System.IO.Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }

        public static void PairUiSidecars(Folder folder)
        {
            if (folder == null)
                return;

            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    PairUiSidecars(subdir);
                }
            }

            if (folder.Files == null || folder.Files.Count == 0)
                return;

            var fileNames = folder.Files
                .Where(file => file != null && !string.IsNullOrWhiteSpace(file.Name))
                .ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var file in folder.Files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.Name))
                    continue;

                if (file.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(file.UiName))
                    continue;

                var uiName = GetUiSidecarName(file.Name);
                if (!string.IsNullOrWhiteSpace(uiName) && fileNames.ContainsKey(uiName))
                {
                    file.UiName = uiName;
                    continue;
                }

                var uiPath = GetUiSidecarPath(file.FullPath);
                if (!string.IsNullOrWhiteSpace(uiPath)
                    && System.IO.Path.IsPathRooted(file.FullPath)
                    && System.IO.File.Exists(uiPath))
                {
                    file.UiName = System.IO.Path.GetFileName(uiPath);
                }
            }
        }

        public static float makePercent (float baseSize, float value, int digits = 1)
        {
            var val = MathF.Round((value / baseSize) * 100, digits);
            if (float.IsInfinity(val) || float.IsNaN(val))
                throw new Exception("something went wrong with the percentages...");
            return val;
        }
        public static void UpdatePositionRelativeToOriginalSize(StlFile stl)
        {
            if (stl.originalSize == Vector3.zero || stl.Transforms == null)
            { 
                return; 
            } 
            stl.Transforms.PositionRelativeToOriginalSize = new float[] 
            { 
                makePercent(stl.originalSize[0], reversePercentage(uiEvents.BaseSize[0], stl.Transforms.Position[0]), 5),
                makePercent(stl.originalSize[1], reversePercentage(uiEvents.BaseSize[1], stl.Transforms.Position[1]), 5),
                makePercent(stl.originalSize[2], reversePercentage(uiEvents.BaseSize[2], stl.Transforms.Position[2]), 5)
            };
        }
        public static float reversePercentage(float baseSize, float value)
        {
            return (value / 100) * baseSize;
        }
    }
}
