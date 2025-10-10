using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInputHandler
{
    private readonly Mouse mouse;
    private readonly Keyboard keyboard;

    public CameraInputHandler()
    {
        mouse = Mouse.current;
        keyboard = Keyboard.current;
    }

    public bool IsLeftClickPressed() => mouse != null && mouse.leftButton.wasPressedThisFrame;
    public bool IsRightClickPressed() => mouse != null && mouse.rightButton.wasPressedThisFrame;
    public bool IsEscapePressed() => keyboard != null && keyboard[Key.Escape].wasPressedThisFrame;
    public bool IsEnterPressed() => keyboard != null && keyboard[Key.Enter].wasPressedThisFrame;

    public Vector2 GetMousePosition()
    {
        return mouse != null
            ? mouse.position.ReadValue()
            : new Vector2(Screen.width / 2f, Screen.height / 2f);
    }

    public bool IsExitInputPressed() => IsEscapePressed() || IsRightClickPressed();
}
