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

    public bool IsFlipKeyPressed()
    {
        var currentKeyboard = Keyboard.current;
        return currentKeyboard != null && currentKeyboard.fKey.wasPressedThisFrame;
    }

    public float GetScrollInput()
    {
        var currentMouse = Mouse.current;
        return currentMouse != null ? currentMouse.scroll.ReadValue().y : 0f;
    }

    public UnityEngine.Vector2 GetMouseScrollDelta()
    {
        var currentMouse = Mouse.current;
        return currentMouse != null ? currentMouse.scroll.ReadValue() : UnityEngine.Vector2.zero;
    }

    public bool GetMouseButtonDown(int button)
    {
        var currentMouse = Mouse.current;
        if (currentMouse == null) return false;
        
        switch (button)
        {
            case 0: return currentMouse.leftButton.wasPressedThisFrame;
            case 1: return currentMouse.rightButton.wasPressedThisFrame;
            case 2: return currentMouse.middleButton.wasPressedThisFrame;
            default: return false;
        }
    }

    public UnityEngine.Vector2 GetMousePosition()
    {
        var currentMouse = Mouse.current;
        return currentMouse != null ? currentMouse.position.ReadValue() : UnityEngine.Vector2.zero;
    }

    /// <summary>
    /// Выходит из инспекта при нажатии ЛКМ или ESC.
    /// Обработка RMB находится внутри InspectSession (collect-mode).
    /// </summary>
    public bool IsInspectExitPressed()
    {
        // ESC always exits
        if (IsEscapePressed()) return true;

        // RMB exits only when not clicking over UI
        var currentMouse = Mouse.current;
        if (currentMouse != null && currentMouse.rightButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            return true;
        }

        return false;
    }
}
