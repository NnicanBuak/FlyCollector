using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using Bug;
using BugCatching;

public class InspectSession
{
    private readonly GameObject go;
    private readonly Transform holdPoint;
    private readonly float flyTime;
    private readonly System.Action onFinish;

    private Camera cam;
    private MonoBehaviour runner;
    private CameraController cameraController;

    private Transform originalParent;
    private Vector3 origPos;
    private Quaternion origRot;
    private Vector3 origScale;
    private Vector3 holdPointOriginalLocalPos;
    private bool holdPointLocalPosOverridden;
    private Quaternion initialInspectRotation;
    private Rigidbody rb;
    private Collider[] selfColliders;
    private Collider[] parentColliders;
    private InspectableObject inspectableObject;
    private BugAI targetBugAI;

    private Transform bugTablePosition;

    private readonly Mouse mouse;
    private readonly Keyboard keyboard;

    private GameObject currentHoveredObject;
    private IInteractable currentInteractable;

    private BugJarTrap activeJar;

    private bool isAnimating;
    private bool isReturning;
    private bool freezeDuringReturn;

    private float interactionRange = 100f;
    private LayerMask interactableLayer = ~0;


    private bool isCollectMode;
    private Vector3 savedCamPos;
    private Quaternion savedCamRot;
    private float savedCamFov;
    private bool startedAtFocus0;
    protected readonly bool _isBugSession;
    private DG.Tweening.Tween camFlyRoutine;
    private DG.Tweening.Tween activeFlyTween;
    private bool collectRotationLocked;

    // Return overrides when ending inspect
    private bool hasReturnOverride;
    private Vector3 returnOverridePos;
    private Quaternion returnOverrideRot;


    public InspectSession(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish)
    {
        this.go = go;
        this.holdPoint = holdPoint;
        this.flyTime = flyTime;
        this.onFinish = onFinish;

        mouse = Mouse.current;
        keyboard = Keyboard.current;
    }

    protected InspectSession(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish, bool isBugSession)
        : this(go, holdPoint, flyTime, onFinish)
    {
        this._isBugSession = isBugSession;
    }

    public void Begin()
    {
        if (go == null || holdPoint == null)
        {
            Debug.LogError("[InspectSession] go/holdPoint == null");
            onFinish?.Invoke();
            return;
        }

        cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (cam != null) runner = cam.GetComponent<MonoBehaviour>();
        cameraController = Object.FindFirstObjectByType<CameraController>();
        if (runner == null && cameraController != null) runner = cameraController;

        if (runner == null)
        {
            Debug.LogError("[InspectSession] Не найден MonoBehaviour для корутин");
            onFinish?.Invoke();
            return;
        }

        originalParent = go.transform.parent;
        origPos = go.transform.position;
        origRot = go.transform.rotation;
        origScale = go.transform.localScale;
        initialInspectRotation = go.transform.rotation;

        // Subscribe to flip button clicks (UI routed via InspectFlipButtonUI)
        InspectFlip.OnClicked += OnFlipClicked;

        rb = go.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        selfColliders = go.GetComponents<Collider>();
        foreach (var c in selfColliders) if (c) c.enabled = true;

        parentColliders = go.GetComponentsInParent<Collider>(true);
        foreach (var c in parentColliders)
        {
            if (c == null) continue;
            bool isSelf = false;
            for (int i = 0; i < selfColliders.Length; i++)
                if (selfColliders[i] == c) { isSelf = true; break; }
            if (!isSelf) c.enabled = false;
        }

        inspectableObject = go.GetComponent<InspectableObject>();
        targetBugAI = go.GetComponent<BugAI>();

        Debug.Log($"[InspectSession] Begin inspect for {go.name}. Is bug: {targetBugAI != null}");

        bugTablePosition = ResolveBugTablePosition(go);

        holdPointOriginalLocalPos = holdPoint.localPosition;
        holdPointLocalPosOverridden = false;

        if (inspectableObject != null && inspectableObject.UsesDynamicHoldPoint())
        {
            float d = inspectableObject.GetDynamicDistance();
            var lp = holdPoint.localPosition;
            holdPoint.localPosition = new Vector3(lp.x, lp.y, d);
            holdPointLocalPosOverridden = true;
        }

        Quaternion toRot = (inspectableObject != null && inspectableObject.UsesCustomOrientation())
            ? inspectableObject.GetInspectRotation()
            : holdPoint.rotation;


        if (cam != null)
        {
            savedCamPos = cam.transform.position;
            savedCamRot = cam.transform.rotation;
            savedCamFov = cam.orthographic ? 0f : cam.fieldOfView;
        }


        var flm = FocusLevelManager.Instance;
        startedAtFocus0 = (flm == null) ? true : (flm.CurrentNestLevel == 0);

        isAnimating = true;
        InteractionFreeze.Push();

        Fly(go.transform, holdPoint.position, toRot, flyTime, () =>
        {
            Quaternion targetRotation = go.transform.rotation;

            go.transform.SetParent(holdPoint, true);
            go.transform.rotation = targetRotation;
            initialInspectRotation = targetRotation;

            isAnimating = false;
            InteractionFreeze.Pop();

            if (targetBugAI != null)
            {
                // Freeze AI during inspect
                targetBugAI.DisableAI(true);

                // If started at focus level 0, enter CollectMode immediately
                if (startedAtFocus0)
                {
                    TryEnterCollectModeOrFallback();
                }
                else
                {
                    // Otherwise show standard hints with RMB available
                    ShowInspectHints(true);
                }
            }
            else
            {
                // Non-bug objects: only LMB hint
                ShowInspectHints(false);
            }
        });
    }


