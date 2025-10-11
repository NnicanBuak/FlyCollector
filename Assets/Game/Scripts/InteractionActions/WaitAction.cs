
using System.Collections;
using UnityEngine;

public class WaitAction : InteractionActionBase
{
    [SerializeField] private float seconds = 0.5f;
    public override IEnumerator Execute(InteractionContext ctx)
    {
        if (seconds > 0f) yield return new WaitForSeconds(seconds);
    }
}