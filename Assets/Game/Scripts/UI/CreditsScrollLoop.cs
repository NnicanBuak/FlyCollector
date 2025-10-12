using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class CreditsScrollLoop : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform content; // default: this
    [Tooltip("Optional viewport to compute offscreen start/end. If null, uses root canvas size.")]
    [SerializeField] private RectTransform viewport;

    [Header("Scroll Settings")]
    [Tooltip("Pixels per second for upward movement.")]
    [SerializeField] private float pixelsPerSecond = 40f;
    [Tooltip("Extra padding (pixels) below start and above end offscreen positions.")]
    [SerializeField] private float edgePadding = 50f;

    [Header("DOTween")]
    [SerializeField] private Ease ease = Ease.Linear;

    private Tweener _tw;

    private void Reset()
    {
        content = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (!content) content = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        Play();
    }

    private void OnDisable()
    {
        Kill();
    }

    public void Play()
    {
        Kill();
        if (!content) return;

        var vp = viewport;
        if (!vp)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas && canvas.rootCanvas)
                vp = canvas.rootCanvas.GetComponent<RectTransform>();
        }

        float vpHeight = vp ? vp.rect.height : 1080f;
        float contentHeight = content.rect.height;

        // Start just below bottom, end just above top
        float calcStartY = -(vpHeight / 2f) - (content.pivot.y * contentHeight) - edgePadding;
        float calcEndY   =  (vpHeight / 2f) + ((1f - content.pivot.y) * contentHeight) + edgePadding;

        // Ensure animation goes towards positive (increase Y)
        float fromY = Mathf.Min(calcStartY, calcEndY);
        float toY   = Mathf.Max(calcStartY, calcEndY);

        // place at start (lowest value)
        var anchored = content.anchoredPosition;
        anchored.y = fromY;
        content.anchoredPosition = anchored;

        float distance = Mathf.Abs(toY - fromY);
        float duration = Mathf.Max(0.01f, distance / Mathf.Max(1f, pixelsPerSecond));

        _tw = content.DOAnchorPosY(toY, duration)
            .SetEase(ease)
            .SetLoops(-1, LoopType.Restart);
    }

    public void Kill()
    {
        if (_tw != null && _tw.IsActive())
        {
            _tw.Kill();
            _tw = null;
        }
    }
}
