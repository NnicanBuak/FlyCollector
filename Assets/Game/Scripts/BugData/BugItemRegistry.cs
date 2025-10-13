using System;
using System.Collections.Generic;
using UnityEngine;
using Bug;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

namespace BugData
{
    [CreateAssetMenu(menuName = "Bugs/Bug Item Registry", fileName = "BugItemRegistry")]
    public class BugItemRegistry : ScriptableObject
    {
        private const string DefaultResourcePath = "BugItemRegistry";
        private static BugItemRegistry _instance;

        #region Serialized Fields
        [Serializable]
        public class Entry
        {
            [Tooltip("Ключ жука (имя/ID). Можно писать как в prefab/GO: без учёта регистра, без расширения.")]
            public string key;

            [Tooltip("Original prefab name used as a lookup fallback. Auto-populated when generating entries.")]
            public string prefabKey;

            [Header("Инвентарь")]
            public Item item;
            public int defaultQuantity = 1;

            [Header("Эффекты (опц.)")]
            public ParticleSystem pickupEffectPrefab;
            public AudioClip pickupSound;
        }

#if UNITY_EDITOR
        [Header("Auto-populate Settings (Editor only)")]
        [Tooltip("Папка с префабами жуков (например: Assets/Prefabs/Bugs)")]
        [SerializeField] private string bugPrefabsPath = "Assets/Prefabs/Bugs";

        [Tooltip("Папка где создавать Item ScriptableObjects (например: Assets/Items)")]
        [SerializeField] private string outputItemsPath = "Assets/Items";

        [Tooltip("Optional prefix to match and trim from prefab names during auto-populate. Leave empty to disable.")]
        [SerializeField] private string itemNamePrefix = "";

        [Tooltip("Optional suffix to match and trim from prefab names during auto-populate. Leave empty to disable.")]
        [SerializeField] private string itemNameSuffix = "";

        [Space(10)]
#endif

        [SerializeField] private List<Entry> entries = new();
        #endregion

        #region Properties
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        private Dictionary<string, Entry> _map;
        private Dictionary<Item, string> _itemToKey;

        void OnEnable()
        {
            _instance = this;
            RebuildMap();
        }

        private void RebuildMap()
        {
            _map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            _itemToKey = new Dictionary<Item, string>();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key)) continue;

                string normalizedKey = Normalize(e.key);
                if (!string.IsNullOrEmpty(normalizedKey))
                {
                    _map[normalizedKey] = e;
                }

                if (!string.IsNullOrWhiteSpace(e.prefabKey))
                {
                    string normalizedPrefabKey = Normalize(e.prefabKey);
                    if (!string.IsNullOrEmpty(normalizedPrefabKey))
                    {
                        _map[normalizedPrefabKey] = e;
                    }
                }

                if (e.item != null)
                {
                    string canonical = BugKeyUtil.CanonicalizeKey(e.key);
                    if (string.IsNullOrEmpty(canonical))
                        canonical = TargetBugsRuntime.NormalizeKey(e.key);
                    if (!string.IsNullOrEmpty(canonical))
                        _itemToKey[e.item] = canonical;
                }
            }
        }

        private void EnsureInitialized()
        {
            if (_map == null)
                RebuildMap();
        }
        #endregion

        #region Public Methods
        public static BugItemRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    var loaded = Resources.FindObjectsOfTypeAll<BugItemRegistry>();
                    if (loaded != null && loaded.Length > 0)
                        _instance = loaded[0];
                    if (_instance == null)
                    {
                        _instance = Resources.Load<BugItemRegistry>(DefaultResourcePath);
                        if (_instance == null)
                        {
                            Debug.LogWarning($"[BugItemRegistry] Unable to locate registry at Resources/{DefaultResourcePath}");
                            return null;
                        }
                    }
                }

                _instance.EnsureInitialized();
                return _instance;
            }
        }

        public static bool TryGetInstance(out BugItemRegistry registry)
        {
            registry = Instance;
            return registry != null;
        }

        public bool TryGet(string rawKey, out Entry entry)
        {
            EnsureInitialized();
            return _map.TryGetValue(Normalize(rawKey), out entry);
        }

        public bool TryGetKey(Item item, out string key)
        {
            key = string.Empty;
            EnsureInitialized();
            if (item == null || _itemToKey == null) return false;
            if (_itemToKey.TryGetValue(item, out var stored) && !string.IsNullOrEmpty(stored))
            {
                key = stored;
                return true;
            }
            return false;
        }

        public bool TryGetItem(string rawKey, out Item item)
        {
            item = null;
            if (!TryGet(rawKey, out var e) || e.item == null) return false;
            item = e.item;
            return true;
        }

        public bool TryGetItem(string rawKey, out Item item, out int quantity)
        {
            item = null; quantity = 0;
            if (!TryGet(rawKey, out var e) || e.item == null) return false;
            item = e.item; quantity = Mathf.Max(1, e.defaultQuantity);
            return true;
        }
        #endregion

        #region Private Methods
        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            s = s.Trim().ToLowerInvariant();
            s = s.Replace("(clone)", "").Trim();
            s = s.Replace('\\', '/');

            int slash = s.LastIndexOf('/');
            if (slash >= 0 && slash < s.Length - 1)
            {
                s = s.Substring(slash + 1);
            }

            if (s.EndsWith(".prefab") || s.EndsWith(".asset"))
            {
                int dot = s.LastIndexOf('.');
                if (dot > 0) s = s.Substring(0, dot);
            }

            string suffix = string.Empty;
            int underscore = s.IndexOf('_');
            if (underscore >= 0)
            {
                suffix = s.Substring(underscore);
                s = s.Substring(0, underscore);
            }

            string canonical = BugKeyUtil.CanonicalizeKey(s);
            if (!string.IsNullOrEmpty(canonical))
            {
                s = canonical.ToLowerInvariant();
            }

            if (!string.IsNullOrEmpty(suffix))
                s += suffix;

            return s;
        }
        #endregion

        #region Gizmos
        #endregion

