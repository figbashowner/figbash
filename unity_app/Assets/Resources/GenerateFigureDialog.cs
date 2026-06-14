using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets
{
    [UxmlElement]
    public partial class GenerateFigureDialog : VisualElement
    {
        private Toggle _two;
        private Toggle _three;
        private Toggle _six;
        private Toggle _nine;
        private RadioButtonGroup _toleranceRadio;
        private Button _generateButton;
        private Button _cancelButton;
        private bool _initialized;

        public event Action GenerateRequested;

        public GenerateFigureDialog()
        {
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            style.display = DisplayStyle.None;

            RegisterCallback<AttachToPanelEvent>(e =>
            {
                if (_initialized)
                    return;

                VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("GenerateFigureDialog");
                if (uiAsset == null)
                {
                    Debug.LogError("GenerateFigureDialog UXML could not be loaded from Resources/GenerateFigureDialog");
                    return;
                }

                uiAsset.CloneTree(this);

                _two = this.Q<Toggle>("Two");
                _three = this.Q<Toggle>("Three");
                _six = this.Q<Toggle>("Six");
                _nine = this.Q<Toggle>("Nine");
                _toleranceRadio = this.Q<RadioButtonGroup>("ToleranceRadio");
                _generateButton = this.Q<Button>("Generate");
                _cancelButton = this.Q<Button>("Cancel");

                if (_generateButton != null)
                    _generateButton.RegisterCallback<ClickEvent>(HandleGenerateClick);

                if (_cancelButton != null)
                    _cancelButton.RegisterCallback<ClickEvent>(HandleCancelClick);

                _initialized = true;
            });
        }

        public bool GenerateTwo => _two != null && _two.value;
        public bool GenerateThree => _three != null && _three.value;
        public bool GenerateSix => _six != null && _six.value;
        public bool GenerateNine => _nine != null && _nine.value;

        public string SelectedTolerance => _toleranceRadio == null
            ? "0.25"
            : _toleranceRadio.value switch
            {
                0 => "0.25",
                1 => "0.30",
                2 => "0.35",
                _ => "WHAA"
            };

        public void ShowDialog()
        {
            style.display = DisplayStyle.Flex;
        }

        public void HideDialog()
        {
            style.display = DisplayStyle.None;
        }

        private void HandleGenerateClick(ClickEvent evt)
        {
            HideDialog();
            GenerateRequested?.Invoke();
        }

        private void HandleCancelClick(ClickEvent evt)
        {
            HideDialog();
        }
    }
}
