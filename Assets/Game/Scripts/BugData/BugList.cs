using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Scripts.BugData
{
    public class BugList : MonoBehaviour
    {
        [Serializable] public class StringListEvent : UnityEvent<List<string>> {}

        public static BugList Instance { get; private set; }
        public static event Action<BugList> InstanceChanged;
        public event Action TargetsChanged;
        public event Action CollectedChanged;
        public event Action BugsToSpawnChanged;
        
        // Добавляем событие готовности
        public static event Action OnBugListReady;
        public bool IsReady { get; private set; }

        [Header("Localization")]
        [SerializeField] private string localizationTableCollectionName = "bugs";
        [SerializeField] private string titleSuffix = ".name";
        [SerializeField] private string descSuffix = ".description";

#if UNITY_EDITOR
        [Header("Папка с префабами (опц. проверка в Editor)")]
        [SerializeField] private string assetsSubPath = "Game/Prefabs/Bugs";
        [SerializeField] private bool validateIdsAgainstFolder = true;
#endif

        [Header("Выбор жуков")]
        [SerializeField, Min(1)] private int totalBugsToSpawn = 10;
        [Tooltip("Сколько из заспавненных жуков будут целевыми (для ловли)")]
        [SerializeField, Min(1)] private int targetCount = 6;
        [SerializeField] private bool chooseOnAwake = true;

        [Header("События")]
        public StringListEvent OnTargetsSelected;
        public StringListEvent OnBugsToSpawnSelected;

        // Публичные свойства для совместимости с TargetBugList
        public List<string> BugsToSpawn { get; private set; } = new List<string>();
        public List<string> Targets { get; private set; } = new List<string>();

        private readonly List<string> _allBugKeys = new List<string>();
        private readonly Dictionary<string, (string title, string desc)> _meta = new Dictionary<string, (string title, string desc)>();
        private readonly HashSet<string> _collectedBugs = new HashSet<string>();

        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            InstanceChanged?.Invoke(this);

            // Подписываемся на изменения инвентаря
            SubscribeToInventory();

            // Подписываемся на смену локали
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            // Загружаем из локализации (это запустит ChooseBugsAndTargets после загрузки)
            LoadListFromLocalization();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                InstanceChanged?.Invoke(null);
            }
            
            // Отписываемся от событий инвентаря
            UnsubscribeFromInventory();
            
            // Отписываемся от смены локали
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnEnable()
        {
            RebuildDisplay();
        }


        public void LoadListFromLocalization()
        {
            _allBugKeys.Clear();
            _meta.Clear();

            if (string.IsNullOrWhiteSpace(localizationTableCollectionName))
            {
                Debug.LogWarning("[BugList] Имя таблицы локализации не задано.");
                return;
            }

            var handle = LocalizationSettings.StringDatabase.GetTableAsync(localizationTableCollectionName);
            if (handle.IsDone)
            {
                ProcessStringTable(handle.Result);
            }
            else
            {
                handle.Completed += op => ProcessStringTable(op.Result);
            }
        }

        private void ProcessStringTable(StringTable table)
        {
            if (table == null)
            {
                Debug.LogWarning($"[BugList] Таблица локализации не найдена: {localizationTableCollectionName}");
                return;
            }

            var titleSuf = titleSuffix?.ToUpperInvariant() ?? string.Empty;
            var descSuf = descSuffix?.ToUpperInvariant() ?? string.Empty;
            var seenIds = new HashSet<string>();

            foreach (var entry in table.Values)
            {
                string key = (entry.Key ?? string.Empty).ToUpperInvariant();
                string value = entry.Value ?? string.Empty;

                // Обрабатываем только записи с суффиксами title
                if (!string.IsNullOrEmpty(titleSuf) && key.EndsWith(titleSuf))
                {
                    string id = key.Substring(0, key.Length - titleSuf.Length);
                    id = NormalizeKey(id);
                    if (string.IsNullOrEmpty(id)) continue;
                    
                    // Добавляем ID в с��исок только один раз
                    seenIds.Add(id);
                    
                    // Обновляем метаданные
                    if (!_meta.ContainsKey(id))
                    {
                        _meta[id] = (title: value, desc: "(no description)");
                    }
                    else
                    {
                        _meta[id] = (title: value, desc: _meta[id].desc);
                    }
                    continue;
                }

                // Обрабатываем записи с суффиксами description
                if (!string.IsNullOrEmpty(descSuf) && key.EndsWith(descSuf))
                {
                    string id = key.Substring(0, key.Length - descSuf.Length);
                    id = NormalizeKey(id);
                    if (string.IsNullOrEmpty(id)) continue;
                    
                    // Обновляем только описание, не добавляем в список повторно
                    if (!_meta.ContainsKey(id))
                    {
                        _meta[id] = (title: id, desc: value);
                    }
                    else
                    {
                        _meta[id] = (title: _meta[id].title, desc: value);
                    }
                    continue;
                }
            }

            // Добавляем только уникальные ID в список
            _allBugKeys.Clear();
            _allBugKeys.AddRange(seenIds.OrderBy(s => s));
            
            Debug.Log($"[BugList] ✓ Загружено {_allBugKeys.Count} уникальных ID жуков из локализации ({localizationTableCollectionName})");
            Debug.Log($"[BugList] Примеры ID: {string.Join(", ", _allBugKeys.Take(10).ToArray())}");

            RebuildDisplay();
            
            // Выбираем жук��в для спавна после загрузки списка
            if (chooseOnAwake && BugsToSpawn.Count == 0)
            {
                ChooseBugsAndTargets();
            }

            // Вызываем со��ытие готовности после загрузки списка
            IsReady = true;
            OnBugListReady?.Invoke();
        }


        public void ChooseBugsAndTargets()
        {
            if (_allBugKeys.Count == 0) 
            {
                LoadListFromLocalization();
            }

            int spawnCount = Mathf.Min(totalBugsToSpawn, _allBugKeys.Count * 3);
            var bugsToSpawn = new List<string>();

            // Генерируем список жуков для спавна
            while (bugsToSpawn.Count < spawnCount)
            {
                var shuffled = _allBugKeys.OrderBy(_ => UnityEngine.Random.value).ToList();
                int needed = spawnCount - bugsToSpawn.Count;
                bugsToSpawn.AddRange(shuffled.Take(needed));
            }

            // Выбираем целевых жуков из уникальных
            int targetCountClamped = Mathf.Min(targetCount, _allBugKeys.Count);
            var uniqueBugs = bugsToSpawn.Distinct().ToList();
            var targets = uniqueBugs.OrderBy(_ => UnityEngine.Random.value).Take(targetCountClamped).ToList();

            // Устанавливаем значения и выз��ваем события
            SetBugsToSpawn(bugsToSpawn);
            SetTargets(targets);

            // Вызываем UnityEvents
            OnBugsToSpawnSelected?.Invoke(bugsToSpawn);
            OnTargetsSelected?.Invoke(targets);

            Debug.Log($"[BugList] Жуков для спавна: {bugsToSpawn.Count} (уникальных: {uniqueBugs.Count})");
            Debug.Log($"[BugList] Целевых жуков: {targets.Count} ({string.Join(", ", targets)})");
        }

        // Методы для совместимости с TargetBugList API
        public void SetBugsToSpawn(List<string> keys)
        {
            BugsToSpawn = (keys ?? new List<string>()).Select(NormalizeKey).ToList();
            BugsToSpawnChanged?.Invoke();
        }

        public void SetTargets(List<string> keys)
        {
            Targets = (keys ?? new List<string>()).Select(NormalizeKey).Distinct().ToList();
            TargetsChanged?.Invoke();
            RebuildDisplay();
        }

        public void SetMeta(Dictionary<string, (string title, string desc)> meta)
        {
            _meta.Clear();
            if (meta != null)
            {
                foreach (var kv in meta)
                    _meta[NormalizeKey(kv.Key)] = kv.Value;
            }
            RebuildDisplay();
        }

        public bool TryGetMeta(string id, out string title, out string desc)
        {
            var key = NormalizeKey(id);
            if (_meta.TryGetValue(key, out var t))
            {
                title = t.title; 
                desc = t.desc; 
                return true;
            }
            title = null; 
            desc = null; 
            return false;
        }

        public bool IsTarget(string id)
        {
            var key = NormalizeKey(id);
            return Targets != null && Targets.Contains(key);
        }

        public static string NormalizeKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string s = raw.Trim();
            int cut = s.IndexOfAny(new[] { ' ', '(' });
            if (cut >= 0) s = s.Substring(0, cut);
            s = s.ToUpperInvariant();
            return s;
        }

        private void RebuildDisplay()
        {
            // Уведомляем подписчиков (например, BugListTMP) об изменениях
            CollectedChanged?.Invoke();
        }

