using UnityEngine;

namespace Game.Scripts.CameraInspect
{
    /// <summary>
    /// Session for inspecting regular (non-bug) objects.
    /// Most logic is handled by the base InspectSessionCore class.
    /// </summary>
    public class RegularInspectSession : InspectSessionCore
    {
        public RegularInspectSession(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish)
            : base(go, holdPoint, flyTime, onFinish)
        {
        }
    }
}
