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
        private class Repository
        {
            public string Source;
            public Folder CatalogRoot;
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
                    if (subdir.Name == "tempClears" || subdir.Name == "base")
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
                if (subfile.Extension != ".stl" || subfile.Name.StartsWith("cuts_"))
                    continue;
                allChildren.Add(new TreeViewItemData<ITreeItem>(++id, new StlFile()
                {
                    Name = subfile.Name,
                    Selected = (subfile.Name == "base.stl"),
                    SelectionCanChange = (subfile.Name != "base.stl"),
                    FullPath = subfile.FullName,
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
                foreach (var subfile in folder.Files)
                {
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
            if (catalogRoot == null)
                return;

            _repositories.Add(new Repository()
            {
                Source = catalogRoot.FullPath,
                CatalogRoot = catalogRoot,
            });

            var id = _nextTreeIndex;
            var repoChildren = GetFolderList(catalogRoot, ref id);
            if (repoChildren.Count == 0)
                return;

            var repoTitle = string.IsNullOrWhiteSpace(catalogRoot.Name)
                ? (!string.IsNullOrWhiteSpace(catalogRoot.FullPath) ? Path.GetFileName(catalogRoot.FullPath) : "Repository")
                : catalogRoot.Name;

            var repoRoot = new TreeViewItemData<ITreeItem>(++id, new Folder()
            {
                Name = repoTitle,
                FullPath = catalogRoot.FullPath,
            }, repoChildren);

            StlTree.Add(repoRoot);
            _nextTreeIndex = id;

            AllClears.AddRange(GetClears(catalogRoot));

            if (OnDataChanged != null)
                OnDataChanged();
        }
        public void ApplyTransfroms(StlFile stl)
        {
            var current = AllApplied.FirstOrDefault(a => a.UiKey == stl.UiKey);
            if (current != null)
            {
                current.Transforms = stl.Transforms;
                Utils.UpdatePositionRelativeToOriginalSize(current);
                NotifiyAppliedChanged();
            }
            else
            {
                ApplyObject(stl, true);
                Utils.UpdatePositionRelativeToOriginalSize(stl);
            }
        }

        public void ApplyObject(StlFile item, bool notifyChanges = true)
        {
            StlFile current = AllApplied.FirstOrDefault(a => a.FullPath == item.FullPath);
            if (item.IsImport)
            {
                current = AllApplied.FirstOrDefault(a => a.Guid == item.Guid);
            }
            if (current == null)
            {
                AllApplied.Add(item);
                AllApplied.Sort((a1, a2) =>
                                                a1.Name == "base.stl" ? -1
                                                                        : (a2.Name == "base.stl" ? 1
                                                                                                 : a1.Name.CompareTo(a2.Name)));
            }

            if (current == null || item.Transforms != null)
            {
                 if (notifyChanges && OnAppliedChanged != null)
                    OnAppliedChanged();
            }

        }

        public void RemoveObject(StlFile item, bool notifyChanges = true)
        {
            var current = AllApplied.FirstOrDefault(a => a.FullPath == item.FullPath || a.Guid == item.Guid);
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
                stlFile.ClearToApply = null;
            else
                stlFile.ClearToApply = AllClears.FirstOrDefault(c => c.Name == newValue)?.FullPath;
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

            AddRepo(catalogRoot);
        }
    }
}
