using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.Scripts.UI
{
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
        [SerializeField] private float initialDelay;
        [SerializeField] private float offscreenFactor = 0.7f;
        [SerializeField] private Ease easeIn = Ease.InCubic;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip rightAppearSound;
        [SerializeField] private AudioClip wrongAppearSound;
        [SerializeField] private AudioClip skippedAppearSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        [Header("Result UI")]
        [Tooltip("Reference to EndGameResultUI to play result label animation after slots appear")]
        [SerializeField] private EndGameResultUI endGameResultUI;

        private Sequence _seq;
        private readonly List<Behaviour> _disabledLayouts = new List<Behaviour>();
        private bool _layoutsDisabled;

        private object _lastOutcome = "Victory";

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
            var summary = BugSummaryUtil.Build(preferInventory: true);
            int target = summary.Targets?.Count ?? 0;
            int totalCaught = summary.TotalCaught;
            int wrong = summary.Wrong;
            int correct = summary.Correct;
            int missing = summary.Missing;


            if (!summary.HasData)
            {
                var gsm = GameSceneManager.Instance;
                if (gsm != null)
                {
                    totalCaught = gsm.GetPersistentData<int>(totalCaughtKey);
                    wrong = gsm.GetPersistentData<int>(wrongCountKey);
                    if (TargetBugsRuntime.Instance != null && TargetBugsRuntime.Instance.Targets != null)
                        target = TargetBugsRuntime.Instance.Targets.Count;

                    correct = Mathf.Clamp(totalCaught - wrong, 0, target > 0 ? target : int.MaxValue);
                    missing = Mathf.Max(0, target - correct);
                }
            }

            bool hasInfo = target > 0 || totalCaught > 0 || wrong > 0;
            if (!hasInfo && Debug.isDebugBuild)
            {
                if (logInfo)
                    Debug.Log("[GameOverBugsSummaryUI] No summary data; filling all slots as Right for debug view.");
                ApplyAllRight();
                return;
            }

            if (logInfo)
            {
                string source = summary.HasData
                    ? (summary.UsedInventory ? "Inventory" : "CaughtBugsRuntime")
                    : "PersistentFallback";
                Debug.Log(
                    $"[GameOverBugsSummaryUI] source={source}, target={target}, totalCaught={totalCaught}, wrong={wrong}, correct={correct}, missing={missing}");
            }


            if (wrong > 0) _lastOutcome = "WrongBugs";
            else if (missing > 0) _lastOutcome = "Timeout";
            else _lastOutcome = "Escaped";

            Apply(correct, wrong, missing);
        }

        public void Apply(int correct, int wrong, int missing)
        {
            int idx = 0;


            for (int i = 0; i < correct && idx < slots.Count; i++, idx++)
                SetExclusive(slots[idx], rightStateName);


            for (int i = 0; i < wrong && idx < slots.Count; i++, idx++)
                SetExclusive(slots[idx], wrongStateName);


            for (int i = 0; i < missing && idx < slots.Count; i++, idx++)
                SetExclusive(slots[idx], missingStateName);


            for (; idx < slots.Count; idx++)
                ClearAll(slots[idx]);
        }

        private void SetExclusive(UIStateToggle toggle, string stateName)
        {
            if (!toggle) return;

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
            var canvas = (slotsRoot ? slotsRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>()) ??
                         FindFirstObjectByType<Canvas>();
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
                rt.anchoredPosition = end + offset;
                if (!go.activeSelf) go.SetActive(true);

                var tween = rt.DOAnchorPos(end, perItemDuration).SetEase(easeIn);
                hasTweens = true;

                _seq.AppendCallback(() => PlayAppearSfx(go));
                _seq.Append(tween);
                if (perItemStagger > 0f) _seq.AppendInterval(perItemStagger);
            }


            _seq.AppendCallback(() =>
            {
                if (endGameResultUI == null)
                    endGameResultUI = FindFirstObjectByType<EndGameResultUI>();

                if (endGameResultUI != null)
                {
                    var labels = endGameResultUI.GetLabelsForOutcome(_lastOutcome);
                    endGameResultUI.PlayResultAnimation(_lastOutcome, labels);
                }
            });

            if (hasTweens)
            {
                _seq.OnComplete(RestoreLayouts);
                _seq.OnKill(RestoreLayouts);
            }
            else
            {
                if (endGameResultUI == null)
                    endGameResultUI = FindFirstObjectByType<EndGameResultUI>();
                if (endGameResultUI != null)
                {
                    var labels = endGameResultUI.GetLabelsForOutcome(_lastOutcome);
                    endGameResultUI.PlayResultAnimation(_lastOutcome, labels);
                }

                RestoreLayouts();
            }
        }

        public void KillAnim()
        {
            if (_seq != null)
            {
                if (_seq.IsActive())
                    _seq.Kill();
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

    public class EndGameResultUI : MonoBehaviour
    {
        [Header("UI State Controller")]
        [SerializeField] private UIStateToggle stateToggle;

        [Header("State Names")]
        [SerializeField] private string victoryStateName = "Escape";
        [SerializeField] private string wrongBugsStateName = "Mismatch";
        [SerializeField] private string timeoutStateName = "Fail";

        [Header("Keys in GameSceneManager persistent data")]
        [SerializeField] private string outcomeKey = "gameOutcome";
        [SerializeField] private string totalCaughtKey = "totalCaught";
        [SerializeField] private string wrongCountKey = "wrongCount";

        [Header("Optional: animated labels")]
        [Tooltip("CanvasGroup for the victory text (used for animated appearance)")]
        [SerializeField] private CanvasGroup victoryLabel;
        [Tooltip("CanvasGroup for the wrong-bugs text (used for animated appearance)")]
        [SerializeField] private CanvasGroup wrongLabel;
        [Tooltip("CanvasGroup for the timeout text (used for animated appearance)")]
        [SerializeField] private CanvasGroup timeoutLabel;

        [Header("Animation")]
        [SerializeField] private float labelAnimDuration = 0.35f;
        [SerializeField] private float labelAnimDelay = 0.12f;
        [SerializeField] private Ease labelEase = Ease.OutBack;
        [SerializeField] private float labelStartScale = 1.08f;
        [Tooltip("Vertical start offset (in local units) for the 'appear from top' effect")]
        [SerializeField] private float labelStartYOffset = 18f;

        [Header("Audio (optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip labelAppearClip;
        [SerializeField, Range(0f, 1f)] private float labelVolume = 1f;

        private Sequence _labelSeq;

        private void Awake()
        {
            if (stateToggle == null)
                stateToggle = GetComponent<UIStateToggle>();


            InitLabelGroup(victoryLabel);
            InitLabelGroup(wrongLabel);
            InitLabelGroup(timeoutLabel);
        }

        private void InitLabelGroup(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 0f;
            cg.transform.localScale = Vector3.one * labelStartScale;
            cg.gameObject.SetActive(true);
        }

        private void Start()
        {
            string outcomeName = null;

            var gsm = GameSceneManager.Instance;
            if (gsm != null && gsm.HasPersistentData(outcomeKey))
            {
                var obj = gsm.GetPersistentData<object>(outcomeKey);
                outcomeName = obj?.ToString();
            }

            if (!string.IsNullOrEmpty(outcomeName))
                ApplyOutcomeFromName(outcomeName);
            else
                ApplyOutcomeFromName("Victory");
        }

        private void ApplyOutcomeFromName(string outcomeName)
        {
            if (stateToggle == null)
            {
                Debug.LogWarning("[EndGameResultUI] UIStateToggle не назначен!");
                return;
            }

            string stateName;
            if (outcomeName == "Escaped" || outcomeName == "Victory")
                stateName = victoryStateName;
            else if (outcomeName == "WrongBugs" || outcomeName == "Wrong")
                stateName = wrongBugsStateName;
            else if (outcomeName == "Timeout")
                stateName = timeoutStateName;
            else
                stateName = victoryStateName;

            stateToggle.SetExclusive(stateName);
        }

        public void PlayResultAnimation(object outcome, params CanvasGroup[] sequentialLabels)
        {
            if (_labelSeq != null && _labelSeq.IsActive())
            {
                _labelSeq.Kill();
                _labelSeq = null;
            }

            CanvasGroup target;
            var outcomeName = outcome?.ToString() ?? string.Empty;

            if (outcomeName == "Escaped" || outcomeName == "Victory")
                target = victoryLabel;
            else if (outcomeName == "WrongBugs" || outcomeName == "Wrong")
                target = wrongLabel;
            else if (outcomeName == "Timeout")
                target = timeoutLabel;
            else
                target = victoryLabel;

            if (target == null)
            {
                ApplyOutcomeFromName(outcomeName);
                return;
            }


            ApplyOutcomeFromName(outcomeName);


            target.alpha = 0f;
            target.transform.localScale = Vector3.one * labelStartScale;
            target.gameObject.SetActive(true);

            Vector3 originalLocalPos = target.transform.localPosition;
            Vector3 startLocalPos = originalLocalPos + new Vector3(0f, labelStartYOffset, 0f);

            target.transform.localPosition = startLocalPos;

            _labelSeq = DOTween.Sequence();
            _labelSeq.AppendInterval(labelAnimDelay);
            _labelSeq.Append(target.DOFade(1f, labelAnimDuration).SetEase(Ease.Linear));
            _labelSeq.Join(target.transform.DOLocalMove(originalLocalPos, labelAnimDuration).SetEase(labelEase));
            _labelSeq.Join(target.transform.DOScale(Vector3.one, labelAnimDuration).SetEase(labelEase));
            _labelSeq.OnStart(() => { PlayLabelSfx(); });


            if (sequentialLabels != null && sequentialLabels.Length > 0)
            {
                foreach (var extra in sequentialLabels)
                {
                    if (extra == null) continue;


                    _labelSeq.AppendInterval(0.06f);
                    _labelSeq.AppendCallback(() =>
                    {
                        extra.alpha = 0f;
                        extra.transform.localScale = Vector3.one * labelStartScale;
                        extra.gameObject.SetActive(true);

                        var orig = extra.transform.localPosition;
                        extra.transform.localPosition = orig + new Vector3(0f, labelStartYOffset, 0f);
                    });

                    _labelSeq.Append(extra.DOFade(1f, labelAnimDuration).SetEase(Ease.Linear));
                    _labelSeq.Join(extra.transform
                        .DOLocalMoveY(extra.transform.localPosition.y - labelStartYOffset, labelAnimDuration)
                        .SetRelative(false).SetEase(labelEase));
                    _labelSeq.Join(extra.transform.DOScale(Vector3.one, labelAnimDuration).SetEase(labelEase));
                    _labelSeq.AppendCallback(() => { PlayLabelSfx(); });
                }
            }

            _labelSeq.OnComplete(() => { });
        }

        private void PlayLabelSfx()
        {
            if (audioSource == null || labelAppearClip == null) return;
            audioSource.PlayOneShot(labelAppearClip, labelVolume);
        }

        private void OnDisable()
        {
            if (_labelSeq != null && _labelSeq.IsActive())
            {
                _labelSeq.Kill();
                _labelSeq = null;
            }
        }

        public CanvasGroup[] GetLabelsForOutcome(object outcome)
        {
            var outcomeName = outcome?.ToString() ?? string.Empty;
            if (outcomeName == "Escaped" || outcomeName == "Victory")
                return new[] { victoryLabel };
            if (outcomeName == "WrongBugs" || outcomeName == "Wrong")
                return new[] { wrongLabel };
            if (outcomeName == "Timeout")
                return new[] { timeoutLabel };
            return new[] { victoryLabel };
        }
    }
}