using UnityEngine;

public class FocusLevelCondition : InteractionConditionBase
{
    [SerializeField] private int requiredLevel = 1;
    [SerializeField] private bool exact = true; // ровно ==, иначе >=

    public override bool Evaluate(InteractableObject @object)
    {
        int lvl = FocusLevelManager.Instance ? FocusLevelManager.Instance.CurrentNestLevel : 0;
        return exact ? (lvl == requiredLevel) : (lvl >= requiredLevel);
    }
}