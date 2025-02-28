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
    public partial class TransformTrioControl : VisualElement
    {
        public enum Axis
        {
            BackToFront,
            DownToUp,
            LeftToRight,
        }

        public delegate void TransformsChanged(TransformTrioControl sender, Vector3 newValues);
        public event TransformsChanged OnTransfromsChanged;

        private TextField _backToFront;
        private TextField _downToUp;
        private TextField _leftToRight;
        private Label _descriptionLabel;
        private Toggle _uniformToggle;

        [UxmlAttribute]
        public string description { get; set; }

        [UxmlAttribute]
        public bool showUniformToggle { get; set; }

        private bool _uniform = false;
        public bool uniform 
        { 
            get
            {
                return _uniform;
            } 
            set
            {
                _uniform = value;
                if (_uniform )
                {
                    _uniformToggle.value = value;
                }
            }
        }
        public void SetUniformWithoutNotify(bool newValue) 
        { 
            _uniformToggle.SetValueWithoutNotify(newValue);
            _uniform = newValue;
        }
        public TransformTrioControl()
        {
//            UnityEngine.Debug.Log($"TransformTrioConstructor");

            RegisterCallback<AttachToPanelEvent>(AttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
            });

        }

        private void AttachToPanel(AttachToPanelEvent evt)
        {
            VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("TransformTrioControl");
            uiAsset.CloneTree(this);

            _descriptionLabel = this.Q<Label>("description");
            _descriptionLabel.text = description;
//            UnityEngine.Debug.Log($"TransformTrio setting description to {description}");

            _uniformToggle = this.Q<Toggle>("uniformToggle");

            _uniformToggle.style.display = showUniformToggle ? DisplayStyle.Flex : DisplayStyle.None;
            _uniformToggle.RegisterValueChangedCallback((evt) =>
            {
                if (evt.newValue)
                {
                    _downToUp.SetValueWithoutNotify(_backToFront.value);
                    _leftToRight.SetValueWithoutNotify(_backToFront.value);
                    Notify();
                }
            });
            
            _backToFront = this.Q<TextField>("BackToFront");
            _downToUp = this.Q<TextField>("DownToUp");
            _leftToRight = this.Q<TextField>("LeftToRight");

            _backToFront.RegisterCallback<FocusOutEvent>(TransformEntryChanged);
            _downToUp.RegisterCallback<FocusOutEvent>(TransformEntryChanged);
            _leftToRight.RegisterCallback<FocusOutEvent>(TransformEntryChanged);
        }

        private void TransformEntryChanged(FocusOutEvent evt)
        {
            if (_uniformToggle.value)
            {
                var tf = evt.target as TextElement;
                _backToFront.SetValueWithoutNotify(tf.text);
                _downToUp.SetValueWithoutNotify(tf.text);
                _leftToRight.SetValueWithoutNotify(tf.text);
            }
            Notify();
        }
        private void Notify()
        {
            if (OnTransfromsChanged != null)
            {
                OnTransfromsChanged(this, new Vector3(
                                                GetTransformValue(Axis.BackToFront),
                                                GetTransformValue(Axis.DownToUp),
                                                GetTransformValue(Axis.LeftToRight)));
            }
        }
        public float GetTransformValue(Axis axis) 
        {
            TextField chosen = null;
            switch (axis) 
            {
                case Axis.BackToFront: chosen = _backToFront; break;
                case Axis.DownToUp: chosen = _downToUp; break;
                case Axis.LeftToRight: chosen = _leftToRight; break;
            }
            return Utils.GetEntryValue(chosen);
        }

        public void SetTransformValue(Axis axis, float value)
        {
            TextField chosen = null;
            switch (axis)
            {
                case Axis.BackToFront: chosen = _backToFront; break;
                case Axis.DownToUp: chosen = _downToUp; break;
                case Axis.LeftToRight: chosen = _leftToRight; break;
            }
            chosen.SetValueWithoutNotify(value.ToString());
        }

        internal float[] GetTransformValues()
        {
            return new float[] { GetTransformValue(Axis.BackToFront), GetTransformValue(Axis.DownToUp), GetTransformValue(Axis.LeftToRight) };
        }
    }
}
