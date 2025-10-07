using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Взаимодействие с объектом (винтом) доступно ТОЛЬКО при уровне фокуса == 1.
/// Совместим с CameraController (ищет IInteractable).
/// </summary>
public class ScrewInteractableAtFocus1 : MonoBehaviour, IInteractable
{
    [Header("События")]
    public UnityEvent onInteract;

    [Header("Настройки")]
    [Tooltip("Требуемый уровень фокуса (ровно)")]
    [SerializeField] private int requiredFocusLevel = 1;

    [Tooltip("Доп. флаг для общего отключения взаимодействия")]
    [SerializeField] private bool canInteract = true;

    [Header("Опциональный Outline")]
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Color hoverOutlineColor = Color.white;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private Outline outline;

    void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    // ========= IInteractable =========

    public void OnHoverEnter()
    {
        if (!IsAllowedNow()) return;

        if (showDebugInfo)
            Debug.Log($"[ScrewInteractableAtFocus1] HoverEnter: {name} (focus OK)");

        if (useOutline && outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = hoverOutlineColor;
        }
    }

    public void OnHoverExit()
    {
        if (showDebugInfo)
            Debug.Log($"[ScrewInteractableAtFocus1] HoverExit: {name}");

        if (useOutline && outline != null)
            outline.enabled = false;
    }

    public void OnInteract(Camera camera)
    {
        if (!IsAllowedNow())
        {
            if (showDebugInfo)
            {
                var lvl = GetCurrentFocusLevel();
                Debug.LogWarning($"[ScrewInteractableAtFocus1] Блок: уровень фокуса {lvl}, нужен {requiredFocusLevel} (объект: {name})");
            }
            return;
        }

        if (!canInteract)
        {
            if (showDebugInfo)
                Debug.LogWarning($"[ScrewInteractableAtFocus1] canInteract=false для {name}");
            return;
        }

        if (showDebugInfo)
            Debug.Log($"[ScrewInteractableAtFocus1] ✓ Взаимодействие с {name}");

        onInteract?.Invoke();
    }

    // ========== Helpers ==========

    private bool IsAllowedNow()
    {
        if (!canInteract) return false;
        return GetCurrentFocusLevel() == requiredFocusLevel;
    }

    private int GetCurrentFocusLevel()
    {
        return FocusLevelManager.Instance != null
            ? FocusLevelManager.Instance.CurrentNestLevel
            : 0; // если менеджера нет, считаем 0
    }

    // Публичные вспомогательные методы (по желанию)
    public void SetInteractable(bool value)
    {
        canInteract = value;
        if (!value && outline != null) outline.enabled = false;
    }

    public void SetHoverColor(Color color)
    {
        hoverOutlineColor = color;
    }
}
