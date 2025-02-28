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
    public partial class TransformsControl : VisualElement
    {
        private TransformTrioControl _positionTrio;
        private TransformTrioControl _scaleTrio;
        private TransformTrioControl _rotationTrio;
        private StlFile _selectedImport;

        public TransformsControl()
        {
            RegisterCallback<AttachToPanelEvent>(e =>
            { /* do something here when element is added to UI */
                VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("TransformsControl");
                uiAsset.CloneTree(this);

                _positionTrio = this.Q<TransformTrioControl>("Position");
                _scaleTrio = this.Q<TransformTrioControl>("Scale");
                _rotationTrio = this.Q<TransformTrioControl>("Rotation");
                _positionTrio.OnTransfromsChanged += TransformEntryChanged;
                _scaleTrio.OnTransfromsChanged += TransformEntryChanged;
                _rotationTrio.OnTransfromsChanged += TransformEntryChanged;
                DataManager.Instance.OnDataChanged += OnDataChanged;
                DataManager.Instance.OnAppliedChanged += OnAppliedChanged;

            });
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
            });
        }

        private void TransformEntryChanged(TransformTrioControl sender, Vector3 newValues)
        {
            var t = new Transforms()
            {
                Position = _positionTrio.GetTransformValues(),
                Scale = _scaleTrio.GetTransformValues(),
                Rotations = _rotationTrio.GetTransformValues(),
            };
            _selectedImport.Transforms = t;
            DataManager.Instance.ApplyTransfroms(_selectedImport);
        }

        public void SetSelectedImport(StlFile stl)
        {
            _selectedImport = stl;
            if (stl != null && stl.Transforms?.Position?.Length > 0)
            {
                PopulateTransformEntries(stl.Transforms);
                _positionTrio.SetEnabled(true);
                _scaleTrio.SetEnabled(true);
                _rotationTrio.SetEnabled(true);
            }
            else
            {
                PopulateTransformEntries(Transforms.zero);
                _positionTrio.SetEnabled(false);
                _scaleTrio.SetEnabled(false);
                _rotationTrio.SetEnabled(false);
            }

        }

        public void PopulateTransformEntries(Transforms t)
        {
            _positionTrio.SetTransformValue(TransformTrioControl.Axis.BackToFront, t.Position[0]);
            _positionTrio.SetTransformValue(TransformTrioControl.Axis.DownToUp, t.Position[1]);
            _positionTrio.SetTransformValue(TransformTrioControl.Axis.LeftToRight, t.Position[2]);

            _scaleTrio.SetTransformValue(TransformTrioControl.Axis.BackToFront, t.Scale[0]);
            _scaleTrio.SetTransformValue(TransformTrioControl.Axis.DownToUp, t.Scale[1]);
            _scaleTrio.SetTransformValue(TransformTrioControl.Axis.LeftToRight, t.Scale[2]);

            _rotationTrio.SetTransformValue(TransformTrioControl.Axis.BackToFront, t.Rotations[0]);
            _rotationTrio.SetTransformValue(TransformTrioControl.Axis.DownToUp, t.Rotations[1]);
            _rotationTrio.SetTransformValue(TransformTrioControl.Axis.LeftToRight, t.Rotations[2]);
            
            if (t.UniformScale)
            {
                _scaleTrio.SetUniformWithoutNotify(true);
            }
        }

        private void OnAppliedChanged()
        {
            
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
