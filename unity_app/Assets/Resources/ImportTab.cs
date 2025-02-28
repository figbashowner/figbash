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
                _list.filterCallback = (stl) =>
                {
                    return stl.IsImport;
                };
                _list.OnSelectionChanged += List_OnSelectionChanged;
                _transformsControl = this.Q<TransformsControl>("transforms");
                _importButton = this.Q<Button>("ImportButton");
                _importButton.RegisterCallback<ClickEvent>(HandleImportButtonClick);
                _transformsControl.SetSelectedImport(null);
                DataManager.Instance.OnDataChanged += OnDataChanged;

            });
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
                //int tmp = 4;
            });

        }

        private void List_OnSelectionChanged(StlFile stl)
        {
            _transformsControl.PopulateTransformEntries(stl.Transforms);
            _transformsControl.SetSelectedImport(stl);
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
                var stl = new StlFile()
                {
                    Name = Path.GetFileName(path),
                    FullPath = path,
                    SelectionCanChange = true,
                    IsImport = true,
                };
                var result = stlImport.LoadOne(stl, uiEvents.BaseSize);
                result.transform.Translate(uiEvents.importLocation);
                GetTransformsFromObject(result, stl);
                stl.Transforms.UniformScale = true;
                _transformsControl.PopulateTransformEntries(stl.Transforms);
                _transformsControl.SetSelectedImport(stl);
                DataManager.Instance.ApplyObject(stl, true);
                _list.SetSelection(stl);
                /*_currentTransformingObject = result;
                */
                //ZoomCameraToTarget(CameraTarget.Full);
            }
        }

                
        public void SetSelectedImport(StlFile stl)
        {
            _transformsControl?.SetSelectedImport(stl);
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

        }
    }
}
