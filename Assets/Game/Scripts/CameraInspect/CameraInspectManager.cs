using UnityEngine;
using Bug;

public class CameraInspectManager
{
    private readonly Transform holdPoint;
    private readonly float inspectFlyTime;
    private readonly bool showDebug;

    private InspectSession currentInspect;


    public event System.Action<BugAI> OnBugAIInspected;
    public event System.Action<BugAI, GameObject> OnBugAIInspectedWhileFocused;
    public event System.Action OnInspectEnded;

    public bool IsInspecting => currentInspect != null;

    public CameraInspectManager(Transform holdPoint, float inspectFlyTime, bool showDebug = false)
    {
        this.holdPoint = holdPoint;
        this.inspectFlyTime = inspectFlyTime;
        this.showDebug = showDebug;
    }

    public void Update(bool exitPressed)
    {
        if (currentInspect == null)
            return;

        currentInspect.UpdateInput(exitPressed);
    }

    public bool TryStartInspect(GameObject target, Camera cam, GameObject currentFocusTarget, System.Action onFinish = null)
    {
        return StartInspect(target, holdPoint, inspectFlyTime, cam, currentFocusTarget, onFinish);
    }

    public bool StartInspect(GameObject target, Transform hp, float fly, Camera cam, GameObject currentFocusTarget, System.Action onFinish = null)
    {
        if (showDebug)
            Debug.Log($"[CameraInspectManager] Starting inspect: {target.name}");

        var inspectable = target.GetComponent<IInspectable>();
        if (inspectable == null)
        {
            Debug.LogWarning($"[CameraInspectManager] IInspectable not found on {target.name}");
            return false;
        }

        // Check if inspection is allowed (e.g., bug accessibility)
        if (!inspectable.OnInspect(cam))
        {
            if (showDebug)
                Debug.Log($"[CameraInspectManager] Inspection denied for {target.name}");
            return false;
        }

        var bug = target.GetComponentInParent<BugAI>();
        if (bug != null)
        {
            OnBugAIInspected?.Invoke(bug);

            if (currentFocusTarget != null)
            {
                OnBugAIInspectedWhileFocused?.Invoke(bug, currentFocusTarget);
                if (showDebug)
                    Debug.Log($"[CameraInspectManager] BugAI inspected while focused on: {currentFocusTarget.name}");
            }
        }


        if (FocusLevelManager.Instance != null && !FocusLevelManager.Instance.HasEverInteracted)
        {
            if (!InteractionGate.Consume())
                FocusLevelManager.Instance.TriggerFirstInteraction("Inspect");
        }

        // Track camera mode change to Inspect
        GAManager.Instance.TrackCameraModeChange("Normal", "Inspect");

        currentInspect = new InspectSession(
            target,
            hp != null ? hp : holdPoint,
            fly > 0f ? fly : inspectFlyTime,
            () =>
            {
                currentInspect = null;
                onFinish?.Invoke();
                OnInspectEnded?.Invoke();

                // Track camera mode change back to Normal
                GAManager.Instance.TrackCameraModeChange("Inspect", "Normal");
            });

        currentInspect.Begin();
        return true;
    }
}
