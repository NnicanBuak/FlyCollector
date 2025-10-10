using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneTransition
{
    public float duration = 0.6f;
    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private CanvasGroup overlay;

    public SceneTransition(float duration)
    {
        this.duration = Mathf.Max(0.01f, duration);
    }

    public IEnumerator PlayOut(MonoBehaviour host)
    {
        EnsureOverlay();
        overlay.blocksRaycasts = true;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            overlay.alpha = curve.Evaluate(Mathf.Clamp01(t));
            yield return null;
        }
    }

    public IEnumerator PlayIn(MonoBehaviour host)
    {
        EnsureOverlay();
        float t = 1f;
        while (t > 0f)
        {
            t -= Time.deltaTime / duration;
            overlay.alpha = curve.Evaluate(Mathf.Clamp01(t));
            yield return null;
        }
        overlay.blocksRaycasts = false;
    }

    private void EnsureOverlay()
    {
        if (overlay != null) return;

        var go = GameObject.Find("__SceneTransitionOverlay__");
        if (go == null)
        {
            go = new GameObject("__SceneTransitionOverlay__");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var imgGo = new GameObject("FadeImage");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.color = Color.black;

            var ir = imgGo.GetComponent<RectTransform>();
            ir.anchorMin = Vector2.zero;
            ir.anchorMax = Vector2.one;
            ir.offsetMin = Vector2.zero;
            ir.offsetMax = Vector2.zero;

            overlay = cg;
        }
        else
        {
            overlay = go.GetComponent<CanvasGroup>();
        }
    }
}
