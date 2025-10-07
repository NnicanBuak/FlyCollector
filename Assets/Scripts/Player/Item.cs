using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Новый Предмет", menuName = "Инвентарь/Предмет")]
public class Item : ScriptableObject
{
    [Header("Основная информация")]
    [Tooltip("Уникальный ID предмета")]
    public string itemID;
    
    [Tooltip("Название предмета")]
    public string itemName;
    
    [Tooltip("Описание предмета")]
    [TextArea(3, 5)]
    public string description;
    
    [Header("Визуал")]
    [Tooltip("Иконка предмета для UI")]
    public Sprite icon;
    
    [Tooltip("3D модель предмета")]
    public GameObject worldModel;
    
    [Header("Свойства")]
    [Tooltip("Максимальное количество в одном слоте")]
    public int maxStackSize = 1;
    
    [Tooltip("Тип предмета")]
    public ItemType itemType = ItemType.Misc;
    
    [Tooltip("Можно ли использовать предмет")]
    public bool isUsable = false;
    
    [Tooltip("Редкость предмета")]
    public ItemRarity rarity = ItemRarity.Common;

    void OnValidate()
    {
        // Автоматически генерируем ID если он пустой
        if (string.IsNullOrEmpty(itemID))
        {
            itemID = name.Replace(" ", "_").ToLower();
        }
    }
}

[System.Serializable]
public enum ItemType
{
    Misc,      // Разное
    Key,       // Ключи
    Tool,      // Инструменты
    Consumable, // Расходные
    Quest      // Квестовые
}

[System.Serializable]
public enum ItemRarity
{
    Common,    // Обычный
    Uncommon,  // Необычный
    Rare,      // Редкий
    Epic,      // Эпический
    Legendary  // Легендарный
}