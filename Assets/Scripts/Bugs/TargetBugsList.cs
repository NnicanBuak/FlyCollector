// TargetBugsList.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class TargetBugsList : MonoBehaviour
{
    [Header("Источник данных")]
    [Tooltip("Перетащи сюда Assets/BUGS.txt")]
    [SerializeField] private TextAsset bugsTxt;

    [Header("Вывод")]
    [SerializeField] private TextMeshProUGUI leftText;
    [SerializeField] private TextMeshProUGUI rightText;

    private Dictionary<string, (string name, string desc)> index;

    private void Awake()
    {
        if (!bugsTxt)
        {
            Debug.LogError("[TargetBugsList] Не задан BUGS.txt");
            return;
        }
        index = Parse(bugsTxt.text);
    }

    // Подпиши на BugList.OnTargetBugsSelected
    public void HandleTargetsSelected(List<string> targetBugFileNames)
    {
        if (leftText) leftText.text = "";
        if (rightText) rightText.text = "";

        if (targetBugFileNames == null || targetBugFileNames.Count == 0) return;

        var lines = new List<string>(targetBugFileNames.Count * 2);
        foreach (var id in targetBugFileNames)
        {
            var key = NormalizeKey(id);
            if (index != null && index.TryGetValue(key, out var data))
                lines.Add($"{data.name}\n{data.desc}\n");
            else
                lines.Add($"{id}\n(no description)\n");
        }

        int half = lines.Count / 2 + (lines.Count % 2);
        if (leftText)  leftText.text  = string.Join("\n", lines.Take(half));
        if (rightText) rightText.text = string.Join("\n", lines.Skip(half));
    }

    private static Dictionary<string, (string name, string desc)> Parse(string raw)
    {
        var dict = new Dictionary<string, (string name, string desc)>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return dict;

        var lines = raw
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        for (int i = 0; i + 2 < lines.Count; i += 3)
        {
            string fileLine = lines[i];
            string nameLine = lines[i + 1];
            string descLine = lines[i + 2];

            string key = NormalizeKey(fileLine);
            if (!dict.ContainsKey(key))
                dict.Add(key, (nameLine, descLine));
        }
        return dict;
    }

    private static string NormalizeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        int dotIdx = raw.IndexOf('.');
        if (dotIdx >= 0 && dotIdx + 1 < raw.Length && char.IsWhiteSpace(raw[dotIdx + 1]))
            raw = raw.Substring(dotIdx + 2); // "1. FLY (FLY.005)" -> "FLY (FLY.005)"

        int cut = raw.IndexOfAny(new[] { ' ', '(' });
        var key = cut >= 0 ? raw.Substring(0, cut) : raw;
        return key.Trim();
    }
}
