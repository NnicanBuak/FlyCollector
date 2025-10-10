
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

    [Header("События (UnityEvent)")]
    [SerializeField] private UnityEvent<Item, int> onItemAdded = new UnityEvent<Item, int>();
    [SerializeField] private UnityEvent<Item, int> onItemRemoved = new UnityEvent<Item, int>();
    [SerializeField] private UnityEvent onInventoryChanged = new UnityEvent();

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;


    public UnityEvent<Item, int> OnItemAdded  => onItemAdded;
    public UnityEvent<Item, int> OnItemRemoved => onItemRemoved;
    public UnityEvent OnInventoryChanged       => onInventoryChanged;


    public event System.Action InventoryChanged;


    private List<InventorySlot> inventory;


    public static InventoryManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeInventory();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializeInventory()
    {
        inventory = new List<InventorySlot>(maxSlots);
        for (int i = 0; i < maxSlots; i++)
        {
            inventory.Add(new InventorySlot(null, 0));
        }
    }




    public bool AddItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;

        int remaining = quantity;
        int actuallyAdded = 0;


        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            var slot = inventory[i];
            if (!slot.IsEmpty && slot.item == item)
            {
                int free = item.maxStackSize - slot.quantity;
                if (free > 0)
                {
                    int add = Mathf.Min(remaining, free);
                    slot.quantity += add;
                    remaining -= add;
                    actuallyAdded += add;
                }
            }
        }


        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            var slot = inventory[i];
            if (slot.IsEmpty)
            {
                int add = Mathf.Min(remaining, item.maxStackSize);
                slot.item = item;
                slot.quantity = add;
                remaining -= add;
                actuallyAdded += add;
            }
        }

        if (actuallyAdded > 0)
        {
            onItemAdded?.Invoke(item, actuallyAdded);
            RaiseInventoryChanged();

            if (showDebugInfo)
                Debug.Log($"[Inventory] Добавлено: {item.itemName} x{actuallyAdded} (запрошено {quantity})");
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"[Inventory] Не удалось добавить: {item?.itemName} x{quantity}");
        }

        return remaining <= 0;
    }


    public bool RemoveItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;

        int remaining = quantity;
        int actuallyRemoved = 0;

        for (int i = 0; i < inventory.Count && remaining > 0; i++)
        {
            var slot = inventory[i];
            if (!slot.IsEmpty && slot.item == item)
            {
                int rm = Mathf.Min(remaining, slot.quantity);
                slot.quantity -= rm;
                remaining -= rm;
                actuallyRemoved += rm;

                if (slot.quantity <= 0)
                {
                    slot.item = null;
                    slot.quantity = 0;
                }
            }
        }

        if (actuallyRemoved > 0)
        {
            onItemRemoved?.Invoke(item, actuallyRemoved);
            RaiseInventoryChanged();

            if (showDebugInfo)
                Debug.Log($"[Inventory] Удалено: {item.itemName} x{actuallyRemoved} (запрошено {quantity})");
        }

        return remaining <= 0;
    }


    public bool HasItem(Item item, int requiredQuantity = 1)
    {
        if (item == null) return false;
        return GetItemCount(item) >= requiredQuantity;
    }


    public bool HasItem(string itemID, int requiredQuantity = 1)
    {
        if (string.IsNullOrEmpty(itemID)) return false;
        return GetItemCount(itemID) >= requiredQuantity;
    }


    public int GetItemCount(Item item)
    {
        if (item == null) return 0;

        int count = 0;
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item == item)
                count += slot.quantity;
        }
        return count;
    }


    public int GetItemCount(string itemID)
    {
        if (string.IsNullOrEmpty(itemID)) return 0;

        int count = 0;
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item != null && slot.item.itemID == itemID)
                count += slot.quantity;
        }
        return count;
    }


    public List<InventorySlot> GetAllItems()
    {
        var list = new List<InventorySlot>();
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty)
                list.Add(new InventorySlot(slot.item, slot.quantity));
        }
        return list;
    }


    public void ClearInventory()
    {
        for (int i = 0; i < inventory.Count; i++)
        {
            inventory[i].item = null;
            inventory[i].quantity = 0;
        }
        RaiseInventoryChanged();

        if (showDebugInfo)
            Debug.Log("[Inventory] Инвентарь очищен");
    }


    public bool HasSpace(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;

        int remaining = quantity;


        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item == item)
            {
                int free = item.maxStackSize - slot.quantity;
                if (free > 0)
                {
                    remaining -= free;
                    if (remaining <= 0) return true;
                }
            }
        }


        foreach (var slot in inventory)
        {
            if (slot.IsEmpty)
            {
                remaining -= item.maxStackSize;
                if (remaining <= 0) return true;
            }
        }

        return false;
    }


    public List<InventorySlot> GetItemsByType(ItemType itemType)
    {
        var list = new List<InventorySlot>();
        foreach (var slot in inventory)
        {
            if (!slot.IsEmpty && slot.item != null && slot.item.itemType == itemType)
                list.Add(new InventorySlot(slot.item, slot.quantity));
        }
        return list;
    }



    private void RaiseInventoryChanged()
    {
        onInventoryChanged?.Invoke();
        InventoryChanged?.Invoke();
    }

#if UNITY_EDITOR

    [ContextMenu("Log inventory")]
    private void LogInventory()
    {
        for (int i = 0; i < inventory.Count; i++)
        {
            var s = inventory[i];
            Debug.Log($"[{i}] {(s.IsEmpty ? "<empty>" : $"{s.item.itemName} x{s.quantity}")}");
        }
    }
#endif
}
