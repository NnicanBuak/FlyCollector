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

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private Vector3 normalScale = Vector3.one;
    private CanvasGroup[] canvasGroups;
    private RectTransform[] rectTransforms;
    private bool[] activeStates;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (showDebug)
                Debug.LogWarning($"[MultiHintController] Duplicate instance detected, destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Debug.Log($"[MultiHintController] Instance initialized on {gameObject.name}. Array size: {hintPanels.Length}");

        InitializePanels();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            if (showDebug)
                Debug.Log("[MultiHintController] Instance destroyed");
        }
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

        Debug.Log($"[MultiHintController] Initializing {count} panels...");

        for (int i = 0; i < count; i++)
        {
            if (hintPanels[i] == null)
            {
                Debug.LogWarning($"[MultiHintController] Panel {i} is null in inspector!");
                continue;
            }

            Debug.Log($"[MultiHintController] Panel {i} = {hintPanels[i].name}");

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
            if (hintPanels[i] == null)
            {
                if (canvasGroups[i] != null || rectTransforms[i] != null)
                {
                    Debug.LogWarning($"[MultiHintController] Panel {i} became null during runtime! Was it destroyed?");
                    canvasGroups[i] = null;
                    rectTransforms[i] = null;
                }
                continue;
            }

            if (canvasGroups[i] == null) continue;

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
        Debug.Log($"[MultiHintController] Show called with indices: [{string.Join(", ", indices)}]");

        // Check if arrays are initialized
        if (activeStates == null || hintPanels == null)
        {
            Debug.LogError("[MultiHintController] Show called but arrays not initialized! Awake may not have run yet.");
            return;
        }

        // Log current state of all panels
        for (int i = 0; i < hintPanels.Length; i++)
        {
            Debug.Log($"[MultiHintController] Panel {i} state: {(hintPanels[i] != null ? hintPanels[i].name : "NULL")}");
        }

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
                {
                    hintPanels[index].SetActive(true);

                    // Log detailed state for debugging visibility issues
                    if (canvasGroups[index] != null)
                    {
                        Debug.Log($"[MultiHintController] Panel {index} ({hintPanels[index].name}) set to active. " +
                                  $"CanvasGroup alpha={canvasGroups[index].alpha:F2}, " +
                                  $"scale={rectTransforms[index]?.localScale}, " +
                                  $"activeInHierarchy={hintPanels[index].activeInHierarchy}");
                    }
                    else
                    {
                        Debug.Log($"[MultiHintController] Panel {index} ({hintPanels[index].name}) set to active, but CanvasGroup is null!");
                    }
                }
                else
                {
                    Debug.LogWarning($"[MultiHintController] Panel {index} is null! Was destroyed or never assigned.");
                }
            }
        }
    }

    /// <summary>
    /// Hide all hint panels.
    /// </summary>
    public void HideAll()
    {
        if (showDebug)
            Debug.Log("[MultiHintController] HideAll called");

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
