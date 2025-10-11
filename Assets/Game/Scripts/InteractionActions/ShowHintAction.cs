using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace InteractionActions
{
    /// <summary>
    /// Shows hint panels via <see cref="MultiHintController"/> using named identifiers.
    /// Used for displaying context hints.
    /// </summary>
    public class ShowHintAction : InteractionActionBase
    {
        [Header("Hint Settings")]
        [Tooltip("Hint panel identifiers to show (e.g. RMB, LMB, Custom).")]
        [SerializeField] private string[] panelIds = { MultiHintController.PanelNames.Custom };

        [FormerlySerializedAs("hintIndices")]
        [SerializeField, HideInInspector] private int[] legacyPanelIndices = Array.Empty<int>();

        [SerializeField] private float displayDuration = 2f;
        [SerializeField] private bool autoHide = true;

        private void OnValidate()
        {
            if (legacyPanelIndices != null && legacyPanelIndices.Length > 0)
            {
                panelIds = ConvertLegacyIndices(legacyPanelIndices);
                legacyPanelIndices = Array.Empty<int>();
            }
        }

        public override IEnumerator Execute(InteractionContext context)
        {
            if (MultiHintController.Instance == null)
            {
                Debug.LogWarning("[ShowHintAction] MultiHintController.Instance is null");
                yield break;
            }

            MultiHintController.Instance.Show(panelIds);

            if (autoHide && displayDuration > 0f)
            {
                yield return new WaitForSeconds(displayDuration);
                MultiHintController.Instance.HideAll();
            }
        }

        /// <summary>
        /// Allows runtime configuration of panels to display.
        /// </summary>
        public void SetPanelNames(params string[] names)
        {
            panelIds = names ?? Array.Empty<string>();
        }

        [Obsolete("Use SetPanelNames instead.")]
        public void SetHintIndices(params int[] indices)
        {
            panelIds = ConvertLegacyIndices(indices);
        }

        private static string[] ConvertLegacyIndices(int[] indices)
        {
            if (indices == null || indices.Length == 0)
                return Array.Empty<string>();

            var result = new string[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                result[i] = indices[i] switch
                {
                    0 => MultiHintController.PanelNames.RightMouse,
                    1 => MultiHintController.PanelNames.LeftMouse,
                    2 => MultiHintController.PanelNames.Custom,
                    _ => $"Panel{indices[i]}"
                };
            }

            return result;
        }
    }
}
