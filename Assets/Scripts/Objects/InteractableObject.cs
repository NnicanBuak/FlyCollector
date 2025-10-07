using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Компонент для объектов, которые можно инспектировать при клике ЛКМ
/// Работает совместно с CameraController
/// Автоматически отключает AI, Animator и коллайдеры детей при инспекции
/// </summary>
public class InteractableObject : MonoBehaviour, IInteractable
{
    [Header("События")]
    public UnityEvent onInteract;
    
    [Header("Настройки взаимодействия")]
    [Tooltip("Можно ли взаимодействовать с этим объектом")]
    [SerializeField] private bool canInteract = true;
    
    [Header("Опциональный Outline")]
    [Tooltip("Включать ли outline при наведении")]
    [SerializeField] private bool useOutline = true;
    
    [SerializeField] private Color hoverOutlineColor = Color.white;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private Outline outline;

    void Awake()
    {
        outline = GetComponent<Outline>();
        
        if (outline != null && useOutline)
        {
            outline.enabled = false;
        }
    }

    // ============= МЕТОДЫ ИНТЕРФЕЙСА IInteractable =============
    
    public void OnHoverEnter()
    {
        if (!canInteract) return;

        if (showDebugInfo)
        {
            Debug.Log($"[InteractableObject] Наведение на {gameObject.name}");
        }

        if (useOutline && outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = hoverOutlineColor;
        }
    }

    public void OnHoverExit()
    {
        if (!canInteract) return;

        if (showDebugInfo)
        {
            Debug.Log($"[InteractableObject] Уход с {gameObject.name}");
        }

        if (useOutline && outline != null)
        {
            outline.enabled = false;
        }
    }

    public void OnInteract(Camera camera)
    {
        if (!canInteract)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[InteractableObject] Взаимодействие отключено для {gameObject.name}");
            }
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[InteractableObject] ✓ Взаимодействие с {gameObject.name}");
        }
        
        // Вызываем событие UnityEvent
        onInteract?.Invoke();
    }
    
    // ============= ПУБЛИЧНЫЕ МЕТОДЫ =============
    
    public void SetInteractable(bool value)
    {
        canInteract = value;
        
        if (!value && outline != null)
        {
            outline.enabled = false;
        }
    }

    public bool CanInteract()
    {
        return canInteract;
    }

    public void SetHoverColor(Color color)
    {
        hoverOutlineColor = color;
    }
}