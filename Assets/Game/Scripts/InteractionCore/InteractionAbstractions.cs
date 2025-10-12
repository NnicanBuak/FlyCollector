using System.Collections;
using UnityEngine;

public struct InteractionContext
{
    public Camera Camera;
    public InteractableObject Object;
    public GameObject GameObject;
    public Transform Transform;
    public InventoryManager Inventory;
    public Animator Animator;
    public string FailureReason;
}

public abstract class InteractionConditionBase : MonoBehaviour
{
    public abstract bool Evaluate(InteractableObject @object);
}

public abstract class InteractionActionBase : MonoBehaviour
{
    public abstract IEnumerator Execute(InteractionContext ctx);
}
