using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Менеджер для управления уровнями вложенности фокуса
/// Синглтон, отслеживает текущий уровень и историю переходов
/// </summary>
public class FocusLevelManager : MonoBehaviour
{
    public static FocusLevelManager Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Начальный уровень вложенности")]
    [SerializeField] private int startingNestLevel = 0;
    
    [Tooltip("Максимальная глубина вложенности")]
    [SerializeField] private int maxNestLevel = 10;
    
    [Tooltip("Сохранять между сценами (DontDestroyOnLoad)")]
    [SerializeField] private bool persistBetweenScenes = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // Текущий уровень вложенности
    private int currentNestLevel = 0;
    
    // История уровней вложенности (для навигации назад)
    private Stack<int> levelHistory = new Stack<int>();
    
    // История сфокусированных объектов
    private Stack<GameObject> focusedObjects = new Stack<GameObject>();
    
    // Флаг первого фокуса
    private bool hasEverFocused = false;
    
    // === ПЕРВОЕ ВЗАИМОДЕЙСТВИЕ (фокус или инспекция) ===
    private bool hasEverInteracted = false;

    public int CurrentNestLevel => currentNestLevel;
    public int PreviousNestLevel => levelHistory.Count > 0 ? levelHistory.Peek() : 0;
    public bool HasEverInteracted => hasEverInteracted;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        // DontDestroyOnLoad если нужно
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        currentNestLevel = startingNestLevel;

        if (showDebugInfo)
        {
            Debug.Log($"[FocusLevelManager] Инициализирован. Начальный уровень: {startingNestLevel}");
            Debug.Log($"[FocusLevelManager] Событие OnFirstInteraction готово к использованию");
        }
    }

    /// <summary>
    /// Установить новый уровень вложенности при фокусе
    /// </summary>
    public void SetNestLevel(int newLevel, GameObject focusedObject = null)
    {
        if (newLevel < 0 || newLevel > maxNestLevel)
        {
            Debug.LogWarning($"[FocusLevelManager] Попытка установить недопустимый уровень: {newLevel}");
            return;
        }

        // === ПРОВЕРКА ПЕРВОГО ВЗАИМОДЕЙСТВИЯ (фокус) ===
        if (!hasEverInteracted && currentNestLevel == startingNestLevel && newLevel > startingNestLevel)
        {
            TriggerFirstInteraction("Focus");
        }

        // === ПРОВЕРКА ПЕРВОГО ФОКУСА ===
        if (!hasEverFocused && currentNestLevel == startingNestLevel && newLevel > startingNestLevel)
        {
            hasEverFocused = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"[FocusLevelManager] 🎯 ПЕРВЫЙ ФОКУС! Запуск события OnFirstFocusEver");
            }
            
            // Вызываем событие первого фокуса
            OnFirstFocusEver?.Invoke();
        }

        // Сохраняем предыдущий уровень в историю
        levelHistory.Push(currentNestLevel);
        
        if (focusedObject != null)
        {
            focusedObjects.Push(focusedObject);
        }

        int previousLevel = currentNestLevel;
        currentNestLevel = newLevel;

        if (showDebugInfo)
        {
            Debug.Log($"[FocusLevelManager] SetNestLevel: {previousLevel} → {currentNestLevel}" + 
                     (focusedObject != null ? $" (объект: {focusedObject.name})" : "") +
                     $" | История: {levelHistory.Count} элементов");
        }

        // Вызываем событие изменения уровня
        OnNestLevelChanged?.Invoke(currentNestLevel);
    }

    /// <summary>
    /// Вызвать событие первого взаимодействия (фокус или инспекция)
    /// Вызывается из CameraController
    /// </summary>
    public void TriggerFirstInteraction(string interactionType)
    {
        if (hasEverInteracted) return;
        
        hasEverInteracted = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"[FocusLevelManager] ⭐⭐⭐ ПЕРВОЕ ВЗАИМОДЕЙСТВИЕ ({interactionType})! ⭐⭐⭐");
        }
        
        // Вызываем событие первого взаимодействия
        OnFirstInteraction?.Invoke();
    }

    /// <summary>
    /// Вернуться на предыдущий уровень вложенности
    /// </summary>
    public bool GoToPreviousLevel()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[FocusLevelManager] GoToPreviousLevel вызван. История до: {levelHistory.Count} элементов, текущий уровень: {currentNestLevel}");
        }

        if (levelHistory.Count == 0)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("[FocusLevelManager] Нет предыдущих уровней в истории!");
            }
            return false;
        }

        int previousLevel = levelHistory.Pop();
        
        if (focusedObjects.Count > 0)
        {
            focusedObjects.Pop();
        }

        if (showDebugInfo)
        {
            Debug.Log($"[FocusLevelManager] Возврат: {currentNestLevel} → {previousLevel} | История после: {levelHistory.Count} элементов");
        }

        currentNestLevel = previousLevel;
        OnNestLevelChanged?.Invoke(currentNestLevel);
        
        return true;
    }

    /// <summary>
    /// Сбросить уровень вложенности к начальному
    /// </summary>
    public void ResetToStartingLevel()
    {
        levelHistory.Clear();
        focusedObjects.Clear();
        currentNestLevel = startingNestLevel;
        
        if (showDebugInfo)
        {
            Debug.Log($"[FocusLevelManager] Сброс к начальному уровню: {startingNestLevel}");
        }

        OnNestLevelChanged?.Invoke(currentNestLevel);
    }

    /// <summary>
    /// Получить последний сфокусированный объект
    /// </summary>
    public GameObject GetLastFocusedObject()
    {
        return focusedObjects.Count > 0 ? focusedObjects.Peek() : null;
    }

    // Событие изменения уровня вложенности
    public event System.Action<int> OnNestLevelChanged;
    
    /// <summary>
    /// Событие первого фокуса в игре (вызывается только один раз)
    /// Используйте для запуска таймера или других одноразовых действий
    /// </summary>
    public event System.Action OnFirstFocusEver;
    
    /// <summary>
    /// Событие первого взаимодействия (фокус ИЛИ инспекция) в игре (вызывается только один раз)
    /// Используйте для запуска таймера - более универсальное, чем OnFirstFocusEver
    /// </summary>
    public event System.Action OnFirstInteraction;
}