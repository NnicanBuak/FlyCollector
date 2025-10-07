// BugList.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class BugList : MonoBehaviour
{
    [System.Serializable]
    public class StringListEvent : UnityEvent<List<string>> {}

    [Header("Спавнеры")]
    [SerializeField] private BugSpawner[] spawners;

    [Header("Количество")]
    [SerializeField, Min(1)] private int poolCount = 50;
    [SerializeField, Min(1)] private int targetCount = 16;

    [Header("Полный список имён (файловых)")]
    [SerializeField] private List<string> allBugFileNames = new();

    [Header("События")]
    public StringListEvent OnTargetBugsSelected;

    [ContextMenu("Build & Spawn")]
    public void BuildAndSpawn()
    {
        if (spawners == null || spawners.Length == 0)
        {
            Debug.LogError("[BugList] Нет спавнеров");
            return;
        }
        if (allBugFileNames == null || allBugFileNames.Count == 0)
        {
            Debug.LogError("[BugList] Нет исходного списка имён жуков");
            return;
        }

        var chosen = allBugFileNames
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .OrderBy(_ => Random.value)
            .Take(poolCount)
            .ToList();

        var spawned = new List<GameObject>(chosen.Count);
        for (int i = 0; i < chosen.Count; i++)
        {
            var sp = spawners[i % spawners.Length];
            var go = sp.SpawnBug(chosen[i]);
            if (go) spawned.Add(go);
        }

        var targets = spawned
            .OrderBy(_ => Random.value)
            .Take(targetCount)
            .Select(go =>
            {
                var m = go.GetComponent<BugMeta>();
                return m ? m.FileName : go.name;
            })
            .ToList();

        OnTargetBugsSelected?.Invoke(targets);
    }

    public void SetAllBugNames(IEnumerable<string> names)
    {
        allBugFileNames = names?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .ToList() ?? new List<string>();
    }

    public IReadOnlyList<string> GetAllBugNames() => allBugFileNames;
}
