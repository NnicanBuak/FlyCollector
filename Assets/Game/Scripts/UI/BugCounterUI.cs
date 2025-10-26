using TMPro;
using UnityEngine;
using Game.Scripts.Core;
using Game.Scripts.BugData;

namespace Game.Scripts.UI
{
    public class BugCounterUI : MonoBehaviour
    {
        public static BugCounterUI Instance { get; private set; }

        [Header("UI")]
        private TextMeshProUGUI _counterText;

        [Header("Debug")]
        [SerializeField] private bool showDebug;

        private bool _subscribedToBugCounter;
        private BugList _currentTargetRuntime;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        
            // Автоматически получаем TextMeshProUGUI со своего объекта
            _counterText = GetComponent<TextMeshProUGUI>();
        
            // Если не нашли на том же объекте, ищем в дочерних
            if (_counterText == null)
            {
                _counterText = GetComponentInChildren<TextMeshProUGUI>();
            }
        
            // Проверка успешности
            if (_counterText == null)
            {
                Debug.LogError($"[BugCounterUI] TextMeshProUGUI component not found on {gameObject.name} or its children!");
            }
            else
            {
                if (showDebug)
                    Debug.Log($"[BugCounterUI] TextMeshProUGUI found on {_counterText.gameObject.name}");
            }
        }

        private void OnEnable()
        {
            BugCounter.InstanceChanged += OnBugCounterInstanceChanged;
            TrySubscribeBugCounter();

            BugList.InstanceChanged += OnTargetRuntimeInstanceChanged;
            SubscribeTargetRuntime(BugList.Instance);
        }

        private void Start()
        {
            // NOTE: Start() called after all Awake(), so singletons should be ready
            UpdateCounter();
        }

        private void OnDisable()
        {
            BugCounter.InstanceChanged -= OnBugCounterInstanceChanged;
            UnsubscribeBugCounter();

            BugList.InstanceChanged -= OnTargetRuntimeInstanceChanged;
            UnsubscribeTargetRuntime();
        }

        private void OnBugCounterInstanceChanged(BugCounter counter)
        {
            UnsubscribeBugCounter();
            TrySubscribeBugCounter();
            UpdateCounter();
        }

        private void TrySubscribeBugCounter()
        {
            if (!_subscribedToBugCounter && BugCounter.Instance != null)
            {
                BugCounter.Instance.OnJarsChanged += OnJarsChanged;
                _subscribedToBugCounter = true;
            }
        }

        private void UnsubscribeBugCounter()
        {
            if (_subscribedToBugCounter && BugCounter.Instance != null)
            {
                BugCounter.Instance.OnJarsChanged -= OnJarsChanged;
            }
            _subscribedToBugCounter = false;
        }

        private void SubscribeTargetRuntime(BugList runtime)
        {
            if (_currentTargetRuntime == runtime)
                return;

            UnsubscribeTargetRuntime();

            if (runtime != null)
            {
                runtime.TargetsChanged += UpdateCounter;
                runtime.CollectedChanged += UpdateCounter;
                runtime.BugsToSpawnChanged += UpdateCounter;
                _currentTargetRuntime = runtime;

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
            if (_currentTargetRuntime != null)
            {
                _currentTargetRuntime.TargetsChanged -= UpdateCounter;
                _currentTargetRuntime.CollectedChanged -= UpdateCounter;
                _currentTargetRuntime.BugsToSpawnChanged -= UpdateCounter;
                _currentTargetRuntime = null;
            }
        }

        private void OnJarsChanged(int _)
        {
            UpdateCounter();
        }

        private void OnTargetRuntimeInstanceChanged(BugList runtime)
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

            Debug.Log($"[BugCounterUI] BugList.Instance={(BugList.Instance != null ? "exists" : "null")}");

            if (BugList.Instance != null)
            {
                Debug.Log($"[BugCounterUI] BugList.Instance.Targets={(BugList.Instance.Targets == null ? "null" : BugList.Instance.Targets.Count.ToString())}");
            }

            if (BugList.Instance != null &&
                BugList.Instance.Targets != null &&
                BugList.Instance.Targets.Count > 0)
            {
                targetCount = BugList.Instance.Targets.Count;

                if (InventoryManager.Instance != null)
                {
                    // Подсчитываем правильные и неправильные жуки по уникальным типам (только баги ItemType.Quest)
                    var collectedSet = new System.Collections.Generic.HashSet<string>();
                    var bugSlots = InventoryManager.Instance.GetItemsByType(ItemType.Quest);
                    foreach (var slot in bugSlots)
                    {
                        if (slot.item != null && !string.IsNullOrEmpty(slot.item.itemID))
                        {
                            string normalizedId = BugList.NormalizeKey(slot.item.itemID);
                            collectedSet.Add(normalizedId);
                        }
                    }
                    
                    correctCount = 0;
                    foreach (var target in BugList.Instance.Targets)
                    {
                        if (collectedSet.Contains(BugList.NormalizeKey(target)))
                            correctCount++;
                    }
                    
                    wrongCount = Mathf.Max(0, collectedSet.Count - correctCount);
                    
                    usedRuntimeStats = true;
                }

                Debug.Log($"[BugCounterUI] Using BugList: targetCount={targetCount}, correctCount={correctCount}");
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

            int remain = Mathf.Max(0, targetCount - (correctCount + wrongCount));
            Debug.Log($"[BugCounterUI] Setting text to {remain} (counterText={(_counterText != null ? "exists" : "null")})");

            if (_counterText != null) _counterText.text = remain.ToString();

            Debug.Log($"[BugCounterUI] Final: target={targetCount}, correct={correctCount}, wrong={wrongCount} => remain={remain}");
        }

        private string ExtractBugKeyFromItemName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            
            // Нормализация имени предмета для ��звлечения ключа жука
            string normalized = itemName.Replace("(Clone)", "").Replace("_Variant", "").Trim();
            return BugList.NormalizeKey(normalized);
        }

        public void DecrementCounter(int amount = 1)
        {
            if (_counterText == null) return;

            if (int.TryParse(_counterText.text, out int current))
            {
                int newValue = Mathf.Max(0, current - amount);
                _counterText.text = newValue.ToString();

                if (showDebug)
                    Debug.Log($"[BugCounterUI] Decremented: {current} -> {newValue}");
            }
        }

        public void IncrementCounter(int amount = 1)
        {
            if (_counterText == null) return;

            if (int.TryParse(_counterText.text, out int current))
            {
                int newValue = current + amount;
                _counterText.text = newValue.ToString();

                if (showDebug)
                    Debug.Log($"[BugCounterUI] Incremented: {current} -> {newValue}");
            }
        }
    }
}
