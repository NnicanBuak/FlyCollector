using System.Collections;
using UnityEngine;

/// <summary>
/// Action that starts a CooldownTimer
/// </summary>
public class StartCooldownAction : InteractionActionBase
{
    [Header("Cooldown Settings")]
    [Tooltip("CooldownTimer to start (auto-find if null)")]
    [SerializeField] private CooldownTimer cooldownTimer;

    [Tooltip("Override duration (0 = use timer's default duration)")]
    [SerializeField] private float overrideDuration = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private void Awake()
    {
        if (cooldownTimer == null)
        {
            cooldownTimer = GetComponent<CooldownTimer>();
            if (cooldownTimer == null)
            {
                cooldownTimer = GetComponentInParent<CooldownTimer>();
            }
        }
    }

    public override IEnumerator Execute(InteractionContext ctx)
    {
        if (cooldownTimer == null)
        {
            Debug.LogError($"[StartCooldownAction] CooldownTimer not found on {gameObject.name}!");
            yield break;
        }

        if (showDebug)
        {
            float duration = overrideDuration > 0f ? overrideDuration : cooldownTimer.TimeRemaining;
            Debug.Log($"[StartCooldownAction] Starting cooldown timer on {gameObject.name} ({duration}s)");
        }

        if (overrideDuration > 0f)
        {
            cooldownTimer.StartTimer(overrideDuration);
        }
        else
        {
            cooldownTimer.StartTimer();
        }

        yield break;
    }
}
