using System;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PromptDialog : VisualElement
{
    private static PromptDialog CreateWindow()
    {
        var window = new PromptDialog();
        window.style.position = Position.Absolute;
        window.style.top = 100;
        window.style.left = 100;
        window.style.height = 200;
        window.style.backgroundColor = new StyleColor(Color.red);
        window.style.justifyContent = new StyleEnum<Justify>(Justify.SpaceAround);
        return window;
    }

    private static void AddButtonRow(VisualElement window, params Button[] buttons)
    {
        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        buttonContainer.style.justifyContent = new StyleEnum<Justify>(Justify.SpaceAround);
        foreach (var button in buttons)
        {
            button.style.flexGrow = 1;
            buttonContainer.Add(button);
        }
        window.Add(buttonContainer);
    }

    public static void Show(VisualElement owner, string pTitle, string pDescription, string pText, Action<string> successCallback, string pOkButton = "Ok", string pCancelButton = "Cancel")
    {
        string r = null;
        var window = CreateWindow();
        //window.titleContent = new GUIContent(pTitle);

        if (!string.IsNullOrWhiteSpace(pTitle))
        {
            var titleLabel = new Label(pTitle);
            window.Add(titleLabel);
        }

        var label = new Label(pDescription);
        window.Add(label);

        var inputText = new TextField();
        inputText.value = pText;
        UiInputCaptureState.TrackTextInput(inputText);
        window.Add(inputText);

        Action okCallback = () =>
        {
            r = inputText.value;
            window.Close(owner);
            successCallback(r);
        };
        Action cancelCallback = () =>
        {
            r = inputText.value;
            window.Close(owner);
        };

        var okButton = new Button(okCallback) { text = pOkButton };
        var cancelButton = new Button(cancelCallback) { text = pCancelButton };
        AddButtonRow(window, okButton, cancelButton);

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

        window.schedule.Execute(() =>
        {
            inputText.Focus();
        }).ExecuteLater(10);

        owner?.Add(window);
    }

    public static void ShowChoice(VisualElement owner, string pTitle, string pDescription, string firstButton, Action firstCallback, string secondButton, Action secondCallback, string pCancelButton = "Cancel")
    {
        var window = CreateWindow();

        if (!string.IsNullOrWhiteSpace(pTitle))
        {
            var titleLabel = new Label(pTitle);
            window.Add(titleLabel);
        }

        var label = new Label(pDescription);
        window.Add(label);

        Action close = () => window.Close(owner);

        var first = new Button(() =>
        {
            close();
            firstCallback?.Invoke();
        }) { text = firstButton };

        var second = new Button(() =>
        {
            close();
            secondCallback?.Invoke();
        }) { text = secondButton };

        var cancel = new Button(() =>
        {
            close();
        }) { text = pCancelButton };

        AddButtonRow(window, first, second, cancel);
        owner?.Add(window);
    }

    private void Close(VisualElement owner)
    {
        this.style.display = DisplayStyle.None;
        this.RemoveFromHierarchy();
    }

}
