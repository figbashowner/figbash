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

        private FloatField _backToFront;
        private FloatField _downToUp;
        private FloatField _leftToRight;
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
            
            _backToFront = this.Q<FloatField>("BackToFront");
            _downToUp = this.Q<FloatField>("DownToUp");
            _leftToRight = this.Q<FloatField>("LeftToRight");
            var setupFF = new Action<FloatField>((ff) =>
            {
                ff.SetRangeValidation(-1000, 1000);
                ff.RegisterValueChangedCallback(TransformEntryChanged);
                UiInputCaptureState.TrackTextInput(ff);

            });
            setupFF(_backToFront);
            setupFF(_downToUp);
            setupFF(_leftToRight); 
        }

        private void TransformEntryChanged(ChangeEvent<float> evt)
        {
            if (_uniformToggle.value)
            {
                var tf = evt.target as FloatField;
                if (tf != null)
                {
                    _backToFront.SetValueWithoutNotify(tf.value);
                    _downToUp.SetValueWithoutNotify(tf.value);
                    _leftToRight.SetValueWithoutNotify(tf.value);

                }
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
            FloatField chosen = null;
            switch (axis) 
            {
                case Axis.BackToFront: chosen = _backToFront; break;
                case Axis.DownToUp: chosen = _downToUp; break;
                case Axis.LeftToRight: chosen = _leftToRight; break;
            }
            return chosen.value;
        }

        public void SetTransformValue(Axis axis, float value)
        {
            FloatField chosen = null;
            switch (axis)
            {
                case Axis.BackToFront: chosen = _backToFront; break;
                case Axis.DownToUp: chosen = _downToUp; break;
                case Axis.LeftToRight: chosen = _leftToRight; break;
            }
            chosen.SetValueWithoutNotify(value);
        }

        internal float[] GetTransformValues()
        {
            return new float[] { GetTransformValue(Axis.BackToFront), GetTransformValue(Axis.DownToUp), GetTransformValue(Axis.LeftToRight) };
        }
    }
}


public static class FloatRangeExtension
{
    public static void SetRangeValidation(this FloatField ff, float minValue, float maxValue)
    {
        ff.RegisterCallback<ChangeEvent<float>>((evt) =>
        {
            if (evt.target == ff)
            {
                var validValue = Mathf.Clamp(evt.newValue, minValue, maxValue);

                if (!Mathf.Approximately(validValue, evt.newValue))
                {
                    Debug.Log($"Value {evt.newValue} is out of range");
                    evt.StopImmediatePropagation(); //we cancel the event notification
                    evt.PreventDefault(); // might be needed if running Unity 2022.3 and below
                    ff.value = validValue; //which will trigger another ChangeEvent to be dispatched
                }
                else
                {
                    Debug.Log($"Value {evt.newValue} is valid!");
                }
            }

        }, TrickleDown.TrickleDown);  //TrickleDown phase is key here
    }
}
