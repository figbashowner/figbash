using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static stlImport;
using UnityEngine.UIElements;
using Unity.VisualScripting;
using System.Net.Http.Headers;
using UnityEngine;
using Unity.Properties;
using Newtonsoft.Json;

namespace Assets
{
    internal class DataManager 
    {
        [Serializable]
        public sealed class RepositorySummary
        {
            public string Name;
            public string Source;
        }

        private class Repository
        {
            public string Source;
            public string CacheRoot;
            public Folder CatalogRoot;

            public bool TryCreateDownloadRequest(string fullPath, out DownloadRequest request)
            {
                request = null;

                if (string.IsNullOrWhiteSpace(Source)
                    || string.IsNullOrWhiteSpace(CacheRoot)
                    || string.IsNullOrWhiteSpace(fullPath))
                {
                    return false;
                }

                if (!Uri.TryCreate(Source, UriKind.Absolute, out var sourceUri))
                    return false;

                if (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps)
                    return false;

                var normalizedRoot = Path.GetFullPath(CacheRoot);
                var normalizedPath = Path.GetFullPath(fullPath);
                if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    return false;

                var relativePath = Path.GetRelativePath(normalizedRoot, normalizedPath).Replace('\\', '/');
                if (!TryResolveCatalogEntry(normalizedPath, out var catalogFile, out var isUiSidecar))
                    return false;

                var expectedHash = isUiSidecar ? catalogFile.UiHash : catalogFile.Hash;
                request = new DownloadRequest(sourceUri, relativePath, normalizedPath, expectedHash);
                return true;
            }

            private bool TryResolveCatalogEntry(string normalizedPath, out StlFile file, out bool isUiSidecar)
            {
                file = null;
                isUiSidecar = false;
                return TryResolveCatalogEntry(CatalogRoot, normalizedPath, out file, out isUiSidecar);
            }

            private bool TryResolveCatalogEntry(Folder folder, string normalizedPath, out StlFile file, out bool isUiSidecar)
            {
                file = null;
                isUiSidecar = false;

                if (folder == null)
                    return false;

                if (folder.Files != null)
                {
                    foreach (var candidate in folder.Files)
                    {
                        if (candidate == null || string.IsNullOrWhiteSpace(candidate.FullPath))
                            continue;

                        if (PathsEqual(candidate.FullPath, normalizedPath))
                        {
                            file = candidate;
                            return true;
                        }

                        var candidateUiPath = candidate.UiPath;
                        if (!string.IsNullOrWhiteSpace(candidateUiPath) && PathsEqual(candidateUiPath, normalizedPath))
                        {
                            file = candidate;
                            isUiSidecar = true;
                            return true;
                        }
                    }
                }

                if (folder.Subdirs == null)
                    return false;

                foreach (var subdir in folder.Subdirs)
                {
                    if (TryResolveCatalogEntry(subdir, normalizedPath, out file, out isUiSidecar))
                        return true;
                }

                return false;
            }

            private static bool PathsEqual(string left, string right)
            {
                if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                    return false;

                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
        }
        private static readonly DataManager _instance= new DataManager();
        private int _nextTreeIndex = 0;
        private readonly List<Repository> _repositories = new List<Repository>();

        public static DataManager Instance
        {
            get { return _instance; }
        }

        public delegate void DataChanged();
        public event DataChanged OnDataChanged;

        public delegate void AppliedChanged();
        public event AppliedChanged OnAppliedChanged;
        public List<TreeViewItemData<ITreeItem>> StlTree { get; private set; } = new List<TreeViewItemData<ITreeItem>>();
        public List<ITreeItem> AllClears { get; private set; } = new List<ITreeItem>();
        public List<StlFile> AllApplied { get; private set; } = new List<StlFile>();


        private DataManager() { }
        
        public static List<string> hiddenFiles = new List<string>() 
        { 
            "base.stl"
            , "base.ui.stl"
            , "cuts_2.75_0.25.stl"
            , "cuts_3.75_0.25.stl", "cuts_3.75_0.30.stl", "cuts_3.75_0.35.stl"
            , "cuts_6_0.25.stl", "cuts_6_0.30.stl", "cuts_6_0.35.stl"
            , "cuts_9_0.25.stl", "cuts_9_0.30.stl", "cuts_9_0.35.stl" 
        };

