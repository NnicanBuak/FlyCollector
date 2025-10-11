using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class GameOverSceneController : MonoBehaviour
{
    [Header("Переход")]
    [SerializeField] private float fadeDuration = 0.6f;

    private SceneTransition transition;
    private bool transitioning;

    void Awake()
    {
        transition = new SceneTransition(fadeDuration);
    }

    void Update()
    {
        if (transitioning) return;

        // Check for any keyboard input
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            GoNext();
            return;
        }

        // Check for mouse buttons
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
        {
            GoNext();
            return;
        }

        // Check for touch input
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            GoNext();
            return;
        }
    }

    private void GoNext()
    {
        if (transitioning) return;
        transitioning = true;

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene("Credits", transition);
        else
            Debug.LogError("[GameOverController] GameSceneManager.Instance == null");
    }
}