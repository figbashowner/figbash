using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Properties;
using Unity.VisualScripting;
using Newtonsoft.Json;

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Assets
{

    [UxmlElement]
    public partial class RepoTab : VisualElement
    {
        private MultiColumnListView list;
        private Button _addRepoButton;

        public RepoTab()
        {
//            UnityEngine.Debug.Log($"ImportTab constructor");

            RegisterCallback<AttachToPanelEvent>(e =>
            { /* do something here when element is added to UI */
                VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("RepoTab");
                uiAsset.CloneTree(this);

                _addRepoButton = this.Q<Button>("AddRepoButton");
                _addRepoButton.RegisterCallback<ClickEvent>(HandleImportButtonClick);
                DataManager.Instance.OnDataChanged += OnDataChanged;

            });
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
                //int tmp = 4;
            });

        }

        private void List_OnSelectionChanged(StlFile stl)
        {
        }

        private void HandleImportButtonClick(ClickEvent evt)
        {
            var owner = GetRootOwner();
            PromptDialog.ShowChoice(
                owner,
                "Add Repository",
                "Choose whether the repository lives on disk or at a URL.",
                "Local directory",
                () => HandleLocalDirectory(owner),
                "URL",
                () => PromptForUrl(owner));
        }

        private VisualElement GetRootOwner()
        {
            var p = this.parent;
            while (p != null && p.parent != null)
            {
                p = p.parent;
            }
            return p ?? this;
        }

        private void HandleLocalDirectory(VisualElement owner)
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
            var catalogPath = Path.Combine(directory, "catalog.json");
            if (!File.Exists(catalogPath))
            {
                Debug.LogWarning($"Could not find catalog.json in repository folder: {directory}");
                return;
            }

            try
            {
                var catalog = JsonConvert.DeserializeObject<Folder>(File.ReadAllText(catalogPath));
                if (catalog == null)
                {
                    Debug.LogWarning($"Repository catalog at {catalogPath} was empty or invalid.");
                    return;
                }

                Utils.PairUiSidecars(catalog);
                ResolveCatalogPaths(catalog, directory);
                DataManager.Instance.AddRepo(catalog);
                SelectAllTab(owner);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
            }
        }

        private void PromptForUrl(VisualElement owner)
        {
            PromptDialog.Show(owner, "Enter Repo URL", "Enter the URL of the desired repository catalog.", "", url => LoadRepoFromUrl(url, owner), "Load", "Cancel");
        }

        private void LoadRepoFromUrl(string url, VisualElement owner)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;
            if (url.EndsWith("/catalog.json") == false)
                url += "/catalog.json";
            try
            {
                using (UnityWebRequest www = UnityWebRequest.Get(url))
                {
                    www.SendWebRequest();
                    while (www.result == UnityWebRequest.Result.InProgress)
                    {
                    }

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"Failed to download repository catalog from {url}: {www.error}");
                        return;
                    }

                    var catalog = JsonConvert.DeserializeObject<Folder>(www.downloadHandler.text);


                    if (catalog == null)
                    {
                        Debug.LogWarning($"Repository catalog at {url} was empty or invalid.");
                        return;
                    }

                    var cacheRoot = GetRepoCacheRoot(url);
                    File.WriteAllText(Path.Combine(cacheRoot, "catalog.json"), www.downloadHandler.text);
                    Utils.PairUiSidecars(catalog);
                    DownloadRepoFiles(new Uri(url), catalog, cacheRoot);
                    ResolveCatalogPaths(catalog, cacheRoot);
                    DataManager.Instance.AddRepo(catalog);
                    SelectAllTab(owner);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
            }
        }

        private static string GetRepoCacheRoot(string sourceUrl)
        {
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceUrl));
            }
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            var cacheRoot = Path.Combine(uiEvents.folderRoot, "repos", hashString);
            Directory.CreateDirectory(cacheRoot);
            return cacheRoot;
        }

        private static void DownloadRepoFiles(Uri baseUri, Folder folder, string cacheRoot)
        {
            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    DownloadRepoFiles(baseUri, subdir, cacheRoot);
                }
            }

            if (folder.Files == null)
                return;

            foreach (var file in folder.Files)
            {
                if (string.IsNullOrWhiteSpace(file.FullPath))
                    continue;

                if (string.IsNullOrWhiteSpace(file.Name))
                    continue;

                if (file.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                    continue;

                DownloadRepoFile(baseUri, cacheRoot, file.FullPath);

                if (!string.IsNullOrWhiteSpace(file.UiName))
                    DownloadRepoFile(baseUri, cacheRoot, Utils.GetUiSidecarPath(file.FullPath));
            }
        }

        private static void DownloadRepoFile(Uri baseUri, string cacheRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            var localPath = Path.Combine(cacheRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var localDirectory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrWhiteSpace(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            if (File.Exists(localPath))
                return;

            var fileUri = new Uri(baseUri, relativePath);
            using (UnityWebRequest www = UnityWebRequest.Get(fileUri.AbsoluteUri))
            {
                www.SendWebRequest();
                while (www.result == UnityWebRequest.Result.InProgress)
                {
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Failed to download repository file {fileUri}: {www.error}");
                    return;
                }

                File.WriteAllBytes(localPath, www.downloadHandler.data);
            }
        }

        private static void ResolveCatalogPaths(Folder folder, string rootPath)
        {
            if (folder == null)
                return;

            ResolveFolderPath(folder, rootPath);
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
                var property = tabViewType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
                var property = tabViewType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null || !property.CanWrite || property.PropertyType != typeof(int))
                    continue;

                property.SetValue(tabView, 0);
                return true;
            }

            foreach (var methodName in new[] { "SetActiveTab", "SetSelectedTab" })
            {
                var method = tabViewType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

                
        public void SetSelectedImport(StlFile stl)
        {
        }

        private void OnDataChanged()
        {

        }
    }
}