        public void Load(string path)

        {
            _nextTreeIndex = 0;
            int id = 0;
            StlTree = GetFolderList(path, ref id);
            _nextTreeIndex = id;
            AllClears = GetClears(path);
            if (OnDataChanged != null)
                OnDataChanged();
            ApplyObject(StlTree.FirstOrDefault(f => f.data.Name == "base.stl").data as StlFile);
        }
        private static List<TreeViewItemData<ITreeItem>> GetFolderList(string path, ref int id)
        {
            List<TreeViewItemData<ITreeItem>> allChildren = new List<TreeViewItemData<ITreeItem>>();
            var dinfo = new DirectoryInfo(path);
            if (dinfo.Exists)
            {
                foreach (var subdir in dinfo.EnumerateDirectories())
                {
                    if (subdir.Name == "tempClears" || subdir.Name == "base" || subdir.Name == "repos")
                        continue;
                    allChildren.Add(new TreeViewItemData<ITreeItem>(++id, new Folder()
                    {
                        Name = subdir.Name,
                        FullPath = subdir.FullName,
                    }, GetFolderList(Path.Combine(path, subdir.Name), ref id))); ;
                }
            }
            foreach (var subfile in dinfo.EnumerateFiles())
            {
                if (subfile.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (subfile.Extension != ".stl" || subfile.Name.StartsWith("cuts_"))
                    continue;

                var uiPath = Utils.GetUiSidecarPath(subfile.FullName);
                allChildren.Add(new TreeViewItemData<ITreeItem>(++id, new StlFile()
                {
                    Name = subfile.Name,
                    Selected = (subfile.Name == "base.stl"),
                    SelectionCanChange = (subfile.Name != "base.stl"),
                    FullPath = subfile.FullName,
                    UiName = File.Exists(uiPath) ? Path.GetFileName(uiPath) : null,
                }));
            }
            return allChildren;
        }

        private static List<TreeViewItemData<ITreeItem>> GetFolderList(Folder folder, ref int id)
        {
            List<TreeViewItemData<ITreeItem>> allChildren = new List<TreeViewItemData<ITreeItem>>();
            if (folder == null)
                return allChildren;

            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    allChildren.Add(new TreeViewItemData<ITreeItem>(++id, subdir, GetFolderList(subdir, ref id)));
                }
            }

            if (folder.Files != null)
            {
                var fileNames = folder.Files
                    .Where(file => file != null && !string.IsNullOrWhiteSpace(file.Name))
                    .ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var subfile in folder.Files)
                {
                    if (subfile == null || string.IsNullOrWhiteSpace(subfile.Name))
                        continue;

                    if (subfile.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(subfile.UiName))
                    {
                        var uiName = Utils.GetUiSidecarName(subfile.Name);
                        if (!string.IsNullOrWhiteSpace(uiName) && fileNames.ContainsKey(uiName))
                        {
                            subfile.UiName = uiName;
                        }
                    }

                    allChildren.Add(new TreeViewItemData<ITreeItem>(++id, subfile));
                }
            }

