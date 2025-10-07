using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class ItemRequirement
{
    [Tooltip("Требуемый предмет")]
    public Item requiredItem;
    
    [Tooltip("Требуемое количество")]
    public int requiredQuantity = 1;
    
    [Tooltip("Потратить предмет при взаимодействии")]
    public bool consumeOnUse = false;
    
    [Tooltip("Сообщение если предмета нет")]
    public string missingItemMessage = "Нужен предмет: {0}";
}

/// <summary>
/// Компонент для взаимодействий с условиями (проверка наличия предметов в инвентаре)
/// Работает совместно с InteractableObject
/// </summary>
[RequireComponent(typeof(InteractableObject))]
public class ConditionalInteraction : MonoBehaviour
{
    [Header("Условия взаимодействия")]
    [Tooltip("Требуемые предметы для взаимодействия")]
    [SerializeField] private List<ItemRequirement> requiredItems = new List<ItemRequirement>();
    
    [Tooltip("Нужны ли все предметы или достаточно одного")]
    [SerializeField] private bool requireAllItems = true;
    
    [Header("Сообщения")]
    [Tooltip("Сообщение при недостатке предметов")]
    [SerializeField] private string insufficientItemsMessage = "У вас нет необходимых предметов";
    
    [Tooltip("Показывать сообщения в консоли")]
    [SerializeField] private bool showMessages = true;
    
    [Header("События")]
    [Tooltip("Событие при успешном взаимодействии")]
    [SerializeField] private UnityEvent onSuccessfulInteraction;
    
    [Tooltip("Событие при неудачном взаимодействии")]
    [SerializeField] private UnityEvent onFailedInteraction;
    
    [Tooltip("Событие при недостатке предметов")]
    [SerializeField] private UnityEvent<string> onInsufficientItems;
    
    [Header("Настройки")]
    [Tooltip("Можно ли взаимодействовать без предметов")]
    [SerializeField] private bool allowWithoutItems = false;
    
    [Tooltip("Одноразовое взаимодействие")]
    [SerializeField] private bool oneTimeUse = false;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private InteractableObject interactable;
    private bool hasBeenUsed = false;

    void Awake()
    {
        interactable = GetComponent<InteractableObject>();
    }

    void Start()
    {
        // Подписываемся на событие взаимодействия
        if (interactable != null)
        {
            interactable.onInteract.AddListener(OnInteract);
        }
    }

    void OnDestroy()
    {
        // Отписываемся от событий
        if (interactable != null)
        {
            interactable.onInteract.RemoveListener(OnInteract);
        }
    }

