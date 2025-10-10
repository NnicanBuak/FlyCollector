using System.Collections;
using UnityEngine;

public class CameraFlyAction : InteractionActionBase
{
    [Header("Target")]
    [SerializeField] private Transform focusPoint;

    [Header("Options")]
    [SerializeField] private bool allowReturn = true;
    [SerializeField] private float flyTimeOverride = -1f;

    public override IEnumerator Execute(InteractionContext ctx)
    {
        var cam = ctx.Camera != null ? ctx.Camera : Camera.main;
        if (cam == null)
        {
            Debug.LogError("[CameraFlyAction] No Camera found");
            yield break;
        }
        
        var controller = cam.GetComponentInParent<CameraController>();
        if (controller == null) controller = Object.FindFirstObjectByType<CameraController>();
        if (controller == null)
        {
            Debug.LogError("[CameraFlyAction] CameraController not found");
            yield break;
        }

        if (focusPoint == null)
        {
            Debug.LogWarning("[CameraFlyAction] focusPoint is null");
            yield break;
        }

        controller.FocusToPoint(focusPoint, allowReturn, flyTimeOverride);
        yield break;
    }
    
}