#if UNITY_EDITOR
        private void ValidateAgainstFolder(List<string> keys)
        {
            if (string.IsNullOrWhiteSpace(assetsSubPath)) return;

            string folderPath = assetsSubPath.StartsWith("Assets/")
                ? assetsSubPath
                : "Assets/" + assetsSubPath.TrimStart('/');

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[BugList] Папка не найдена: {folderPath}");
                return;
            }

            var guids = AssetDatabase.FindAssets("", new[] { folderPath });
            var existing = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path.EndsWith(".meta")) continue;

                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (!obj) continue;

                existing.Add(NormalizeKey(obj.name));
            }

            var missing = keys.Where(k => !existing.Contains(k)).ToList();
            if (missing.Count > 0)
            {
                Debug.LogWarning($"[BugList] Отсутствуют префабы для следующих id в {folderPath}:\n - " +
                                 string.Join("\n - ", missing.ToArray()));
            }
        }
#endif

        private void SubscribeToInventory()
        {
            // Подписывае��ся с задержкой, если инвентарь еще не создан
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged.AddListener(OnInventoryChanged);
                UpdateCollectedBugs();
            }
            else
            {
                StartCoroutine(WaitForInventory());
            }
        }

        private void UnsubscribeFromInventory()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged.RemoveListener(OnInventoryChanged);
            }
        }

        private System.Collections.IEnumerator WaitForInventory()
        {
            // Ждем пока инвентарь не будет создан
            while (InventoryManager.Instance == null)
            {
                yield return null;
            }
            
            InventoryManager.Instance.OnInventoryChanged.AddListener(OnInventoryChanged);
            UpdateCollectedBugs();
        }

        private void OnInventoryChanged()
        {
            UpdateCollectedBugs();
            CollectedChanged?.Invoke();
        }

        private void UpdateCollectedBugs()
        {
            _collectedBugs.Clear();
            
            if (InventoryManager.Instance == null) return;

            // Учитываем только баг-айтемы (ItemType.Quest)
            var bugSlots = InventoryManager.Instance.GetItemsByType(ItemType.Quest);
            foreach (var slot in bugSlots)
            {
                if (slot.item != null && !string.IsNullOrEmpty(slot.item.itemID))
                {
                    string normalizedId = NormalizeKey(slot.item.itemID);
                    _collectedBugs.Add(normalizedId);
                }
            }
            
            Debug.Log($"[BugList] Собрано жуков в инвентаре: {_collectedBugs.Count}");
        }

        public bool IsCollected(string id)
        {
            var key = NormalizeKey(id);
            return _collectedBugs.Contains(key);
        }

        private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            Debug.Log($"[BugList] Локаль изменена на: {locale.Identifier.Code}");
            
            // Перезагружаем данные из новой локали
            LoadListFromLocalization();
            
            // Перестраиваем отображение
            RebuildDisplay();
        }
    }
}
