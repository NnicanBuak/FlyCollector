using System.Collections;
using UnityEngine;
using BugCatching;

public class CancelBugCatchAction : InteractionActionBase
{
    [Header("Cancel Bug Catch Settings")]
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
            Debug.Log($"[CancelBugCatchAction] Execute called on {ctx.Object.name}");
        }


        if (jarTrap == null)
        {
            Debug.LogError($"[CancelBugCatchAction] BugJarTrap component not found! Assign it or attach to same GameObject.");
            yield break;
        }


        if (jarTrap.GetState() != BugJarTrap.State.AtTable)
        {
            Debug.LogWarning($"[CancelBugCatchAction] Jar is not at table (state: {jarTrap.GetState()})");
            yield break;
        }

        if (showDebug)
        {
            Debug.Log($"[CancelBugCatchAction] Canceling catch - jar flying back to original position");
        }


        jarTrap.FlyBack();


        yield break;
    }
}
