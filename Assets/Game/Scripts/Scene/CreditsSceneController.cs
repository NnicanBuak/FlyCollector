using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CreditsController : MonoBehaviour
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
            GoToMenu();
            return;
        }

        // Check for mouse buttons
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
        {
            GoToMenu();
            return;
        }

        // Check for touch input
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            GoToMenu();
            return;
        }
    }

    private void GoToMenu()
    {
        if (transitioning) return;
        transitioning = true;

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene("MainMenu", transition);
        else
            Debug.LogError("[CreditsController] GameSceneManager.Instance == null");
    }
}