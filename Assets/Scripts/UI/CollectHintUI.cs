using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI подсказка для сбора жуков (ПКМ -> Collect)
/// Показывается когда жук в инспекции
/// </summary>
public class CollectHintUI : MonoBehaviour
{
    public static CollectHintUI Instance { get; private set; }

    [Header("UI элементы")]
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private Image buttonIcon;
    [SerializeField] private TextMeshProUGUI hintText;
    
    [Header("Иконки")]
    [SerializeField] private Sprite rightMouseButtonIcon;
    
    [Header("Анимация")]
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float scaleSpeed = 8f;
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.8f, 0.8f, 1f);
    
    [Header("Настройки")]
    [SerializeField] private string collectText = "Collect";
    
    private CanvasGroup canvasGroup;
    private RectTransform panelTransform;
    private bool shouldShow = false;
    private Vector3 normalScale = Vector3.one;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Получаем компоненты
        if (hintPanel != null)
        {
            canvasGroup = hintPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = hintPanel.AddComponent<CanvasGroup>();
            }
            
            panelTransform = hintPanel.GetComponent<RectTransform>();
            normalScale = panelTransform.localScale;
        }
        
        // Начинаем скрытыми
        Hide();
    }

    void Update()
    {
        if (hintPanel == null || canvasGroup == null) return;
        
        // Плавная анимация появления/скрытия
        float targetAlpha = shouldShow ? 1f : 0f;
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        
        Vector3 targetScale = shouldShow ? normalScale : hiddenScale;
        panelTransform.localScale = Vector3.Lerp(panelTransform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        
        // Отключаем интерактивность когда скрыто
        canvasGroup.interactable = shouldShow;
        canvasGroup.blocksRaycasts = shouldShow;
    }

    /// <summary>
    /// Показать подсказку сбора
    /// </summary>
    public void Show()
    {
        shouldShow = true;
        
        if (hintPanel != null)
        {
            hintPanel.SetActive(true);
        }
        
        // Устанавливаем иконку и текст
        if (buttonIcon != null && rightMouseButtonIcon != null)
        {
            buttonIcon.sprite = rightMouseButtonIcon;
        }
        
        if (hintText != null)
        {
            hintText.text = collectText;
        }
    }

    /// <summary>
    /// Скрыть подсказку
    /// </summary>
    public void Hide()
    {
        shouldShow = false;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        if (panelTransform != null)
        {
            panelTransform.localScale = hiddenScale;
        }
    }

    /// <summary>
    /// Установить текст подсказки
    /// </summary>
    public void SetText(string text)
    {
        collectText = text;
        if (hintText != null)
        {
            hintText.text = text;
        }
    }
}