using UnityEngine;

public class BugCounterCondition : InteractionConditionBase
{
    public enum ComparisonType
    {
        GreaterThan,
        GreaterOrEqual,
        Equal,
        LessOrEqual,
        LessThan,
        NotEqual
    }

    [Header("Jar Count Check")]
    [Tooltip("Required jar count for comparison")]
    [SerializeField] private int requiredAmount = 1;

    [Tooltip("Type of comparison")]
    [SerializeField] private ComparisonType comparison = ComparisonType.GreaterOrEqual;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    public override bool Evaluate(InteractableObject @object)
    {
        if (BugCounter.Instance == null)
        {
            if (showDebug)
            {
                Debug.LogWarning("[BugCounterCondition] BugCounter.Instance is null!");
            }
            return false;
        }

        int currentJars = BugCounter.Instance.CurrentJars;
        bool result = false;

        switch (comparison)
        {
            case ComparisonType.GreaterThan:
                result = currentJars > requiredAmount;
                break;

            case ComparisonType.GreaterOrEqual:
                result = currentJars >= requiredAmount;
                break;

            case ComparisonType.Equal:
                result = currentJars == requiredAmount;
                break;

            case ComparisonType.LessOrEqual:
                result = currentJars <= requiredAmount;
                break;

            case ComparisonType.LessThan:
                result = currentJars < requiredAmount;
                break;

            case ComparisonType.NotEqual:
                result = currentJars != requiredAmount;
                break;
        }

        if (showDebug)
        {
            Debug.Log($"[BugCounterCondition] {currentJars} {comparison} {requiredAmount} = {result}");
        }

        return result;
    }
}
