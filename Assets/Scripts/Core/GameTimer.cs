using UnityEngine;
using UnityEngine.Events;

public class GameTimer : MonoBehaviour
{
    [Header("Настройки таймера")]
    [SerializeField] private float totalTime = 300f;
    [SerializeField] private bool startOnAwake = true;

    [Header("=== ЗАПУСК ПО ПЕРВОМУ ФОКУСУ ===")]
    [Tooltip("Запускать таймер при первом фокусе вместо startOnAwake")]
    [SerializeField] private bool startOnFirstFocus = false;
    
    [Tooltip("Запускать таймер при первом взаимодействии (фокус ИЛИ инспекция)")]
    [SerializeField] private bool startOnFirstInteraction = false;
    
    [Header("События - ПУБЛИЧНЫЕ для подписки из других скриптов")]
    [Tooltip("Событие когда проходит МИНУТНАЯ отсечка (например, с 5:00 на 4:59 → сработает как «прошла минута»)")]
    public UnityEvent onMinutePassed = new UnityEvent();
    
    [Tooltip("Событие когда таймер запустился")]
    public UnityEvent onTimerStart = new UnityEvent();
    
    [Tooltip("Событие когда таймер закончился")]
    public UnityEvent onTimerEnd = new UnityEvent();
    
    [Tooltip("Событие обновления времени (передает оставшееся время в секундах)")]
    public UnityEvent<float> onTimeUpdate = new UnityEvent<float>();


    public UnityEvent OnMinutePassed => onMinutePassed;
    public UnityEvent OnTimerStart   => onTimerStart;
    public UnityEvent OnTimerEnd     => onTimerEnd;
    public UnityEvent<float> OnTimeUpdate => onTimeUpdate;

    private float currentTime;
    private bool isRunning = false;


    private int lastMinuteMark = -1;


    public static GameTimer Instance { get; private set; }
    

    public float CurrentTime => currentTime;
    public float TotalTime => totalTime;
    public bool IsRunning => isRunning;
    public float TimeLeft => currentTime;
    public float TimeElapsed => totalTime - currentTime;
    public float TimeLeftInMinutes => currentTime / 60f;

    void Awake()
    {

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        currentTime = totalTime;
        lastMinuteMark = Mathf.CeilToInt(currentTime / 60f);
    }

    void Start()
    {

        if (startOnFirstInteraction)
        {
            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnFirstInteraction += OnFirstInteraction;
            }
            else
            {
                Debug.LogError("[GameTimer] FocusLevelManager не найден! Проверьте, что он есть в сцене.");
            }
        }

        else if (startOnFirstFocus)
        {
            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnFirstFocusEver += OnFirstFocus;
            }
            else
            {
                Debug.LogError("[GameTimer] FocusLevelManager не найден! Проверьте, что он есть в сцене.");
            }
        }
        else if (startOnAwake)
        {
            StartTimer();
        }
    }

    void OnDestroy()
    {

        if (FocusLevelManager.Instance != null)
        {
            FocusLevelManager.Instance.OnFirstFocusEver -= OnFirstFocus;
            FocusLevelManager.Instance.OnFirstInteraction -= OnFirstInteraction;
        }
    }


    void OnFirstFocus()
    {
        if (InteractionGate.Consume())
        {
            return;
        }

        StartTimer();
    }


    void OnFirstInteraction()
    {

        if (InteractionGate.Consume())
        {
            return;
        }

        StartTimer();
    }

    void Update()
    {
        if (!isRunning) return;

        currentTime -= Time.deltaTime;


        int minutesLeftNow = Mathf.CeilToInt(Mathf.Max(currentTime, 0f) / 60f);


        if (minutesLeftNow < lastMinuteMark)
        {
            lastMinuteMark = minutesLeftNow;
            OnMinutePassed?.Invoke();
        }
        else if (minutesLeftNow > lastMinuteMark)
        {

            lastMinuteMark = minutesLeftNow;
        }
        

        OnTimeUpdate?.Invoke(currentTime);
        

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            isRunning = false;
            OnTimerEnd?.Invoke();
        }
    }


    public void StartTimer()
    {
        isRunning = true;
        OnTimerStart?.Invoke();
    }


    public void StopTimer()
    {
        isRunning = false;
    }


    public void ResetTimer()
    {
        currentTime = totalTime;
        lastMinuteMark = Mathf.CeilToInt(currentTime / 60f);
    }


    public void AddTime(float seconds)
    {
        currentTime += seconds;


        lastMinuteMark = Mathf.CeilToInt(Mathf.Max(currentTime, 0f) / 60f);


        if (currentTime > totalTime)
        {
            currentTime = totalTime;
            lastMinuteMark = Mathf.CeilToInt(currentTime / 60f);
        }
    }


    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// Get remaining time in seconds (alias for TimeLeft property)
    /// </summary>
    public float GetRemainingTime()
    {
        return TimeLeft;
    }

    /// <summary>
    /// Get elapsed time in seconds (alias for TimeElapsed property)
    /// </summary>
    public float GetElapsedTime()
    {
        return TimeElapsed;
    }
}
