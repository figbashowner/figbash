using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Properties;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets
{
    [UxmlElement]
    public partial class SelectorListControl : VisualElement
    {
        private MultiColumnListView list;

        public delegate void SelectionChanged(StlFile stl);
        public event SelectionChanged OnSelectionChanged;

        [UxmlAttribute]
        public string filter { get; set; }

        public Func<StlFile, bool> filterCallback { get; set; } = null;
        public SelectorListControl()
        {
            filterCallback = (stl) => filter == null
                || stl.Name == "base.stl"
                || stl.FullPath.Contains(filter)
                || (!string.IsNullOrWhiteSpace(stl.UiName) && stl.UiName.Contains(filter));
            RegisterCallback<AttachToPanelEvent>(AttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
            });

        }

        private void AttachToPanel(AttachToPanelEvent evt)
        {
                VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("SelectorListControl");
                uiAsset.CloneTree(this);
                list = this.Q<MultiColumnListView>("SelectedItemsList");

            list.selectionChanged += (selectionList) =>
            {
                var stl = selectionList.FirstOrDefault() as StlFile;
                Notify(stl);
            };
                list.columns["name"].makeCell = () => new Label();
                list.columns["modifier"].makeCell = () => new DropdownField();
                list.columns["remove"].makeCell = () => new Button() { text = "-" };
                list.columns["name"].bindCell = (VisualElement element, int index) =>
                    (element as Label).text = (list.itemsSource[index] as StlFile).Name;
                list.columns["modifier"].bindCell = (VisualElement element, int index) =>
                {
                    var t = element as DropdownField;
                    t.UnregisterValueChangedCallback(applyClear);
                    var item = list.itemsSource[index] as StlFile;
                    t.choices = DataManager.Instance.AllClears.Where(c => c.Name[0] == '1' || (filter != null && c.Name[0] == filter[0])).Select(c => c.Name).ToList();
                    t.choices.Insert(0, "None");
                    t.SetValueWithoutNotify(item.ClearToApply != null ? System.IO.Path.GetFileName(item.ClearToApply) : "None");
                    t.userData = item;
                    t.RegisterValueChangedCallback(applyClear);
                };
                list.columns["remove"].bindCell = (VisualElement element, int index) =>
                {
                    var t = element as Button;
                    var item = list.itemsSource[index] as StlFile;
                    t.SetEnabled(item.SelectionCanChange);
                    t.userData = item;
                    //    ZoomCameraTo(item.FullPath);
                    t.UnregisterCallback<ClickEvent>(removeObjectCallback);
                    t.RegisterCallback<ClickEvent>(removeObjectCallback);
                };
                DataManager.Instance.OnAppliedChanged += OnAppliedChanged;
        }

        private void OnAppliedChanged()
        {

            list.itemsSource = DataManager.Instance.AllApplied.Where(o => filterCallback(o)).ToList();
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

        private void Notify(StlFile stl)
        {
            if (OnSelectionChanged != null)
            {
                OnSelectionChanged(stl);
            }
        }
        public void SetSelection(StlFile stl)
        {
            var index = list.itemsSource.IndexOf(stl);
            list.SetSelection(index >= 0 ? new List<int>() { index } : new List<int>());
        }
    }
}
