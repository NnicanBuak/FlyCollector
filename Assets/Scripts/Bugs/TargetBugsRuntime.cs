// TargetBugsRuntime.cs
// Синглтон с целевыми жуками, живёт между сценами (DontDestroyOnLoad).
using System.Collections.Generic;
using UnityEngine;

public class TargetBugsRuntime : MonoBehaviour
{
    public static TargetBugsRuntime Instance { get; private set; }

    [Tooltip("Список целевых жуков (файловые имена: FLY, FLY.001, ...)")]
    [SerializeField] private List<string> targetBugFileNames = new();

    public IReadOnlyList<string> Targets => targetBugFileNames;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetTargets(IEnumerable<string> list)
    {
        targetBugFileNames = new List<string>();
        if (list == null) return;
        foreach (var s in list)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            var key = NormalizeKey(s);
            if (!targetBugFileNames.Contains(key)) targetBugFileNames.Add(key);
        }
    }

    // Удобно подписать прямо событие BugList.OnTargetBugsSelected в Инспекторе
    public void HandleTargetsSelected(System.Collections.Generic.List<string> list) => SetTargets(list);

    public bool IsTarget(string bugFileName)
    {
        if (string.IsNullOrWhiteSpace(bugFileName)) return false;
        var key = NormalizeKey(bugFileName);
        return targetBugFileNames.Contains(key);
    }

    public static string NormalizeKey(string raw)
    {
        raw = (raw ?? "").Trim();
        int dotIdx = raw.IndexOf('.');
        if (dotIdx >= 0 && dotIdx + 1 < raw.Length && char.IsWhiteSpace(raw[dotIdx + 1]))
            raw = raw.Substring(dotIdx + 2); // "1. FLY (..)" -> "FLY (..)"
        int cut = raw.IndexOfAny(new[] { ' ', '(' });
        return (cut >= 0 ? raw.Substring(0, cut) : raw).Trim();
    }
}