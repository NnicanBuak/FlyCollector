using System.Collections;
using UnityEngine;

[AddComponentMenu("Interactions/Actions/Toggle Light")]
public class ToggleLightAction : InteractionActionBase
{
    [Header("Ссылки (необяз.)")]
    [SerializeField] private LightController controller;

    public override IEnumerator Execute(InteractionContext ctx)
    {
        var ctrl = ResolveController(ctx);
        Debug.Log($"[ToggleLightAction] EXECUTE. ctx={ctx.GameObject?.name}; " +
                  $"ctrl={(ctrl? ctrl.gameObject.name : "null")}", this);
        if (!ctrl) yield break;

        Debug.Log($"[ToggleLightAction] CanBeSwitched={ctrl.CanBeSwitched}, IsLightOn={ctrl.IsLightOn}", ctrl);
        ctrl.Toggle();  // единственный вызов внутри Execute
        yield break;
    }


    private LightController ResolveController(InteractionContext ctx)
    {
        if (controller) return controller;
        if (ctx.GameObject)
        {
            var fromCtx = ctx.GameObject.GetComponentInParent<LightController>();
            if (fromCtx) return fromCtx;
        }
        return GetComponentInParent<LightController>();
    }
}