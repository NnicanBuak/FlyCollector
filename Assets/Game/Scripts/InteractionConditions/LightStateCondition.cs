using UnityEngine;

[DisallowMultipleComponent]
public class LightStateCondition : InteractionConditionBase
{
    public enum Mode
    {
        IsOn,
        IsOff,
        CanBeSwitched
    }

    [Header("Что проверяем")]
    [SerializeField] private Mode check = Mode.IsOn;

    [Header("Где искать свет")]
    [Tooltip("Если не задан, попробует найти на самом объекте, родителях и детях.")]
    [SerializeField] private LightController targetController;

    [Tooltip("Если контроллера нет, будет использован обычный Light.")]
    [SerializeField] private Light fallbackLight;

    public override bool Evaluate(InteractableObject @object)
    {

        var go = targetController ? targetController.gameObject :
                 @object ? @object.gameObject : gameObject;

        var controller = targetController ? targetController : go.GetComponentInParent<LightController>() ?? go.GetComponentInChildren<LightController>(true);
        var light = fallbackLight ? fallbackLight :
                    controller ? controller.GetComponent<Light>() :
                    go.GetComponentInParent<Light>() ?? go.GetComponentInChildren<Light>(true);

        switch (check)
        {
            case Mode.IsOn:
                if (controller) return controller.IsLightOn;
                return light && light.enabled;

            case Mode.IsOff:
                if (controller) return !controller.IsLightOn;
                return light && !light.enabled;

            case Mode.CanBeSwitched:

                return controller && controller.CanBeSwitched;

            default:
                return false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {

        if (!fallbackLight && targetController)
            fallbackLight = targetController.GetComponent<Light>();
    }
#endif
}