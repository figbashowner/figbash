using System;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PromptDialog : VisualElement
{
    private const string WelcomeDialogPreferenceKey = "FigBash.ShowWelcomeDialog";
    private static VisualTreeAsset _template;

    private VisualElement _card;
    private Label _titleLabel;
    private Label _descriptionLabel;
    private TextField _inputText;
    private VisualElement _buttonRow;

    public PromptDialog()
    {
        AddToClassList("prompt-dialog");
        style.position = Position.Absolute;
        pickingMode = PickingMode.Ignore;
    }

    private static PromptDialog CreateWindow()
    {
        var window = new PromptDialog();
        window.InitializeTemplate();
        return window;
    }

    private void InitializeTemplate()
    {
        if (_card != null)
        {
            return;
        }

        var template = LoadTemplate();
        if (template == null)
        {
            BuildFallback();
            return;
        }

        template.CloneTree(this);
        CacheTemplateElements();

        if (_card == null || _titleLabel == null || _descriptionLabel == null || _inputText == null || _buttonRow == null)
        {
            Debug.LogWarning("PromptDialog UXML is missing one or more expected elements.");
            Clear();
            BuildFallback();
            return;
        }

        _titleLabel.style.display = DisplayStyle.None;
        _inputText.style.display = DisplayStyle.None;
    }

    private static VisualTreeAsset LoadTemplate()
    {
        if (_template != null)
        {
            return _template;
        }

        _template = Resources.Load<VisualTreeAsset>("PromptDialog");
        if (_template == null)
        {
            Debug.LogWarning("PromptDialog UXML could not be loaded from Resources/PromptDialog");
        }

        return _template;
    }

    private void CacheTemplateElements()
    {
        _card = this.Q<VisualElement>("PromptDialogCard");
        _titleLabel = this.Q<Label>("PromptDialogTitle");
        _descriptionLabel = this.Q<Label>("PromptDialogDescription");
        _inputText = this.Q<TextField>("PromptDialogInput");
        _buttonRow = this.Q<VisualElement>("PromptDialogButtonRow");
        UiInputCaptureState.TrackTextInput(_inputText);
    }

    private void BuildFallback()
    {
        Clear();

        _card = new VisualElement();
        _card.AddToClassList("panel-card");
        _card.AddToClassList("repo-dialog");

        _titleLabel = new Label();
        _titleLabel.AddToClassList("repo-dialog__title");
        _titleLabel.style.display = DisplayStyle.None;

        _descriptionLabel = new Label();
        _descriptionLabel.AddToClassList("repo-dialog__description");

        _inputText = new TextField();
        _inputText.AddToClassList("repo-dialog__input");
        _inputText.style.display = DisplayStyle.None;
        UiInputCaptureState.TrackTextInput(_inputText);

        _buttonRow = new VisualElement();
        _buttonRow.AddToClassList("repo-dialog__button-row");

        _card.Add(_titleLabel);
        _card.Add(_descriptionLabel);
        _card.Add(_inputText);
        _card.Add(_buttonRow);
        Add(_card);
    }

    private void SetTitle(string title)
    {
        if (_titleLabel == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            _titleLabel.text = string.Empty;
            _titleLabel.style.display = DisplayStyle.None;
            return;
        }

        _titleLabel.text = title;
        _titleLabel.style.display = DisplayStyle.Flex;
    }

    private void SetDescription(string description)
    {
        if (_descriptionLabel == null)
        {
            return;
        }

        _descriptionLabel.text = description ?? string.Empty;
    }

    private void SetInput(string text, bool visible)
    {
        if (_inputText == null)
        {
            return;
        }

        _inputText.value = text ?? string.Empty;
        _inputText.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ClearButtons()
    {
        _buttonRow?.Clear();
    }

    private void AddButtons(params Button[] buttons)
    {
        if (_buttonRow == null || buttons == null)
        {
            return;
        }

        ClearButtons();

        foreach (var button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            button.AddToClassList("repo-dialog__button");
            _buttonRow.Add(button);
        }
    }

    public static void Show(VisualElement owner, string pTitle, string pDescription, string pText, Action<string> successCallback, string pOkButton = "Ok", string pCancelButton = "Cancel")
    {
        string r = null;
        var window = CreateWindow();

        window.SetTitle(pTitle);
        window.SetDescription(pDescription);
        window.SetInput(pText, true);

        Action okCallback = () =>
        {
            r = window._inputText?.value;
            window.Close(owner);
            try
            {
                successCallback?.Invoke(r);
            }
            catch { }
        };

        Action cancelCallback = () =>
        {
            r = window._inputText?.value;
            window.Close(owner);
        };

        var okButton = new Button(okCallback) { text = pOkButton };
        var cancelButton = new Button(cancelCallback) { text = pCancelButton };
        window.AddButtons(okButton, cancelButton);

        window.RegisterCallback<KeyUpEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                cancelCallback();
            }

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                okCallback();
            }
        });

        AttachWindow(owner, window);
        window.schedule.Execute(() =>
        {
            window._inputText?.Focus();
        }).ExecuteLater(10);
    }

    public static void ShowChoice(VisualElement owner, string pTitle, string pDescription, string firstButton, Action firstCallback, string secondButton, Action secondCallback, string pCancelButton = "Cancel")
    {
        var window = CreateWindow();

        window.SetTitle(pTitle);
        window.SetDescription(pDescription);
        window.SetInput(string.Empty, false);

        Action close = () => window.Close(owner);

        var first = new Button(() =>
        {
            close();
            try
            {
                firstCallback?.Invoke();
            }
            catch { }
        }) { text = firstButton };

        var second = new Button(() =>
        {
            close();
            try
            {
                secondCallback?.Invoke();
            }
            catch { }
        }) { text = secondButton };

        var cancel = new Button(() =>
        {
            close();
        }) { text = pCancelButton };

        window.AddButtons(first, second, cancel);
        AttachWindow(owner, window);
    }

    public static void ShowAlert(VisualElement owner, string pTitle, string pDescription, string pOkButton = "OK")
    {
        var window = CreateWindow();

        window.SetTitle(pTitle);
        window.SetDescription(pDescription);
        window.SetInput(string.Empty, false);

        var okButton = new Button(() => window.Close(owner)) { text = pOkButton };
        window.AddButtons(okButton);
        AttachWindow(owner, window);
    }

    public static void ShowOrientation(VisualElement owner)
    {
        if (!ShouldShowOrientation())
            return;

        var window = CreateWindow();

        window.SetTitle("Welcome to FigBash.");
        window.SetDescription("FigBash is a system of 3d parts that can combine to create your own custom action figure.");
        window.SetInput(string.Empty, false);

        Action close = () => window.Close(owner);

        var continueButton = new Button(() =>
        {
            close();
        })
        {
            text = "OK"
        };

        var dontShowAgainButton = new Button(() =>
        {
            SetShowOrientation(false);
            close();
        })
        {
            text = "Don't show this again"
        };

        window.AddButtons(continueButton, dontShowAgainButton);

        window.RegisterCallback<KeyUpEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                close();
            }

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                close();
            }
        });

        AttachWindow(owner, window);
    }

    public static bool ShouldShowOrientation()
    {
        return PlayerPrefs.GetInt(WelcomeDialogPreferenceKey, 1) != 0;
    }

    private static void SetShowOrientation(bool show)
    {
        PlayerPrefs.SetInt(WelcomeDialogPreferenceKey, show ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void Close(VisualElement owner)
    {
        RemoveFromHierarchy();
    }

    private static void AttachWindow(VisualElement owner, PromptDialog window)
    {
        if (owner == null || window == null)
        {
            return;
        }

        owner.Add(window);
        window.BringToFront();
    }
}
