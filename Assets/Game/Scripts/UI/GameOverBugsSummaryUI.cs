using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;
using Game.Scripts.BugData;

namespace Game.Scripts.UI
{
    public enum GameOutcome { Victory, WrongBugs, Timeout, Escaped }

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
        [SerializeField] private string missingStateName = "Missing";

        [Header("Game Rules")]
        [SerializeField] private int minCorrectForVictory = 12;
        [SerializeField] private int totalTargets = 16;

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

        [Header("Canvas")]
        [Tooltip("Reference to the root Canvas for calculating offscreen offset")]
        [SerializeField] private Canvas rootCanvas;

        [Header("End Game Result UI")]
        [Tooltip("UI State Controller for result labels")]
        [SerializeField] private UIStateToggle resultStateToggle;
        [Tooltip("State name for victory/escape")]
        [SerializeField] private string victoryStateName = "Escape";
        [Tooltip("State name for wrong bugs")]
        [SerializeField] private string wrongBugsStateName = "Mismatch";
        [Tooltip("State name for timeout")]
        [SerializeField] private string timeoutStateName = "Fail";
        [Tooltip("Key in GameSceneManager persistent data for outcome")]
        [SerializeField] private string outcomeKey = "gameOutcome";
        [Tooltip("CanvasGroup for victory text")]
        [SerializeField] private CanvasGroup victoryLabel;
        [Tooltip("CanvasGroup for wrong bugs text")]
        [SerializeField] private CanvasGroup wrongLabel;
        [Tooltip("CanvasGroup for timeout text")]
        [SerializeField] private CanvasGroup timeoutLabel;
        [Tooltip("Animation duration for labels")]
        [SerializeField] private float labelAnimDuration = 0.35f;
        [Tooltip("Delay before label animation")]
        [SerializeField] private float labelAnimDelay = 0.12f;
        [Tooltip("Ease for label animation")]
        [SerializeField] private Ease labelEase = Ease.OutBack;
        [Tooltip("Start scale for label animation")]
        [SerializeField] private float labelStartScale = 1.08f;
        [Tooltip("Vertical start offset for label animation")]
        [SerializeField] private float labelStartYOffset = 18f;
        [Tooltip("Audio source for label sounds")]
        [SerializeField] private AudioSource resultAudioSource;
        [Tooltip("Sound clip for label appear")]
        [SerializeField] private AudioClip labelAppearClip;
        [Tooltip("Volume for label sound")]
        [Range(0f, 1f)] [SerializeField] private float labelVolume = 1f;
        [Tooltip("Flip axis for label animation")]
        [SerializeField] private FlipAxis flipAxis = FlipAxis.None;

        private Sequence _seq;
        private Sequence _labelSeq;
        private readonly List<Behaviour> _disabledLayouts = new List<Behaviour>();
        private bool _layoutsDisabled;

        private GameOutcome _lastOutcome = GameOutcome.Victory;

        private bool _isAnimationComplete;
        public bool IsAnimationComplete => _isAnimationComplete;
        public event Action OnAnimationComplete;

        private void OnValidate()
        {
            // Auto collect slots in editor when root set
            AutoCollectSlotsIfRequested();
        }

        private void Awake()
        {
            AutoCollectSlotsIfRequested();
            if (resultStateToggle == null)
                resultStateToggle = GetComponent<UIStateToggle>();
            InitLabelGroup(victoryLabel);
            InitLabelGroup(wrongLabel);
            InitLabelGroup(timeoutLabel);
            RefreshFromPersistent();
            if (animateOnAwake)
                PlayAppearAnimation();
        }

        private void Start()
        {
            GameOutcome outcome = GameOutcome.Victory;
            var gsm = GameSceneManager.Instance;
            if (gsm != null && gsm.HasPersistentData(outcomeKey))
            {
                var obj = gsm.GetPersistentData<object>(outcomeKey);
                if (Enum.TryParse<GameOutcome>(obj?.ToString(), out var parsed))
                    outcome = parsed;
            }
            ApplyOutcomeFromName(outcome);
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
                    if (BugList.Instance != null && BugList.Instance.Targets != null)
                        target = BugList.Instance.Targets.Count;

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
                    ? (summary.UsedInventory ? "Inventory" : "InventoryManager")
                    : "PersistentFallback";
                Debug.Log(
                    $"[GameOverBugsSummaryUI] source={source}, target={target}, totalCaught={totalCaught}, wrong={wrong}, correct={correct}, missing={missing}");
            }


            if (correct >= minCorrectForVictory) _lastOutcome = GameOutcome.Victory;
            else if (wrong > 0) _lastOutcome = GameOutcome.WrongBugs;
            else if (missing > 0) _lastOutcome = GameOutcome.Timeout;
            else _lastOutcome = GameOutcome.Escaped;

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

            if (toggle.States != null)
            {
                foreach (var e in toggle.States)
                    if (e != null)
                        e.Show = false;
            }

