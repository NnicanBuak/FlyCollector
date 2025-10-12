using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]
public class CreditsController : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.6f;

    [Header("Auto Proceed")]
    [Tooltip("Automatically leave the credits after a short delay.")]
    [SerializeField] private bool autoProceed = true;
    [SerializeField] private float autoProceedDelay = 6f;

    [Header("Countdown UI")]
    [SerializeField] private bool showCountdown = false;
    [SerializeField] private TextMeshProUGUI countdownLabel;

    private SceneTransition transition;
    private bool transitioning;
    private Coroutine autoProceedRoutine;

    private void Awake()
    {
        transition = new SceneTransition(fadeDuration);
        if (autoProceed && autoProceedDelay > 0f)
            autoProceedRoutine = StartCoroutine(AutoProceedAfterDelay());
        else
            UpdateCountdownLabel(-1f);
    }

    private void Update()
    {
        if (transitioning) return;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            GoToMenu();
            return;
        }

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
        {
            GoToMenu();
            return;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            GoToMenu();
            return;
        }
    }

    private void OnDisable()
    {
        if (autoProceedRoutine != null)
        {
            StopCoroutine(autoProceedRoutine);
            autoProceedRoutine = null;
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

    private IEnumerator AutoProceedAfterDelay()
    {
        float remaining = Mathf.Max(0f, autoProceedDelay);
        while (remaining > 0f)
        {
            UpdateCountdownLabel(remaining);
            yield return null;
            remaining -= Time.unscaledDeltaTime;
        }

        UpdateCountdownLabel(0f);

        if (!transitioning)
            GoToMenu();
    }

    private void UpdateCountdownLabel(float secondsRemaining)
    {
        if (!showCountdown || countdownLabel == null)
            return;

        if (secondsRemaining < 0f)
        {
            countdownLabel.text = string.Empty;
            return;
        }

        int seconds = Mathf.CeilToInt(secondsRemaining);
        countdownLabel.text = seconds > 0 ? seconds.ToString() : string.Empty;
    }
}
