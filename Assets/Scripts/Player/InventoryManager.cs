using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class InventorySlot
{
    public Item item;
    public int quantity;
    
    public InventorySlot(Item item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }
    
    public bool IsEmpty => item == null || quantity <= 0;
    
    public bool CanAddItem(Item newItem, int amount)
    {
        if (IsEmpty) return true;
        return item == newItem && quantity + amount <= item.maxStackSize;
    }
}

public class InventoryManager : MonoBehaviour
{
    [Header("Настройки инвентаря")]
    [Tooltip("Максимальное количество слотов")]
    [SerializeField] private int maxSlots = 20;
    
    [Header("События")]
    [SerializeField] private UnityEvent<Item, int> onItemAdded;
    [SerializeField] private UnityEvent<Item, int> onItemRemoved;
    [SerializeField] private UnityEvent onInventoryChanged;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private List<InventorySlot> inventory;
    
    // Singleton pattern
    public static InventoryManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeInventory();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeInventory()
    {
        inventory = new List<InventorySlot>();
        for (int i = 0; i < maxSlots; i++)
        {
            inventory.Add(new InventorySlot(null, 0));
        }
    }

    /// <summary>
    /// Добавить предмет в инвентарь
    /// </summary>
    /// <param name="item">Предмет для добавления</param>
    /// <param name="quantity">Количество</param>
    /// <returns>True если предмет был добавлен</returns>
    public bool AddItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
        {
            return false;
        }

        int remainingQuantity = quantity;

        // Сначала попробуем добавить в существующие стаки
        for (int i = 0; i < inventory.Count; i++)
        {
            var slot = inventory[i];
            if (!slot.IsEmpty && slot.item == item)
            {
                int canAdd = Mathf.Min(remainingQuantity, item.maxStackSize - slot.quantity);
                if (canAdd > 0)
                {
                    slot.quantity += canAdd;
                    remainingQuantity -= canAdd;
                    
                    if (remainingQuantity <= 0)
                        break;
                }
            }
        }

        // Если остались предметы, добавляем в пустые слоты
        if (remainingQuantity > 0)
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                var slot = inventory[i];
                if (slot.IsEmpty)
                {
                    int canAdd = Mathf.Min(remainingQuantity, item.maxStackSize);
                    slot.item = item;
                    slot.quantity = canAdd;
                    remainingQuantity -= canAdd;
                    
                    if (remainingQuantity <= 0)
                        break;
                }
            }
        }

        bool allAdded = remainingQuantity <= 0;
        int actuallyAdded = quantity - remainingQuantity;

        if (actuallyAdded > 0)
        {
            onItemAdded?.Invoke(item, actuallyAdded);
            onInventoryChanged?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log($"[Inventory] Добавлен предмет: {item.itemName} x{actuallyAdded}");
            }
        }

        if (showDebugInfo && !allAdded)
        {
            Debug.LogWarning($"[Inventory] Не удалось добавить все предметы. Осталось: {remainingQuantity}");
        }

        return allAdded;
    }

    /// <summary>
    /// Удалить предмет из инвентаря
    /// </summary>
    public bool RemoveItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
            return false;

        int remainingToRemove = quantity;

        for (int i = 0; i < inventory.Count; i++)
        {
            var slot = inventory[i];
            if (!slot.IsEmpty && slot.item == item)
            {
                int canRemove = Mathf.Min(remainingToRemove, slot.quantity);
                slot.quantity -= canRemove;
                remainingToRemove -= canRemove;

                if (slot.quantity <= 0)
                {
                    slot.item = null;
                    slot.quantity = 0;
                }

                if (remainingToRemove <= 0)
                    break;
            }
        }

        bool allRemoved = remainingToRemove <= 0;
        int actuallyRemoved = quantity - remainingToRemove;

        if (actuallyRemoved > 0)
        {
            onItemRemoved?.Invoke(item, actuallyRemoved);
            onInventoryChanged?.Invoke();
            
            if (showDebugInfo)
            {
                Debug.Log($"[Inventory] Удален предмет: {item.itemName} x{actuallyRemoved}");
            }
        }

        return allRemoved;
    }

    /// <summary>
    /// Проверить наличие предмета в инвентаре
    /// </summary>
    public bool HasItem(Item item, int requiredQuantity = 1)
    {
        if (item == null)
            return false;

        return GetItemCount(item) >= requiredQuantity;
    }

    /// <summary>
    /// Проверить наличие предмета по ID
    /// </summary>
    public bool HasItem(string itemID, int requiredQuantity = 1)
    {
        return GetItemCount(itemID) >= requiredQuantity;
    }

    /// <summary>
    /// Получить количество предмета в инвентаре
    /// </summary>
    public int GetItemCount(Item item)
    {
        if (item == null)
            return 0;

        int count = 0;
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item == item)
            {
                count += slot.quantity;
            }
        }
        return count;
    }

    /// <summary>
    /// Получить количество предмета по ID
    /// </summary>
    public int GetItemCount(string itemID)
    {
        if (string.IsNullOrEmpty(itemID))
            return 0;

        int count = 0;
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item.itemID == itemID)
            {
                count += slot.quantity;
            }
        }
        return count;
    }

    /// <summary>
    /// Получить все предметы в инвентаре
    /// </summary>
    public List<InventorySlot> GetAllItems()
    {
        var items = new List<InventorySlot>();
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty)
            {
                items.Add(slot);
            }
        }
        return items;
    }

    /// <summary>
    /// Очистить инвентарь
    /// </summary>
    public void ClearInventory()
    {
        for (int i = 0; i < inventory.Count; i++)
        {
            inventory[i].item = null;
            inventory[i].quantity = 0;
        }
        onInventoryChanged?.Invoke();
        
        if (showDebugInfo)
        {
            Debug.Log("[Inventory] Инвентарь очищен");
        }
    }

    /// <summary>
    /// Проверить есть ли свободное место
    /// </summary>
    public bool HasSpace(Item item, int quantity = 1)
    {
        if (item == null)
            return false;

        int remainingQuantity = quantity;

        // Проверяем существующие стаки
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item == item)
            {
                int canAdd = item.maxStackSize - slot.quantity;
                remainingQuantity -= canAdd;
                if (remainingQuantity <= 0)
                    return true;
            }
        }

        // Проверяем пустые слоты
        foreach (var slot in inventory)
        {
            if (slot.IsEmpty)
            {
                remainingQuantity -= item.maxStackSize;
                if (remainingQuantity <= 0)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Получить список всех предметов определенного типа
    /// </summary>
    public List<InventorySlot> GetItemsByType(ItemType itemType)
    {
        var items = new List<InventorySlot>();
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item.itemType == itemType)
            {
                items.Add(slot);
            }
        }
        return items;
    }
}