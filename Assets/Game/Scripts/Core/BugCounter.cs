using UnityEngine;
using System;

public class BugCounter : MonoBehaviour
{
    public static BugCounter Instance { get; private set; }

    [Header("Jar Settings")]
    [Tooltip("Maximum number of jars available")]
    [SerializeField] private int maxJars = 3;

    [Tooltip("Starting number of jars (if different from max)")]
    [SerializeField] private int startingJars = -1;

    [Header("Persistence")]
    [Tooltip("Persist between scenes (DontDestroyOnLoad)")]
    [SerializeField] private bool persistBetweenScenes = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private int _currentJars;

    public int CurrentJars => _currentJars;
    public int MaxJars => maxJars;
    public bool HasAnyJars => _currentJars > 0;


    public event Action<int> OnJarsChanged;
    public event Action OnJarsEmpty;
    public event Action OnJarsRefilled;

    void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }


        _currentJars = startingJars >= 0 ? startingJars : maxJars;

        if (showDebug)
        {
            Debug.Log($"[BugCounter] Initialized with {_currentJars} jars (max: {maxJars})");
        }
    }


    public bool DecrementJars(int amount = 1)
    {
        if (_currentJars <= 0)
        {
            if (showDebug)
            {
                Debug.LogWarning("[BugCounter] Cannot decrement - no jars available");
            }
            return false;
        }

        int previousCount = _currentJars;
        _currentJars = Mathf.Max(0, _currentJars - amount);

        if (showDebug)
        {
            Debug.Log($"[BugCounter] Decremented: {previousCount} → {_currentJars}");
        }

        OnJarsChanged?.Invoke(_currentJars);

        if (_currentJars == 0)
        {
            if (showDebug)
            {
                Debug.Log("[BugCounter] ⚠️ Jars empty!");
            }
            OnJarsEmpty?.Invoke();
        }

        return true;
    }


    public void AddJars(int amount = 1)
    {
        int previousCount = _currentJars;
        _currentJars = Mathf.Min(maxJars, _currentJars + amount);

        if (showDebug)
        {
            Debug.Log($"[BugCounter] Added jars: {previousCount} → {_currentJars}");
        }

        OnJarsChanged?.Invoke(_currentJars);

        if (previousCount == 0 && _currentJars > 0)
        {
            OnJarsRefilled?.Invoke();
        }
    }


    public void SetJarCount(int count)
    {
        int previousCount = _currentJars;
        _currentJars = Mathf.Clamp(count, 0, maxJars);

        if (showDebug)
        {
            Debug.Log($"[BugCounter] Set jars: {previousCount} → {_currentJars}");
        }

        OnJarsChanged?.Invoke(_currentJars);

        if (_currentJars == 0 && previousCount > 0)
        {
            OnJarsEmpty?.Invoke();
        }
        else if (_currentJars > 0 && previousCount == 0)
        {
            OnJarsRefilled?.Invoke();
        }
    }


    public void RefillJars()
    {
        SetJarCount(maxJars);
    }


    public bool HasJars(int requiredAmount)
    {
        return _currentJars >= requiredAmount;
    }
}
