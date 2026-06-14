using System.Collections.Generic;
using UnityEngine.UIElements;

public static class UiInputCaptureState
{
    private static readonly HashSet<VisualElement> hoveredUiElements = new HashSet<VisualElement>();
    private static readonly HashSet<VisualElement> focusedTextInputs = new HashSet<VisualElement>();

    public static bool IsPointerOverTabView => hoveredUiElements.Count > 0;
    public static bool IsPointerOverUiToolkit => IsPointerOverTabView;
    public static bool IsTextInputFocused => focusedTextInputs.Count > 0;

    public static void TrackPointerHover(VisualElement element)
    {
        if (element == null)
        {
            return;
        }

        element.RegisterCallback<PointerOverEvent>(_ => hoveredUiElements.Add(element), TrickleDown.TrickleDown);
        element.RegisterCallback<PointerOutEvent>(_ => hoveredUiElements.Remove(element), TrickleDown.TrickleDown);
        element.RegisterCallback<DetachFromPanelEvent>(_ => hoveredUiElements.Remove(element));
    }

    public static void TrackTextInput(VisualElement element)
    {
        if (element == null)
        {
            return;
        }

        element.RegisterCallback<FocusInEvent>(_ => focusedTextInputs.Add(element), TrickleDown.TrickleDown);
        element.RegisterCallback<FocusOutEvent>(_ => focusedTextInputs.Remove(element), TrickleDown.TrickleDown);
        element.RegisterCallback<DetachFromPanelEvent>(_ => focusedTextInputs.Remove(element));
    }
}