            toggle.SetExclusive(stateName);
            toggle.ApplyStateVisibility();
        }

        private void ClearAll(UIStateToggle toggle)
        {
            if (!toggle) return;
            if (toggle.States != null)
            {
                foreach (var e in toggle.States)
                    if (e != null)
                        e.Show = false;
            }
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
            _isAnimationComplete = false;

            canvasRect = rootCanvas ? rootCanvas.GetComponent<RectTransform>() : null;

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

            // После появления всех слотов — проигрываем анимацию результата (текст)
            // Сначала убедимся, что у нас есть ссылка на EndGameResultUI, затем вычислим длительность
            float labelAnimationTime = GetTotalAnimationDuration();

            _seq.AppendCallback(() =>
            {
                // Вызываем один раз — EndGameResultUI самостоятельно выберет целевой label
                PlayResultAnimation(_lastOutcome);
            });

            if (hasTweens || labelAnimationTime > 0f)
            {
                // Ждём время анимации текста (если есть)
                if (labelAnimationTime > 0f)
                    _seq.AppendInterval(labelAnimationTime);

                _seq.AppendCallback(() =>
                {
                    _isAnimationComplete = true;
                    OnAnimationComplete?.Invoke();
                });

                _seq.OnComplete(RestoreLayouts);
                _seq.OnKill(RestoreLayouts);
            }
            else
            {
                // Нет анимаций — сразу восстановим лэйауты и пометим как завершено
                _seq.AppendCallback(() =>
                {
                    _isAnimationComplete = true;
                    OnAnimationComplete?.Invoke();
                });
                _seq.OnComplete(RestoreLayouts);
                _seq.OnKill(RestoreLayouts);
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
            if (_labelSeq != null && _labelSeq.IsActive())
            {
                _labelSeq.Kill();
                _labelSeq = null;
            }
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

        private void ApplyOutcomeFromName(GameOutcome outcome)
        {
            if (resultStateToggle == null)
            {
                Debug.LogWarning("[GameOverBugsSummaryUI] UIStateToggle не назначен!");
                return;
            }

            string stateName;
            switch (outcome)
            {
                case GameOutcome.Victory:
                case GameOutcome.Escaped:
                    stateName = victoryStateName;
                    break;
                case GameOutcome.WrongBugs:
                    stateName = wrongBugsStateName;
                    break;
                case GameOutcome.Timeout:
                    stateName = timeoutStateName;
                    break;
                default:
                    stateName = victoryStateName;
                    break;
            }

            resultStateToggle.SetExclusive(stateName);
        }

        public void PlayResultAnimation(GameOutcome outcome, params CanvasGroup[] sequentialLabels)
        {
            if (_labelSeq != null && _labelSeq.IsActive())
            {
                _labelSeq.Kill();
                _labelSeq = null;
            }

            CanvasGroup target;
            switch (outcome)
            {
                case GameOutcome.Victory:
                case GameOutcome.Escaped:
                    target = victoryLabel;
                    break;
                case GameOutcome.WrongBugs:
                    target = wrongLabel;
                    break;
                case GameOutcome.Timeout:
                    target = timeoutLabel;
                    break;
                default:
                    target = victoryLabel;
                    break;
            }

            // Ensure state is set so the correct label is present in UI
            ApplyOutcomeFromName(outcome);

            // Hide all and then enable only target (we rely on alpha for visibility)
            if (victoryLabel != null) victoryLabel.alpha = 0f;
            if (wrongLabel != null) wrongLabel.alpha = 0f;
            if (timeoutLabel != null) timeoutLabel.alpha = 0f;

            if (target == null)
                return;

            target.transform.localScale = GetStartScale();
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
                        extra.transform.localScale = GetStartScale();
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

            _labelSeq.OnComplete(() => { /* no-op by default */ });
        }

        private void PlayLabelSfx()
        {
            if (resultAudioSource == null || labelAppearClip == null) return;
            resultAudioSource.PlayOneShot(labelAppearClip, labelVolume);
        }

        public CanvasGroup[] GetLabelsForOutcome(GameOutcome outcome)
        {
            // Возвращаем дополнительные лейблы (которые могут идти после основного),
            // но обычно их нет — вернуть пустой массив безопасно.
            return Array.Empty<CanvasGroup>();
        }

        public float GetTotalAnimationDuration()
        {
            // Базовая длительность для первого лейбла
            float duration = labelAnimDuration + labelAnimDelay;

            // Добавим время для дополнительных лейблов
            int additionalLabels = 0;
            if (victoryLabel != null) additionalLabels++;
            if (wrongLabel != null) additionalLabels++;
            if (timeoutLabel != null) additionalLabels++;

            // Вычитаем 1, так как один лейбл уже учтён в базовой длительности
            additionalLabels = Mathf.Max(0, additionalLabels - 1);

            // Для каждого дополнительного лейбла добавляем задержку и время анимации
            duration += additionalLabels * (0.06f + labelAnimDuration);

            // Добавим небольшой запас времени
            duration += 0.2f;

            return duration;
        }

        private void InitLabelGroup(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 0f;
            cg.transform.localScale = GetStartScale();
            // Keep gameObject active so layout can compute; hide by alpha
            cg.gameObject.SetActive(true);
        }

        private Vector3 GetStartScale()
        {
            switch (flipAxis)
            {
                case FlipAxis.X: return new Vector3(-labelStartScale, 1, 1);
                case FlipAxis.Y: return new Vector3(1, -labelStartScale, 1);
                case FlipAxis.GlobalY: return new Vector3(1, 1, -labelStartScale);
                default: return Vector3.one * labelStartScale;
            }
        }
    }

    public enum FlipAxis { None, X, Y, GlobalY }
}
