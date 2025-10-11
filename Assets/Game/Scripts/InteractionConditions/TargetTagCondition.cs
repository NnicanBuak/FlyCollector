using UnityEngine;

public class TargetTagCondition : InteractionConditionBase
{
    [SerializeField] private GameObject target;
    [SerializeField] private string requiredTag = "Open";
    [SerializeField] private bool mustBeActive = true;

    public override bool Evaluate(InteractableObject @object)
    {
        if (!target) return false;
        if (mustBeActive && !target.activeInHierarchy) return false;
        return target.CompareTag(requiredTag);
    }
}