#if UNITY_EDITOR
        #region Editor Methods
        public void AutoPopulateFromFolder()
        {
            if (string.IsNullOrWhiteSpace(bugPrefabsPath))
            {
                Debug.LogError("[BugItemRegistry] Bug prefabs path not specified!");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputItemsPath))
            {
                Debug.LogError("[BugItemRegistry] Output items path not specified!");
                return;
            }


            string prefabsPath = bugPrefabsPath.StartsWith("Assets/")
                ? bugPrefabsPath
                : "Assets/" + bugPrefabsPath.TrimStart('/');

            string itemsPath = outputItemsPath.StartsWith("Assets/")
                ? outputItemsPath
                : "Assets/" + outputItemsPath.TrimStart('/');

            if (!AssetDatabase.IsValidFolder(prefabsPath))
            {
                Debug.LogError($"[BugItemRegistry] Bug prefabs folder not found: {prefabsPath}");
                return;
            }


            if (!AssetDatabase.IsValidFolder(itemsPath))
            {
                string parentFolder = System.IO.Path.GetDirectoryName(itemsPath).Replace('\\', '/');
                string folderName = System.IO.Path.GetFileName(itemsPath);
                AssetDatabase.CreateFolder(parentFolder, folderName);
                Debug.Log($"[BugItemRegistry] Created folder: {itemsPath}");
            }


            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { prefabsPath });
            var createdItems = new List<(string cleanName, string prefabName, Item item)>();
            int createdCount = 0;
            int skippedCount = 0;

            foreach (var guid in guids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null) continue;


                bool hasBugAI = prefab.GetComponent<BugAI>() != null;

                if (!hasBugAI)
                {
                    skippedCount++;
                    continue;
                }

                string bugName = prefab.name;

                bool prefixSpecified = !string.IsNullOrWhiteSpace(itemNamePrefix);
                bool suffixSpecified = !string.IsNullOrWhiteSpace(itemNameSuffix);

                if (prefixSpecified && !bugName.StartsWith(itemNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    skippedCount++;
                    continue;
                }

                if (suffixSpecified && !bugName.EndsWith(itemNameSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    skippedCount++;
                    continue;
                }

                string cleanName = bugName;

                if (prefixSpecified && cleanName.StartsWith(itemNamePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleanName = cleanName.Substring(itemNamePrefix.Length);
                }

                if (suffixSpecified && cleanName.EndsWith(itemNameSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    cleanName = cleanName.Substring(0, cleanName.Length - itemNameSuffix.Length);
                }

                cleanName = cleanName.Trim();

                if (string.IsNullOrEmpty(cleanName))
                {
                    Debug.LogWarning($"[BugItemRegistry] Clean bug name became empty after trimming for prefab '{bugName}'. Skipping.");
                    skippedCount++;
                    continue;
                }

                if (createdItems.Any(x => string.Equals(x.cleanName, cleanName, StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.LogWarning($"[BugItemRegistry] Duplicate clean bug name '{cleanName}' detected (prefab '{bugName}'). Skipping duplicate.");
                    skippedCount++;
                    continue;
                }

                string itemAssetName = cleanName;
                string itemPath = $"{itemsPath}/{itemAssetName}.asset";


                Item existingItem = AssetDatabase.LoadAssetAtPath<Item>(itemPath);
                Item item;

                if (existingItem != null)
                {
                    Debug.Log($"[BugItemRegistry] Item already exists, reusing: {itemAssetName}");
                    item = existingItem;
                }
                else
                {

                    item = ScriptableObject.CreateInstance<Item>();
                    item.itemID = cleanName.ToLowerInvariant();
                    item.itemName = cleanName;
                    item.itemType = ItemType.Quest;
                    item.maxStackSize = 1;


                    AssetDatabase.CreateAsset(item, itemPath);
                    createdCount++;
                    Debug.Log($"[BugItemRegistry] Created Item: {itemPath}");
                }


                item.itemID = cleanName.ToLowerInvariant();
                item.itemName = cleanName;
                EditorUtility.SetDirty(item);

                createdItems.Add((cleanName, bugName, item));
            }

            if (createdItems.Count == 0)
            {
                Debug.LogWarning($"[BugItemRegistry] No bug prefabs found in {prefabsPath}");
                return;
            }

            entries.Clear();
            foreach (var data in createdItems.OrderBy(x => x.cleanName, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(new Entry
                {
                    key = data.cleanName,
                    prefabKey = data.prefabName,
                    item = data.item,
                    defaultQuantity = 1
                });
            }


            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            OnEnable(); // refresh lookup cache in editor so runtime has latest mappings

            Debug.Log($"[BugItemRegistry] ✅ Complete! Created: {createdCount} new Items, Reused: {createdItems.Count - createdCount}, Skipped: {skippedCount} filtered or non-bug prefabs");
        }
        #endregion
#endif
    }
}
