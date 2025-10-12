using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

[DisallowMultipleComponent]
public class GameOverBugsSummaryUI : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Parent under which to auto-collect UIStateToggle slots (optional).")]
    [SerializeField] private Transform slotsRoot;
    [Tooltip("Slots to control. If 'Slots Root' is set, this list is auto-filled on Validate/Awake.")]
    [SerializeField] private List<UIStateToggle> slots = new List<UIStateToggle>(16);

    [Header("State Names")]
    [SerializeField] private string rightStateName = "Right";
    [SerializeField] private string wrongStateName = "Wrong";
    [SerializeField] private string missingStateName = "WShow";

    [Header("Persistent Keys (GameSceneManager)")]
    [SerializeField] private string totalCaughtKey = "totalCaught";
    [SerializeField] private string wrongCountKey = "wrongCount";

    [Header("Debug")]
    [SerializeField] private bool logInfo;

    [Header("Animation (DOTween)")]
    [FormerlySerializedAs("animateOnStart")]
    [SerializeField] private bool animateOnAwake = true;
    [SerializeField] private float perItemDuration = 0.35f;
    [SerializeField] private float perItemStagger = 0.04f;
    [SerializeField] private float initialDelay = 0.0f;
    [SerializeField] private float offscreenFactor = 0.7f; // portion of canvas size to offset from bottom-right
    [SerializeField] private Ease easeIn = Ease.InCubic;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rightAppearSound;
    [SerializeField] private AudioClip wrongAppearSound;
    [SerializeField] private AudioClip skippedAppearSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private Sequence _seq;
    private readonly List<Behaviour> _disabledLayouts = new List<Behaviour>();
    private bool _layoutsDisabled;

    private void OnValidate()
    {
        AutoCollectSlotsIfRequested();
    }

    private void Awake()
    {
        AutoCollectSlotsIfRequested();
        RefreshFromPersistent();
        if (animateOnAwake)
            PlayAppearAnimation();
    }

    public void RefreshFromPersistent()
    {
        int totalCaught = 0;
        int wrong = 0;
        int target = 0;

        var gsm = GameSceneManager.Instance;
        if (gsm != null)
        {
            totalCaught = gsm.GetPersistentData<int>(totalCaughtKey, 0);
            wrong = gsm.GetPersistentData<int>(wrongCountKey, 0);
        }

        if (TargetBugsRuntime.Instance != null && TargetBugsRuntime.Instance.Targets != null)
            target = TargetBugsRuntime.Instance.Targets.Count;

        int correct = Mathf.Clamp(totalCaught - wrong, 0, target > 0 ? target : int.MaxValue);
        int missing = Mathf.Max(0, target - correct);

        bool hasInfo = target > 0 || totalCaught > 0 || wrong > 0;
        if (!hasInfo && Debug.isDebugBuild)
        {
            if (logInfo)
                Debug.Log("[GameOverBugsSummaryUI] No summary data; filling all slots as Right for debug view.");
            ApplyAllRight();
            return;
        }

        if (logInfo)
            Debug.Log($"[GameOverBugsSummaryUI] target={target}, totalCaught={totalCaught}, wrong={wrong}, correct={correct}, missing={missing}");

        Apply(correct, wrong, missing);
    }

    public void Apply(int correct, int wrong, int missing)
    {
        int idx = 0;

        // Right slots
        for (int i = 0; i < correct && idx < slots.Count; i++, idx++)
            SetExclusive(slots[idx], rightStateName);

        // Wrong slots
        for (int i = 0; i < wrong && idx < slots.Count; i++, idx++)
            SetExclusive(slots[idx], wrongStateName);

        // Missing slots
        for (int i = 0; i < missing && idx < slots.Count; i++, idx++)
            SetExclusive(slots[idx], missingStateName);

        // Clear the rest
        for (; idx < slots.Count; idx++)
            ClearAll(slots[idx]);
    }

    private void SetExclusive(UIStateToggle toggle, string stateName)
    {
        if (!toggle) return;
        // Clear all flags first, then try to set the desired state
        foreach (var e in toggle.States)
            e.Show = false;
        toggle.SetExclusive(stateName);
        toggle.ApplyStateVisibility();
    }

    private void ClearAll(UIStateToggle toggle)
    {
        if (!toggle) return;
        foreach (var e in toggle.States)
            e.Show = false;
        toggle.ApplyStateVisibility();
    }

    private void ApplyAllRight()
    {
        foreach (var slot in slots)
            SetExclusive(slot, rightStateName);
    }

    private void AutoCollectSlotsIfRequested()
    {
        if (slotsRoot == null) return;
        var list = new List<UIStateToggle>();
        slotsRoot.GetComponentsInChildren(true, list);
        slots = list;
    }

    public void PlayAppearAnimation()
    {
        KillAnim();

        DisableLayoutsForAnimation();

        RectTransform canvasRect = null;
        var canvas = (slotsRoot ? slotsRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>()) ?? FindFirstObjectByType<Canvas>();
        if (canvas != null && canvas.rootCanvas != null)
            canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();

        var rect = canvasRect ? canvasRect.rect : new Rect(0, 0, 1920, 1080);
        Vector2 offset = new Vector2(rect.width * offscreenFactor, -rect.height * offscreenFactor);

        _seq = DOTween.Sequence();
        if (initialDelay > 0f) _seq.AppendInterval(initialDelay);

        bool hasTweens = false;

        foreach (var slot in slots)
        {
            if (!slot) continue;
            var go = slot.gameObject;
            var rt = go.GetComponent<RectTransform>();
            if (!rt) continue;

            var end = rt.anchoredPosition;
            rt.DOKill();
            rt.anchoredPosition = end + offset; // start off-screen bottom-right
            if (!go.activeSelf) go.SetActive(true);

            var tween = rt.DOAnchorPos(end, perItemDuration).SetEase(easeIn);
            hasTweens = true;

            _seq.AppendCallback(() => PlayAppearSfx(go));
            _seq.Append(tween);
            if (perItemStagger > 0f) _seq.AppendInterval(perItemStagger);
        }

        if (hasTweens)
        {
            _seq.OnComplete(RestoreLayouts);
            _seq.OnKill(RestoreLayouts);
        }
        else
        {
            RestoreLayouts();
        }
    }

    public void KillAnim()
    {
        if (_seq != null && _seq.IsActive())
        {
            _seq.Kill();
            _seq = null;
        }
        else if (_seq != null)
        {
            _seq = null;
        }

        RestoreLayouts();
    }

    private void PlayAppearSfx(GameObject slotGo)
    {
        if (!audioSource) return;

        var toggle = slotGo ? slotGo.GetComponent<UIStateToggle>() : null;
        var sound = ResolveClip(toggle);

        if (sound)
        {
            audioSource.PlayOneShot(sound, soundVolume);
        }
        else if (audioSource.clip)
        {
            audioSource.Play();
        }
    }

    private AudioClip ResolveClip(UIStateToggle toggle)
    {
        if (!toggle || toggle.States == null) return null;

        foreach (var state in toggle.States)
        {
            if (state == null || !state.Show) continue;

            if (state.StateName == rightStateName)
                return rightAppearSound;
            if (state.StateName == wrongStateName)
                return wrongAppearSound;
            if (state.StateName == missingStateName)
                return skippedAppearSound;
        }

        return null;
    }

    private void OnDisable()
    {
        KillAnim();
    }

    private void DisableLayoutsForAnimation()
    {
        if (_layoutsDisabled) return;

        _disabledLayouts.Clear();

        DisableLayoutsOn(slotsRoot);
        if (slotsRoot && slotsRoot != transform)
            DisableLayoutsOn(slotsRoot.parent);
        else
            DisableLayoutsOn(transform);

        _layoutsDisabled = _disabledLayouts.Count > 0;
    }

    private void DisableLayoutsOn(Transform target)
    {
        if (!target) return;

        foreach (var layout in target.GetComponents<LayoutGroup>())
            DisableLayoutBehaviour(layout);

        foreach (var fitter in target.GetComponents<ContentSizeFitter>())
            DisableLayoutBehaviour(fitter);
    }

    private void DisableLayoutBehaviour(Behaviour behaviour)
    {
        if (!behaviour || !behaviour.enabled) return;
        behaviour.enabled = false;
        _disabledLayouts.Add(behaviour);
    }

    private void RestoreLayouts()
    {
        if (!_layoutsDisabled) return;

        foreach (var behaviour in _disabledLayouts)
        {
            if (!behaviour) continue;
            behaviour.enabled = true;

            var rect = behaviour.transform as RectTransform;
            if (rect)
                LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        _disabledLayouts.Clear();
        _layoutsDisabled = false;
    }
}
