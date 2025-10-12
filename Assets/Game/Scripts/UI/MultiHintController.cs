using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Controls multiple hint panels with fade/scale animation.
/// </summary>
public class MultiHintController : MonoBehaviour
{
    [Serializable]
    private struct HintPanelEntry
    {
        [Tooltip("Unique identifier used when calling Show.")]
        public string id;

        [Tooltip("Root GameObject of the hint panel.")]
        public GameObject panel;
    }

    public static class PanelNames
    {
        public const string RightMouse = "RMB";
        public const string LeftMouse = "LMB";
        public const string Custom = "Custom";
    }

    public static MultiHintController Instance { get; private set; }

    [Header("Hint Panels")]
    [FormerlySerializedAs("hintPanels")]
    [SerializeField, HideInInspector] private GameObject[] legacyHintPanels = Array.Empty<GameObject>();
    [SerializeField] private HintPanelEntry[] hintPanels = Array.Empty<HintPanelEntry>();

    [Header("Animation")]
    [FormerlySerializedAs("fadeSpeed")]
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.8f, 0.8f, 1f);
    [SerializeField, Min(0f)] private float horizontalSpacing = 40f;

    [Header("Debug")]
    [SerializeField] private bool showDebug;

    private readonly Vector3 normalScale = Vector3.one;

    private CanvasGroup[] canvasGroups = Array.Empty<CanvasGroup>();
    private RectTransform[] rectTransforms = Array.Empty<RectTransform>();
    private Vector2[] baseAnchoredPositions = Array.Empty<Vector2>();
    private bool[] activeStates = Array.Empty<bool>();
    private Dictionary<string, int> panelLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private bool initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (showDebug)
                Debug.LogWarning($"[MultiHintController] Duplicate instance detected, destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializePanels();
    }

    private void Update()
    {
        if (!initialized)
            return;

        AnimatePanels();
    }

    private void OnValidate()
    {
        MigrateLegacyPanels();

        if (hintPanels == null)
            return;

        for (int i = 0; i < hintPanels.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(hintPanels[i].id))
            {
                var entry = hintPanels[i];
                entry.id = GetDefaultPanelId(i);
                hintPanels[i] = entry;
            }
        }
    }

    private void InitializePanels()
    {
        MigrateLegacyPanels();
        int count = hintPanels?.Length ?? 0;

        canvasGroups = new CanvasGroup[count];
        rectTransforms = new RectTransform[count];
        activeStates = new bool[count];
        baseAnchoredPositions = new Vector2[count];
        panelLookup = new Dictionary<string, int>(count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < count; i++)
        {
            var entry = hintPanels[i];
            string id = NormalizeId(entry.id, i);

            if (!panelLookup.ContainsKey(id))
            {
                panelLookup.Add(id, i);
            }
            else
            {
                Debug.LogWarning($"[MultiHintController] Duplicate hint id '{id}' detected at index {i}. Only the first occurrence will be used.");
            }

            if (entry.panel == null)
            {
                Debug.LogWarning($"[MultiHintController] Panel '{id}' is null in inspector!");
                continue;
            }

            if (showDebug)
                Debug.Log($"[MultiHintController] Panel '{id}' resolved to {entry.panel.name}");

            var canvasGroup = entry.panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null && Application.isPlaying)
            {
                canvasGroup = entry.panel.AddComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                Debug.LogWarning($"[MultiHintController] Panel '{id}' is missing a CanvasGroup component and will be skipped.", entry.panel);
                canvasGroups[i] = null;
                rectTransforms[i] = entry.panel.GetComponent<RectTransform>();
                StoreBaseAnchoredPosition(i, id);
                activeStates[i] = false;
                continue;
            }

            canvasGroups[i] = canvasGroup;
            rectTransforms[i] = entry.panel.GetComponent<RectTransform>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            StoreBaseAnchoredPosition(i, id);
            if (rectTransforms[i] != null)
                rectTransforms[i].localScale = hiddenScale;

            entry.panel.SetActive(true);
            activeStates[i] = false;
        }

        UpdatePanelPositions();
        initialized = true;
    }

    private void MigrateLegacyPanels()
    {
        if (legacyHintPanels == null || legacyHintPanels.Length == 0)
            return;

        int count = legacyHintPanels.Length;

        if (hintPanels == null || hintPanels.Length < count)
        {
            Array.Resize(ref hintPanels, count);
        }

        for (int i = 0; i < count; i++)
        {
            if (legacyHintPanels[i] == null)
                continue;

            if (hintPanels == null)
                continue;

            if (hintPanels[i].panel == null)
            {
                var entry = hintPanels[i];
                entry.panel = legacyHintPanels[i];
                if (string.IsNullOrWhiteSpace(entry.id))
                    entry.id = GetDefaultPanelId(i);
                hintPanels[i] = entry;
            }
        }

        legacyHintPanels = Array.Empty<GameObject>();
    }

    private string NormalizeId(string id, int index)
    {
        if (string.IsNullOrWhiteSpace(id))
            id = GetDefaultPanelId(index);

        id = id.Trim();
        var entry = hintPanels[index];
        if (entry.id != id)
        {
            entry.id = id;
            hintPanels[index] = entry;
        }
        return id;
    }

    private string GetDefaultPanelId(int index)
    {
        return index switch
        {
            0 => PanelNames.RightMouse,
            1 => PanelNames.LeftMouse,
            2 => PanelNames.Custom,
            _ => $"Panel{index}"
        };
    }

    private bool TryGetPanelIndex(string panelId, out int index)
    {
        if (!initialized)
            InitializePanels();

        if (string.IsNullOrWhiteSpace(panelId) || panelLookup == null)
        {
            index = -1;
            return false;
        }

        return panelLookup.TryGetValue(panelId.Trim(), out index);
    }

    /// <summary>
    /// Shows hint panels by their identifiers.
    /// </summary>
    public void Show(params string[] panelIds)
    {
        if (!initialized)
            InitializePanels();

        if (activeStates == null)
            return;

        for (int i = 0; i < activeStates.Length; i++)
            activeStates[i] = false;

        if (panelIds == null)
            return;

        foreach (string rawId in panelIds)
        {
            if (!TryGetPanelIndex(rawId, out int index))
            {
                if (showDebug)
                    Debug.LogWarning($"[MultiHintController] Unknown hint panel '{rawId}'");
                continue;
            }

            activeStates[index] = true;

            var entry = hintPanels[index];
            if (entry.panel != null)
            {
                entry.panel.SetActive(true);

                if (showDebug && canvasGroups[index] != null)
                {
                    Debug.Log($"[MultiHintController] Showing '{entry.id}' (alpha={canvasGroups[index].alpha:F2}, scale={rectTransforms[index]?.localScale})");
                }
            }
        }

        UpdatePanelPositions();
    }

    /// <summary>
    /// Hides all hint panels.
    /// </summary>
    public void HideAll()
    {
        if (!initialized)
            InitializePanels();

        if (activeStates == null)
            return;

        for (int i = 0; i < activeStates.Length; i++)
            activeStates[i] = false;

        UpdatePanelPositions();
    }

    /// <summary>
    /// Shows all configured hint panels.
    /// </summary>
    public void ShowAll()
    {
        if (!initialized)
            InitializePanels();

        if (activeStates == null)
            return;

        for (int i = 0; i < activeStates.Length; i++)
        {
            activeStates[i] = true;
            hintPanels[i].panel?.SetActive(true);
        }

        UpdatePanelPositions();
    }

    /// <summary>
    /// Checks whether a panel with the provided identifier is currently active.
    /// </summary>
    public bool IsActive(string panelId)
    {
        return TryGetPanelIndex(panelId, out int index) &&
               index >= 0 &&
               index < activeStates.Length &&
               activeStates[index];
    }

    private void AnimatePanels()
    {
        if (canvasGroups == null)
            return;

        float speed = animationSpeed > 0f ? animationSpeed * Time.deltaTime : float.PositiveInfinity;

        for (int i = 0; i < hintPanels.Length; i++)
        {
            var entry = hintPanels[i];
            var canvasGroup = (canvasGroups != null && i < canvasGroups.Length) ? canvasGroups[i] : null;
            var rect = (rectTransforms != null && i < rectTransforms.Length) ? rectTransforms[i] : null;

            if (entry.panel == null || canvasGroup == null)
                continue;

            bool shouldShow = activeStates != null && i < activeStates.Length && activeStates[i];
            float targetAlpha = shouldShow ? 1f : 0f;
            Vector3 targetScale = shouldShow ? normalScale : hiddenScale;

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed);
            if (rect != null)
                rect.localScale = Vector3.MoveTowards(rect.localScale, targetScale, speed);

            bool fullyVisible = Mathf.Approximately(canvasGroup.alpha, 1f);
            canvasGroup.interactable = shouldShow && fullyVisible;
            canvasGroup.blocksRaycasts = canvasGroup.interactable;
        }
    }

    private void UpdatePanelPositions()
    {
        if (rectTransforms == null || rectTransforms.Length == 0)
            return;

        if (baseAnchoredPositions == null || baseAnchoredPositions.Length != rectTransforms.Length)
            baseAnchoredPositions = new Vector2[rectTransforms.Length];

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            var rect = rectTransforms[i];
            if (rect == null)
                continue;

            Vector2 basePos = baseAnchoredPositions[i];
            rect.anchoredPosition = basePos;
        }

        if (activeStates == null)
            return;

        List<int> activeMouseIndices = new List<int>();
        for (int i = 0; i < activeStates.Length && i < rectTransforms.Length; i++)
        {
            if (!activeStates[i] || rectTransforms[i] == null)
                continue;

            if (hintPanels != null && i < hintPanels.Length)
            {
                string id = hintPanels[i].id;
                if (!string.IsNullOrEmpty(id))
                {
                    id = id.Trim();
                    if (string.Equals(id, PanelNames.LeftMouse, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(id, PanelNames.RightMouse, StringComparison.OrdinalIgnoreCase))
                    {
                        activeMouseIndices.Add(i);
                    }
                }
            }
        }

        if (activeMouseIndices.Count > 1)
        {
            activeMouseIndices.Sort((a, b) =>
            {
                int pa = GetMousePanelPriority(a);
                int pb = GetMousePanelPriority(b);
                if (pa != pb)
                    return pa.CompareTo(pb);
                return a.CompareTo(b);
            });
        }

        if (activeMouseIndices.Count == 0)
            return;

        if (activeMouseIndices.Count == 1)
        {
            int idx = activeMouseIndices[0];
            var rect = rectTransforms[idx];
            if (rect != null)
            {
                Vector2 basePos = baseAnchoredPositions[idx];
                rect.anchoredPosition = new Vector2(0f, basePos.y);
            }
            return;
        }

        float[] widths = new float[activeMouseIndices.Count];
        float totalWidth = 0f;

        for (int idx = 0; idx < activeMouseIndices.Count; idx++)
        {
            var rect = rectTransforms[activeMouseIndices[idx]];
            float width = rect.rect.width;
            if (Mathf.Approximately(width, 0f))
                width = rect.sizeDelta.x;
            widths[idx] = width;
            totalWidth += width;
        }

        if (activeMouseIndices.Count > 1)
            totalWidth += horizontalSpacing * (activeMouseIndices.Count - 1);

        float cursor = -totalWidth * 0.5f;

        for (int idx = 0; idx < activeMouseIndices.Count; idx++)
        {
            int panelIndex = activeMouseIndices[idx];
            var rect = rectTransforms[panelIndex];
            if (rect == null)
                continue;

            float width = widths[idx];
            float centerX = cursor + width * 0.5f;
            Vector2 basePos = baseAnchoredPositions[panelIndex];
            rect.anchoredPosition = new Vector2(centerX, basePos.y);
            cursor += width + horizontalSpacing;
        }
    }

    private int GetMousePanelPriority(int index)
    {
        if (hintPanels == null || index < 0 || index >= hintPanels.Length)
            return 0;

        string id = hintPanels[index].id;
        if (string.Equals(id, PanelNames.LeftMouse, StringComparison.OrdinalIgnoreCase))
            return -1;
        if (string.Equals(id, PanelNames.RightMouse, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 0;
    }

    private void StoreBaseAnchoredPosition(int index, string id)
    {
        if (index < 0 || index >= rectTransforms.Length)
            return;

        var rect = rectTransforms[index];
        if (rect == null)
        {
            baseAnchoredPositions[index] = Vector2.zero;
            return;
        }

        Vector2 ap = rect.anchoredPosition;
        if (IsMouseHintId(id))
            baseAnchoredPositions[index] = new Vector2(0f, ap.y);
        else
            baseAnchoredPositions[index] = ap;
    }

    private static bool IsMouseHintId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        return string.Equals(id, PanelNames.LeftMouse, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(id, PanelNames.RightMouse, StringComparison.OrdinalIgnoreCase);
    }
}
