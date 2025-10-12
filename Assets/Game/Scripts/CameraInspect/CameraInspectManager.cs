using System;
using UnityEngine;

public class CameraInspectManager
{
    private Transform defaultHoldPoint;
    private float defaultInspectFlyTime;
    private readonly bool showDebug;

    public event Action OnInspectStarted;
    public event Action OnInspectEnded;

    private InspectSession currentInspect;
    private bool finishing;
    private bool lastWasBug;

    public bool IsInspecting => currentInspect != null;
    public bool LastWasBug => lastWasBug;

    public CameraInspectManager(Transform holdPoint, float inspectFlyTime, bool showDebug = false)
    {
        defaultHoldPoint = holdPoint;
        defaultInspectFlyTime = inspectFlyTime;
        this.showDebug = showDebug;
    }

    public void ConfigureDefaults(Transform holdPoint, float inspectFlyTime)
    {
        defaultHoldPoint = holdPoint;
        defaultInspectFlyTime = inspectFlyTime;
    }

    public void StartInspect(GameObject target, Action onFinish = null)
    {
        if (target == null)
        {
            Debug.LogError("[CameraInspectManager] StartInspect: target == null");
            return;
        }
        if (defaultHoldPoint == null)
        {
            Debug.LogError("[CameraInspectManager] StartInspect: holdPoint == null (assign in inspector)");
            return;
        }

        StartInspectInternal(target, defaultHoldPoint, defaultInspectFlyTime, onFinish);
    }

    public void StartInspect(GameObject go, Transform customHoldPoint, float flyTime)
    {
        if (go == null)
        {
            Debug.LogError("[CameraInspectManager] StartInspect(old): go == null");
            return;
        }
        if (customHoldPoint == null)
        {
            Debug.LogError("[CameraInspectManager] StartInspect(old): holdPoint == null");
            return;
        }

        StartInspectInternal(go, customHoldPoint, flyTime, null);
    }

    public void UpdateInspect()
    {
        if (!IsInspecting) return;
        currentInspect.UpdateInput();
    }

    public void ForceEndInspect()
    {
        if (!IsInspecting) return;
        if (finishing) return;

        finishing = true;

        currentInspect.EndInspectNow();

        if (currentInspect != null)
        {
            currentInspect = null;
            OnInspectEnded?.Invoke();
        }

        if (showDebug)
            Debug.Log("[CameraInspectManager] Inspect force-ended");
    }

    private void HandleSessionFinish(Action externalOnFinish)
    {
        if (finishing)
        {
            currentInspect = null;
            externalOnFinish?.Invoke();
            OnInspectEnded?.Invoke();
            return;
        }

        finishing = true;
        currentInspect = null;
        externalOnFinish?.Invoke();
        OnInspectEnded?.Invoke();
    }

    private void StartInspectInternal(GameObject target, Transform point, float flyTime, Action onFinish)
    {
        if (IsInspecting) ForceEndInspect();

        finishing = false;

        // Choose bug-specific session if requested by InspectableObject
        var insp = target.GetComponent<InspectableObject>();
        bool isBug = insp != null && insp.IsBug();
        lastWasBug = isBug;

        currentInspect = isBug
            ? new BugInspectSession(target, point, flyTime, () => HandleSessionFinish(onFinish))
            : new InspectSession(target, point, flyTime, () => HandleSessionFinish(onFinish));

        currentInspect.Begin();

        if (showDebug)
            Debug.Log($"[CameraInspectManager] Inspect started for {target.name}");

        OnInspectStarted?.Invoke();
    }
}
