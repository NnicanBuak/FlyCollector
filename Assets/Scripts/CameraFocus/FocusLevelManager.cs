using UnityEngine;
using System.Collections.Generic;

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


    private int currentNestLevel = 0;
    

    private Stack<int> levelHistory = new Stack<int>();
    

    private Stack<GameObject> focusedObjects = new Stack<GameObject>();
    

    private bool hasEverFocused = false;
    

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


    public void SetNestLevel(int newLevel, GameObject focusedObject = null)
    {
        if (newLevel < 0 || newLevel > maxNestLevel)
        {
            Debug.LogWarning($"[FocusLevelManager] Попытка установить недопустимый уровень: {newLevel}");
            return;
        }


        if (!hasEverInteracted && currentNestLevel == startingNestLevel && newLevel > startingNestLevel)
        {
            TriggerFirstInteraction("Focus");
        }


        if (!hasEverFocused && currentNestLevel == startingNestLevel && newLevel > startingNestLevel)
        {
            hasEverFocused = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"[FocusLevelManager] 🎯 ПЕРВЫЙ ФОКУС! Запуск события OnFirstFocusEver");
            }
            

            OnFirstFocusEver?.Invoke();
        }


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

        // Track nest level change in analytics
        GAManager.Instance.TrackNestLevelChange(previousLevel, currentNestLevel, focusedObject != null ? focusedObject.name : "");

        OnNestLevelChanged?.Invoke(currentNestLevel);
    }


    public void TriggerFirstInteraction(string interactionType)
    {
        if (hasEverInteracted) return;
        
        hasEverInteracted = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"[FocusLevelManager] ⭐⭐⭐ ПЕРВОЕ ВЗАИМОДЕЙСТВИЕ ({interactionType})! ⭐⭐⭐");
        }
        

        OnFirstInteraction?.Invoke();
    }


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


    public GameObject GetLastFocusedObject()
    {
        return focusedObjects.Count > 0 ? focusedObjects.Peek() : null;
    }


    public event System.Action<int> OnNestLevelChanged;
    

    public event System.Action OnFirstFocusEver;
    

    public event System.Action OnFirstInteraction;
}