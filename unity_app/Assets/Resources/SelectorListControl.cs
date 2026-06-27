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
        private DataManager.DataChanged _dataRefreshHandler;
        private DataManager.AppliedChanged _appliedRefreshHandler;

        public delegate void SelectionChanged(StlFile stl);
        public event SelectionChanged OnSelectionChanged;

        [UxmlAttribute]
        public string filter { get; set; }

        public Func<StlFile, bool> filterCallback { get; set; } = null;
        public Func<IEnumerable<StlFile>> itemsSourceProvider { get; set; } = null;
        public Func<StlFile, string> actionLabelProvider { get; set; } = null;
        public Action<StlFile> actionCallback { get; set; } = null;
        public SelectorListControl()
        {
            filterCallback = (stl) => filter == null
                || stl.Name == "base.stl"
                || stl.FullPath.Contains(filter)
                || (!string.IsNullOrWhiteSpace(stl.UiName) && stl.UiName.Contains(filter));
            RegisterCallback<AttachToPanelEvent>(AttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
                if (_dataRefreshHandler != null)
                {
                    DataManager.Instance.OnDataChanged -= _dataRefreshHandler;
                    _dataRefreshHandler = null;
                }

                if (_appliedRefreshHandler != null)
                {
                    DataManager.Instance.OnAppliedChanged -= _appliedRefreshHandler;
                    _appliedRefreshHandler = null;
                }
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
                    t.text = actionLabelProvider?.Invoke(item) ?? "-";
                    t.UnregisterCallback<ClickEvent>(removeObjectCallback);
                    t.RegisterCallback<ClickEvent>(removeObjectCallback);
                };
                RefreshItems();

                _dataRefreshHandler = RefreshItems;
                _appliedRefreshHandler = RefreshItems;
                DataManager.Instance.OnDataChanged += _dataRefreshHandler;
                DataManager.Instance.OnAppliedChanged += _appliedRefreshHandler;
        }

        private void OnAppliedChanged()
        {
            RefreshItems();
        }

        private IEnumerable<StlFile> GetItems()
        {
            var source = itemsSourceProvider?.Invoke() ?? DataManager.Instance.AllApplied;
            if (source == null)
                return Enumerable.Empty<StlFile>();

            return filterCallback == null ? source : source.Where(o => filterCallback(o));
        }

        public void RefreshItems()
        {
            if (list == null)
                return;

            list.itemsSource = GetItems().ToList();
            list.RefreshItems();
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
                var stl = item.userData as StlFile;
                if (stl == null)
                    return;

                if (actionCallback != null)
                {
                    actionCallback(stl);
                    return;
                }

                DataManager.Instance.RemoveObject(stl);
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
            var index = list.itemsSource == null
                ? -1
                : list.itemsSource.OfType<StlFile>()
                    .Select((item, i) => new { item, i })
                    .FirstOrDefault(x => ReferenceEquals(x.item, stl)
                        || (x.item != null && stl != null && string.Equals(x.item.FullPath, stl.FullPath, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.item.Name, stl.Name, StringComparison.OrdinalIgnoreCase)))?.i ?? -1;
            list.SetSelection(index >= 0 ? new List<int>() { index } : new List<int>());
        }
    }
}
