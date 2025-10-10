using System.Collections;
using UnityEngine;

[AddComponentMenu("Interactions/Actions/Turn Off Light")]
public class TurnOffLightAction : InteractionActionBase
{
    [Header("Ссылки (необяз.)")]
    [SerializeField] private LightController controller;

    [Tooltip("Вызывать ли OnLightStateChanged у LightController")]
    [SerializeField] private bool invokeEvent = true;

    public override IEnumerator Execute(InteractionContext ctx)
    {
        var ctrl = ResolveController(ctx);
        if (!ctrl)
        {
            Debug.LogWarning("[TurnOffLightAction] LightController не найден.", this);
            yield break;
        }

        ctrl.TurnOff(invokeEvent); // прямое выключение (минуя CanBeSwitched)
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