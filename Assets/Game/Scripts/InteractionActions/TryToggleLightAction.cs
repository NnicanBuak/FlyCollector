using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class TryToggleLightAction : InteractionActionBase
{
    [Range(0f, 1f)] [SerializeField] private float chance = 0.25f;
    public UnityEvent OnSuccess;
    public UnityEvent OnFail;

    public void SetChance(float value)
    {
        chance = Mathf.Clamp01(value);
    }

    public override IEnumerator Execute(InteractionContext ctx)
    {
        bool ok = Random.value <= chance;
        if (ok) OnSuccess?.Invoke(); else OnFail?.Invoke();
        yield break;
    }
}