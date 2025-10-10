using System.Collections;
using UnityEngine;

namespace InteractionActions
{
    /// <summary>
    /// Hides hint panels via MultiHintController.
    /// Used to clear displayed hints after interactions.
    /// </summary>
    public class HideHintAction : InteractionActionBase
    {
        public override IEnumerator Execute(InteractionContext context)
        {
            if (MultiHintController.Instance == null)
            {
                Debug.LogWarning("[HideHintAction] MultiHintController.Instance is null");
                yield break;
            }

            // Hide all hint panels
            MultiHintController.Instance.HideAll();
            yield break;
        }
    }
}
