using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Общеигровой таймер обратного отсчета
/// </summary>
public class GameTimer : MonoBehaviour
{
    [Header("Настройки таймера")]
    [SerializeField] private float totalTime = 300f; // 5 минут в секундах
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private bool showDebugInfo = true;
    
    [Header("=== ЗАПУСК ПО ПЕРВОМУ ФОКУСУ ===")]
    [Tooltip("Запускать таймер при первом фокусе вместо startOnAwake")]
    [SerializeField] private bool startOnFirstFocus = false;
    
    [Tooltip("Запускать таймер при первом взаимодействии (фокус ИЛИ инспекция)")]
    [SerializeField] private bool startOnFirstInteraction = false;
    
    [Header("События - ПУБЛИЧНЫЕ для подписки из других скриптов")]
    [Tooltip("Событие когда остается 2 минуты")]
    public UnityEvent onTwoMinutesLeft = new UnityEvent();
    
    [Tooltip("Событие когда таймер запустился")]
    public UnityEvent onTimerStart = new UnityEvent();
    
    [Tooltip("Событие когда таймер закончился")]
    public UnityEvent onTimerEnd = new UnityEvent();
    
    [Tooltip("Событие каждую секунду (передает оставшееся время)")]
    public UnityEvent<float> onTimeUpdate = new UnityEvent<float>();

    private float currentTime;
    private bool isRunning = false;
    private bool twoMinuteEventTriggered = false;
    
    // Singleton для удобного доступа
    public static GameTimer Instance { get; private set; }
    
    // Публичные свойства
    public float CurrentTime => currentTime;
    public float TotalTime => totalTime;
    public bool IsRunning => isRunning;
    public float TimeLeft => currentTime;
    public float TimeElapsed => totalTime - currentTime;
    public float TimeLeftInMinutes => currentTime / 60f;
    public bool IsLessThanTwoMinutesLeft => currentTime <= 120f;

    void Awake()
    {
        // Singleton pattern
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
    }

    void Start()
    {
        // === ПОДПИСКА НА ПЕРВОЕ ВЗАИМОДЕЙСТВИЕ (фокус или инспекция) ===
        if (startOnFirstInteraction)
        {
            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnFirstInteraction += OnFirstInteraction;
                
                if (showDebugInfo)
                {
                    Debug.Log("[GameTimer] Ожидание первого взаимодействия (фокус или инспекция) для запуска таймера...");
                }
            }
            else
            {
                Debug.LogError("[GameTimer] FocusLevelManager не найден! Проверьте, что он есть в сцене.");
            }
        }
        // === ПОДПИСКА НА ПЕРВЫЙ ФОКУС (только фокус) ===
        else if (startOnFirstFocus)
        {
            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnFirstFocusEver += OnFirstFocus;
                
                if (showDebugInfo)
                {
                    Debug.Log("[GameTimer] Ожидание первого фокуса для запуска таймера...");
                }
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
        // Отписываемся от событий
        if (FocusLevelManager.Instance != null)
        {
            FocusLevelManager.Instance.OnFirstFocusEver -= OnFirstFocus;
            FocusLevelManager.Instance.OnFirstInteraction -= OnFirstInteraction;
        }
    }

    /// <summary>
    /// Вызывается при первом фокусе в игре
    /// </summary>
    void OnFirstFocus()
    {
        if (showDebugInfo)
        {
            Debug.Log("[GameTimer] 🎯 Первый фокус обнаружен! Запуск таймера...");
        }
        
        StartTimer();
    }

    /// <summary>
    /// Вызывается при первом взаимодействии (фокус или инспекция) в игре
    /// </summary>
    void OnFirstInteraction()
    {
        if (showDebugInfo)
        {
            Debug.Log("[GameTimer] ⭐ Первое взаимодействие обнаружено! Запуск таймера...");
        }
        
        StartTimer();
    }

    void Update()
    {
        if (!isRunning) return;

        currentTime -= Time.deltaTime;
        
        // Проверяем события
        if (!twoMinuteEventTriggered && currentTime <= 120f)
        {
            twoMinuteEventTriggered = true;
            onTwoMinutesLeft?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameTimer] Осталось 2 минуты!");
            }
        }
        
        // Обновление каждую секунду
        onTimeUpdate?.Invoke(currentTime);
        
        // Проверяем завершение
        if (currentTime <= 0f)
        {
            currentTime = 0f;
            isRunning = false;
            onTimerEnd?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log("[GameTimer] Время вышло!");
            }
        }
    }

    /// <summary>
    /// Запустить таймер
    /// </summary>
    public void StartTimer()
    {
        isRunning = true;
        onTimerStart?.Invoke(); // ✅ Добавлен вызов события
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameTimer] Таймер запущен на {totalTime / 60f:F1} минут");
        }
    }

    /// <summary>
    /// Остановить таймер
    /// </summary>
    public void StopTimer()
    {
        isRunning = false;
        
        if (showDebugInfo)
        {
            Debug.Log("[GameTimer] Таймер остановлен");
        }
    }

    /// <summary>
    /// Сбросить таймер
    /// </summary>
    public void ResetTimer()
    {
        currentTime = totalTime;
        twoMinuteEventTriggered = false;
        
        if (showDebugInfo)
        {
            Debug.Log("[GameTimer] Таймер сброшен");
        }
    }

    /// <summary>
    /// Добавить время к таймеру
    /// </summary>
    public void AddTime(float seconds)
    {
        currentTime += seconds;
        if (currentTime > totalTime)
        {
            currentTime = totalTime;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[GameTimer] Добавлено {seconds} секунд");
        }
    }

    /// <summary>
    /// Получить время в формате MM:SS
    /// </summary>
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
}