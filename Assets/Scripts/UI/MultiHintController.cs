using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls multiple hint panels with fade/scale animation.
/// Manages up to 3 hint objects that can be shown/hidden simultaneously.
/// </summary>
public class MultiHintController : MonoBehaviour
{
    public static MultiHintController Instance { get; private set; }

    [Header("Hint Panels")]
    [SerializeField] private GameObject[] hintPanels = new GameObject[3];

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float scaleSpeed = 8f;
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.8f, 0.8f, 1f);

    private Vector3 normalScale = Vector3.one;
    private CanvasGroup[] canvasGroups;
    private RectTransform[] rectTransforms;
    private bool[] activeStates;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializePanels();
    }

    void Update()
    {
        AnimatePanels();
    }

    private void InitializePanels()
    {
        int count = hintPanels.Length;
        canvasGroups = new CanvasGroup[count];
        rectTransforms = new RectTransform[count];
        activeStates = new bool[count];

        for (int i = 0; i < count; i++)
        {
            if (hintPanels[i] == null) continue;

            // Get or add CanvasGroup
            canvasGroups[i] = hintPanels[i].GetComponent<CanvasGroup>();
            if (canvasGroups[i] == null)
                canvasGroups[i] = hintPanels[i].AddComponent<CanvasGroup>();

            // Get RectTransform
            rectTransforms[i] = hintPanels[i].GetComponent<RectTransform>();

            // Initialize hidden
            canvasGroups[i].alpha = 0f;
            if (rectTransforms[i] != null)
                rectTransforms[i].localScale = hiddenScale;

            activeStates[i] = false;
        }
    }

    private void AnimatePanels()
    {
        for (int i = 0; i < hintPanels.Length; i++)
        {
            if (hintPanels[i] == null || canvasGroups[i] == null) continue;

            bool shouldShow = activeStates[i];

            // Animate alpha
            float targetAlpha = shouldShow ? 1f : 0f;
            canvasGroups[i].alpha = Mathf.Lerp(canvasGroups[i].alpha, targetAlpha, Time.deltaTime * fadeSpeed);

            // Animate scale
            if (rectTransforms[i] != null)
            {
                Vector3 targetScale = shouldShow ? normalScale : hiddenScale;
                rectTransforms[i].localScale = Vector3.Lerp(rectTransforms[i].localScale, targetScale, Time.deltaTime * scaleSpeed);
            }

            // Update interactivity
            canvasGroups[i].interactable = shouldShow;
            canvasGroups[i].blocksRaycasts = shouldShow;
        }
    }

    /// <summary>
    /// Show specific hint panels by index.
    /// Example: Show(0, 2) - shows panels 0 and 2, hides panel 1
    /// </summary>
    public void Show(params int[] indices)
    {
        // Hide all first
        for (int i = 0; i < activeStates.Length; i++)
            activeStates[i] = false;

        // Show specified indices
        foreach (int index in indices)
        {
            if (index >= 0 && index < activeStates.Length)
            {
                activeStates[index] = true;
                if (hintPanels[index] != null)
                    hintPanels[index].SetActive(true);
            }
        }
    }

    /// <summary>
    /// Hide all hint panels.
    /// </summary>
    public void HideAll()
    {
        for (int i = 0; i < activeStates.Length; i++)
        {
            activeStates[i] = false;
        }
    }

    /// <summary>
    /// Show all hint panels.
    /// </summary>
    public void ShowAll()
    {
        for (int i = 0; i < activeStates.Length; i++)
        {
            activeStates[i] = true;
            if (hintPanels[i] != null)
                hintPanels[i].SetActive(true);
        }
    }

    /// <summary>
    /// Check if specific panel is currently active.
    /// </summary>
    public bool IsActive(int index)
    {
        if (index >= 0 && index < activeStates.Length)
            return activeStates[index];
        return false;
    }
}
