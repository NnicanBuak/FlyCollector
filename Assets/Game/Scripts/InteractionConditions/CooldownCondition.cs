using UnityEngine;

/// <summary>
/// Condition that blocks interaction while CooldownTimer is running
/// </summary>
public class CooldownCondition : InteractionConditionBase
{
    [Header("Cooldown Settings")]
    [Tooltip("CooldownTimer component (auto-find if null)")]
    [SerializeField] private CooldownTimer cooldownTimer;

    [Tooltip("Allow interaction only when timer is running (invert logic)")]
    [SerializeField] private bool invertLogic = false;

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

    public override bool Evaluate(InteractableObject @object)
    {
        if (cooldownTimer == null)
        {
            if (showDebug)
            {
                Debug.LogWarning($"[CooldownCondition] CooldownTimer not found on {gameObject.name}");
            }
            return true; // Allow interaction if no timer found
        }

        bool isRunning = cooldownTimer.IsRunning;

        // Normal logic: allow interaction when timer is NOT running (cooldown finished)
        // Inverted logic: allow interaction only when timer IS running
        bool result = invertLogic ? isRunning : !isRunning;

        if (showDebug)
        {
            Debug.Log($"[CooldownCondition] Timer running: {isRunning}, Allow interaction: {result}");
        }

        return result;
    }
}
