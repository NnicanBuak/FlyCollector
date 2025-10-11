using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class CameraInputHandler
{
    public bool IsLeftClickPressed()
    {
        var currentMouse = Mouse.current;
        return currentMouse != null && currentMouse.leftButton.wasPressedThisFrame;
    }

    public bool IsRightClickPressed()
    {
        var currentMouse = Mouse.current;
        return currentMouse != null && currentMouse.rightButton.wasPressedThisFrame;
    }

    public bool IsEscapePressed()
    {
        var currentKeyboard = Keyboard.current;
        return currentKeyboard != null && currentKeyboard.escapeKey.wasPressedThisFrame;
    }

    /// <summary>
    /// Выходит из инспекта при нажатии ЛКМ или ESC.
    /// Обработка RMB находится внутри InspectSession (collect-mode).
    /// </summary>
    public bool IsInspectExitPressed()
    {
        // ESC always exits
        if (IsEscapePressed()) return true;

        // LMB exits only when not clicking over UI
        var currentMouse = Mouse.current;
        if (currentMouse != null && currentMouse.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            return true;
        }

        return false;
    }
}
