using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InteractableObject : MonoBehaviour, IInteractable
{
    [Header("Interact Settings")]
    [Tooltip("Глобальный флаг доступности.")]
    [SerializeField] private bool canInteract = true;

    [Tooltip("Не позволять повторный интеракт, пока выполняются действия.")]
    [SerializeField] private bool lockWhileRunning = true;

    [Tooltip("Полностью игнорировать попытки взаимодействия во время выполнения действий (без звуков fail и hover эффектов).")]
    [SerializeField] private bool silentWhileRunning = true;

    [Header("Conditions & Actions (do not rename/remove)")]
    [SerializeField] private List<InteractionConditionBase> conditions = new List<InteractionConditionBase>();
    [SerializeField] private List<InteractionActionBase> actions = new List<InteractionActionBase>();
    [Header("Failure Handling")]
    [Tooltip("Actions executed when interaction conditions fail.")]
    [SerializeField] private List<InteractionActionBase> failureActions = new List<InteractionActionBase>();

    [Header("Hover Outline (color only)")]
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Color hoverAvailableColor = Color.white;
    [SerializeField] private Color hoverUnavailableColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private bool disableOutlineOnExit = true;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(0f,1f)] private float audioVolume = 1f;
    [SerializeField] private AudioClip hoverAvailableClip;
    [SerializeField] private AudioClip hoverUnavailableClip;
    [SerializeField] private AudioClip interactSuccessClip;
    [SerializeField] private AudioClip interactFailClip;

    [Header("Events (optional)")]
    public UnityEvent onInteractStarted;
    public UnityEvent onInteractFinished;

    [Header("Dynamic Payload (optional)")]
    [SerializeField] private Item dynamicItem;


    private bool _hovered;
    private bool _isRunning;
    private Behaviour _outline;
    private Type _outlineType;

    private static readonly int OUTLINE_COLOR_ID = Shader.PropertyToID("_OutlineColor");




    public void SetCanInteract(bool value)
    {
        canInteract = value;
        if (_hovered) ApplyHoverVisuals();
    }


    public bool IsInteractable => canInteract && (!lockWhileRunning || !_isRunning);



    public void OnHoverEnter()
    {

        if (_isRunning && silentWhileRunning)
            return;

        _hovered = true;
        ApplyHoverVisuals();
        Play(IsAvailableForInteraction(out _) ? hoverAvailableClip : hoverUnavailableClip);
    }

    public void OnHoverExit()
    {
        _hovered = false;
        if (useOutline && _outline && disableOutlineOnExit)
            _outline.enabled = false;
    }

    public void SetDynamicItem(Item item)
    {
        dynamicItem = item;
    }

    public Item GetDynamicItem()
    {
        return dynamicItem;
    }

    public void OnInteract(Camera playerCamera)
    {

        if (_isRunning && silentWhileRunning)
            return;


        if (!IsAvailableForInteraction(out string failedCondition))
        {
            // Track blocked interaction for analytics
            if (!string.IsNullOrEmpty(failedCondition))
            {
                GAManager.Instance.TrackInteractionBlocked(gameObject.name, failedCondition);
            }

            Play(hoverUnavailableClip != null ? hoverUnavailableClip : interactFailClip);
            if (failureActions != null && failureActions.Count > 0)
            {
                if (!_isRunning)
                    _isRunning = true;
                StartCoroutine(RunFailureActionsRoutine(playerCamera, null, null, failedCondition));
            }
            else if (_hovered)
            {
                ApplyHoverVisuals();
            }
            return;
        }
        StartCoroutine(RunActionsRoutine(playerCamera, null, null));
    }


    private void Awake()
    {

        var behaviours = GetComponents<Behaviour>();
        foreach (var b in behaviours)
        {
            if (b == null) continue;
            var t = b.GetType();
            if ((t.Name == "Outline" || t.Name == "QuickOutline") && t.Namespace != "UnityEngine.UI")
            {
                _outline = b;
                _outlineType = t;
                break;
            }
        }

        if (useOutline && _outline != null) _outline.enabled = false;
    }




    private bool IsAvailableForInteraction(out string failedCondition)
    {
        failedCondition = null;

        if (!IsInteractable)
        {
            failedCondition = "NotInteractable";
            return false;
        }


        for (int i = 0; i < conditions.Count; i++)
        {
            var cond = conditions[i];
            if (!cond) continue;
            try
            {
                if (!cond.Evaluate(this))
                {
                    failedCondition = cond.GetType().Name;
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{name}] Condition {cond.GetType().Name} threw: {e.Message}", this);
                failedCondition = $"{cond.GetType().Name}_Error";
                return false;
            }
        }

        return true;
    }

    private IEnumerator RunActionsRoutine(Camera cam, InventoryManager inv, Animator animator)
    {
        _isRunning = true;
        onInteractStarted?.Invoke();


        var ctx = new InteractionContext
        {
            Camera = cam,
            Object = this,
            GameObject = gameObject,
            Transform = transform,
            Inventory = inv,
            Animator = animator,
            FailureReason = null
        };


        for (int i = 0; i < actions.Count; i++)
        {
            var act = actions[i];
            if (!act) continue;
            IEnumerator e = null;
            try
            {
                e = act.Execute(ctx);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{name}] Action {act.GetType().Name} threw on start: {ex.Message}", this);
                break;
            }

            if (e != null) { yield return StartCoroutine(e); }
        }

        Play(interactSuccessClip);
        _isRunning = false;
        onInteractFinished?.Invoke();


        if (_hovered) ApplyHoverVisuals();
    }

    private IEnumerator RunFailureActionsRoutine(Camera cam, InventoryManager inv, Animator animator, string failedCondition)
    {
        if (failureActions == null || failureActions.Count == 0)
        {
            if (_hovered) ApplyHoverVisuals();
            yield break;
        }

        _isRunning = true;

        var ctx = new InteractionContext
        {
            Camera = cam,
            Object = this,
            GameObject = gameObject,
            Transform = transform,
            Inventory = inv,
            Animator = animator,
            FailureReason = failedCondition
        };

        try
        {
            for (int i = 0; i < failureActions.Count; i++)
            {
                var act = failureActions[i];
                if (!act) continue;
                IEnumerator routine = null;
                try
                {
                    routine = act.Execute(ctx);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{name}] FailureAction {act.GetType().Name} threw on start: {ex.Message}", this);
                    continue;
                }

                if (routine != null)
                {
                    yield return StartCoroutine(routine);
                }
            }
        }
        finally
        {
            _isRunning = false;
            if (_hovered) ApplyHoverVisuals();
        }
    }



    private void ApplyHoverVisuals()
    {
        if (!useOutline) return;

        var available = IsAvailableForInteraction(out _);
        var color = available ? hoverAvailableColor : hoverUnavailableColor;


        if (_outline != null && TrySetOutlineColor(_outline, _outlineType, color))
        {
            _outline.enabled = true;
            return;
        }


        var any = TrySetShaderOutlineColor(color);
        if (any) return;


    }

    private bool TrySetOutlineColor(Behaviour outline, Type t, Color c)
    {
        if (outline == null || t == null) return false;


        var p = t.GetProperty("OutlineColor");
        if (p != null) { p.SetValue(outline, c, null); return true; }
        var f = t.GetField("OutlineColor");
        if (f != null) { f.SetValue(outline, c); return true; }


        p = t.GetProperty("Color");
        if (p != null) { p.SetValue(outline, c, null); return true; }
        f = t.GetField("Color");
        if (f != null) { f.SetValue(outline, c); return true; }

        return false;
    }

    private bool TrySetShaderOutlineColor(Color c)
    {
        bool any = false;
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;
                if (m.HasProperty(OUTLINE_COLOR_ID))
                {

                    var runtimeMat = r.materials[i];
                    if (runtimeMat != null && runtimeMat.HasProperty(OUTLINE_COLOR_ID))
                    {
                        runtimeMat.SetColor(OUTLINE_COLOR_ID, c);
                        any = true;
                    }
                }
            }
        }
        return any;
    }


    private void Play(AudioClip clip)
    {
        if (!clip) return;
        var src = audioSource ? audioSource : GetComponent<AudioSource>();
        if (src) src.PlayOneShot(clip, audioVolume);
        else AudioSource.PlayClipAtPoint(clip, transform.position, audioVolume);
    }
}
