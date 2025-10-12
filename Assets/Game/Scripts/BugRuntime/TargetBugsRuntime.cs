using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TargetBugsRuntime : MonoBehaviour
{
    public static TargetBugsRuntime Instance { get; private set; }
    public static event Action<TargetBugsRuntime> InstanceChanged;

    public event Action TargetsChanged;
    public event Action BugsToSpawnChanged;


    public List<string> BugsToSpawn { get; private set; } = new List<string>();


    public List<string> Targets { get; private set; } = new List<string>();


    private readonly Dictionary<string, (string title, string desc)> _meta =
        new Dictionary<string, (string title, string desc)>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InstanceChanged?.Invoke(this);
    }




    public void SetBugsToSpawn(List<string> keys)
    {
        BugsToSpawn = (keys ?? new List<string>()).Select(NormalizeKey).ToList();
        BugsToSpawnChanged?.Invoke();
    }


    public void SetTargets(List<string> keys)
    {
        Targets = (keys ?? new List<string>()).Select(NormalizeKey).Distinct().ToList();
        TargetsChanged?.Invoke();
    }

    public void SetMeta(Dictionary<string, (string title, string desc)> meta)
    {
        _meta.Clear();
        if (meta != null)
        {
            foreach (var kv in meta)
                _meta[NormalizeKey(kv.Key)] = kv.Value;
        }
        TargetsChanged?.Invoke();
    }

    public bool TryGetMeta(string id, out string title, out string desc)
    {
        var key = NormalizeKey(id);
        if (_meta.TryGetValue(key, out var t))
        {
            title = t.title; desc = t.desc; return true;
        }
        title = null; desc = null; return false;
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke(null);
        }
    }
}
