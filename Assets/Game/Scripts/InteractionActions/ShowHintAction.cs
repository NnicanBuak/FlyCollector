using System.Collections;
using UnityEngine;

namespace InteractionActions
{
    /// <summary>
    /// Shows hint panels via MultiHintController.
    /// Used for displaying context hints.
    /// Panel 0 = RMB hint
    /// Panel 1 = LMB hint
    /// Panel 2 = Custom/Action hint (panel 3)
    /// </summary>
    public class ShowHintAction : InteractionActionBase
    {
        [Header("Hint Settings")]
        [Tooltip("Hint panel indices to show (0=RMB, 1=LMB, 2=Custom)")]
        [SerializeField] private int[] hintIndices = new int[] { 2 };
        [SerializeField] private float displayDuration = 2f;
        [SerializeField] private bool autoHide = true;

        public override IEnumerator Execute(InteractionContext context)
        {
            if (MultiHintController.Instance == null)
            {
                Debug.LogWarning("[ShowHintAction] MultiHintController.Instance is null");
                yield break;
            }

            // Show hint panels
            MultiHintController.Instance.Show(hintIndices);

            // Wait if auto-hide is enabled
            if (autoHide && displayDuration > 0f)
            {
                yield return new WaitForSeconds(displayDuration);
                MultiHintController.Instance.HideAll();
            }
        }

        /// <summary>
        /// Sets hint indices dynamically (useful for runtime configuration).
        /// </summary>
        public void SetHintIndices(params int[] indices)
        {
            hintIndices = indices;
        }
    }
}