    /// <summary>
    /// Обработчик взаимодействия
    /// </summary>
    private void OnInteract()
    {
        if (oneTimeUse && hasBeenUsed)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[ConditionalInteraction] {gameObject.name} уже был использован");
            }
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[ConditionalInteraction] InventoryManager не найден!");
            return;
        }

        // Проверяем условия
        bool conditionsMet = CheckConditions();
        
        if (conditionsMet)
        {
            // Потребляем предметы если нужно
            ConsumeRequiredItems();
            
            // Отмечаем как использованный
            hasBeenUsed = true;
            
            // Вызываем событие успешного взаимодействия
            onSuccessfulInteraction?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log($"[ConditionalInteraction] Условия выполнены для {gameObject.name}");
            }
        }
        else
        {
            // Вызываем событие неудачного взаимодействия
            onFailedInteraction?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log($"[ConditionalInteraction] Условия не выполнены для {gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Проверить все условия
    /// </summary>
    private bool CheckConditions()
    {
        if (requiredItems.Count == 0)
        {
            return allowWithoutItems;
        }

        if (requireAllItems)
        {
            // Нужны все предметы
            foreach (var requirement in requiredItems)
            {
                if (!CheckSingleRequirement(requirement))
                {
                    return false;
                }
            }
            return true;
        }
        else
        {
            // Достаточно одного предмета
            foreach (var requirement in requiredItems)
            {
                if (CheckSingleRequirement(requirement))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Проверить одно условие
    /// </summary>
    private bool CheckSingleRequirement(ItemRequirement requirement)
    {
        if (requirement.requiredItem == null)
        {
            return true;
        }

        bool hasItem = InventoryManager.Instance.HasItem(requirement.requiredItem, requirement.requiredQuantity);
        
        if (!hasItem && showMessages)
        {
            string message;
            if (!string.IsNullOrEmpty(requirement.missingItemMessage))
            {
                message = string.Format(requirement.missingItemMessage, requirement.requiredItem.itemName);
            }
            else
            {
                message = insufficientItemsMessage;
            }
            
            onInsufficientItems?.Invoke(message);
            
            if (showDebugInfo)
            {
                Debug.Log($"[ConditionalInteraction] {message}");
            }
        }

        return hasItem;
    }

    /// <summary>
    /// Потребить требуемые предметы
    /// </summary>
    private void ConsumeRequiredItems()
    {
        foreach (var requirement in requiredItems)
        {
            if (requirement.consumeOnUse && requirement.requiredItem != null)
            {
                bool removed = InventoryManager.Instance.RemoveItem(requirement.requiredItem, requirement.requiredQuantity);
                
                if (showDebugInfo)
                {
                    if (removed)
                    {
                        Debug.Log($"[ConditionalInteraction] Потрачено: {requirement.requiredItem.itemName} x{requirement.requiredQuantity}");
                    }
                    else
                    {
                        Debug.LogWarning($"[ConditionalInteraction] Не удалось потратить: {requirement.requiredItem.itemName}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Добавить новое условие
    /// </summary>
    public void AddRequirement(Item item, int quantity = 1, bool consume = false)
    {
        var requirement = new ItemRequirement
        {
            requiredItem = item,
            requiredQuantity = quantity,
            consumeOnUse = consume
        };
        requiredItems.Add(requirement);
    }

    /// <summary>
    /// Удалить условие
    /// </summary>
    public void RemoveRequirement(Item item)
    {
        requiredItems.RemoveAll(req => req.requiredItem == item);
    }

    /// <summary>
    /// Проверить можно ли взаимодействовать сейчас
    /// </summary>
    public bool CanInteract()
    {
        if (oneTimeUse && hasBeenUsed)
            return false;

        return CheckConditions();
    }

    /// <summary>
    /// Сбросить состояние использования
    /// </summary>
    public void ResetUsage()
    {
        hasBeenUsed = false;
    }

    /// <summary>
    /// Получить список недостающих предметов
    /// </summary>
    public List<ItemRequirement> GetMissingRequirements()
    {
        var missing = new List<ItemRequirement>();
        
        foreach (var requirement in requiredItems)
        {
            if (!CheckSingleRequirement(requirement))
            {
                missing.Add(requirement);
            }
        }
        
        return missing;
    }

    /// <summary>
    /// Получить информацию о требованиях
    /// </summary>
    public string GetRequirementsInfo()
    {
        if (requiredItems.Count == 0)
        {
            return "Нет требований";
        }

        var info = new System.Text.StringBuilder();
        info.AppendLine("Требуется:");
        
        foreach (var requirement in requiredItems)
        {
            if (requirement.requiredItem != null)
            {
                bool hasItem = InventoryManager.Instance?.HasItem(requirement.requiredItem, requirement.requiredQuantity) ?? false;
                string status = hasItem ? "✓" : "✗";
                string consume = requirement.consumeOnUse ? " (потратится)" : "";
                
                info.AppendLine($"{status} {requirement.requiredItem.itemName} x{requirement.requiredQuantity}{consume}");
            }
        }
        
        return info.ToString().TrimEnd();
    }

    // Пример методов для UnityEvents
    public void LogMessage(string message)
    {
        Debug.Log($"[ConditionalInteraction] {gameObject.name}: {message}");
    }

    public void OpenDoor()
    {
        // Пример: открыть дверь
        LogMessage("Дверь открыта!");
        // Здесь может быть анимация двери
    }

    public void ActivateMechanism()
    {
        // Пример: активировать механизм
        LogMessage("Механизм активирован!");
        // Здесь может быть логика активации
    }

    public void ShowMessage(string message)
    {
        // Пример: показать сообщение
        Debug.Log($"Сообщение: {message}");
        // Здесь может быть UI для показа сообщений
    }
}