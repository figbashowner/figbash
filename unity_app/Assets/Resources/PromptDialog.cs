using System;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class PromptDialog : VisualElement
{
    public static void Show(VisualElement owner, string pTitle, string pDescription, string pText, Action<string> successCallback, string pOkButton = "Ok", string pCancelButton = "Cancel")
    {
        string r = null;
        var window = new PromptDialog();
        //window.titleContent = new GUIContent(pTitle);
        window.style.height = new Length(100, LengthUnit.Percent);
        window.style.justifyContent = new StyleEnum<Justify>(Justify.SpaceAround);

        var label = new Label(pDescription);
        window.Add(label);

        var inputText = new TextField();
        inputText.value = pText;
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
        okButton.style.flexGrow = 1;
        var cancelButton = new Button(cancelCallback) { text = pCancelButton };
        cancelButton.style.flexGrow = 1;

        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        buttonContainer.style.justifyContent = new StyleEnum<Justify>(Justify.SpaceAround);
        buttonContainer.Add(okButton);
        buttonContainer.Add(cancelButton);
        window.Add(buttonContainer);

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

        window.style.position = Position.Absolute;
        window.style.top = 100;
        window.style.left = 100;
        window.style.height = 200;
        window.style.backgroundColor = new StyleColor(Color.red);
        window.schedule.Execute(() =>
        {
            inputText.Focus();
        }).ExecuteLater(10);

        owner.Add(window);
    }

    private void Close(VisualElement owner)
    {
        this.style.display = DisplayStyle.None;
        this.RemoveFromHierarchy();
    }

}