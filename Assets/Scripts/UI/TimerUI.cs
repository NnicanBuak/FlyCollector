using UnityEngine;
using TMPro;

/// <summary>
/// Простое отображение таймера в TextMeshPro
/// </summary>
public class SimpleTimerDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    
    [Header("Настройки отображения")]
    [SerializeField] private bool showMinutesSeconds = true; // MM:SS формат
    [SerializeField] private bool showHours = false; // HH:MM:SS формат
    [SerializeField] private string customFormat = "{0:00}:{1:00}"; // Кастомный формат
    
    [Header("Цвета")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow; // < 3 минут
    [SerializeField] private Color criticalColor = Color.red;   // < 2 минут
    [SerializeField] private Color finalColor = Color.red;      // < 30 секунд
    
    [Header("Анимация")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.2f;
    
    [Header("Пороги предупреждений (секунды)")]
    [SerializeField] private float warningThreshold = 180f;  // 3 минуты
    [SerializeField] private float criticalThreshold = 120f; // 2 минуты  
    [SerializeField] private float finalThreshold = 30f;     // 30 секунд

    private Vector3 originalScale;
    private bool isInitialized = false;

    void Awake()
    {
        // Если timerText не задан, попробуем найти на этом объекте
        if (timerText == null)
        {
            timerText = GetComponent<TextMeshProUGUI>();
        }
        
        if (timerText == null)
        {
            Debug.LogError("[SimpleTimerDisplay] TextMeshProUGUI не найден! Добавьте ссылку в Inspector.");
            return;
        }
        
        originalScale = timerText.transform.localScale;
        isInitialized = true;
    }

    void Start()
    {
        if (!isInitialized) return;
        
        // Подписываемся на обновления таймера
        if (GameTimer.Instance != null)
        {
            GameTimer.Instance.onTimeUpdate.AddListener(UpdateTimerDisplay);
            GameTimer.Instance.onTimerEnd.AddListener(OnTimerFinished);
        }
        else
        {
            Debug.LogWarning("[SimpleTimerDisplay] GameTimer не найден! Убедитесь что GameTimer присутствует в сцене.");
        }
    }

    void UpdateTimerDisplay(float timeLeft)
    {
        if (!isInitialized) return;
        
        // Обновляем текст
        UpdateText(timeLeft);
        
        // Обновляем цвет
        UpdateColor(timeLeft);
        
        // Обновляем анимацию
        if (enablePulse)
        {
            UpdatePulseAnimation(timeLeft);
        }
    }

    void UpdateText(float timeLeft)
    {
        string displayText;
        
        if (showHours)
        {
            // Формат HH:MM:SS
            int hours = Mathf.FloorToInt(timeLeft / 3600f);
            int minutes = Mathf.FloorToInt((timeLeft % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            displayText = $"{hours:00}:{minutes:00}:{seconds:00}";
        }
        else if (showMinutesSeconds)
        {
            // Формат MM:SS
            int minutes = Mathf.FloorToInt(timeLeft / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            displayText = string.Format(customFormat, minutes, seconds);
        }
        else
        {
            // Только секунды
            displayText = Mathf.FloorToInt(timeLeft).ToString();
        }
        
        timerText.text = displayText;
    }

    void UpdateColor(float timeLeft)
    {
        Color targetColor = normalColor;
        
        if (timeLeft <= finalThreshold)
        {
            targetColor = finalColor;
        }
        else if (timeLeft <= criticalThreshold)
        {
            targetColor = criticalColor;
        }
        else if (timeLeft <= warningThreshold)
        {
            targetColor = warningColor;
        }
        
        timerText.color = targetColor;
    }

    void UpdatePulseAnimation(float timeLeft)
    {
        float pulseMultiplier = 0f;
        
        // Определяем интенсивность пульсации в зависимости от времени
        if (timeLeft <= finalThreshold)
        {
            pulseMultiplier = 1f; // Максимальная пульсация
        }
        else if (timeLeft <= criticalThreshold)
        {
            pulseMultiplier = 0.6f; // Средняя пульсация
        }
        else if (timeLeft <= warningThreshold)
        {
            pulseMultiplier = 0.3f; // Слабая пульсация
        }
        
        if (pulseMultiplier > 0f)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount * pulseMultiplier;
            timerText.transform.localScale = originalScale + Vector3.one * pulse;
        }
        else
        {
            timerText.transform.localScale = originalScale;
        }
    }

    void OnTimerFinished()
    {
        if (!isInitialized) return;
        
        timerText.text = showMinutesSeconds ? "00:00" : "0";
        timerText.color = finalColor;
        timerText.transform.localScale = originalScale;
        
        Debug.Log("[SimpleTimerDisplay] Время истекло!");
    }

    /// <summary>
    /// Установить кастомный текст (например, для паузы)
    /// </summary>
    public void SetCustomText(string text)
    {
        if (isInitialized)
        {
            timerText.text = text;
        }
    }

    /// <summary>
    /// Сбросить к обычному отображению таймера
    /// </summary>
    public void ResetDisplay()
    {
        if (GameTimer.Instance != null && isInitialized)
        {
            UpdateTimerDisplay(GameTimer.Instance.CurrentTime);
        }
    }

    /// <summary>
    /// Включить/выключить отображение таймера
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (timerText != null)
        {
            timerText.gameObject.SetActive(visible);
        }
    }

    void OnDestroy()
    {
        // Отписываемся от событий
        if (GameTimer.Instance != null)
        {
            GameTimer.Instance.onTimeUpdate.RemoveListener(UpdateTimerDisplay);
            GameTimer.Instance.onTimerEnd.RemoveListener(OnTimerFinished);
        }
    }

    void OnValidate()
    {
        // Проверяем настройки в Editor
        if (warningThreshold <= criticalThreshold)
        {
            warningThreshold = criticalThreshold + 1f;
        }
        
        if (criticalThreshold <= finalThreshold)
        {
            criticalThreshold = finalThreshold + 1f;
        }
    }
}