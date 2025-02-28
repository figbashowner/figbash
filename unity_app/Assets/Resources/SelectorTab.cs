using NUnit.Framework;
using System;
using System.Collections.Generic;
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
    public partial class SelectorTab : VisualElement
    {
        private MultiColumnTreeView tree;

        [UxmlAttribute]
        public string filter { get; set; }

        public SelectorTab()
        {
            RegisterCallback<AttachToPanelEvent>(e =>
            { /* do something here when element is added to UI */

                VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("SelectorTabUI");
                uiAsset.CloneTree(this);
                var list = this.Q<SelectorListControl>("SelectorListControl");
                list.filter = filter;    
                tree = this.Q<MultiColumnTreeView>("TreeControl");

                // For each column, set Column.makeCell to initialize each node in the tree.
                // You can index the columns array with names or numerical indices.
                tree.columns["name"].makeCell = () => new Label();
                tree.columns["selected"].makeCell = () => new Button() { text = "+" };

                // For each column, set Column.bindCell to bind an initialized node to a data item.
                tree.columns["name"].bindCell = (VisualElement element, int index) =>
                    (element as Label).text = tree.GetItemDataForIndex<ITreeItem>(index).Name;
                tree.columns["selected"].bindCell = (VisualElement element, int index) =>
                {
                    var t = element as Button;
                    var item = tree.GetItemDataForIndex<ITreeItem>(index);
                    item.TreeIndex = index;
                    t.SetEnabled(item.SelectionCanChange);
                    t.userData = item;
                    //    ZoomCameraTo(item.FullPath);
                    var stlFile = item as StlFile;
                    if (stlFile != null && item.SelectionCanChange)
                    {
                        t.style.display = DisplayStyle.Flex;
                        t.UnregisterCallback<ClickEvent>(addObjectCallback);
                        t.RegisterCallback<ClickEvent>(addObjectCallback);
                    }
                    else
                    {
                        //                    t.UnregisterValueChangedCallback(HandleSelectedToggleCallback);
                        t.style.display = DisplayStyle.None;
                        //                  _clearDropdown.SetValueWithoutNotify(null);
                        //                _clearDropdown.SetEnabled(false);
                    }
                };

                DataManager.Instance.OnDataChanged += OnDataChanged;

            });
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
                //int tmp = 4;
            });

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

            var filteredTree = filter == null ? 
                                                DataManager.Instance.StlTree : 
                                                DataManager.Instance.StlTree.Where(f => f.data.FullPath.Contains(filter)).First().children.Where(c => c.data.Name[0] == filter[0] && c.data.Name[1] != '1').ToList();
            tree.SetRootItems(filteredTree);

            
            if (filter != null)
                tree.ExpandAll();
            tree.RefreshItems();
        }
    }
}