    public void UpdateInput()
    {
        if (go == null) return;
        if (isAnimating || (typeof(InteractionFreeze) != null && InteractionFreeze.IsLocked)) return;

        // Stop spinning for bugs only after camera tween completes in CollectMode
        if (!(_isBugSession && isCollectMode && collectRotationLocked))
            HandleInspectRotationFollow();

        bool rmb = mouse != null && mouse.rightButton.wasPressedThisFrame;


        if (rmb && targetBugAI != null)
        {
            // RMB can only enter collect; when collectMode is active, don't toggle it off
            if (!isCollectMode) TryEnterCollectModeOrFallback();
            return;
        }


        if (rmb && targetBugAI == null)
        {
            return;
        }


        bool lmb = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (_isBugSession && isCollectMode && TryJarDirectInteraction(lmb))
            return;
        if (lmb && !HandleInteraction())
        {

        }
        else
        {
            HandleInteraction();
        }
    }

    private void HandleInspectRotationFollow()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || go == null) return;

        if (Mouse.current != null)
        {
            // Map to a centered square region (not full screen)
            // Box size as fraction of screen (square), e.g., 0.6 means 60% of width/height
            const float boxSize = 0.6f;
            const float maxAngle = 60f;
            const float lerpSpeed = 10f;

            var mouseViewport = cam.ScreenToViewportPoint(Mouse.current.position.ReadValue());
            float half = Mathf.Clamp(boxSize * 0.5f, 0.05f, 0.49f);
            float nx = Mathf.Clamp((mouseViewport.x - 0.5f) / half, -1f, 1f);
            float ny = Mathf.Clamp((mouseViewport.y - 0.5f) / half, -1f, 1f);

            // Uniform scale on both axes, up to 180 degrees
            float yaw = nx * maxAngle;
            float pitch = -ny * maxAngle;

            Quaternion delta = Quaternion.Euler(pitch, yaw, 0f);
            Quaternion target = initialInspectRotation * delta;

            float s = lerpSpeed * Time.deltaTime;
            go.transform.rotation = Quaternion.Slerp(go.transform.rotation, target, s);
        }
    }

    private bool HandleInteraction()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = cam.ScreenPointToRay(mp);
        if (Physics.Raycast(ray, out var hit, interactionRange, interactableLayer))
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            var obj = interactable != null ? ((MonoBehaviour)interactable).gameObject : null;

            if (interactable != null)
            {
                if (obj != currentHoveredObject)
                {
                    ClearHoveredObject();
                    currentInteractable = interactable;
                    currentHoveredObject = obj;
                    currentInteractable.OnHoverEnter();
                }

                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    interactable.OnInteract(cam);
                    return true;
                }
                return true;
            }
            else
            {
                ClearHoveredObject();
                return false;
            }
        }
        else
        {
            ClearHoveredObject();
            return false;
        }
    }

    private void ClearHoveredObject()
    {
        if (currentHoveredObject != null && currentInteractable != null)
            currentInteractable.OnHoverExit();

        currentHoveredObject = null;
        currentInteractable = null;
    }

    private bool TryJarDirectInteraction(bool lmb)
    {
        if (activeJar == null) return false;
        var io = activeJar.GetInteractable();
        if (io == null || !io.isActiveAndEnabled) return false;

        var obj = io.gameObject;
        if (obj != currentHoveredObject)
        {
            ClearHoveredObject();
            currentHoveredObject = obj;
            currentInteractable = io;
            currentInteractable.OnHoverEnter();
        }

        if (lmb)
        {
            io.OnInteract(cam);
            return true;
        }
        return true;
    }

    private DG.Tweening.Tween Fly(Transform t, Vector3 toPos, Quaternion toRot, float time, System.Action after)
    {
        if (t == null) return null;
        // Kill previous fly tween if any
        if (activeFlyTween != null && activeFlyTween.IsActive()) activeFlyTween.Kill();
        var seq = DG.Tweening.DOTween.Sequence();
        seq.Join(t.DOMove(toPos, time).SetEase(DG.Tweening.Ease.InOutSine))
           .Join(t.DORotateQuaternion(toRot, time).SetEase(DG.Tweening.Ease.InOutSine))
           .OnComplete(() => after?.Invoke());
        activeFlyTween = seq;
        return seq;
    }




    public void EndInspectNow()
    {
        if (isReturning)
            return;

        if (go == null)
        {
            CompleteInspectReturn();
            return;
        }

        if (isCollectMode)
            ExitCollectMode(!_isBugSession);

        isReturning = true;
        isAnimating = true;

        freezeDuringReturn = true;
        InteractionFreeze.Push();

        ClearHoveredObject();
        MultiHintController.Instance?.HideAll();

        Transform target = go.transform;
        target.SetParent(originalParent, true);

        // Decide where to return the bug: if collectMode is active and we started at focus>0,
        // return to jar table position (no offset); otherwise return to original.
        hasReturnOverride = false;
        if (isCollectMode && activeJar != null)
        {
            var table = activeJar.GetTablePosition();
            Vector3 pos = table != null ? table.position : activeJar.transform.position;
            Quaternion rot = table != null ? table.rotation : activeJar.transform.rotation;
            returnOverridePos = pos;
            returnOverrideRot = rot;
            hasReturnOverride = true;
        }

        float returnTime = flyTime > 0f ? flyTime : 0f;

        if (returnTime > 0f && runner != null)
        {
            Vector3 dstPos = hasReturnOverride ? returnOverridePos : origPos;
            Quaternion dstRot = hasReturnOverride ? returnOverrideRot : origRot;
            Fly(target, dstPos, dstRot, returnTime, CompleteInspectReturn);
        }
        else
        {
            CompleteInspectReturn();
        }
    }

    private void CompleteInspectReturn()
    {
        ClearHoveredObject();

        if (go != null)
        {
            Transform target = go.transform;
            target.SetParent(originalParent, true);
            if (hasReturnOverride)
            {
                target.position = returnOverridePos;
                target.rotation = returnOverrideRot;
            }
            else
            {
                target.position = origPos;
                target.rotation = origRot;
            }
            target.localScale = origScale;
        }

        if (holdPointLocalPosOverridden && holdPoint != null)
        {
            holdPoint.localPosition = holdPointOriginalLocalPos;
            holdPointLocalPosOverridden = false;
        }

        isCollectMode = false;
        if (activeJar != null)
        {
            var st = activeJar.GetState();
            if (st == BugJarTrap.State.AtTable)
                activeJar.FlyBack();
            activeJar = null;
        }

        if (rb) rb.isKinematic = false;

        if (parentColliders != null && selfColliders != null)
        {
            foreach (var collider in parentColliders)
            {
                if (collider && System.Array.IndexOf(selfColliders, collider) < 0)
                    collider.enabled = true;
            }
        }

        if (targetBugAI != null)
            targetBugAI.DisableAI(false);

        MultiHintController.Instance?.HideAll();

        if (freezeDuringReturn)
        {
            InteractionFreeze.Pop();
            freezeDuringReturn = false;
        }

        isAnimating = false;
        isReturning = false;

        // Clear override state
        hasReturnOverride = false;

        if (_isBugSession)
        {
            var flm = FocusLevelManager.Instance;
            if (flm != null && flm.CurrentNestLevel != 0)
                flm.SetNestLevel(0);
        }

        // Unsubscribe flip action
        InspectFlip.OnClicked -= OnFlipClicked;
        onFinish?.Invoke();
    }

    private void OnFlipClicked()
    {
        // Flip 180 degrees around Y to see the other side
        initialInspectRotation = initialInspectRotation * Quaternion.Euler(0f, 180f, 0f);
    }

    private void ShowInspectHints(bool includeRightMouse)
    {
        if (MultiHintController.Instance == null)
            return;

        if (includeRightMouse)
            MultiHintController.Instance.Show(MultiHintController.PanelNames.LeftMouse, MultiHintController.PanelNames.RightMouse, "InspectFlip");
        else
            MultiHintController.Instance.Show(MultiHintController.PanelNames.LeftMouse, "InspectFlip");
    }

    private bool ShouldShowRightMouseHint()
    {
        if (targetBugAI == null)
            return false;

        // Show RMB only if player has jars AND the pool has an available jar.
        if (BugCounter.Instance != null && BugCounter.Instance.HasAnyJars &&
            BugCatching.BugJarPool.Instance != null && BugCatching.BugJarPool.Instance.HasAvailableJars)
            return true;

        // Fallback to previous focus-based visibility.
        if (cameraController != null)
            return !cameraController.IsAtZeroFocus;

        var flm = FocusLevelManager.Instance;
        if (flm != null)
            return flm.CurrentNestLevel > 0;

        return !startedAtFocus0;
    }

    private void TryEnterCollectModeOrFallback()
    {
        if (!TryAcquireJar(out var _))
        {
            FallbackNoJar();
            return;
        }

        EnterCollectMode();
    }

    private void EnterCollectMode()
    {
        if (isCollectMode) return;
        isCollectMode = true;

        // In collect mode, hide Collect (RMB) hint and keep only Put (LMB)
        MultiHintController.Instance?.Show(MultiHintController.PanelNames.LeftMouse);
        TrySummonJarToTable();
        // Trigger jar open animation immediately in collect mode
        if (activeJar != null)
        {
            activeJar.TriggerOpen();
            // Pass dynamic Item to InteractableObject based on bug name (Items are under Assets/Items)
            var registry = Object.FindFirstObjectByType<BugData.BugItemRegistry>();
            if (registry != null)
            {
                string bugName = go != null ? go.name.Replace("(Clone)", "").Trim() : string.Empty;
                string variant = string.IsNullOrEmpty(bugName) ? string.Empty : $"{bugName}_Variant";
                if (!string.IsNullOrEmpty(variant) && registry.TryGetItem(variant, out var item) && item != null)
                {
                    activeJar.SetInteractableItem(item);
                }
            }

            // Set override return position to jar's table target (no offset)
            var table = activeJar.GetTablePosition();
            returnOverridePos = table != null ? table.position : activeJar.transform.position;
            returnOverrideRot = table != null ? table.rotation : activeJar.transform.rotation;
            hasReturnOverride = true;
        }
        MoveCameraToCollectPose();

        // Ensure focus level = 0 and clear focus stack
        var flm = FocusLevelManager.Instance;
        if (flm != null)
        {
            flm.ResetToStartingLevel();
            if (flm.CurrentNestLevel != 0) flm.SetNestLevel(0);
        }

        // After camera finishes moving, animate bug to jar target with offset, staying parented
        // Allow rotation until camera finishes; then lock and move bug
        collectRotationLocked = false;
        if (camFlyRoutine != null)
        {
            camFlyRoutine.OnComplete(() => { collectRotationLocked = true; StartBugCollectMovement(); });
        }
        else
        {
            collectRotationLocked = true;
            StartBugCollectMovement();
        }
    }

    private void ExitCollectMode(bool restoreCamera = true)
    {
        if (!isCollectMode) return;
        isCollectMode = false;

        // Ensure the jar returns to its place when leaving collect mode
        if (activeJar != null)
        {
            var st = activeJar.GetState();
            if (st == BugJarTrap.State.AtTable)
            {
                activeJar.FlyBack();
            }
            activeJar = null;
        }

        if (restoreCamera && cam != null)
        {
            if (camFlyRoutine != null && camFlyRoutine.IsActive()) camFlyRoutine.Kill();
            camFlyRoutine = StartCamFly(savedCamPos, savedCamRot, savedCamFov, 0.25f);
        }

        ShowInspectHints(ShouldShowRightMouseHint());
    }

    private void StartBugCollectMovement()
    {
        if (go == null || activeJar == null) return;
        Vector3 basePos = activeJar.GetTablePosition() != null
            ? activeJar.GetTablePosition().position
            : activeJar.transform.position;
        Quaternion baseRot = activeJar.GetTablePosition() != null
            ? activeJar.GetTablePosition().rotation
            : go.transform.rotation;
        Vector3 offset = cameraController != null ? cameraController.CollectBugOffset : Vector3.zero;
        Vector3 targetPos = basePos + offset;

        float dur = (activeJar != null) ? Mathf.Max(0.01f, activeJar.FlyDuration * 0.5f) : 0.125f;
        Fly(go.transform, targetPos, baseRot, dur, null);
    }

    private void MoveCameraToCollectPose()
    {
        if (cameraController != null && cameraController.CollectModeCameraPose != null)
        {
            // Enter collect with focus level 0 via FocusLevelManager
            var flm = FocusLevelManager.Instance;
            if (flm != null)
                flm.SetNestLevel(0);

            // Use a fixed transform provided by CameraController (slower tween = 1.0s)
            cameraController.FocusToPoint(cameraController.CollectModeCameraPose, allowReturn: true, flyTimeOverride: 1.0f);
            // Create a local timing tween to signal completion after 1.0s
            if (camFlyRoutine != null && camFlyRoutine.IsActive()) camFlyRoutine.Kill();
            camFlyRoutine = DG.Tweening.DOTween.Sequence().AppendInterval(1.0f);
            return;
        }

        if (cam == null || holdPoint == null) return;

        // Fallback approximation based on holdPoint
        Vector3 offset = -holdPoint.forward * 0.5f + holdPoint.up * 0.2f;
        Vector3 dstPos = holdPoint.position + offset;
        Quaternion dstRot = Quaternion.LookRotation((holdPoint.position - dstPos).normalized, Vector3.up);
        float dstFov = cam.orthographic ? 0f : Mathf.Clamp(savedCamFov <= 0f ? 50f : savedCamFov, 25f, 60f);

        if (camFlyRoutine != null && camFlyRoutine.IsActive()) camFlyRoutine.Kill();
        camFlyRoutine = StartCamFly(dstPos, dstRot, dstFov, 1.0f);
    }

    private DG.Tweening.Tween StartCamFly(Vector3 pos, Quaternion rot, float fov, float time)
    {
        if (cam == null) return null;
        var t = cam.transform;
        var seq = DG.Tweening.DOTween.Sequence();
        seq.Join(t.DOMove(pos, time).SetEase(DG.Tweening.Ease.InOutSine))
           .Join(t.DORotateQuaternion(rot, time).SetEase(DG.Tweening.Ease.InOutSine));
        if (!cam.orthographic)
            seq.Join(cam.DOFieldOfView(fov, time).SetEase(DG.Tweening.Ease.InOutSine));
        return seq;
    }



    private bool TryAcquireJar(out BugJarTrap jar)
    {
        jar = null;

        // Reuse existing active jar if present
        if (activeJar != null)
        {
            jar = activeJar;
            return true;
        }

        if (BugCounter.Instance == null)
        {
            Debug.LogError("[InspectSession] BugCounter.Instance == null");
            return false;
        }
        if (!BugCounter.Instance.HasAnyJars)
        {
            Debug.LogWarning("[InspectSession] Нет доступных банок.");
            return false;
        }
        if (BugJarPool.Instance == null)
        {
            Debug.LogError("[InspectSession] BugJarPool.Instance == null");
            return false;
        }

        jar = BugJarPool.Instance.GetAvailableJar();
        if (jar == null)
        {
            Debug.LogError("[InspectSession] Не удалось взять банку из пула.");
            return false;
        }


        jar.SetTargetBug(go);
        activeJar = jar;
        return true;
    }

    private void TrySummonJarToTable()
    {
        if (activeJar == null)
        {
            if (!TryAcquireJar(out var jar))
            {
                FallbackNoJar();
                return;
            }
            activeJar = jar;
        }

        Debug.Log($"[InspectSession] Target bug '{go.name}' set on jar. Flying jar to TABLE...");
        activeJar.FlyToTable();
    }

    private void FallbackNoJar()
    {

        isCollectMode = false;
        // Keep LMB visible; do not clear all hints to avoid LMB disappearing
        MultiHintController.Instance?.Show(MultiHintController.PanelNames.LeftMouse);
        cameraController?.ExitAllFocus();
    }

    private Transform ResolveBugTablePosition(GameObject bugRoot)
    {
        if (bugRoot == null) return null;

        var mbs = bugRoot.GetComponents<MonoBehaviour>();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var mb in mbs)
        {
            if (mb == null) continue;
            var type = mb.GetType();

            var f1 = type.GetField("tablePosition", flags);
            if (f1 != null && typeof(Transform).IsAssignableFrom(f1.FieldType))
            {
                var val = f1.GetValue(mb) as Transform;
                if (val != null) return val;
            }
            var f2 = type.GetField("TablePosition", flags);
            if (f2 != null && typeof(Transform).IsAssignableFrom(f2.FieldType))
            {
                var val = f2.GetValue(mb) as Transform;
                if (val != null) return val;
            }

            var p1 = type.GetProperty("tablePosition", flags);
            if (p1 != null && typeof(Transform).IsAssignableFrom(p1.PropertyType))
            {
                var val = p1.GetValue(mb, null) as Transform;
                if (val != null) return val;
            }
            var p2 = type.GetProperty("TablePosition", flags);
            if (p2 != null && typeof(Transform).IsAssignableFrom(p2.PropertyType))
            {
                var val = p2.GetValue(mb, null) as Transform;
                if (val != null) return val;
            }
        }

        var child = bugRoot.transform.Find("TablePosition");
        if (child != null) return child;

        return null;
    }
}










