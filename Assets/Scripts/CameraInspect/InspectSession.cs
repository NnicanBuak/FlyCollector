using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
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


    private bool isReturning;
    private bool isAnimating;


    private float interactionRange = 100f;
    private LayerMask interactableLayer = ~0;

    public InspectSession(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish)
    {
        this.go = go;
        this.holdPoint = holdPoint;
        this.flyTime = flyTime;
        this.onFinish = onFinish;

        mouse = Mouse.current;
        keyboard = Keyboard.current;
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


        bugTablePosition = ResolveBugTablePosition(go);


        if (inspectableObject != null && inspectableObject.UsesDynamicHoldPoint())
        {
            float d = inspectableObject.GetDynamicDistance();
            var lp = holdPoint.localPosition;
            holdPoint.localPosition = new Vector3(lp.x, lp.y, d);
        }

        Quaternion toRot = (inspectableObject != null && inspectableObject.UsesCustomOrientation())
            ? inspectableObject.GetInspectRotation()
            : holdPoint.rotation;


        isAnimating = true;
        InteractionFreeze.Push();

        runner.StartCoroutine(Fly(go.transform, holdPoint.position, toRot, flyTime, () =>
        {

            Quaternion targetRotation = go.transform.rotation;

            go.transform.SetParent(holdPoint, true);


            go.transform.rotation = targetRotation;


            initialInspectRotation = targetRotation;

            isAnimating = false;
            InteractionFreeze.Pop();

            if (targetBugAI != null)
            {
                targetBugAI.DisableAI(true);
                // Show both hints for bugs: panel 0 (LMB - Put) and panel 1 (RMB - Collect)
                MultiHintController.Instance?.Show(0, 1);
            }
            else
            {
                // Show only Put hint for non-bug inspectables: panel 0 (LMB)
                MultiHintController.Instance?.Show(0);
            }
        }));
    }


    public bool UpdateInput(bool exitPressed)
    {
        if (go == null) return false;
        if (isAnimating || (typeof(InteractionFreeze) != null && InteractionFreeze.IsLocked)) return false;


        // Update hints based on inspected object type
        if (targetBugAI != null)
        {
            // Show both hints for bugs: panel 0 (LMB - Put) and panel 1 (RMB - Collect)
            MultiHintController.Instance?.Show(0, 1);
        }
        else
        {
            // Show only Put hint for non-bug inspectables: panel 0 (LMB)
            MultiHintController.Instance?.Show(0);
        }


        HandleInspectRotationFollow();


        bool rmb = mouse != null && mouse.rightButton.wasPressedThisFrame;

        // RMB on bug: collect bug (summon jar to holdPoint, bug stays in holdPoint)
        if (rmb && targetBugAI != null)
        {
            cameraController?.ExitAllFocus();

            TrySummonJarToHoldPoint();

            // End inspect session WITHOUT moving bug (bug stays in holdPoint for jar to collect)
            FinishInspectWithoutMovingBug();
            return false;
        }

        // RMB on non-bug: exit inspect mode
        if (rmb && targetBugAI == null)
        {
            Return();
            return false;
        }


        // ESC: always exit inspect mode
        if (exitPressed)
        {
            Return();
            return false;
        }


        bool lmb = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (lmb && !HandleInteraction())
        {
            Return();
            return false;
        }

        return HandleInteraction();
    }

    private void HandleInspectRotationFollow()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || go == null) return;

        if (Mouse.current != null)
        {
            var mouseViewport = cam.ScreenToViewportPoint(Mouse.current.position.ReadValue());
            float nx = Mathf.Clamp((mouseViewport.x - 0.5f) * 2f, -1f, 1f);
            float ny = Mathf.Clamp((mouseViewport.y - 0.5f) * 2f, -1f, 1f);

            float yaw = nx * 180f;
            float pitch = -ny * 45f;

            Quaternion delta = Quaternion.Euler(pitch, yaw, 0f);
            Quaternion target = initialInspectRotation * delta;

            float s = 10f * Time.deltaTime;
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

    private void Return()
    {
        if (isReturning || go == null || runner == null) return;
        isReturning = true;

        isAnimating = true;
        InteractionFreeze.Push();

        ClearHoveredObject();
        go.transform.SetParent(null, true);

        runner.StartCoroutine(ReturnBoth());
    }

    private IEnumerator ReturnBoth()
    {
        // Return to original position
        Vector3 targetPos = origPos;
        Quaternion targetRot = origRot;

        yield return runner.StartCoroutine(Fly(go.transform, targetPos, targetRot, flyTime, () =>
        {
            go.transform.SetPositionAndRotation(targetPos, targetRot);
            go.transform.localScale = origScale;
        }));

        go.transform.SetParent(originalParent, true);

        // Restore rigidbody
        if (rb)
        {
            rb.isKinematic = false;
        }

        // Re-enable parent colliders
        foreach (var c in parentColliders) if (c && System.Array.IndexOf(selfColliders, c) < 0) c.enabled = true;

        // Re-enable AI
        if (targetBugAI != null)
        {
            targetBugAI.DisableAI(false);
        }

        MultiHintController.Instance?.HideAll();

        isAnimating = false;
        isReturning = false;
        InteractionFreeze.Pop();

        onFinish?.Invoke();
    }

    /// <summary>
    /// Move bug to table position and end inspect session, leaving bug on table waiting to be sealed
    /// </summary>
    private void MoveBugToTableAndFinish()
    {
        if (isReturning || go == null || runner == null) return;
        isReturning = true;

        isAnimating = true;
        InteractionFreeze.Push();

        ClearHoveredObject();
        go.transform.SetParent(null, true);

        runner.StartCoroutine(MoveBugToTableCoroutine());
    }

    private IEnumerator MoveBugToTableCoroutine()
    {
        // Determine table position
        Vector3 targetPos = bugTablePosition != null ? bugTablePosition.position : origPos;
        Quaternion targetRot = bugTablePosition != null ? bugTablePosition.rotation : origRot;

        if (bugTablePosition == null)
        {
            Debug.LogWarning("[InspectSession] bugTablePosition is null! Bug will return to original position.");
        }

        // Fly bug to table
        yield return runner.StartCoroutine(Fly(go.transform, targetPos, targetRot, flyTime, () =>
        {
            go.transform.SetPositionAndRotation(targetPos, targetRot);
            go.transform.localScale = origScale;
        }));

        go.transform.SetParent(originalParent, true);

        // KEEP rigidbody kinematic (bug stays on table)
        if (rb)
        {
            rb.isKinematic = true;
        }

        // Re-enable parent colliders
        foreach (var c in parentColliders) if (c && System.Array.IndexOf(selfColliders, c) < 0) c.enabled = true;

        // KEEP AI disabled (bug waits on table)
        // targetBugAI.DisableAI stays true

        MultiHintController.Instance?.HideAll();

        isAnimating = false;
        isReturning = false;
        InteractionFreeze.Pop();

        Debug.Log($"[InspectSession] Bug '{go.name}' placed on table, waiting for jar to seal.");

        // End inspect session
        onFinish?.Invoke();
    }

    private IEnumerator Fly(Transform t, Vector3 toPos, Quaternion toRot, float time, System.Action after)
    {
        if (t == null) yield break;

        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;
        float elapsed = 0f;

        while (elapsed < time && t != null)
        {
            float k = Mathf.SmoothStep(0f, 1f, elapsed / time);
            t.position = Vector3.Lerp(fromPos, toPos, k);
            t.rotation = Quaternion.Slerp(fromRot, toRot, k);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (t != null)
        {
            t.position = toPos;
            t.rotation = toRot;
        }

        after?.Invoke();
    }

    private void TrySummonJarToTable()
    {
        if (BugCounter.Instance == null)
        {
            Debug.LogError("[InspectSession] BugCounter.Instance == null");
            return;
        }
        if (!BugCounter.Instance.HasAnyJars)
        {
            Debug.LogWarning("[InspectSession] Нет доступных банок.");
            return;
        }
        if (BugJarPool.Instance == null)
        {
            Debug.LogError("[InspectSession] BugJarPool.Instance == null");
            return;
        }

        activeJar = BugJarPool.Instance.GetAvailableJar();
        if (activeJar == null)
        {
            Debug.LogError("[InspectSession] Не удалось взять банку из пула.");
            return;
        }

        // Set target bug BEFORE flying to table
        activeJar.SetTargetBug(go);
        Debug.Log($"[InspectSession] Target bug '{go.name}' set on jar. Flying jar to table...");
        activeJar.FlyToTable();
    }

    private void TrySummonJarToHoldPoint()
    {
        if (BugCounter.Instance == null)
        {
            Debug.LogError("[InspectSession] BugCounter.Instance == null");
            return;
        }
        if (!BugCounter.Instance.HasAnyJars)
        {
            Debug.LogWarning("[InspectSession] Нет доступных банок.");
            return;
        }
        if (BugJarPool.Instance == null)
        {
            Debug.LogError("[InspectSession] BugJarPool.Instance == null");
            return;
        }

        activeJar = BugJarPool.Instance.GetAvailableJar();
        if (activeJar == null)
        {
            Debug.LogError("[InspectSession] Не удалось взять банку из пула.");
            return;
        }

        // Set target bug BEFORE flying to holdPoint
        activeJar.SetTargetBug(go);
        Debug.Log($"[InspectSession] Target bug '{go.name}' set on jar. Flying jar to holdPoint...");

        // Fly jar to holdPoint with "Open" animation
        activeJar.FlyToHoldPoint(holdPoint, "Open");
    }

    private void FinishInspectWithoutMovingBug()
    {
        if (isReturning || go == null) return;
        isReturning = true;

        // Clear hover state
        ClearHoveredObject();

        // Hide hints
        MultiHintController.Instance?.HideAll();

        // Bug stays in holdPoint, jar will handle it via InteractableObject
        Debug.Log($"[InspectSession] Bug '{go.name}' stays in holdPoint, waiting for jar interaction.");

        // End inspect session immediately
        isReturning = false;
        onFinish?.Invoke();
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
