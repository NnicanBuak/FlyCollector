using UnityEngine;

public class BugInspectSession : InspectSession
{
    public BugInspectSession(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish)
        : base(go, holdPoint, flyTime, onFinish, true)
    {
    }
}

