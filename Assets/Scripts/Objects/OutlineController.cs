using UnityEngine;

/// <summary>
/// Управляет компонентом Outline, включая/выключая его при наведении через raycast
/// </summary>
[RequireComponent(typeof(Outline))]
public class OutlineController : MonoBehaviour, IInteractable
{
    [Header("Настройки Outline")]
    [SerializeField] private bool enableOnHover = true;
    [SerializeField] private bool disableOnExit = true;
    
    [Header("Настройки цвета при наведении")]
    [SerializeField] private bool changeColorOnHover = false;
    [SerializeField] private Color hoverColor = Color.red;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private Outline outline;
    private Color originalColor;
    private bool wasEnabledOriginally;

    void Awake()
    {
        outline = GetComponent<Outline>();
        
        if (outline == null)
        {
            Debug.LogError($"[OutlineController] На объекте {gameObject.name} не найден компонент Outline!");
            enabled = false;
            return;
        }

        // Сохраняем исходные настройки
        originalColor = outline.OutlineColor;
        wasEnabledOriginally = outline.enabled;

        // Выключаем Outline в начале, если требуется
        if (enableOnHover && disableOnExit)
        {
            outline.enabled = false;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[OutlineController] Инициализация на {gameObject.name}. " +
                     $"Исходный цвет: {originalColor}, Включен: {wasEnabledOriginally}");
        }
    }

    public void OnHoverEnter()
    {
        if (outline == null) return;

        if (showDebugInfo)
        {
            Debug.Log($"[OutlineController] Наведение на {gameObject.name}");
        }

        // Включаем outline
        if (enableOnHover)
        {
            outline.enabled = true;
        }

        // Меняем цвет, если нужно
        if (changeColorOnHover)
        {
            outline.OutlineColor = hoverColor;
        }
    }

    public void OnHoverExit()
    {
        if (outline == null) return;

        if (showDebugInfo)
        {
            Debug.Log($"[OutlineController] Уход с {gameObject.name}");
        }

        // Выключаем outline
        if (disableOnExit)
        {
            outline.enabled = false;
        }

        // Возвращаем исходный цвет
        if (changeColorOnHover)
        {
            outline.OutlineColor = originalColor;
        }
    }

    public void OnInteract(Camera camera)
    {
        // Этот метод можно оставить пустым или добавить свою логику
        // Например, можно переключать outline при клике
        if (showDebugInfo)
        {
            Debug.Log($"[OutlineController] Взаимодействие с {gameObject.name}");
        }
    }

    /// <summary>
    /// Принудительно включить outline
    /// </summary>
    public void EnableOutline()
    {
        if (outline != null)
        {
            outline.enabled = true;
        }
    }

    /// <summary>
    /// Принудительно выключить outline
    /// </summary>
    public void DisableOutline()
    {
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    /// <summary>
    /// Установить цвет outline
    /// </summary>
    public void SetOutlineColor(Color color)
    {
        if (outline != null)
        {
            outline.OutlineColor = color;
        }
    }

    /// <summary>
    /// Восстановить исходные настройки
    /// </summary>
    public void ResetToOriginal()
    {
        if (outline != null)
        {
            outline.OutlineColor = originalColor;
            outline.enabled = wasEnabledOriginally;
        }
    }
}