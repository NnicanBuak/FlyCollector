
using System.Collections.Generic;
using UnityEngine;

public class CaughtBugsRuntime : MonoBehaviour
{
    public static CaughtBugsRuntime Instance { get; private set; }

    [SerializeField] private List<string> caught = new();
    public IReadOnlyList<string> Caught => caught;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterCaught(string bugFileName)
    {
        if (string.IsNullOrWhiteSpace(bugFileName)) return;
        var key = TargetBugsRuntime.NormalizeKey(bugFileName);
        caught.Add(key);

    }

    public void ClearAll() => caught.Clear();
}