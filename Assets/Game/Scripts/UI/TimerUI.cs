
using UnityEngine;
using TMPro;

public class SimpleTimerDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    
    [Header("Настройки отображения")]
    [SerializeField] private bool showMinutesSeconds = true;
    [SerializeField] private bool showHours = false;
    [SerializeField] private string customFormat = "{0:00}:{1:00}";
    
    [Header("Статичный режим")]
    [SerializeField] private bool staticRedNoAnimation = true;
    
    [Header("Цвета")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private Color finalColor = Color.red;
    
    [Header("Анимация")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.2f;
    
    [Header("Пороги предупреждений (секунды)")]
    [SerializeField] private float warningThreshold = 180f;
    [SerializeField] private float criticalThreshold = 120f;
    [SerializeField] private float finalThreshold = 30f;

    private Vector3 originalScale;
    private bool isInitialized = false;

    void Awake()
    {
        if (timerText == null)
            timerText = GetComponent<TextMeshProUGUI>();
        
        if (timerText == null)
        {
            Debug.LogError("[SimpleTimerDisplay] TextMeshProUGUI не назначен!");
            return;
        }
        
        originalScale = timerText.transform.localScale;
        isInitialized = true;
    }

    void Start()
    {
        if (!isInitialized) return;
        
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
        
        UpdateText(timeLeft);
        
        if (staticRedNoAnimation)
        {
            timerText.color = Color.red;
            timerText.transform.localScale = originalScale;
            return;
        }
        
        UpdateColor(timeLeft);
        
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
            int hours = Mathf.FloorToInt(timeLeft / 3600f);
            int minutes = Mathf.FloorToInt((timeLeft % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            displayText = $"{hours:00}:{minutes:00}:{seconds:00}";
        }
        else if (showMinutesSeconds)
        {
            int minutes = Mathf.FloorToInt(timeLeft / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            displayText = string.Format(customFormat, minutes, seconds);
        }
        else
        {
            displayText = Mathf.FloorToInt(timeLeft).ToString();
        }
        
        timerText.text = displayText;
    }

    void UpdateColor(float timeLeft)
    {
        Color targetColor = normalColor;
        
        if (timeLeft <= finalThreshold)      targetColor = finalColor;
        else if (timeLeft <= criticalThreshold) targetColor = criticalColor;
        else if (timeLeft <= warningThreshold)  targetColor = warningColor;

        timerText.color = targetColor;
    }

    void UpdatePulseAnimation(float timeLeft)
    {
        float urgency = Mathf.InverseLerp(warningThreshold, 0f, timeLeft);
        float scaleFactor = 1f + Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed)) * pulseAmount * urgency;
        timerText.transform.localScale = originalScale * scaleFactor;
    }

    void OnTimerFinished()
    {
        if (!isInitialized) return;
        
        timerText.text = showMinutesSeconds ? "00:00" : "0";
        timerText.color = finalColor;
        timerText.transform.localScale = originalScale;
        
        Debug.Log("[SimpleTimerDisplay] Время вышло!");
    }

    public void SetVisible(bool visible)
    {
        if (timerText != null)
            timerText.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        if (GameTimer.Instance != null)
        {
            GameTimer.Instance.onTimeUpdate.RemoveListener(UpdateTimerDisplay);
            GameTimer.Instance.onTimerEnd.RemoveListener(OnTimerFinished);
        }
    }
}
