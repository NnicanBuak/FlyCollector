// BugItemRegistry.cs
// (обновлённый) реестр соответствий файл-жук <-> Item для InventoryManager + обратный поиск.
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BugItemRegistry", menuName = "Bugs/Bug Item Registry")]
public class BugItemRegistry : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string bugFileName; // "FLY.001"
        public Item item;          // ScriptableObject для инвентаря
    }

    [SerializeField] private List<Entry> entries = new();

    private Dictionary<string, Item> _byBug;
    private Dictionary<Item, string> _byItem;

    void OnEnable()
    {
        _byBug = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        _byItem = new Dictionary<Item, string>();
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.bugFileName) || e.item == null) continue;
            var key = TargetBugsRuntime.NormalizeKey(e.bugFileName);
            _byBug[key] = e.item;
            if (!_byItem.ContainsKey(e.item)) _byItem.Add(e.item, key);
        }
    }

    public bool TryGetItem(string bugFileName, out Item item)
    {
        if (_byBug == null) OnEnable();
        return _byBug.TryGetValue(TargetBugsRuntime.NormalizeKey(bugFileName), out item);
    }

    public bool TryGetBugFile(Item item, out string bugFileName)
    {
        if (_byItem == null) OnEnable();
        return _byItem.TryGetValue(item, out bugFileName);
    }

    public IEnumerable<string> AllBugFileNames()
    {
        if (_byBug == null) OnEnable();
        return _byBug.Keys;
    }
}