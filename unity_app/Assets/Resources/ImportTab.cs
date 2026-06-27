using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Properties;
using Unity.VisualScripting;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets
{

    [UxmlElement]
    public partial class ImportTab : VisualElement
    {
        private MultiColumnListView list;
        private Button _importButton;
        private TransformsControl _transformsControl;
        private SelectorListControl _list;

        public ImportTab()
        {
//            UnityEngine.Debug.Log($"ImportTab constructor");

            RegisterCallback<AttachToPanelEvent>(e =>
            { /* do something here when element is added to UI */
                VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("ImportTab");
                uiAsset.CloneTree(this);

                _list = this.Q<SelectorListControl>("SelectorListControl");
                _list.filterCallback = stl => stl != null && stl.IsImport;
                _list.itemsSourceProvider = GetImportedFiles;
                _list.actionLabelProvider = GetImportActionLabel;
                _list.actionCallback = HandleImportAction;
                _list.OnSelectionChanged += List_OnSelectionChanged;
                _transformsControl = this.Q<TransformsControl>("transforms");
                _importButton = this.Q<Button>("ImportButton");
                _importButton.RegisterCallback<ClickEvent>(HandleImportButtonClick);
                _transformsControl.SetSelectedImport(null);
                DataManager.Instance.OnDataChanged += OnDataChanged;
                _list.RefreshItems();

            });
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
                //int tmp = 4;
            });

        }

        private void List_OnSelectionChanged(StlFile stl)
        {
            _transformsControl?.SetSelectedImport(stl);
        }

        private void HandleImportButtonClick(ClickEvent evt)
        {
            var results = StandaloneFileBrowser.OpenFilePanel("Import STL", uiEvents.previousFolderStl, "stl", false);
            if (results == null || results.Length == 0)
                return;
            string path = results[0];
            if (path.Length != 0)
            {
                FileInfo fileInfo = new FileInfo(path);
                uiEvents.previousFolderStl = fileInfo.DirectoryName;
                var copiedPath = stlImport.CopyImportedStlToDataFolder(path);
                var stl = new StlFile()
                {
                    Name = Path.GetFileName(copiedPath),
                    FullPath = copiedPath,
                    SelectionCanChange = true,
                    IsImport = true,
                };
                var result = stlImport.LoadOne(stl, uiEvents.BaseSize);
                result.transform.Translate(uiEvents.importLocation);
                GetTransformsFromObject(result, stl);

                if (stlImport.TryLoadImportState(copiedPath, out var savedState) && savedState?.transforms != null)
                {
                    stl.Transforms = savedState.transforms;
                    if (savedState.OriginalSize != Vector3.zero)
                        stl.originalSize = savedState.OriginalSize;
                }

                EnsureImportTransformDefaults(stl.Transforms, out var normalizedTransforms);
                stl.Transforms = normalizedTransforms;

                _transformsControl?.SetSelectedImport(stl);
                DataManager.Instance.ApplyObject(stl, true);
                _list?.RefreshItems();
                _list?.SetSelection(stl);
                /*_currentTransformingObject = result;
                */
                //ZoomCameraToTarget(CameraTarget.Full);
            }
        }

                
        public void SetSelectedImport(StlFile stl)
        {
            _transformsControl?.SetSelectedImport(stl);
        }

        private IEnumerable<StlFile> GetImportedFiles()
        {
            if (string.IsNullOrWhiteSpace(uiEvents.folderRoot))
                return Enumerable.Empty<StlFile>();

            var importsRoot = Path.Combine(uiEvents.folderRoot, "imports");
            if (!Directory.Exists(importsRoot))
                return Enumerable.Empty<StlFile>();

            return Directory.EnumerateFiles(importsRoot, "*.stl", SearchOption.TopDirectoryOnly)
                .Where(ShouldShowImportedFile)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Select(CreateImportedFile)
                .ToList();
        }

        private static bool ShouldShowImportedFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!path.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                return true;

            var basePath = path.Substring(0, path.Length - ".ui.stl".Length) + ".stl";
            return !File.Exists(basePath);
        }

        private StlFile CreateImportedFile(string path)
        {
            var stl = new StlFile()
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                SelectionCanChange = true,
                IsImport = true,
            };

            if (stlImport.TryLoadImportState(path, out var savedState) && savedState != null)
            {
                stl.Transforms = savedState.transforms;
                stl.originalSize = savedState.OriginalSize;
                stl.ClearToApply = savedState.ClearToApplyFullPath;
                stl.RepositorySource = savedState.RepositorySource;
                stl.ClearRepositorySource = savedState.ClearRepositorySource;
            }

            if (!string.IsNullOrWhiteSpace(stl.UiPath) && File.Exists(stl.UiPath))
                stl.UiName = Path.GetFileName(stl.UiPath);

            EnsureImportTransformDefaults(stl.Transforms, out var normalizedTransforms);
            stl.Transforms = normalizedTransforms;

            return stl;
        }

        private static void EnsureImportTransformDefaults(Transforms transforms, out Transforms normalized)
        {
            normalized = transforms ?? new Transforms();

            if (normalized.Rotations == null || normalized.Rotations.Length < 3)
                normalized.Rotations = new float[] { 0f, 0f, 0f };

            if (normalized.Position == null || normalized.Position.Length < 3)
                normalized.Position = new float[] { 0f, 0f, 0f };

            if (normalized.Scale == null || normalized.Scale.Length < 3)
                normalized.Scale = new float[] { 100f, 100f, 100f };

            if (normalized.PositionRelativeToOriginalSize == null || normalized.PositionRelativeToOriginalSize.Length < 3)
                normalized.PositionRelativeToOriginalSize = new float[] { 0f, 0f, 0f };
        }

        private string GetImportActionLabel(StlFile stl)
        {
            return IsImportedFileApplied(stl) ? "-" : "+";
        }

        private void HandleImportAction(StlFile stl)
        {
            if (stl == null)
                return;

            _list?.SetSelection(stl);

            if (IsImportedFileApplied(stl))
                DataManager.Instance.RemoveObject(stl);
            else
                DataManager.Instance.ApplyObject(stl);

            _list?.SetSelection(stl);
        }

        private bool IsImportedFileApplied(StlFile stl)
        {
            if (stl == null || string.IsNullOrWhiteSpace(stl.FullPath))
                return false;

            return DataManager.Instance.AllApplied.Any(applied =>
                applied != null && PathsEqual(applied.FullPath, stl.FullPath));
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private void GetTransformsFromObject(GameObject g, StlFile stl)
        {
            Transforms t = new Transforms();
            var objScale = g.GetComponent<Transform>().localScale;
            t.Scale = new float[] { Utils.makePercent(stlImport.scale, objScale[0]),
                                Utils.makePercent(stlImport.scale, objScale[1]),
                                Utils.makePercent(stlImport.scale, objScale[2])
        };
            var objPosition = g.transform.position;
            t.Position = new float[] { Utils.makePercent(uiEvents.BaseSize[0], objPosition[0]),
                                Utils.makePercent(uiEvents.BaseSize[1], objPosition[1]),
                                Utils.makePercent(uiEvents.BaseSize[2], objPosition[2])
        };
            
            var objRotation = g.transform.rotation;
            t.Rotations = new float[] { objRotation[0] * 360,
                                objRotation[1] * 360,
                                objRotation[2] * 360
        };
            stl.Transforms = t;
            Utils.UpdatePositionRelativeToOriginalSize(stl);
        }



        private void applyClear(ChangeEvent<string> evt)
        {
            var item = evt.currentTarget as DropdownField;
            if (item != null)
            {
                DataManager.Instance.ApplyClear(item.userData as StlFile, evt.newValue);
            }
        }

        private void removeObjectCallback(ClickEvent evt)
        {
            var item = evt.currentTarget as Button;
            if (item != null)
            {
                DataManager.Instance.RemoveObject(item.userData as StlFile);
            }
        }


        private void addObjectCallback(ClickEvent evt)
        {
            var item = evt.currentTarget as Button;
            if (item != null)
            {
                DataManager.Instance.ApplyObject(item.userData as StlFile);
            }
        }

        private void OnDataChanged()
        {
            _list?.RefreshItems();
        }
    }
}
