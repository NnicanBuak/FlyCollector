using TMPro;
using UnityEngine;

public class BugCounterUI : MonoBehaviour
{
    public static BugCounterUI Instance { get; private set; }

    [Header("UI")]
    // ← УБРАТЬ SerializeField, вместо этого получать автоматически
    private TextMeshProUGUI counterText;

    [Header("Debug")]
    [SerializeField] private bool showDebug;

    private bool subscribedToBugCounter;
    private bool subscribedToCaughtRuntime;
    private TargetBugsRuntime currentTargetRuntime;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    
        // Автоматически получаем TextMeshProUGUI со своего объекта
        counterText = GetComponent<TextMeshProUGUI>();
    
        // Если не нашли на том же объекте, ищем в дочерних
        if (counterText == null)
        {
            counterText = GetComponentInChildren<TextMeshProUGUI>();
        }
    
        // Проверка успешности
        if (counterText == null)
        {
            Debug.LogError($"[BugCounterUI] TextMeshProUGUI component not found on {gameObject.name} or its children!");
        }
        else
        {
            if (showDebug)
                Debug.Log($"[BugCounterUI] TextMeshProUGUI found on {counterText.gameObject.name}");
        }
    }

    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged += OnInventoryChangedCSharp;
            InventoryManager.Instance.OnInventoryChanged.AddListener(OnInventoryChangedUnity);
        }

        BugCounter.InstanceChanged += OnBugCounterInstanceChanged;
        TrySubscribeBugCounter();

        CaughtBugsRuntime.InstanceChanged += OnCaughtRuntimeInstanceChanged;
        TrySubscribeCaughtRuntime();

        TargetBugsRuntime.InstanceChanged += OnTargetRuntimeInstanceChanged;
        SubscribeTargetRuntime(TargetBugsRuntime.Instance);
    }

    private void Start()
    {
        // NOTE: Start() called after all Awake(), so singletons should be ready
        UpdateCounter();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged -= OnInventoryChangedCSharp;
            InventoryManager.Instance.OnInventoryChanged.RemoveListener(OnInventoryChangedUnity);
        }

        BugCounter.InstanceChanged -= OnBugCounterInstanceChanged;
        UnsubscribeBugCounter();

        CaughtBugsRuntime.InstanceChanged -= OnCaughtRuntimeInstanceChanged;
        UnsubscribeCaughtRuntime();

        TargetBugsRuntime.InstanceChanged -= OnTargetRuntimeInstanceChanged;
        UnsubscribeTargetRuntime();
    }

    private void OnBugCounterInstanceChanged(BugCounter counter)
    {
        UnsubscribeBugCounter();
        TrySubscribeBugCounter();
        UpdateCounter();
    }

    private void OnCaughtRuntimeInstanceChanged(CaughtBugsRuntime runtime)
    {
        UnsubscribeCaughtRuntime();
        TrySubscribeCaughtRuntime();
        UpdateCounter();
    }

    private void TrySubscribeBugCounter()
    {
        if (!subscribedToBugCounter && BugCounter.Instance != null)
        {
            BugCounter.Instance.OnJarsChanged += OnJarsChanged;
            subscribedToBugCounter = true;
        }
    }

    private void UnsubscribeBugCounter()
    {
        if (subscribedToBugCounter && BugCounter.Instance != null)
        {
            BugCounter.Instance.OnJarsChanged -= OnJarsChanged;
        }
        subscribedToBugCounter = false;
    }

    private void TrySubscribeCaughtRuntime()
    {
        if (!subscribedToCaughtRuntime && CaughtBugsRuntime.Instance != null)
        {
            CaughtBugsRuntime.Instance.OnCaughtChanged += OnCaughtChanged;
            subscribedToCaughtRuntime = true;
        }
    }

    private void UnsubscribeCaughtRuntime()
    {
        if (subscribedToCaughtRuntime && CaughtBugsRuntime.Instance != null)
        {
            CaughtBugsRuntime.Instance.OnCaughtChanged -= OnCaughtChanged;
        }
        subscribedToCaughtRuntime = false;
    }

    private void SubscribeTargetRuntime(TargetBugsRuntime runtime)
    {
        if (currentTargetRuntime == runtime)
            return;

        UnsubscribeTargetRuntime();

        if (runtime != null)
        {
            runtime.TargetsChanged += UpdateCounter;
            runtime.BugsToSpawnChanged += UpdateCounter;
            currentTargetRuntime = runtime;

            // NOTE: Check if data already set before subscription (race condition prevention)
            if (runtime.Targets != null && runtime.Targets.Count > 0)
            {
                Debug.Log($"[BugCounterUI] SubscribeTargetRuntime: Targets already set ({runtime.Targets.Count}), calling UpdateCounter");
                UpdateCounter();
            }
            else
            {
                Debug.Log($"[BugCounterUI] SubscribeTargetRuntime: Targets not set yet (runtime.Targets={(runtime.Targets == null ? "null" : runtime.Targets.Count.ToString())})");
            }
        }
    }

    private void UnsubscribeTargetRuntime()
    {
        if (currentTargetRuntime != null)
        {
            currentTargetRuntime.TargetsChanged -= UpdateCounter;
            currentTargetRuntime.BugsToSpawnChanged -= UpdateCounter;
            currentTargetRuntime = null;
        }
    }

    private void OnJarsChanged(int _)
    {
        UpdateCounter();
    }

    private void OnCaughtChanged()
    {
        UpdateCounter();
    }

    private void OnTargetRuntimeInstanceChanged(TargetBugsRuntime runtime)
    {
        SubscribeTargetRuntime(runtime);
        UpdateCounter();
    }

    private void OnInventoryChangedCSharp() => UpdateCounter();
    private void OnInventoryChangedUnity() => UpdateCounter();

    public void UpdateCounter()
    {
        Debug.Log($"[BugCounterUI] UpdateCounter called");

        int targetCount = 0;
        int correctCount = 0;
        int wrongCount = 0;
        bool usedRuntimeStats = false;

        Debug.Log($"[BugCounterUI] TargetBugsRuntime.Instance={(TargetBugsRuntime.Instance != null ? "exists" : "null")}");

        if (TargetBugsRuntime.Instance != null)
        {
            Debug.Log($"[BugCounterUI] TargetBugsRuntime.Instance.Targets={(TargetBugsRuntime.Instance.Targets == null ? "null" : TargetBugsRuntime.Instance.Targets.Count.ToString())}");
        }

        if (TargetBugsRuntime.Instance != null &&
            TargetBugsRuntime.Instance.Targets != null &&
            TargetBugsRuntime.Instance.Targets.Count > 0)
        {
            targetCount = TargetBugsRuntime.Instance.Targets.Count;

            if (CaughtBugsRuntime.Instance != null)
            {
                CaughtBugsRuntime.Instance.GetStats(out _, out correctCount, out wrongCount);
                usedRuntimeStats = true;
            }

            Debug.Log($"[BugCounterUI] Using TargetBugsRuntime: targetCount={targetCount}, correctCount={correctCount}");
        }

        if (!usedRuntimeStats)
        {
            if (BugCounter.Instance != null)
            {
                targetCount = BugCounter.Instance.MaxJars;
                correctCount = BugCounter.Instance.MaxJars - BugCounter.Instance.CurrentJars;

                Debug.Log($"[BugCounterUI] Fallback mode: using BugCounter (max={targetCount}, current={BugCounter.Instance.CurrentJars})");
            }
        }

        int remain = Mathf.Max(0, targetCount - correctCount);
        Debug.Log($"[BugCounterUI] Setting text to {remain} (counterText={(counterText != null ? "exists" : "null")})");

        if (counterText != null) counterText.text = remain.ToString();

        Debug.Log($"[BugCounterUI] Final: target={targetCount}, correct={correctCount}, wrong={wrongCount} => remain={remain}");
    }

    public void DecrementCounter(int amount = 1)
    {
        if (counterText == null) return;

        if (int.TryParse(counterText.text, out int current))
        {
            int newValue = Mathf.Max(0, current - amount);
            counterText.text = newValue.ToString();

            if (showDebug)
                Debug.Log($"[BugCounterUI] Decremented: {current} -> {newValue}");
        }
    }

    public void IncrementCounter(int amount = 1)
    {
        if (counterText == null) return;

        if (int.TryParse(counterText.text, out int current))
        {
            int newValue = current + amount;
            counterText.text = newValue.ToString();

            if (showDebug)
                Debug.Log($"[BugCounterUI] Incremented: {current} -> {newValue}");
        }
    }
}
