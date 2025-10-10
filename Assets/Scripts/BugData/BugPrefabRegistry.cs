using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

[CreateAssetMenu(menuName = "Bugs/Bug Prefab Registry", fileName = "BugPrefabRegistry")]
public class BugPrefabRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Ключ жука (имя/ID, например: FLY, BEETLE)")]
        public string key;

        [Tooltip("Префаб жука для спавна")]
        public GameObject prefab;
    }

#if UNITY_EDITOR
    [Header("Auto-populate Settings (Editor only)")]
    [Tooltip("Папка для поиска префабов (например: Assets/Prefabs/Bugs)")]
    [SerializeField] private string prefabFolderPath = "Assets/Prefabs/Bugs";

    [Tooltip("Суффикс имени префаба (например: _Variant)")]
    [SerializeField] private string prefabSuffix = "_Variant";

    [Tooltip("Префикс имени префаба (оставьте пустым если не используется)")]
    [SerializeField] private string prefabPrefix = "";

    [Space(10)]
#endif

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, GameObject> _map;

    void OnEnable()
    {
        _map = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.key) || e.prefab == null) continue;
            _map[Normalize(e.key)] = e.prefab;
        }
    }


    public bool TryGetPrefab(string rawKey, out GameObject prefab)
    {
        if (_map == null) OnEnable();
        return _map.TryGetValue(Normalize(rawKey), out prefab);
    }


    public GameObject GetPrefab(string rawKey)
    {
        TryGetPrefab(rawKey, out var prefab);
        return prefab;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Trim().ToLowerInvariant();


        s = s.Replace("(clone)", "").Trim();


        int dot = s.LastIndexOf('.');
        if (dot > 0) s = s.Substring(0, dot);

        return s;
    }

#if UNITY_EDITOR

    public void AutoPopulateFromFolder()
    {
        if (string.IsNullOrWhiteSpace(prefabFolderPath))
        {
            Debug.LogError("[BugPrefabRegistry] Путь к папке не указан!");
            return;
        }

        string folderPath = prefabFolderPath.StartsWith("Assets/")
            ? prefabFolderPath
            : "Assets/" + prefabFolderPath.TrimStart('/');

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"[BugPrefabRegistry] Папка не найдена: {folderPath}");
            return;
        }


        var guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
        var foundPrefabs = new List<(string key, GameObject prefab)>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null) continue;

            string prefabName = prefab.name;


            if (!string.IsNullOrEmpty(prefabPrefix) && !prefabName.StartsWith(prefabPrefix))
                continue;


            if (!string.IsNullOrEmpty(prefabSuffix) && !prefabName.EndsWith(prefabSuffix))
                continue;


            string key = prefabName;
            if (!string.IsNullOrEmpty(prefabPrefix))
                key = key.Substring(prefabPrefix.Length);
            if (!string.IsNullOrEmpty(prefabSuffix))
                key = key.Substring(0, key.Length - prefabSuffix.Length);

            foundPrefabs.Add((key, prefab));
        }

        if (foundPrefabs.Count == 0)
        {
            Debug.LogWarning($"[BugPrefabRegistry] Не найдено префабов с префиксом '{prefabPrefix}' и суффиксом '{prefabSuffix}' в {folderPath}");
            return;
        }


        entries.Clear();
        foreach (var (key, prefab) in foundPrefabs.OrderBy(x => x.key))
        {
            entries.Add(new Entry { key = key, prefab = prefab });
        }

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        Debug.Log($"[BugPrefabRegistry] Найдено и добавлено {foundPrefabs.Count} префабов из {folderPath}");
    }
#endif
}