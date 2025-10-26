using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Scripts.BugData; 

namespace Game.Scripts.Scene
{
    public class GameSceneController : MonoBehaviour
    {
        [Header("Scene flow")]
        [SerializeField] private string gameOverScene = "GameOver";

        [Header("References (auto if empty)")]
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private InventoryManager inventory;

        [Header("Bug quota source")]
        [Tooltip("Если включено — считаем жуков по инвентарю, иначе — по InventoryManager")]
        [SerializeField] private bool useInventoryAsSource = true;

        [Tooltip("Если спис��к не пуст — учитываем только эти ассеты как жуков")]
        [SerializeField] private List<Item> bugWhitelistItems = new List<Item>();

        [Tooltip("Если список пуст — считаем жуков по типу (например, Quest)")]
        [SerializeField] private ItemType bugItemType = ItemType.Quest;

        [Header("Game data")]
        [SerializeField] private List<string> targets = new List<string>();
        [SerializeField] private List<string> bugsToSpawn = new List<string>();
        [SerializeField] private List<string> caught = new List<string>();

        [Header("Behaviour")]
        [Tooltip("Завершать игру сразу после открытия выхода")]
        [SerializeField] private bool finishWhenExitOpens = true;

        [Header("Debug")]
        [SerializeField] private bool showDebug = true;

        private bool _isFinished;

        void Awake()
        {
            if (!gameTimer) gameTimer = GameTimer.Instance;
            if (!inventory) inventory = InventoryManager.Instance;
        }

        void OnEnable()
        {
            if (gameTimer) gameTimer.onTimerEnd.AddListener(OnTimerEnd);
            if (inventory) inventory.OnInventoryChanged.AddListener(HandleInventoryChanged);
        }

        void OnDisable()
        {
            if (gameTimer) gameTimer.onTimerEnd.RemoveListener(OnTimerEnd);
            if (inventory) inventory.OnInventoryChanged.RemoveListener(HandleInventoryChanged);
        }

        void HandleInventoryChanged()
        {
            CheckQuotaAndMaybeOpenExit();
        }

        void Start()
        {
            if (BugList.Instance != null)
            {
                targets = new List<string>(BugList.Instance.Targets);
                bugsToSpawn = new List<string>(BugList.Instance.BugsToSpawn);
            }
            CheckQuotaAndMaybeOpenExit();
        }

        void OnTimerEnd()
        {
            if (showDebug) Debug.Log("[GameSceneController] ⏳ Timer ended → Timeout");
            FinishGame(GameOutcome.Timeout, overrideCaught: 0, wrongOverride: 0);
        }

        void CheckQuotaAndMaybeOpenExit()
        {
            int target = GetTargetCount();
            if (target <= 0) return;

            int caughtCount = GetCaughtCount();
            if (showDebug) Debug.Log($"[GameSceneController] Quota check: caught={caughtCount}, target={target}");

            if (caughtCount >= target && finishWhenExitOpens)
            {
                int wrong = ComputeWrongCount();
                var outcome = (wrong > 0) ? GameOutcome.WrongBugs : GameOutcome.Escaped;
                FinishGame(outcome);
            }
        }

        void FinishGame(GameOutcome outcome, int? overrideCaught = null, int? wrongOverride = null)
        {
            if (_isFinished) return;
            _isFinished = true;

            // Compute stats using local data
            int totalCaught = overrideCaught ?? GetCaughtCount();
            int wrongCount = wrongOverride ?? ComputeWrongCount();
            int correctCount = Mathf.Max(0, totalCaught - wrongCount);

            if (showDebug)
                Debug.Log($"[GameSceneController] Finish → {outcome} | caught={totalCaught}, wrong={wrongCount}, target={targets.Count}");

            // Сформируем список пойманных багов для отчета
            var caughtList = new List<string>();
            if (useInventoryAsSource && inventory != null)
            {
                var collected = new HashSet<string>();
                var slots = inventory.GetItemsByType(ItemType.Quest);
                foreach (var s in slots)
                {
                    if (s.item == null) continue;
                    if (bugWhitelistItems != null && bugWhitelistItems.Count > 0)
                    {
                        if (!bugWhitelistItems.Contains(s.item)) continue;
                    }
                    if (!string.IsNullOrEmpty(s.item.itemID))
                    {
                        collected.Add(BugList.NormalizeKey(s.item.itemID));
                    }
                }
                caughtList = collected.ToList();
            }
            else
            {
                // Используем уже накопленный список (если ведется где-то еще)
                caughtList = new List<string>(caught);
            }

            // Pass results via GameSceneManager
            var result = new GameResult
            {
                Outcome = outcome,
                Caught = caughtList,
                Targets = new List<string>(targets),
                BugsToSpawn = new List<string>(bugsToSpawn),
                Total = totalCaught,
                Correct = correctCount,
                Wrong = wrongCount
            };
            var gsm = GameSceneManager.Instance;
            if (gsm != null)
            {
                gsm.SetPersistentData("gameResult", result);
            }
            else
            {
                Debug.LogWarning("[GameSceneController] GameSceneManager not found");
            }
            SceneManager.LoadScene(gameOverScene);
        }

