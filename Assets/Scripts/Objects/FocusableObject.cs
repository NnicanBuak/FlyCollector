using UnityEngine;

/// <summary>
/// Компонент для объектов, на которые можно сфокусировать камеру с поддержкой вложенности
/// </summary>
public class FocusableObject : MonoBehaviour, IFocusable
{
    [Header("=== Система вложенности ===")]
    [Tooltip("Уровень вложенности, на котором доступен этот объект\n0 = базовый уровень (всегда доступен)")]
    [SerializeField] private int requiredNestLevel = 0;
    
    [Tooltip("На какой уровень вложенности переходит камера при фокусе\n0 = возврат к базовому уровню")]
    [SerializeField] private int targetNestLevel = 1;

    [Header("Camera Position")]
    [Tooltip("Transform, определяющий позицию и поворот камеры при фокусе")]
    [SerializeField] private Transform cameraPosition;
    
    [Tooltip("Если не задан cameraPosition, использовать автоматическое позиционирование")]
    [SerializeField] private bool useAutomaticPositioning = true;
    
    [Header("Automatic Positioning (если cameraPosition не задан)")]
    [SerializeField] private float distance = 3f;
    [SerializeField] private Vector3 offset = Vector3.zero;
    
    [Header("Camera Behavior")]
    [Tooltip("Фиксировать камеру в заданной позиции (отключает вращение мышью)")]
    [SerializeField] private bool lockCameraPosition = false;

    [Header("Outline")]
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Color focusHoverColor = new Color(0, 212, 103);
    [SerializeField] private Color focusActiveColor = Color.yellow;

    [Header("Animation")]
    [Tooltip("Animator для воспроизведения анимаций при фокусе")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Использовать анимацию")]
    [SerializeField] private bool useAnimation = true;
    
    [Tooltip("Имя триггера для анимации при фокусе")]
    [SerializeField] private string focusStartTrigger = "FocusStart";
    
    [Tooltip("Имя триггера для анимации при расфокусе")]
    [SerializeField] private string focusEndTrigger = "FocusEnd";
    
    [Tooltip("Имя триггера для анимации при наведении")]
    [SerializeField] private string hoverEnterTrigger = "HoverEnter";
    
    [Tooltip("Имя триггера для анимации при уходе курсора")]
    [SerializeField] private string hoverExitTrigger = "HoverExit";

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private Outline outline;
    private bool isHovered = false;

    void Awake()
    {
        outline = GetComponent<Outline>();
        
        if (outline != null && useOutline)
        {
            outline.enabled = false;
        }
    }

    // === МЕТОДЫ ВЛОЖЕННОСТИ ===
    
    public int GetRequiredNestLevel()
    {
        return requiredNestLevel;
    }
    
    public int GetTargetNestLevel()
    {
        return targetNestLevel;
    }
    
    public bool IsAvailableAtNestLevel(int currentLevel)
    {
        return currentLevel == requiredNestLevel;
    }

    // === СОБЫТИЯ HOVER ===

    public void OnFocusHoverEnter()
    {
        isHovered = true;
        
        // Проверяем доступность на текущем уровне
        int currentLevel = FocusLevelManager.Instance != null ? 
            FocusLevelManager.Instance.CurrentNestLevel : 0;
        
        bool isAvailable = IsAvailableAtNestLevel(currentLevel);
        
        // ИСПРАВЛЕНИЕ: Показываем обводку ТОЛЬКО если объект доступен
        if (isAvailable)
        {
            if (useOutline && outline != null)
            {
                outline.enabled = true;
                outline.OutlineColor = focusHoverColor;
            }

            if (useAnimation && animator != null && !string.IsNullOrEmpty(hoverEnterTrigger))
            {
                animator.SetTrigger(hoverEnterTrigger);
            }
        }

        if (showDebugInfo)
        {
            string status = isAvailable ? "доступен" : $"недоступен (нужен уровень {requiredNestLevel})";
            Debug.Log($"[FocusableObject] Hover enter: {gameObject.name} - {status}");
        }
    }

    public void OnFocusHoverExit()
    {
        isHovered = false;
        
        if (useOutline && outline != null)
        {
            outline.enabled = false;
        }

        if (useAnimation && animator != null && !string.IsNullOrEmpty(hoverExitTrigger))
        {
            animator.SetTrigger(hoverExitTrigger);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[FocusableObject] Hover exit: {gameObject.name}");
        }
    }

    // Замените метод OnFocusStart в FocusableObject.cs

    public void OnFocusStart()
    {
        if (useOutline && outline != null)
        {
            outline.enabled = true;  // ✅ ИСПРАВЛЕНИЕ: включаем outline
            outline.OutlineColor = focusActiveColor;
        }

        if (useAnimation && animator != null && !string.IsNullOrEmpty(focusStartTrigger))
        {
            animator.SetTrigger(focusStartTrigger);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[FocusableObject] Focus start: {gameObject.name}, переход на уровень {targetNestLevel}");
        }
    }

    public void OnFocusEnd()
    {
        if (useOutline && outline != null)
        {
            outline.enabled = false;
        }

        if (useAnimation && animator != null && !string.IsNullOrEmpty(focusEndTrigger))
        {
            animator.SetTrigger(focusEndTrigger);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[FocusableObject] Focus end: {gameObject.name}");
        }
    }

    /// <summary>
    /// Получить позицию для камеры при фокусе
    /// </summary>
    public Vector3 GetCameraPosition()
    {
        if (cameraPosition != null)
        {
            return cameraPosition.position;
        }

        if (useAutomaticPositioning)
        {
            Bounds bounds = GetObjectBounds();
            Vector3 center = bounds.center + offset;
            
            Vector3 direction = Camera.main != null ? 
                (Camera.main.transform.position - center).normalized : 
                -transform.forward;
                
            return center + direction * distance;
        }

        return transform.position;
    }

    /// <summary>
    /// Получить поворот для камеры при фокусе
    /// </summary>
    public Quaternion GetCameraRotation()
    {
        if (cameraPosition != null)
        {
            return cameraPosition.rotation;
        }

        if (useAutomaticPositioning)
        {
            Bounds bounds = GetObjectBounds();
            Vector3 center = bounds.center + offset;
            Vector3 direction = (center - GetCameraPosition()).normalized;
            return Quaternion.LookRotation(direction);
        }

        return Quaternion.LookRotation(transform.position - GetCameraPosition());
    }

    /// <summary>
    /// Проверить, заблокирована ли позиция камеры
    /// </summary>
    public bool IsCameraPositionLocked()
    {
        return lockCameraPosition;
    }

    /// <summary>
    /// Получить центр объекта для фокуса
    /// </summary>
    public Vector3 GetFocusCenter()
    {
        return GetObjectBounds().center + offset;
    }

    /// <summary>
    /// Установить Transform для позиции камеры
    /// </summary>
    public void SetCameraPosition(Transform cameraTransform)
    {
        cameraPosition = cameraTransform;
    }

    /// <summary>
    /// Установить блокировку позиции камеры
    /// </summary>
    public void SetCameraLock(bool locked)
    {
        lockCameraPosition = locked;
    }

    private Bounds GetObjectBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    // Визуализация в редакторе
    void OnDrawGizmosSelected()
    {
        if (cameraPosition != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cameraPosition.position, 0.1f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawLine(cameraPosition.position, 
                cameraPosition.position + cameraPosition.forward * 0.5f);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(cameraPosition.position, GetFocusCenter());
        }
        else if (useAutomaticPositioning)
        {
            Vector3 camPos = GetCameraPosition();
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(camPos, 0.1f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(camPos, GetFocusCenter());
        }
    }
}