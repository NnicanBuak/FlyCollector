using TMPro;
using UnityEngine;
using System.Linq;

public class BugCounterUI : MonoBehaviour
{
    public static BugCounterUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI counterText;

    [Header("Debug")]
    [SerializeField] private bool showDebug;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged += OnInventoryChangedCSharp;
            InventoryManager.Instance.OnInventoryChanged.AddListener(OnInventoryChangedUnity);
        }

        if (TargetBugsRuntime.Instance != null)
        {
            TargetBugsRuntime.Instance.TargetsChanged += UpdateCounter;
            TargetBugsRuntime.Instance.BugsToSpawnChanged += UpdateCounter;
        }


        if (BugCounter.Instance != null)
        {
            BugCounter.Instance.OnJarsChanged += OnJarsChanged;
        }

        UpdateCounter();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged -= OnInventoryChangedCSharp;
            InventoryManager.Instance.OnInventoryChanged.RemoveListener(OnInventoryChangedUnity);
        }

        if (TargetBugsRuntime.Instance != null)
        {
            TargetBugsRuntime.Instance.TargetsChanged -= UpdateCounter;
            TargetBugsRuntime.Instance.BugsToSpawnChanged -= UpdateCounter;
        }

        if (BugCounter.Instance != null)
        {
            BugCounter.Instance.OnJarsChanged -= OnJarsChanged;
        }
    }

    private void OnJarsChanged(int newCount)
    {
        UpdateCounter();
    }

    private void OnInventoryChangedCSharp() => UpdateCounter();
    private void OnInventoryChangedUnity()   => UpdateCounter();

    public void UpdateCounter()
    {
        int targetCount = 0;
        int caughtCount = 0;


        if (TargetBugsRuntime.Instance != null && TargetBugsRuntime.Instance.Targets != null && TargetBugsRuntime.Instance.Targets.Count > 0)
        {
            targetCount = TargetBugsRuntime.Instance.Targets.Count;


            if (CaughtBugsRuntime.Instance != null)
            {
                var targets = TargetBugsRuntime.Instance.Targets;
                caughtCount = CaughtBugsRuntime.Instance.Caught.Count(id => targets.Contains(id));
            }
        }
        else if (BugCounter.Instance != null)
        {

            targetCount = BugCounter.Instance.MaxJars;
            caughtCount = BugCounter.Instance.MaxJars - BugCounter.Instance.CurrentJars;

            if (showDebug)
                Debug.Log($"[BugCounterUI] Fallback mode: using BugCounter (max={targetCount}, current={BugCounter.Instance.CurrentJars})");
        }
        else
        {
            if (showDebug)
                Debug.LogWarning("[BugCounterUI] Both TargetBugsRuntime and BugCounter are not available!");
        }

        int remain = Mathf.Max(0, targetCount - caughtCount);
        if (counterText != null) counterText.text = remain.ToString();

        if (showDebug)
            Debug.Log($"[BugCounterUI] target={targetCount}, caught={caughtCount} => remain={remain}");
    }


    public void DecrementCounter(int amount = 1)
    {
        if (counterText == null) return;

        if (int.TryParse(counterText.text, out int current))
        {
            int newValue = Mathf.Max(0, current - amount);
            counterText.text = newValue.ToString();

            if (showDebug)
                Debug.Log($"[BugCounterUI] Decremented: {current} → {newValue}");
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
                Debug.Log($"[BugCounterUI] Incremented: {current} → {newValue}");
        }
    }
}