        int GetTargetCount()
        {
            return targets.Count;
        }

        int GetCaughtCount()
        {
            if (!useInventoryAsSource)
            {
                // Используем InventoryManager вместо BugInventory (оставим поведение как было — по количеству)
                if (InventoryManager.Instance != null)
                {
                    var bugItems = InventoryManager.Instance.GetItemsByType(ItemType.Quest);
                    int totalCount = 0;
                    foreach (var slot in bugItems)
                    {
                        totalCount += slot.quantity;
                    }
                    return totalCount;
                }
                return 0;
            }

            if (inventory == null) return 0;

            // Подсчитываем уникальные ID багов
            var set = new HashSet<string>();
            var slots = inventory.GetItemsByType(ItemType.Quest);
            foreach (var s in slots)
            {
                if (s.item == null) continue;
                // Проверяем по белому списку или по типу — здесь уже только Quest, проверка whitelist опциональна
                if (bugWhitelistItems != null && bugWhitelistItems.Count > 0)
                {
                    if (!bugWhitelistItems.Contains(s.item)) continue;
                }
                if (!string.IsNullOrEmpty(s.item.itemID))
                {
                    set.Add(BugList.NormalizeKey(s.item.itemID));
                }
            }

            return set.Count;
        }

        int ComputeWrongCount()
        {
            if (useInventoryAsSource)
            {
                if (inventory == null) return 0;
                var targetSet = new HashSet<string>(targets.Select(BugList.NormalizeKey));
                var collected = new HashSet<string>();
                var slots = inventory.GetItemsByType(ItemType.Quest);
                foreach (var s in slots)
                {
                    if (s.item == null) continue;
                    if (bugWhitelistItems != null && bugWhitelistItems.Count > 0)
                    {
                        if (!bugWhitelistItems.Contains(s.item)) continue;
                    }
                    if (!string.IsNullOrEmpty(s.item.itemID))
                    {
                        collected.Add(BugList.NormalizeKey(s.item.itemID));
                    }
                }
                int correct = collected.Count(id => targetSet.Contains(id));
                int wrong = Mathf.Max(0, collected.Count - correct);
                return wrong;
            }

            // Старый путь: InventoryManager + сравнение по имени (оставим без изменений)
            if (InventoryManager.Instance != null)
            {
                var targetSet = new System.Collections.Generic.HashSet<string>(targets);
                var bugItems = InventoryManager.Instance.GetItemsByType(ItemType.Quest);
                int wrongCount = 0;
                
                foreach (var slot in bugItems)
                {
                    if (slot.item != null)
                    {
                        string bugKey = ExtractBugKeyFromItemName(slot.item.itemName);
                        if (!string.IsNullOrEmpty(bugKey) && !targetSet.Contains(bugKey))
                        {
                            wrongCount += slot.quantity;
                        }
                    }
                }
                
                return wrongCount;
            }
            return 0;
        }

        private string ExtractBugKeyFromItemName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            
            // Нормализация имени предмета для извлечения ключа жука
            string normalized = itemName.Replace("(Clone)", "").Replace("_Variant", "").Trim();
            return BugList.NormalizeKey(normalized);
        }
    }
}
