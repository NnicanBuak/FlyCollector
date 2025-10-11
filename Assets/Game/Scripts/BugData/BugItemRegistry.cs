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
        #region Serialized Fields
        [Serializable]
        public class Entry
        {
            [Tooltip("Ключ жука (имя/ID). Можно писать как в prefab/GO: без учёта регистра, без расширения.")]
            public string key;

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

        [Tooltip("Префикс для создаваемых Items")]
        [SerializeField] private string itemNamePrefix = "";

        [Tooltip("Суффикс для создаваемых Items (например: _Item)")]
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

        void OnEnable()
        {
            _map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key)) continue;
                _map[Normalize(e.key)] = e;
            }
        }
        #endregion

        #region Public Methods
        public bool TryGet(string rawKey, out Entry entry)
        {
            if (_map == null) OnEnable();
            return _map.TryGetValue(Normalize(rawKey), out entry);
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


            int dot = s.LastIndexOf('.');
            if (dot > 0) s = s.Substring(0, dot);

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
            var createdItems = new List<(string key, Item item)>();
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


                string itemAssetName = itemNamePrefix + bugName + itemNameSuffix;
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
                    item.itemID = bugName.ToLower();
                    item.itemName = bugName;
                    item.itemType = ItemType.Quest;
                    item.maxStackSize = 1;


                    AssetDatabase.CreateAsset(item, itemPath);
                    createdCount++;
                    Debug.Log($"[BugItemRegistry] Created Item: {itemPath}");
                }


                createdItems.Add((bugName, item));
            }

            if (createdItems.Count == 0)
            {
                Debug.LogWarning($"[BugItemRegistry] No bug prefabs found in {prefabsPath}");
                return;
            }


            entries.Clear();
            foreach (var (key, item) in createdItems.OrderBy(x => x.key))
            {
                entries.Add(new Entry
                {
                    key = key,
                    item = item,
                    defaultQuantity = 1
                });
            }


            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BugItemRegistry] ✅ Complete! Created: {createdCount} new Items, Reused: {createdItems.Count - createdCount}, Skipped: {skippedCount} non-bug prefabs");
        }
        #endregion
#endif
    }
}
