using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BugList : MonoBehaviour
{
    [System.Serializable] public class StringListEvent : UnityEvent<List<string>> {}

    [Header("Источник списка (тройками строк: id / title / description)")]
    [SerializeField] private TextAsset listText;

#if UNITY_EDITOR
    [Header("Папка с префабами (опц. проверка в Editor)")]
    [SerializeField] private string assetsSubPath = "Prefabs/Bugs";
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


    private readonly List<string> allBugKeys = new List<string>();

    private void Awake()
    {
        LoadListFromText();
        if (chooseOnAwake) ChooseBugsAndTargets();
    }


    public void LoadListFromText()
    {
        allBugKeys.Clear();

        if (listText == null || string.IsNullOrWhiteSpace(listText.text))
        {
            Debug.LogWarning("[BugList] Текстовый список не задан или пуст.");

            EnsureRuntime();
            TargetBugsRuntime.Instance.SetMeta(null);
            return;
        }

        var lines = listText.text
            .Split('\n')
            .Select(l => l.Trim('\r', ' ', '\t'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var meta = new Dictionary<string, (string title, string desc)>();


        for (int i = 0; i + 2 < lines.Count; i += 3)
        {
            string rawId = ExtractId(lines[i]);
            if (string.IsNullOrEmpty(rawId)) continue;
            string key   = TargetBugsRuntime.NormalizeKey(rawId);
            string title = lines[i + 1];
            string desc  = lines[i + 2];

            allBugKeys.Add(key);
            if (!meta.ContainsKey(key)) meta[key] = (title, desc);
        }


        for (int i = 0; i < allBugKeys.Count; i++) allBugKeys[i] = TargetBugsRuntime.NormalizeKey(allBugKeys[i]);
        var distinct = allBugKeys.Distinct().OrderBy(s => s).ToList();
        allBugKeys.Clear(); allBugKeys.AddRange(distinct);

        Debug.Log($"[BugList] В списке найдено id: {allBugKeys.Count} (пример: {string.Join(", ", allBugKeys.Take(5).ToArray())}...)");


        EnsureRuntime();
        TargetBugsRuntime.Instance.SetMeta(meta);

#if UNITY_EDITOR
        if (validateIdsAgainstFolder) ValidateAgainstFolder(allBugKeys);
#endif
    }


    public void ChooseBugsAndTargets()
    {
        if (allBugKeys.Count == 0) LoadListFromText();

        EnsureRuntime();


        int spawnCount = Mathf.Min(totalBugsToSpawn, allBugKeys.Count * 3);
        var bugsToSpawn = new List<string>();


        while (bugsToSpawn.Count < spawnCount)
        {
            var shuffled = allBugKeys.OrderBy(_ => Random.value).ToList();
            int needed = spawnCount - bugsToSpawn.Count;
            bugsToSpawn.AddRange(shuffled.Take(needed));
        }


        int targetCountClamped = Mathf.Min(targetCount, allBugKeys.Count);
        var uniqueBugs = bugsToSpawn.Distinct().ToList();
        var targets = uniqueBugs.OrderBy(_ => Random.value).Take(targetCountClamped).ToList();


        TargetBugsRuntime.Instance.SetBugsToSpawn(bugsToSpawn);
        TargetBugsRuntime.Instance.SetTargets(targets);


        OnBugsToSpawnSelected?.Invoke(bugsToSpawn);
        OnTargetsSelected?.Invoke(targets);

        Debug.Log($"[BugList] Жуков для спавна: {bugsToSpawn.Count} (уникальных: {uniqueBugs.Count})");
        Debug.Log($"[BugList] Целевых жуков: {targets.Count} ({string.Join(", ", targets)})");
    }


    [System.Obsolete("Используйте ChooseBugsAndTargets() вместо этого")]
    public void ChooseTargetsAndPublish()
    {
        ChooseBugsAndTargets();
    }



    private static string ExtractId(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        while (i < line.Length && char.IsDigit(line[i])) i++;
        if (i < line.Length && line[i] == '.') i++;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

        int j = i;
        while (j < line.Length && !char.IsWhiteSpace(line[j]) && line[j] != '(') j++;

        var id = line.Substring(i, j - i).Trim();
        return string.IsNullOrEmpty(id) ? null : id;
    }

    private static void EnsureRuntime()
    {
        if (TargetBugsRuntime.Instance == null)
            new GameObject("TargetBugsRuntime").AddComponent<TargetBugsRuntime>();
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

            existing.Add(TargetBugsRuntime.NormalizeKey(obj.name));
        }

        var missing = keys.Where(k => !existing.Contains(k)).ToList();
        if (missing.Count > 0)
        {
            Debug.LogWarning($"[BugList] Отсутствуют префабы для следующих id в {folderPath}:\n - " +
                             string.Join("\n - ", missing.ToArray()));
        }
    }
#endif
}
