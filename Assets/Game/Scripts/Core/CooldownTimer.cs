using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple countdown timer component (not a singleton, can be used multiple times)
/// Useful for cooldowns, delays, temporary locks, etc.
/// </summary>
public class CooldownTimer : MonoBehaviour
{
    #region Serialized Fields
    [Header("Timer Settings")]
    [Tooltip("Duration in seconds")]
    [SerializeField] private float duration = 5f;

    [Tooltip("Start timer automatically on Awake")]
    [SerializeField] private bool startOnAwake = false;

    [Tooltip("Restart timer automatically when it finishes")]
    [SerializeField] private bool loop = false;

    [Header("Events")]
    [Tooltip("Called when timer starts")]
    public UnityEvent OnTimerStart;

    [Tooltip("Called when timer finishes")]
    public UnityEvent OnTimerComplete;

    [Tooltip("Called every second")]
    public UnityEvent<int> OnSecondTick;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;
    #endregion

    #region Properties
    /// <summary>
    /// Is timer currently running
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Time remaining in seconds
    /// </summary>
    public float TimeRemaining { get; private set; }

    /// <summary>
    /// Progress from 0.0 to 1.0 (0 = just started, 1 = finished)
    /// </summary>
    public float Progress => duration > 0 ? Mathf.Clamp01(1f - (TimeRemaining / duration)) : 1f;

    /// <summary>
    /// Progress from 1.0 to 0.0 (1 = just started, 0 = finished)
    /// </summary>
    public float ReverseProgress => duration > 0 ? Mathf.Clamp01(TimeRemaining / duration) : 0f;
    #endregion

    #region Events
    public event System.Action OnStart;
    public event System.Action OnComplete;
    public event System.Action<float> OnTick;
    #endregion

    #region Unity Lifecycle
    private int _lastSecond = -1;

    private void Awake()
    {
        TimeRemaining = duration;

        if (startOnAwake)
        {
            StartTimer();
        }
    }

    private void Update()
    {
        if (!IsRunning) return;

        TimeRemaining -= Time.deltaTime;

        // Tick event
        OnTick?.Invoke(TimeRemaining);

        // Second tick event
        int currentSecond = Mathf.CeilToInt(TimeRemaining);
        if (currentSecond != _lastSecond && currentSecond >= 0)
        {
            _lastSecond = currentSecond;
            OnSecondTick?.Invoke(currentSecond);

            if (showDebug)
            {
                Debug.Log($"[CooldownTimer] {gameObject.name}: {currentSecond}s remaining");
            }
        }

        // Timer finished
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            IsRunning = false;

            if (showDebug)
            {
                Debug.Log($"[CooldownTimer] {gameObject.name}: Timer complete!");
            }

            OnTimerComplete?.Invoke();
            OnComplete?.Invoke();

            if (loop)
            {
                StartTimer();
            }
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Start or restart the timer
    /// </summary>
    public void StartTimer()
    {
        TimeRemaining = duration;
        IsRunning = true;
        _lastSecond = -1;

        if (showDebug)
        {
            Debug.Log($"[CooldownTimer] {gameObject.name}: Timer started ({duration}s)");
        }

        OnTimerStart?.Invoke();
        OnStart?.Invoke();
    }

    /// <summary>
    /// Start timer with custom duration
    /// </summary>
    public void StartTimer(float customDuration)
    {
        duration = customDuration;
        StartTimer();
    }

    /// <summary>
    /// Stop the timer
    /// </summary>
    public void StopTimer()
    {
        IsRunning = false;

        if (showDebug)
        {
            Debug.Log($"[CooldownTimer] {gameObject.name}: Timer stopped");
        }
    }

    /// <summary>
    /// Reset timer to initial duration (doesn't start it)
    /// </summary>
    public void ResetTimer()
    {
        TimeRemaining = duration;
        IsRunning = false;
        _lastSecond = -1;

        if (showDebug)
        {
            Debug.Log($"[CooldownTimer] {gameObject.name}: Timer reset");
        }
    }

    /// <summary>
    /// Add time to remaining duration
    /// </summary>
    public void AddTime(float seconds)
    {
        TimeRemaining += seconds;

        if (showDebug)
        {
            Debug.Log($"[CooldownTimer] {gameObject.name}: Added {seconds}s, remaining: {TimeRemaining}s");
        }
    }

    /// <summary>
    /// Set time remaining directly
    /// </summary>
    public void SetTimeRemaining(float seconds)
    {
        TimeRemaining = Mathf.Max(0f, seconds);
    }

    /// <summary>
    /// Check if timer has finished
    /// </summary>
    public bool IsComplete()
    {
        return !IsRunning && TimeRemaining <= 0f;
    }
    #endregion

    #region Gizmos
    #endregion
}
