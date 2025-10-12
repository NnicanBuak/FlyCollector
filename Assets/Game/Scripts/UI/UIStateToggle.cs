using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls visibility of UI objects by named states.
/// Each list entry defines: State Name + Target GameObject + Show flag.
/// </summary>
public class UIStateToggle : MonoBehaviour
{
    [Serializable]
    public class StateEntry
    {
        public string StateName;
        public GameObject Target;
        public bool Show;
    }

    [Header("States")]
    [Tooltip("One entry per state: Name + Target + Show flag.")]
    public List<StateEntry> States = new List<StateEntry>();

    // Lookup from normalized state name to entries
    private readonly Dictionary<string, List<StateEntry>> _lookup = new();

    private void Awake()
    {
        RebuildLookup();
        ApplyStateVisibility();
    }

    private void OnValidate()
    {
        RebuildLookup();
        ApplyStateVisibility();
    }

    // Public API
    public void SetState(string stateName, bool value)
    {
        var key = Normalize(stateName);
        if (string.IsNullOrEmpty(key)) return;
        if (!_lookup.TryGetValue(key, out var entries)) return;

        foreach (var e in entries)
            e.Show = value;

        ApplyStateVisibility();
    }

    public void SetExclusive(string stateName)
    {
        var key = Normalize(stateName);
        if (string.IsNullOrEmpty(key)) return;

        bool anyMatched = false;
        foreach (var e in States)
        {
            bool match = Normalize(e.StateName) == key;
            if (match) anyMatched = true;
        }

        if (!anyMatched) return; // do not blank everything if name doesn't exist

        foreach (var e in States)
        {
            bool match = Normalize(e.StateName) == key;
            e.Show = match;
        }

        ApplyStateVisibility();
    }

    public void ApplyStateVisibility()
    {
        foreach (var e in States)
        {
            if (e.Target == null) continue;
            if (e.Target.activeSelf != e.Show)
                e.Target.SetActive(e.Show);
        }
    }

    private void RebuildLookup()
    {
        _lookup.Clear();
        foreach (var e in States)
        {
            var key = Normalize(e.StateName);
            if (string.IsNullOrEmpty(key))
                continue;

            if (!_lookup.TryGetValue(key, out var list))
            {
                list = new List<StateEntry>();
                _lookup.Add(key, list);
            }
            if (!list.Contains(e))
                list.Add(e);
        }
    }

    private static string Normalize(string state)
    {
        return string.IsNullOrWhiteSpace(state) ? string.Empty : state.Trim().ToLowerInvariant();
    }
}
