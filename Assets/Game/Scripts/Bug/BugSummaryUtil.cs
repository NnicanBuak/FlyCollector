using System;
using System.Collections.Generic;
using UnityEngine;
using BugData;

public static class BugSummaryUtil
{
    public struct Summary
    {
        public List<string> Targets;
        public List<string> CaughtKeys;
        public int TotalCaught;
        public int Correct;
        public int Wrong;
        public int Missing;
        public bool UsedInventory;

        public bool HasData => (Targets != null && Targets.Count > 0) ||
                               (CaughtKeys != null && CaughtKeys.Count > 0);
    }

    public static Summary Build(bool preferInventory = true)
    {
        var summary = new Summary
        {
            Targets = GetTargets(),
            CaughtKeys = new List<string>()
        };

        bool usedInventory = false;
        if (preferInventory && TryCollectFromInventory(summary.CaughtKeys))
        {
            usedInventory = true;
        }

        if (!usedInventory)
        {
            CollectFromRuntime(summary.CaughtKeys);
        }

        Compute(ref summary);
        summary.UsedInventory = usedInventory;
        return summary;
    }

    private static List<string> GetTargets()
    {
        if (TargetBugsRuntime.Instance?.Targets != null)
            return new List<string>(TargetBugsRuntime.Instance.Targets);
        return new List<string>();
    }

    private static bool TryCollectFromInventory(List<string> caught)
    {
        var inventory = InventoryManager.Instance;
        if (inventory == null) return false;
        if (!BugItemRegistry.TryGetInstance(out var registry) || registry == null) return false;

        bool anyFound = false;
        var slots = inventory.GetAllItems();
        foreach (var slot in slots)
        {
            if (slot == null || slot.item == null || slot.quantity <= 0) continue;
            if (!registry.TryGetKey(slot.item, out var key) || string.IsNullOrEmpty(key)) continue;

            anyFound = true;
            int repeats = Mathf.Max(1, slot.quantity);
            string canonical = Canonicalize(key);
            for (int i = 0; i < repeats; i++)
                caught.Add(canonical);
        }

        return anyFound;
    }

    private static void CollectFromRuntime(List<string> caught)
    {
        var runtime = CaughtBugsRuntime.Instance;
        if (runtime == null || runtime.Caught == null) return;

        foreach (var raw in runtime.Caught)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            caught.Add(Canonicalize(raw));
        }
    }

    private static void Compute(ref Summary summary)
    {
        var targets = summary.Targets ?? new List<string>();
        var caught = summary.CaughtKeys ?? new List<string>();

        var remainingTargets = new HashSet<string>(targets);
        int correct = 0;
        int wrong = 0;

        foreach (var raw in caught)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            string key = Canonicalize(raw);
            if (remainingTargets.Remove(key))
                correct++;
            else
                wrong++;
        }

        summary.Targets = targets;
        summary.CaughtKeys = caught;
        summary.TotalCaught = caught.Count;
        summary.Correct = correct;
        summary.Wrong = wrong;
        summary.Missing = Mathf.Max(0, remainingTargets.Count);
    }

    private static string Canonicalize(string raw)
    {
        string canonical = BugKeyUtil.CanonicalizeKey(raw);
        return string.IsNullOrEmpty(canonical) ? TargetBugsRuntime.NormalizeKey(raw) : canonical;
    }
}
