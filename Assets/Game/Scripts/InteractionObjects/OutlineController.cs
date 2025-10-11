using UnityEngine;

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


        originalColor = outline.OutlineColor;
        wasEnabledOriginally = outline.enabled;


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


        if (enableOnHover)
        {
            outline.enabled = true;
        }


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


        if (disableOnExit)
        {
            outline.enabled = false;
        }


        if (changeColorOnHover)
        {
            outline.OutlineColor = originalColor;
        }
    }

    public void OnInteract(Camera camera)
    {


        if (showDebugInfo)
        {
            Debug.Log($"[OutlineController] Взаимодействие с {gameObject.name}");
        }
    }


    public void EnableOutline()
    {
        if (outline != null)
        {
            outline.enabled = true;
        }
    }


    public void DisableOutline()
    {
        if (outline != null)
        {
            outline.enabled = false;
        }
    }


    public void SetOutlineColor(Color color)
    {
        if (outline != null)
        {
            outline.OutlineColor = color;
        }
    }


    public void ResetToOriginal()
    {
        if (outline != null)
        {
            outline.OutlineColor = originalColor;
            outline.enabled = wasEnabledOriginally;
        }
    }
}