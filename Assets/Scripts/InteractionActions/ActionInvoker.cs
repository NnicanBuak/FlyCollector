using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionInvoker : MonoBehaviour
{
    [SerializeField] private List<InteractionActionBase> actions = new();

    public void RunAll()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var ctx = new InteractionContext
        {
            Camera    = Camera.main,
            GameObject= gameObject,
            Transform = transform,
            Inventory = InventoryManager.Instance,
            Animator  = GetComponent<Animator>()
        };

        foreach (var a in actions)
        {
            if (a == null) continue;
            yield return a.Execute(ctx);
        }
    }
}
