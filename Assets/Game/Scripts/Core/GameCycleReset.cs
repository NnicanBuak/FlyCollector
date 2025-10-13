using BugCatching;
using UnityEngine;

/// <summary>
/// Central place to reset persistent singletons between gameplay runs.
/// Call when finishing a loop (e.g., leaving GameOver or Credits).
/// </summary>
public static class GameCycleReset
{
    /// <summary>
    /// Reset all persistent systems that should not survive into the next run.
    /// </summary>
    public static void ResetPersistentSystems()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearInventory();
        }

        if (CaughtBugsRuntime.Instance != null)
        {
            CaughtBugsRuntime.Instance.ClearAll();
        }

        if (BugCounter.Instance != null)
        {
            BugCounter.Instance.RefillJars();
        }

        if (BugJarPool.Instance != null)
        {
            BugJarPool.Instance.ResetAllJars();
        }

        if (GameTimer.Instance != null)
        {
            GameTimer.Instance.StopTimer();
            GameTimer.Instance.ResetTimer();
        }

        if (TargetBugsRuntime.Instance != null)
        {
            TargetBugsRuntime.Instance.SetBugsToSpawn(null);
            TargetBugsRuntime.Instance.SetTargets(null);
        }

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.ClearAllPersistentData();
        }
    }
}