            return allChildren;
        }
        /*
        private static convertStlFileToTreeItemView(ITreeItem item, ref int id)
        {
            if (item as StlFile)
            {
                return new TreeViewItemData<ITreeItem>(++id, new Folder()
                {
                    Name = item.Name,
                    FullPath = item.FullPath,
                };
            }
            else if (item is Folder)
            {
                return;
            }
        }
        */
        public static List<ITreeItem> GetClears(string path)
        {
            List<ITreeItem> allChildren = new List<ITreeItem>();
            var dinfo = new DirectoryInfo(path);
            if (dinfo.Exists)
            {
                foreach (var subdir in dinfo.EnumerateDirectories())
                {
                    if (subdir.Name.StartsWith("temp") == false)
                        allChildren.AddRange(GetClears(Path.Combine(path, subdir.Name)));
                }
            }
            foreach (var subfile in dinfo.EnumerateFiles())
            {
                if (subfile.Extension != ".stl"
                    || subfile.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase)
                    || hiddenFiles.Contains(subfile.Name))
                    continue;
                if (subfile.Name.ContainsInsensitive("clear"))
                {
                    allChildren.Add(new StlFile()
                    {
                        Name = subfile.Name,
                        FullPath = subfile.FullName,
                    });
                }
            }
            return allChildren;
        }

        public static List<ITreeItem> GetClears(Folder folder)
        {
            List<ITreeItem> allChildren = new List<ITreeItem>();
            CollectClears(folder, allChildren);
            return allChildren;
        }

        private static void CollectClears(Folder folder, List<ITreeItem> allChildren)
        {
            if (folder == null)
                return;

            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    CollectClears(subdir, allChildren);
                }
            }

            if (folder.Files == null)
                return;

            foreach (var subfile in folder.Files)
            {
                if (subfile == null)
                    continue;

                if (subfile.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (hiddenFiles.Contains(subfile.Name))
                    continue;
                if (subfile.Name.ContainsInsensitive("clear"))
                {
                    allChildren.Add(subfile);
                }
            }
        }

        public void AddRepo(Folder catalogRoot)
        {
            AddRepo(catalogRoot, catalogRoot?.FullPath);
        }

        public void AddRepo(Folder catalogRoot, string source, bool notifyDataChanged = true)
        {
            if (catalogRoot == null)
                return;

            Utils.PairUiSidecars(catalogRoot);

            var normalizedSource = NormalizeRepoSource(source ?? catalogRoot.FullPath);
            AssignRepositorySource(catalogRoot, normalizedSource);

            if (!string.IsNullOrWhiteSpace(normalizedSource))
            {
                RemoveRepoBySource(normalizedSource, removeApplied: false, notifyDataChanged: false, notifyAppliedChanged: false);
            }

            _repositories.Add(new Repository()
            {
                Source = normalizedSource ?? catalogRoot.FullPath,
                CacheRoot = catalogRoot.FullPath,
                CatalogRoot = catalogRoot,
            });

            var id = _nextTreeIndex;
            var repoChildren = GetFolderList(catalogRoot, ref id);
            if (repoChildren.Count == 0)
                return;

            var repoTitle = GetRepositoryTitle(catalogRoot, normalizedSource);

            var repoRoot = new TreeViewItemData<ITreeItem>(++id, new Folder()
            {
                Name = repoTitle,
                FullPath = catalogRoot.FullPath,
            }, repoChildren);

            StlTree.Add(repoRoot);
            _nextTreeIndex = id;

            AllClears.AddRange(GetClears(catalogRoot));

            if (notifyDataChanged && OnDataChanged != null)
                OnDataChanged();
        }

        internal void NotifyDataChanged()
        {
            if (OnDataChanged != null)
                OnDataChanged();
        }

        public bool HasRepositorySource(string source)
        {
            var normalizedSource = NormalizeRepoSource(source);
            if (string.IsNullOrWhiteSpace(normalizedSource))
                return false;

            return _repositories.Any(repo => SourceMatches(repo.Source, normalizedSource));
        }

        public bool RemoveRepoBySource(string source, bool removeApplied = true)
        {
            return RemoveRepoBySource(source, removeApplied, notifyDataChanged: true, notifyAppliedChanged: true);
        }

        private bool RemoveRepoBySource(string source, bool removeApplied, bool notifyDataChanged, bool notifyAppliedChanged)
        {
            var sourceKey = NormalizeRepoSource(source);
            if (string.IsNullOrWhiteSpace(sourceKey))
                return false;

            var repoIndex = _repositories.FindIndex(repo => SourceMatches(repo.Source, sourceKey));
            if (repoIndex < 0)
                return false;

            var repo = _repositories[repoIndex];
            _repositories.RemoveAt(repoIndex);

            RemoveRepoTreeRoot(repo);
            RemoveRepoClears(repo);

            if (removeApplied)
            {
                RemoveRepoAppliedObjects(repo, notifyAppliedChanged);
            }

            if (notifyDataChanged && OnDataChanged != null)
                OnDataChanged();

            return true;
        }

        private void RemoveRepoTreeRoot(Repository repo)
        {
            if (repo?.CatalogRoot == null || StlTree == null)
                return;

            var index = StlTree.FindIndex(item => item.data is Folder folder && PathsEqual(folder.FullPath, repo.CatalogRoot.FullPath));
            if (index >= 0)
            {
                StlTree.RemoveAt(index);
            }
        }

        private void RemoveRepoClears(Repository repo)
        {
            if (repo?.CatalogRoot == null)
                return;

            var repoClears = new List<ITreeItem>();
            CollectClears(repo.CatalogRoot, repoClears);
            if (repoClears.Count == 0)
                return;

            AllClears.RemoveAll(c =>
                repoClears.Any(repoClear => repoClear != null && PathsEqual(repoClear.FullPath, c?.FullPath)));
        }

        private void RemoveRepoAppliedObjects(Repository repo, bool notifyAppliedChanged)
        {
            if (repo?.CatalogRoot == null)
                return;

            var repoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectRepoPaths(repo.CatalogRoot, repoPaths);

            var removed = AllApplied.RemoveAll(applied =>
                PathMatches(repoPaths, applied?.FullPath)
                || PathMatches(repoPaths, applied?.LoadedPath)
                || PathMatches(repoPaths, applied?.AfterClearsAppliedFullPath));

            if (removed > 0 && notifyAppliedChanged && OnAppliedChanged != null)
                OnAppliedChanged();
        }

        private static void CollectRepoPaths(Folder folder, HashSet<string> repoPaths)
        {
            if (folder == null || repoPaths == null)
                return;

            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    CollectRepoPaths(subdir, repoPaths);
                }
            }

            if (folder.Files == null)
                return;

            foreach (var file in folder.Files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.FullPath))
                    continue;

                AddPath(repoPaths, file.FullPath);
                AddPath(repoPaths, file.UiPath);
            }
        }

        private static void AddPath(HashSet<string> repoPaths, string path)
        {
            var normalizedPath = NormalizePath(path);
            if (!string.IsNullOrWhiteSpace(normalizedPath))
                repoPaths.Add(normalizedPath);
        }

        private static bool PathMatches(HashSet<string> repoPaths, string path)
        {
            if (repoPaths == null || string.IsNullOrWhiteSpace(path))
                return false;

            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return false;

            return repoPaths.Contains(normalizedPath);
        }

        private static void AssignRepositorySource(Folder folder, string source)
        {
            if (folder == null)
                return;

            if (folder.Files != null)
            {
                foreach (var file in folder.Files)
                {
                    if (file != null)
                        file.RepositorySource = source;
                }
            }

            if (folder.Subdirs == null)
                return;

            foreach (var subdir in folder.Subdirs)
            {
                AssignRepositorySource(subdir, source);
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            var leftPath = NormalizePath(left);
            var rightPath = NormalizePath(right);
            if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
                return false;

            return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }

        private static string NormalizeRepoSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            source = source.Trim();

            if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var normalized = uri.AbsoluteUri.TrimEnd('/');
                if (normalized.EndsWith("/catalog.json", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(0, normalized.Length - "/catalog.json".Length).TrimEnd('/');
                }

                if (!normalized.EndsWith("/"))
                    normalized += "/";

                return normalized;
            }

            if (Uri.TryCreate(source, UriKind.Absolute, out var fileUri) && fileUri.IsFile)
            {
                return NormalizePath(fileUri.LocalPath);
            }

            return NormalizePath(source);
        }

        private static bool SourceMatches(string left, string right)
        {
            return string.Equals(NormalizeRepoSource(left), NormalizeRepoSource(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRepositoryTitle(Folder catalogRoot)
        {
            if (catalogRoot == null)
                return "Repository";

            return string.IsNullOrWhiteSpace(catalogRoot.Name)
                ? (!string.IsNullOrWhiteSpace(catalogRoot.FullPath) ? Path.GetFileName(catalogRoot.FullPath) : "Repository")
                : catalogRoot.Name;
        }

        private static string GetRepositoryTitle(Folder catalogRoot, string source)
        {
            if (catalogRoot != null && !string.IsNullOrWhiteSpace(catalogRoot.Name))
                return catalogRoot.Name;

            if (!string.IsNullOrWhiteSpace(source))
            {
                var normalizedSource = NormalizeRepoSource(source);
                if (Uri.TryCreate(normalizedSource, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var lastSegment = uri.Segments?.LastOrDefault()?.Trim('/');
                    if (!string.IsNullOrWhiteSpace(lastSegment))
                        return lastSegment;

                    var host = uri.Host;
                    if (!string.IsNullOrWhiteSpace(host))
                        return host;
                }

                var fileName = Path.GetFileName(normalizedSource.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }

            if (catalogRoot != null && !string.IsNullOrWhiteSpace(catalogRoot.FullPath))
            {
                var fileName = Path.GetFileName(catalogRoot.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }

            return "Repository";
        }

        public IReadOnlyList<RepositorySummary> GetRepositorySummaries()
        {
            return _repositories
                .Select(repo => new RepositorySummary()
                {
                    Name = GetRepositoryTitle(repo.CatalogRoot, repo.Source),
                    Source = repo.Source,
                })
                .ToList();
        }

        public void ApplyTransfroms(StlFile stl)
        {
            var current = AllApplied.FirstOrDefault(a => a.UiKey == stl.UiKey);
            if (current != null)
            {
                current.Transforms = stl.Transforms;
                Utils.UpdatePositionRelativeToOriginalSize(current);
                stlImport.SaveImportState(current);
                NotifiyAppliedChanged();
            }
            else
            {
                ApplyObject(stl, true);
                Utils.UpdatePositionRelativeToOriginalSize(stl);
                stlImport.SaveImportState(stl);
            }
        }

        public void ApplyObject(StlFile item, bool notifyChanges = true)
        {
            if (item == null)
                return;

            StlFile current = AllApplied.FirstOrDefault(a =>
                PathsEqual(a?.FullPath, item?.FullPath) || a.Guid == item.Guid);
            if (current == null)
            {
                AllApplied.Add(item);
                AllApplied.Sort((a1, a2) =>
                                                a1.Name == "base.stl" ? -1
                                                                        : (a2.Name == "base.stl" ? 1
                                                                                                 : a1.Name.CompareTo(a2.Name)));
            }

            if (item.IsImport && item.Transforms != null)
            {
                stlImport.SaveImportState(current ?? item);
            }

            if (current == null || item.Transforms != null)
            {
                 if (notifyChanges && OnAppliedChanged != null)
                    OnAppliedChanged();
            }

        }

        public void RemoveObject(StlFile item, bool notifyChanges = true)
        {
            if (item == null)
                return;

            var current = AllApplied.FirstOrDefault(a =>
                PathsEqual(a?.FullPath, item?.FullPath) || a.Guid == item.Guid);
            if (current != null)
            {
                AllApplied.Remove(current);
            }
            if (notifyChanges && OnAppliedChanged != null)
                OnAppliedChanged();
        }

        internal void ApplyClear(StlFile stlFile, string newValue, bool notifyChanges = true)
        {
            if (newValue == "None")
            {
                stlFile.ClearToApply = null;
                stlFile.ClearRepositorySource = null;
            }
            else
            {
                var clearItem = AllClears.FirstOrDefault(c => c.Name == newValue) as StlFile;
                stlFile.ClearToApply = clearItem?.FullPath;
                stlFile.ClearRepositorySource = clearItem?.RepositorySource;
            }
            if (stlFile.IsImport)
            {
                //We need to force the removal and readd of the stlFile.
                RemoveObject(stlFile, false);
                stlFile.Guid = Guid.NewGuid();
            }
            ApplyObject(stlFile, false);
            if (notifyChanges && OnAppliedChanged != null)
                OnAppliedChanged();
        }

        internal void NotifiyAppliedChanged()
        {
            if (OnAppliedChanged != null)
                OnAppliedChanged();
        }

        internal void RemoveAllObjects()
        {
            AllApplied.Clear();
            NotifiyAppliedChanged();
        }

        internal void AddRepo(string url, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var catalogRoot = JsonConvert.DeserializeObject<Folder>(text);
            if (catalogRoot == null)
                return;

            AddRepo(catalogRoot, url);
        }

        public bool TryCreateDownloadRequest(string fullPath, out DownloadRequest request)
        {
            request = null;

            if (string.IsNullOrWhiteSpace(fullPath) || File.Exists(fullPath))
                return false;

            foreach (var repo in _repositories)
            {
                if (repo.TryCreateDownloadRequest(fullPath, out request))
                    return true;
            }

            return false;
        }
    }
}
