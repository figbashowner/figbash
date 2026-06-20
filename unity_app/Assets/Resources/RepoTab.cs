using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Assets
{
    [UxmlElement]
    public partial class RepoTab : VisualElement
    {
        [Serializable]
        private sealed class RepositoryEntry
        {
            public string Name;
            public string Source;
        }

        private readonly List<RepositoryEntry> _repositoryEntries = new List<RepositoryEntry>();
        private MultiColumnListView _repoList;
        private Button _addRepoButton;
        private Button _refreshReposButton;
        private bool _isLoadingRepositories;

        public RepoTab()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (childCount == 0)
            {
                var uiAsset = Resources.Load<VisualTreeAsset>("RepoTab");
                if (uiAsset == null)
                {
                    Debug.LogWarning("Could not load RepoTab UXML.");
                    return;
                }

                uiAsset.CloneTree(this);
            }

            _addRepoButton = this.Q<Button>("AddRepoButton");
            _refreshReposButton = this.Q<Button>("RefreshReposButton");
            _repoList = this.Q<MultiColumnListView>("RepositoryList");

            ConfigureRepositoryList();
            RefreshRepoListView();

            if (_addRepoButton != null)
            {
                _addRepoButton.UnregisterCallback<ClickEvent>(HandleImportButtonClick);
                _addRepoButton.RegisterCallback<ClickEvent>(HandleImportButtonClick);
            }

            if (_refreshReposButton != null)
            {
                _refreshReposButton.UnregisterCallback<ClickEvent>(HandleRefreshButtonClick);
                _refreshReposButton.RegisterCallback<ClickEvent>(HandleRefreshButtonClick);
            }
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
        }

        private VisualElement GetRootOwner()
        {
            var parent = this.parent;
            while (parent != null && parent.parent != null)
            {
                parent = parent.parent;
            }

            return parent ?? this;
        }

        public async UniTask LoadSavedRepositoriesAsync()
        {
            if (_isLoadingRepositories)
                return;

            _isLoadingRepositories = true;
            SetRepositoryUiEnabled(false);

            try
            {
                LoadManifestEntries();
                RefreshRepoListView();

                if (_repositoryEntries.Count == 0)
                    return;

                var owner = GetRootOwner();
                var manifestChanged = false;

                foreach (var entry in _repositoryEntries.ToList())
                {
                    var loadedEntry = await ImportRepositoryAsync(entry.Source, owner, selectAllAfterSuccess: false);
                    if (loadedEntry == null)
                        continue;

                    manifestChanged |= UpsertRepositoryEntry(loadedEntry);
                    RefreshRepoListView();
                }

                if (manifestChanged)
                    SaveManifestEntries();
            }
            finally
            {
                SetRepositoryUiEnabled(true);
                _isLoadingRepositories = false;
            }
        }

        private void HandleImportButtonClick(ClickEvent evt)
        {
            if (_isLoadingRepositories)
                return;

            var owner = GetRootOwner();
            PromptDialog.ShowChoice(
                owner,
                "Add Repository",
                "Choose whether the repository lives on disk or at a URL.",
                "Local directory",
                () => _ = AddLocalRepositoryAsync(owner),
                "URL",
                () => PromptForUrl(owner));
        }

        private void PromptForUrl(VisualElement owner)
        {
            if (_isLoadingRepositories)
                return;

            PromptDialog.Show(
                owner,
                "Enter Repo URL",
                "Enter the URL of the desired repository catalog.",
                "",
                url => { _ = AddRemoteRepositoryAsync(url, owner); },
                "Load",
                "Cancel");
        }

        private async UniTask AddLocalRepositoryAsync(VisualElement owner)
        {
            var results = StandaloneFileBrowser.OpenFolderPanel(
                "Select Repository Folder",
                uiEvents.previousFolder,
                false);

            if (results == null || results.Length == 0)
                return;

            var directory = results[0];
            if (string.IsNullOrWhiteSpace(directory))
                return;

            uiEvents.previousFolder = directory;
            var entry = await ImportLocalRepositoryAsync(directory, owner, selectAllAfterSuccess: true);
            if (entry == null)
                return;

            if (UpsertRepositoryEntry(entry))
                SaveManifestEntries();

            RefreshRepoListView();
        }

        private async UniTask AddRemoteRepositoryAsync(string url, VisualElement owner)
        {
            var entry = await ImportRemoteRepositoryAsync(url, owner, selectAllAfterSuccess: true);
            if (entry == null)
                return;

            if (UpsertRepositoryEntry(entry))
                SaveManifestEntries();

            RefreshRepoListView();
        }

        private void HandleRefreshButtonClick(ClickEvent evt)
        {
            if (_isLoadingRepositories)
                return;

            _ = LoadSavedRepositoriesAsync();
        }

        public async UniTask<bool> EnsureRepositoryLoadedAsync(string source)
        {
            var normalizedSource = NormalizeRepoSource(source);
            if (string.IsNullOrWhiteSpace(normalizedSource))
                return true;

            if (DataManager.Instance.HasRepositorySource(normalizedSource))
                return true;

            var entry = await ImportRepositoryAsync(normalizedSource, GetRootOwner(), selectAllAfterSuccess: false);
            if (entry == null)
                return false;

            if (UpsertRepositoryEntry(entry))
                SaveManifestEntries();

            RefreshRepoListView();
            return true;
        }

        private void HandleRemoveRepoClick(ClickEvent evt)
        {
            if (_isLoadingRepositories)
                return;

            if (evt.currentTarget is not Button button)
                return;

            if (button.userData is not RepositoryEntry entry)
                return;

            DataManager.Instance.RemoveRepoBySource(entry.Source);
            if (RemoveRepositoryEntry(entry.Source))
                SaveManifestEntries();

            RefreshRepoListView();
        }

        private async UniTask<RepositoryEntry> ImportRepositoryAsync(string source, VisualElement owner, bool selectAllAfterSuccess)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            if (IsRemoteSource(source))
                return await ImportRemoteRepositoryAsync(source, owner, selectAllAfterSuccess);

            return await ImportLocalRepositoryAsync(source, owner, selectAllAfterSuccess);
        }

        private UniTask<RepositoryEntry> ImportLocalRepositoryAsync(string directory, VisualElement owner, bool selectAllAfterSuccess)
        {
            var normalizedSource = NormalizeRepoSource(directory);
            var catalogPath = Path.Combine(directory, "catalog.json");

            if (!File.Exists(catalogPath))
            {
                PromptDialog.ShowAlert(owner, "Repository not found", $"Could not find catalog.json in repository folder:\n{directory}");
                return UniTask.FromResult<RepositoryEntry>(null);
            }

            try
            {
                var catalogText = File.ReadAllText(catalogPath);
                var catalog = JsonConvert.DeserializeObject<Folder>(catalogText);
                if (catalog == null)
                {
                    PromptDialog.ShowAlert(owner, "Repository not found", $"Repository catalog at {catalogPath} was empty or invalid.");
                    return UniTask.FromResult<RepositoryEntry>(null);
                }

                ResolveCatalogPaths(catalog, directory);
                Utils.PairUiSidecars(catalog);
                DataManager.Instance.AddRepo(catalog, normalizedSource);

                if (selectAllAfterSuccess)
                    SelectAllTab(owner);

                return UniTask.FromResult(new RepositoryEntry
                {
                    Name = GetRepositoryDisplayName(catalog, normalizedSource),
                    Source = normalizedSource,
                });
            }
            catch (Exception ex)
            {
                PromptDialog.ShowAlert(owner, "Repository import failed", ex.Message);
                Debug.LogWarning(ex);
                return UniTask.FromResult<RepositoryEntry>(null);
            }
        }

        private async UniTask<RepositoryEntry> ImportRemoteRepositoryAsync(string source, VisualElement owner, bool selectAllAfterSuccess)
        {
            var normalizedSource = NormalizeRepoSource(source);
            var baseSource = GetRemoteBaseSource(normalizedSource);
            var catalogUrl = GetRemoteCatalogUrl(normalizedSource);

            try
            {
                var catalogText = await DownloadTextAsync(catalogUrl);
                var catalog = JsonConvert.DeserializeObject<Folder>(catalogText);

                if (catalog == null)
                {
                    PromptDialog.ShowAlert(owner, "Repository not found", $"Repository catalog at {catalogUrl} was empty or invalid.");
                    return null;
                }

                var cacheRoot = GetRepoCacheRoot(baseSource);
                Directory.CreateDirectory(cacheRoot);
                File.WriteAllText(Path.Combine(cacheRoot, "catalog.json"), catalogText);
                ResolveCatalogPaths(catalog, cacheRoot);
                Utils.PairUiSidecars(catalog);

                var downloadRequests = BuildUiSidecarDownloadRequests(new Uri(baseSource), catalog, cacheRoot);
                var downloadResult = await DownloadManager.DownloadAsync(downloadRequests, "Downloading repository previews");
                if (!downloadResult.Success)
                {
                    ShowDownloadFailure(owner, "Repository download failed", downloadResult);
                    return null;
                }

                DataManager.Instance.AddRepo(catalog, baseSource);

                if (selectAllAfterSuccess)
                    SelectAllTab(owner);

                return new RepositoryEntry
                {
                    Name = GetRepositoryDisplayName(catalog, baseSource),
                    Source = baseSource,
                };
            }
            catch (Exception ex)
            {
                PromptDialog.ShowAlert(owner, "Repository download failed", ex.Message);
                Debug.LogWarning(ex);
                return null;
            }
        }

        private static async UniTask<string> DownloadTextAsync(string url)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.SendWebRequest();
                while (!www.isDone)
                {
                    await UniTask.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Failed to download repository catalog from {url}: {www.error}");
                }

                return www.downloadHandler.text;
            }
        }

        private static string GetRepoCacheRoot(string source)
        {
            var normalizedSource = NormalizeRepoSource(source);
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedSource));
            }

            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            var cacheRoot = Path.Combine(uiEvents.folderRoot, "repos", hashString);
            Directory.CreateDirectory(cacheRoot);
            return cacheRoot;
        }

        private static List<DownloadRequest> BuildUiSidecarDownloadRequests(Uri baseUri, Folder folder, string cacheRoot)
        {
            var requests = new List<DownloadRequest>();
            CollectUiSidecarDownloadRequests(baseUri, folder, cacheRoot, requests);
            return requests;
        }

        private static void CollectUiSidecarDownloadRequests(Uri baseUri, Folder folder, string cacheRoot, List<DownloadRequest> requests)
        {
            if (folder == null)
                return;

            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    CollectUiSidecarDownloadRequests(baseUri, subdir, cacheRoot, requests);
                }
            }

            if (folder.Files == null)
                return;

            foreach (var file in folder.Files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.FullPath) || string.IsNullOrWhiteSpace(file.Name))
                    continue;

                if (file.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(file.UiName))
                    continue;

                var localPath = Utils.GetUiSidecarPath(file.FullPath);
                if (string.IsNullOrWhiteSpace(localPath))
                    continue;

                var relativePath = Path.GetRelativePath(cacheRoot, localPath).Replace('\\', '/');
                requests.Add(new DownloadRequest(baseUri, relativePath, localPath, file.UiHash));
            }
        }

        private static void ShowDownloadFailure(VisualElement owner, string title, DownloadBatchResult result)
        {
            var failureLines = result.Failures
                .Take(5)
                .Select(f => $"{Path.GetFileName(f.LocalPath)}: {f.Error}")
                .ToList();

            var message = failureLines.Count == 0
                ? "One or more downloads failed."
                : string.Join("\n", failureLines);

            if (result.Failures.Count > failureLines.Count)
                message += $"\n...and {result.Failures.Count - failureLines.Count} more.";

            PromptDialog.ShowAlert(owner, title, message);
        }

        private static void ResolveCatalogPaths(Folder folder, string rootPath)
        {
            if (folder == null)
                return;

            ResolveFolderPath(folder, rootPath);
        }

        private static void ResolveFolderPath(Folder folder, string rootPath)
        {
            if (folder == null)
                return;

            if (string.IsNullOrWhiteSpace(folder.FullPath))
            {
                folder.FullPath = rootPath;
            }
            else if (!Path.IsPathRooted(folder.FullPath))
            {
                folder.FullPath = Path.GetFullPath(Path.Combine(rootPath, folder.FullPath.Replace('/', Path.DirectorySeparatorChar)));
            }

            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    ResolveFolderPath(subdir, rootPath);
                }
            }

            if (folder.Files == null)
                return;

            foreach (var file in folder.Files)
            {
                if (string.IsNullOrWhiteSpace(file.FullPath))
                    continue;

                if (!Path.IsPathRooted(file.FullPath))
                {
                    file.FullPath = Path.GetFullPath(Path.Combine(rootPath, file.FullPath.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
        }

        private void ConfigureRepositoryList()
        {
            if (_repoList == null)
                return;

            _repoList.columns["name"].makeCell = () => new Label();
            _repoList.columns["remove"].makeCell = () => new Button() { text = "-" };

            _repoList.columns["name"].bindCell = (element, index) =>
            {
                if (element is Label label)
                {
                    label.text = GetRepositoryEntry(index)?.Name ?? string.Empty;
                }
            };

            _repoList.columns["remove"].bindCell = (element, index) =>
            {
                if (element is not Button button)
                    return;

                var entry = GetRepositoryEntry(index);
                button.userData = entry;
                button.SetEnabled(entry != null);
                button.UnregisterCallback<ClickEvent>(HandleRemoveRepoClick);
                button.RegisterCallback<ClickEvent>(HandleRemoveRepoClick);
            };
        }

        private void RefreshRepoListView()
        {
            if (_repoList == null)
                return;

            _repoList.itemsSource = _repositoryEntries;
            _repoList.RefreshItems();
        }

        private RepositoryEntry GetRepositoryEntry(int index)
        {
            if (index < 0 || index >= _repositoryEntries.Count)
                return null;

            return _repositoryEntries[index];
        }

        private void SetRepositoryUiEnabled(bool enabled)
        {
            _addRepoButton?.SetEnabled(enabled);
            _refreshReposButton?.SetEnabled(enabled);
            _repoList?.SetEnabled(enabled);
        }

        private void LoadManifestEntries()
        {
            _repositoryEntries.Clear();

            var manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath))
                return;

            try
            {
                var entries = JsonConvert.DeserializeObject<List<RepositoryEntry>>(File.ReadAllText(manifestPath));
                if (entries == null)
                    return;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    var normalized = NormalizeRepositoryEntry(entry);
                    if (normalized == null || string.IsNullOrWhiteSpace(normalized.Source))
                        continue;

                    if (seen.Add(normalized.Source))
                        _repositoryEntries.Add(normalized);
                }
            }
            catch (Exception ex)
            {
                PromptDialog.ShowAlert(GetRootOwner(), "Repository list failed to load", ex.Message);
                Debug.LogWarning(ex);
            }
        }

        private bool UpsertRepositoryEntry(RepositoryEntry entry)
        {
            var normalized = NormalizeRepositoryEntry(entry);
            if (normalized == null || string.IsNullOrWhiteSpace(normalized.Source))
                return false;

            var index = _repositoryEntries.FindIndex(existing => SourcesMatch(existing?.Source, normalized.Source));
            if (index < 0)
            {
                _repositoryEntries.Add(normalized);
                return true;
            }

            var existingEntry = _repositoryEntries[index];
            var changed = !string.Equals(existingEntry.Name, normalized.Name, StringComparison.Ordinal)
                || !string.Equals(existingEntry.Source, normalized.Source, StringComparison.OrdinalIgnoreCase);

            existingEntry.Name = normalized.Name;
            existingEntry.Source = normalized.Source;
            return changed;
        }

        private bool RemoveRepositoryEntry(string source)
        {
            var normalizedSource = NormalizeRepoSource(source);
            if (string.IsNullOrWhiteSpace(normalizedSource))
                return false;

            var removed = _repositoryEntries.RemoveAll(entry => SourcesMatch(entry?.Source, normalizedSource));
            return removed > 0;
        }

        private void SaveManifestEntries()
        {
            try
            {
                var manifestPath = GetManifestPath();
                var manifestDirectory = Path.GetDirectoryName(manifestPath);
                if (!string.IsNullOrWhiteSpace(manifestDirectory))
                    Directory.CreateDirectory(manifestDirectory);

                File.WriteAllText(manifestPath, JsonConvert.SerializeObject(_repositoryEntries, Formatting.Indented));
            }
            catch (Exception ex)
            {
                PromptDialog.ShowAlert(GetRootOwner(), "Repository list failed to save", ex.Message);
                Debug.LogWarning(ex);
            }
        }

        private static RepositoryEntry NormalizeRepositoryEntry(RepositoryEntry entry)
        {
            if (entry == null)
                return null;

            var normalizedSource = NormalizeRepoSource(entry.Source);
            if (string.IsNullOrWhiteSpace(normalizedSource))
                return null;

            return new RepositoryEntry
            {
                Source = normalizedSource,
                Name = string.IsNullOrWhiteSpace(entry.Name)
                    ? GetRepositoryDisplayName(normalizedSource)
                    : entry.Name.Trim(),
            };
        }

        private static string GetManifestPath()
        {
            return Path.Combine(uiEvents.folderRoot, "repos", "repositories.json");
        }

        private static bool IsRemoteSource(string source)
        {
            return Uri.TryCreate(source, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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

        private static string GetRemoteBaseSource(string source)
        {
            return NormalizeRepoSource(source);
        }

        private static string GetRemoteCatalogUrl(string source)
        {
            var baseSource = GetRemoteBaseSource(source);
            if (string.IsNullOrWhiteSpace(baseSource))
                return string.Empty;

            return baseSource.TrimEnd('/') + "/catalog.json";
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

        private static bool SourcesMatch(string left, string right)
        {
            return string.Equals(NormalizeRepoSource(left), NormalizeRepoSource(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRepositoryDisplayName(Folder catalog, string source)
        {
            if (catalog != null && !string.IsNullOrWhiteSpace(catalog.Name))
                return catalog.Name.Trim();

            return GetRepositoryDisplayName(source);
        }

        private static string GetRepositoryDisplayName(string source)
        {
            var normalizedSource = NormalizeRepoSource(source);
            if (string.IsNullOrWhiteSpace(normalizedSource))
                return "Repository";

            if (Uri.TryCreate(normalizedSource, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var lastSegment = uri.Segments?.LastOrDefault()?.Trim('/');
                if (!string.IsNullOrWhiteSpace(lastSegment))
                    return lastSegment;

                if (!string.IsNullOrWhiteSpace(uri.Host))
                    return uri.Host;
            }

            var fileName = Path.GetFileName(normalizedSource.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(fileName))
                return fileName;

            return "Repository";
        }

        private static void SelectAllTab(VisualElement owner)
        {
            if (owner == null)
                return;

            var tabView = owner.Q<TabView>("tabView");
            var allTab = owner.Q<Tab>("AllTab");
            if (tabView == null || allTab == null)
                return;

            if (TrySelectTabViaReflection(tabView, allTab))
                return;

            Debug.LogWarning("Could not switch to the All tab after adding a repository.");
        }

        private static bool TrySelectTabViaReflection(TabView tabView, Tab allTab)
        {
            var tabViewType = tabView.GetType();

            foreach (var propertyName in new[] { "activeTab", "selectedTab" })
            {
                var property = tabViewType.GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property == null || !property.CanWrite)
                    continue;

                if (property.PropertyType.IsAssignableFrom(allTab.GetType()))
                {
                    property.SetValue(tabView, allTab);
                    return true;
                }
            }

            foreach (var propertyName in new[] { "activeTabIndex", "selectedTabIndex" })
            {
                var property = tabViewType.GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property == null || !property.CanWrite || property.PropertyType != typeof(int))
                    continue;

                property.SetValue(tabView, 0);
                return true;
            }

            foreach (var methodName in new[] { "SetActiveTab", "SetSelectedTab" })
            {
                var method = tabViewType.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (method == null)
                    continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                    continue;

                if (parameters[0].ParameterType.IsAssignableFrom(allTab.GetType()))
                {
                    method.Invoke(tabView, new object[] { allTab });
                    return true;
                }

                if (parameters[0].ParameterType == typeof(int))
                {
                    method.Invoke(tabView, new object[] { 0 });
                    return true;
                }
            }

            return false;
        }

        private void OnDataChanged()
        {
            // Intentionally left blank.
        }
    }
}
