using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CaughtBugsRuntime : MonoBehaviour
{
    public static CaughtBugsRuntime Instance { get; private set; }
    public static event Action<CaughtBugsRuntime> InstanceChanged;

    [SerializeField] private List<string> caught = new();
    public IReadOnlyList<string> Caught => caught;

    public event Action OnCaughtChanged;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InstanceChanged?.Invoke(this);
    }

    public void RegisterCaught(string bugFileName)
    {
        if (string.IsNullOrWhiteSpace(bugFileName)) return;
        var key = BugKeyUtil.CanonicalizeKey(bugFileName);
        caught.Add(key);
        OnCaughtChanged?.Invoke();
    }

    public void ClearAll()
    {
        if (caught.Count == 0) return;
        caught.Clear();
        OnCaughtChanged?.Invoke();
    }

    public void GetStats(out int total, out int correct, out int wrong)
    {
        total = caught.Count;
        correct = 0;
        wrong = 0;

        if (total == 0)
            return;

        var targets = TargetBugsRuntime.Instance?.Targets;
        if (targets != null && targets.Count > 0)
        {
            var targetSet = new HashSet<string>(targets);
            correct = caught.Count(id => targetSet.Contains(id));
            wrong = Mathf.Max(0, total - correct);
        }
        else
        {
            correct = total;
            wrong = 0;
        }
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
