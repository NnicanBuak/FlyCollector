using UnityEngine;

public class HasItemCondition : InteractionConditionBase
{
    [SerializeField] private Item item;
    [SerializeField] private int quantity = 1;
    [SerializeField] private bool invert;

    public override bool Evaluate(InteractableObject @object)
    {
        if (InventoryManager.Instance == null) return false;
        bool has = InventoryManager.Instance.HasItem(item, quantity);
        return invert ? !has : has;
    }
}