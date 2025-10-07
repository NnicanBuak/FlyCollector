using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Компонент для предметов, которые можно подобрать в инвентарь
/// Работает совместно с InteractableObject
/// </summary>
[RequireComponent(typeof(InteractableObject))]
public class PickupItem : MonoBehaviour
{
    [Header("Настройки предмета")]
    [Tooltip("Данные предмета")]
    [SerializeField] private Item itemData;
    
    [Tooltip("Количество предметов")]
    [SerializeField] private int quantity = 1;
    
    [Header("Анимация подбора")]
    [Tooltip("Проигрывать анимацию при подборе")]
    [SerializeField] private bool usePickupAnimation = true;
    
    [Tooltip("Длительность анимации подбора")]
    [SerializeField] private float animationDuration = 0.5f;
    
    [Tooltip("Высота подъема при анимации")]
    [SerializeField] private float liftHeight = 1f;
    
    [Tooltip("Скорость вращения при анимации")]
    [SerializeField] private float rotationSpeed = 360f;
    
    [Header("Эффекты")]
    [Tooltip("Эффект частиц при подборе")]
    [SerializeField] private ParticleSystem pickupEffect;
    
    [Tooltip("Звук при подборе")]
    [SerializeField] private AudioClip pickupSound;
    
    [Header("События")]
    [Tooltip("Событие при успешном подборе")]
    [SerializeField] private UnityEvent<Item, int> onItemPickedUp;
    
    [Tooltip("Событие когда инвентарь полон")]
    [SerializeField] private UnityEvent onInventoryFull;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private InteractableObject interactable;
    private bool isBeingPickedUp = false;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Awake()
    {
        interactable = GetComponent<InteractableObject>();
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    void Start()
    {
        // Подписываемся на событие взаимодействия
        if (interactable != null)
        {
            // Добавляем наш метод в UnityEvent через код
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
    /// Обработчик взаимодействия с предметом
    /// </summary>
    private void OnInteract()
    {
        if (isBeingPickedUp)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[PickupItem] {gameObject.name} уже подбирается");
            }
            return;
        }

        if (itemData == null)
        {
            Debug.LogError($"[PickupItem] У {gameObject.name} не установлены данные предмета!");
            return;
        }

        // Проверяем есть ли место в инвентаре
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[PickupItem] InventoryManager не найден!");
            return;
        }

        if (!InventoryManager.Instance.HasSpace(itemData, quantity))
        {
            if (showDebugInfo)
            {
                Debug.Log($"[PickupItem] Нет места в инвентаре для {itemData.itemName}");
            }
            
            onInventoryFull?.Invoke();
            return;
        }

        // Запускаем процесс подбора
        StartCoroutine(PickupSequence());
    }

    /// <summary>
    /// Последовательность подбора предмета
    /// </summary>
    private IEnumerator PickupSequence()
    {
        isBeingPickedUp = true;
        
        // Отключаем взаимодействие
        if (interactable != null)
        {
            interactable.SetInteractable(false);
        }

        // Проигрываем эффект частиц
        if (pickupEffect != null)
        {
            pickupEffect.Play();
        }

        // Проигрываем звук
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        // Анимация подбора
        if (usePickupAnimation)
        {
            yield return StartCoroutine(PlayPickupAnimation());
        }

        // Добавляем предмет в инвентарь
        bool success = InventoryManager.Instance.AddItem(itemData, quantity);
        
        if (success)
        {
            onItemPickedUp?.Invoke(itemData, quantity);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PickupItem] Подобран предмет: {itemData.itemName} x{quantity}");
            }
        }
        else
        {
            Debug.LogError($"[PickupItem] Не удалось добавить предмет {itemData.itemName} в инвентарь");
        }

        // Удаляем объект
        Destroy(gameObject);
    }

    /// <summary>
    /// Анимация подбора предмета
    /// </summary>
    private IEnumerator PlayPickupAnimation()
    {
        float elapsedTime = 0f;
        Vector3 targetPosition = originalPosition + Vector3.up * liftHeight;
        
        while (elapsedTime < animationDuration)
        {
            float progress = elapsedTime / animationDuration;
            
            // Движение вверх
            transform.position = Vector3.Lerp(originalPosition, targetPosition, progress);
            
            // Вращение
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            
            // Масштабирование (исчезновение)
            float scale = Mathf.Lerp(1f, 0f, progress);
            transform.localScale = Vector3.one * scale;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Установить данные предмета
    /// </summary>
    public void SetItemData(Item item, int count = 1)
    {
        itemData = item;
        quantity = count;
        
        // Обновляем визуал если нужно
        if (item != null && item.worldModel != null)
        {
            // Можно заменить модель объекта
            UpdateVisual();
        }
    }

    /// <summary>
    /// Обновить визуал предмета
    /// </summary>
    private void UpdateVisual()
    {
        if (itemData?.worldModel != null)
        {
            // Удаляем старую модель
            var oldModel = transform.Find("Model");
            if (oldModel != null)
            {
                DestroyImmediate(oldModel.gameObject);
            }

            // Создаем новую модель
            var newModel = Instantiate(itemData.worldModel, transform);
            newModel.name = "Model";
            newModel.transform.localPosition = Vector3.zero;
            newModel.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Получить данные предмета
    /// </summary>
    public Item GetItemData()
    {
        return itemData;
    }

    /// <summary>
    /// Получить количество
    /// </summary>
    public int GetQuantity()
    {
        return quantity;
    }

    /// <summary>
    /// Проверить можно ли подобрать предмет
    /// </summary>
    public bool CanPickup()
    {
        return !isBeingPickedUp && 
               itemData != null && 
               InventoryManager.Instance != null && 
               InventoryManager.Instance.HasSpace(itemData, quantity);
    }

    // Метод для создания предмета на сцене из кода
    public static GameObject CreatePickupItem(Item itemData, Vector3 position, int quantity = 1)
    {
        if (itemData?.worldModel == null)
        {
            Debug.LogError("[PickupItem] Нет 3D модели для предмета " + itemData?.itemName);
            return null;
        }

        // Создаем объект
        GameObject pickupObject = Instantiate(itemData.worldModel, position, Quaternion.identity);
        pickupObject.name = $"Pickup_{itemData.itemName}";
        
        // Добавляем необходимые компоненты
        var interactable = pickupObject.AddComponent<InteractableObject>();
        var pickup = pickupObject.AddComponent<PickupItem>();
        
        // Настраиваем компоненты
        pickup.SetItemData(itemData, quantity);
        
        // Добавляем коллайдер если его нет
        if (pickupObject.GetComponent<Collider>() == null)
        {
            pickupObject.AddComponent<BoxCollider>();
        }

        return pickupObject;
    }
}