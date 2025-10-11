using System.Collections;
using UnityEngine;
using BugCatching;

public class SealBugAction : InteractionActionBase
{
    [Header("Seal Bug Settings")]
    [Tooltip("BugJarTrap component (auto-find if null)")]
    [SerializeField] private BugJarTrap jarTrap;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private void Awake()
    {

        if (jarTrap == null)
        {
            jarTrap = GetComponentInParent<BugJarTrap>();
            if (jarTrap == null)
            {
                jarTrap = GetComponent<BugJarTrap>();
            }
        }
    }

    public override IEnumerator Execute(InteractionContext ctx)
    {
        if (showDebug)
        {
            Debug.Log($"[SealBugAction] Execute called on {ctx.Object.name}");
        }


        if (jarTrap == null)
        {
            Debug.LogError($"[SealBugAction] BugJarTrap component not found! Assign it or attach to same GameObject.");
            yield break;
        }


        if (jarTrap.GetState() != BugJarTrap.State.AtTable)
        {
            Debug.LogWarning($"[SealBugAction] Jar is not at table (state: {jarTrap.GetState()})");
            yield break;
        }


        if (jarTrap.GetTargetBug() == null)
        {
            Debug.LogError($"[SealBugAction] Jar has no target bug!");
            yield break;
        }

        if (showDebug)
        {
            Debug.Log($"[SealBugAction] Sealing jar with bug: {jarTrap.GetTargetBug().name}");
        }


        jarTrap.Seal();


        yield break;
    }
}
