using UnityEngine;
using DG.Tweening;
using System.Reflection;

namespace Game.Scripts.CameraInspect
{
    public class InspectSessionCore
    {
        protected readonly GameObject go;
        protected readonly Transform holdPoint;
        protected readonly float flyTime;
        protected readonly System.Action onFinish;

        protected Camera cam;
        protected MonoBehaviour runner;
        protected CameraController cameraController;
        protected CameraInputHandler inputHandler;

        protected Transform originalParent;
        protected Vector3 origPos;
        protected Quaternion origRot;
        protected Vector3 origScale;
        protected Vector3 holdPointOriginalLocalPos;
        protected Quaternion holdPointOriginalLocalRot;
        protected bool holdPointLocalPosOverridden;
        protected Quaternion initialInspectRotation;
        protected Quaternion customInspectRotation;
        protected Rigidbody rb;
        protected Collider[] selfColliders;
        protected Collider[] parentColliders;
        protected InspectableObject inspectableObject;

        protected GameObject currentHoveredObject;
        protected IInteractable currentInteractable;

        protected bool isAnimating;
        protected bool initialFlyCompleted;
        protected bool isReturning;
        protected bool freezeDuringReturn;

        protected float interactionRange = 100f;
        protected LayerMask interactableLayer = ~0;

        protected DG.Tweening.Tween camFlyRoutine;
        protected DG.Tweening.Tween activeFlyTween;

        protected bool hasReturnOverride;
        protected Vector3 returnOverridePos;
        protected Quaternion returnOverrideRot;

        protected const string CamFlyTweenId = "InspectSession.camFly";

        protected bool isFlipped = false;
        protected bool showDebugInfo = false;

        protected float maxRotationAngle = 10f;
        protected float rotationSensitivity = 10f;

        protected float baseMoveSensitivity = 0.0005f;
        protected float baseMaxOffset = 1f;
        protected float zoomOffsetScale = 250f;
        protected float positionLerpSpeed = 3f;

        protected Vector2 objectScreenSize = Vector2.one;

        protected Vector3 baseHoldPointPosition;

        public float MaxRotationAngle
        {
            get => maxRotationAngle;
            set => maxRotationAngle = Mathf.Clamp(value, 0f, 180f);
        }

        public float RotationSensitivity
        {
            get => rotationSensitivity;
            set => rotationSensitivity = Mathf.Clamp(value, 0.01f, 1f);
        }

        public InspectSessionCore(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish)
        {
            this.go = go;
            this.holdPoint = holdPoint;
            this.flyTime = flyTime;
            this.onFinish = onFinish;
        }

        public virtual void Begin()
        {
            if (go == null || holdPoint == null)
            {
                Debug.LogError("[InspectSessionCore] go/holdPoint == null");
                onFinish?.Invoke();
                return;
            }

            cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (cam != null) runner = cam.GetComponent<MonoBehaviour>();
            cameraController = Object.FindFirstObjectByType<CameraController>();
            if (runner == null && cameraController != null) runner = cameraController;


            if (cameraController != null)
            {
                var inputHandlerField = cameraController.GetType().GetField("inputHandler",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (inputHandlerField != null)
                {
                    inputHandler = (CameraInputHandler)inputHandlerField.GetValue(cameraController);
                }
            }

            if (runner == null)
            {
                Debug.LogError("[InspectSessionCore] Не найден MonoBehaviour для корутин");
                onFinish?.Invoke();
                return;
            }

            originalParent = go.transform.parent;
            origPos = go.transform.position;
            origRot = go.transform.rotation;
            origScale = go.transform.localScale;


            inspectableObject = go.GetComponent<InspectableObject>();

            initialInspectRotation = go.transform.rotation;
            customInspectRotation = (inspectableObject != null && inspectableObject.UsesCustomOrientation())
                ? inspectableObject.GetInspectRotation()
                : Quaternion.identity;


            InspectFlip.OnClicked += OnFlipClicked;

            rb = go.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;

            selfColliders = go.GetComponents<Collider>();
            foreach (var c in selfColliders)
                if (c)
                    c.enabled = true;

            parentColliders = go.GetComponentsInParent<Collider>(true);
            foreach (var c in parentColliders)
            {
                if (c == null) continue;
                bool isSelf = false;
                for (int i = 0; i < selfColliders.Length; i++)
                    if (selfColliders[i] == c)
                    {
                        isSelf = true;
                        break;
                    }

                if (!isSelf) c.enabled = false;
            }

            Debug.Log($"[InspectSessionCore] Begin inspect for {go.name}");

            holdPointOriginalLocalPos = holdPoint.localPosition;
            holdPointOriginalLocalRot = holdPoint.localRotation;
            holdPointLocalPosOverridden = false;

            if (inspectableObject != null && inspectableObject.UsesDynamicHoldPoint())
            {
                float d = inspectableObject.GetDynamicDistance();
                var lp = holdPoint.localPosition;
                holdPoint.localPosition = new Vector3(lp.x, lp.y, d);
                holdPointLocalPosOverridden = true;
            }

            Quaternion toRot = CalculateInspectRotation();

            if (cam != null)
            {
            }

            isAnimating = true;
            initialFlyCompleted = false;
            bool freezePopped = false;
            InteractionFreeze.Push();

            try
            {
                Fly(go.transform, holdPoint.position, toRot, flyTime, () =>
                {
                    go.transform.SetParent(holdPoint, true);


                    go.transform.localPosition = Vector3.zero;


                    holdPoint.rotation = Quaternion.identity;


                    if (inspectableObject != null && inspectableObject.UsesCustomOrientation())
                    {
                        Quaternion inspectRot = inspectableObject.GetInspectRotation();
                        go.transform.rotation = inspectRot;
                        initialInspectRotation = Quaternion.identity;
                    }
                    else
                    {
                        go.transform.localRotation = Quaternion.identity;
                        initialInspectRotation = Quaternion.identity;
                    }


                    initialFlyCompleted = true;
                    isAnimating = false;
                    InteractionFreeze.Pop();
                    freezePopped = true;


                    CalculateObjectScreenSize();


                    baseHoldPointPosition = holdPoint.position;

                    ShowInspectHints(true);
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InspectSessionCore] Fly() threw an exception: {ex}");
                if (!freezePopped)
                    InteractionFreeze.Pop();
                isAnimating = false;
                initialFlyCompleted = false;
            }
        }

        protected virtual Quaternion CalculateInspectRotation()
        {
            if (inspectableObject == null)
                return go.transform.rotation;


            if (inspectableObject.UsesCustomOrientation())
            {
                return customInspectRotation;
            }
            else
            {
                return CameraOrientationCalculator.CalculateCameraFacingRotation(
                    cam, go.transform, inspectableObject);
            }
        }

        public virtual void UpdateInput()
        {
            if (go == null) return;


            bool rmb = inputHandler != null && inputHandler.IsRightClickPressed();
            if (rmb)
            {
                if (activeFlyTween != null && activeFlyTween.IsActive())
                {
                    Debug.Log("[InspectSessionCore] Cancelling object fly animation on RMB");
                    activeFlyTween.Kill(complete: false);
                    activeFlyTween = null;
                }

                EndInspectNow();
                return;
            }


            if (isAnimating || (typeof(InteractionFreeze) != null && InteractionFreeze.IsLocked)) return;


            if (inputHandler != null)
            {
                float scroll = inputHandler.GetMouseScrollDelta().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float zoomSpeed = 0.1f;
                    float minDistance = .5f;
                    float maxDistance = 2.0f;


                    float currentDistance = holdPoint.localPosition.z;


                    float newDistance = Mathf.Clamp(currentDistance - scroll * zoomSpeed, minDistance, maxDistance);


                    var localPos = holdPoint.localPosition;
                    holdPoint.localPosition = new Vector3(localPos.x, localPos.y, newDistance);


                    if (showDebugInfo)
                    {
                        Debug.Log($"[InspectSessionCore] Zoom distance changed to: {newDistance:F2}");
                    }
                }
            }


            if (inputHandler != null && inputHandler.IsFlipKeyPressed())
            {
                OnFlipClicked();

                ShowInspectHints(false);
            }


            HandleInspectObjectControl();

            bool lmb = inputHandler != null && inputHandler.IsLeftClickPressed();

            if (lmb && !HandleInteraction())
            {
            }
            else
                HandleInteraction();
        }

        protected virtual void HandleInspectObjectControl()
        {
            HandleObjectPosition();
            HandleObjectRotation();
        }

        protected void HandleObjectPosition()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || holdPoint == null || go == null) return;

            if (inputHandler != null)
            {
                float currentZoom = holdPoint.localPosition.z;


                float moveSensitivity = baseMoveSensitivity * Mathf.Pow(1f / currentZoom, 2f);
                float maxOffsetX;
                float maxOffsetY;


                maxOffsetX = baseMaxOffset * zoomOffsetScale * Mathf.Pow(1f / currentZoom, 2f);
                maxOffsetY = baseMaxOffset * zoomOffsetScale * Mathf.Pow(1f / currentZoom, 2f);


                Vector3 baseHoldPointScreenPos = cam.WorldToScreenPoint(baseHoldPointPosition);
                Vector3 mousePos = inputHandler.GetMousePosition();
                Vector3 delta = mousePos - baseHoldPointScreenPos;


                float clampedX = Mathf.Clamp(-delta.x * moveSensitivity, -maxOffsetX, maxOffsetX);
                float clampedY = Mathf.Clamp(-delta.y * moveSensitivity, -maxOffsetY, maxOffsetY);


                Vector3 targetLocalPos = Vector3.zero;
                targetLocalPos.x = clampedX;
                targetLocalPos.y = clampedY;


                go.transform.localPosition = Vector3.Lerp(go.transform.localPosition, targetLocalPos,
                    positionLerpSpeed * Time.deltaTime);
            }
        }

        protected void HandleObjectRotation()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || holdPoint == null) return;


            Vector3 dirToCam = cam.transform.position - holdPoint.position;
            if (dirToCam.sqrMagnitude < 1e-6f)
                dirToCam = cam.transform.forward;

            Quaternion lookAtCamera = Quaternion.LookRotation(-dirToCam.normalized, cam.transform.up);
            Quaternion targetRotation = lookAtCamera;


            if (isFlipped)
                targetRotation *= Quaternion.Euler(0f, 180f, 0f);


            if (inputHandler != null)
            {
                Vector3 holdPointScreenPos = cam.WorldToScreenPoint(holdPoint.position);
                Vector3 mousePos = inputHandler.GetMousePosition();
                Vector3 delta = mousePos - holdPointScreenPos;


                float normalizedX = (delta.x / Screen.width) * 2f;
                float normalizedY = (delta.y / Screen.height) * 2f;


                float rotY = normalizedX * maxRotationAngle;
                float rotX = -normalizedY * maxRotationAngle;


                rotX = Mathf.Clamp(rotX, -maxRotationAngle, maxRotationAngle);
                rotY = Mathf.Clamp(rotY, -maxRotationAngle, maxRotationAngle);


                Quaternion additionalRotation = Quaternion.Euler(rotX, rotY, 0f);


                targetRotation *= additionalRotation;
            }


            holdPoint.rotation =
                Quaternion.Slerp(holdPoint.rotation, targetRotation, rotationSensitivity * Time.deltaTime);
        }

        protected bool HandleInteraction()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            Vector2 mp = inputHandler != null
                ? inputHandler.GetMousePosition()
                : new Vector2(Screen.width / 2f, Screen.height / 2f);
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

                    if (inputHandler != null && inputHandler.IsLeftClickPressed())
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

        protected void ClearHoveredObject()
        {
            if (currentHoveredObject != null && currentInteractable != null)
                currentInteractable.OnHoverExit();

            currentHoveredObject = null;
            currentInteractable = null;
        }

        protected DG.Tweening.Tween Fly(Transform t, Vector3 toPos, Quaternion toRot, float time, System.Action after)
        {
            if (t == null) return null;

            if (activeFlyTween != null && activeFlyTween.IsActive()) activeFlyTween.Kill();
            var seq = DOTween.Sequence()
                .SetId(CamFlyTweenId)
                .SetTarget(cam);
            seq.Join(t.DOMove(toPos, time).SetEase(Ease.InOutSine))
                .Join(t.DORotateQuaternion(toRot, time).SetEase(Ease.InOutSine))
                .OnComplete(() => after?.Invoke());
            activeFlyTween = seq;
            return seq;
        }

        public virtual void EndInspectNow()
        {
            if (isReturning)
                return;

            if (go == null)
            {
                CompleteInspectReturn();
                return;
            }

            isReturning = true;
            isAnimating = true;

            freezeDuringReturn = true;
            InteractionFreeze.Push();

            ClearHoveredObject();
            MultiHintController.Instance?.HideAll();

            Transform target = go.transform;
            target.SetParent(originalParent, true);


            hasReturnOverride = false;

            float returnTime = flyTime > 0f ? flyTime : 0f;

            if (returnTime > 0f && runner != null)
            {
                bool inFocusMode = cameraController != null && cameraController.FocusManager != null &&
                                   cameraController.FocusManager.FocusDepth > 0;
                if (!inFocusMode && cameraController != null)
                {
                    cameraController.ReturnHomeFromCurrent(returnTime);
                }

                Vector3 dstPos = hasReturnOverride ? returnOverridePos : origPos;
                Quaternion dstRot = hasReturnOverride ? returnOverrideRot : origRot;
                Fly(target, dstPos, dstRot, returnTime, CompleteInspectReturn);
            }
            else
            {
                CompleteInspectReturn();
            }
        }

        protected virtual void CompleteInspectReturn()
        {
            ClearHoveredObject();


            var interactables = Object.FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);
            foreach (var obj in interactables)
            {
                obj.ForceUnlock();
            }

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


                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    if (c != null) c.enabled = true;
                }
            }

            if (holdPointLocalPosOverridden && holdPoint != null)
            {
                holdPoint.localPosition = holdPointOriginalLocalPos;
                holdPointLocalPosOverridden = false;
            }


            if (holdPoint != null)
            {
                holdPoint.localRotation = holdPointOriginalLocalRot;
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

            MultiHintController.Instance?.HideAll();

            if (freezeDuringReturn)
            {
                InteractionFreeze.Pop();
                freezeDuringReturn = false;
            }


            var freezeType = System.Type.GetType("InteractionFreeze");
            if (freezeType != null)
            {
                var isLockedProp = freezeType.GetProperty("IsLocked",
                    BindingFlags.Static | BindingFlags.Public);
                var popMethod = freezeType.GetMethod("Pop",
                    BindingFlags.Static | BindingFlags.Public);

                if (isLockedProp != null && popMethod != null)
                {
                    while ((bool)isLockedProp.GetValue(null))
                    {
                        popMethod.Invoke(null, null);
                    }
                }
            }

            isAnimating = false;
            isReturning = false;
            initialFlyCompleted = false;


            hasReturnOverride = false;


            InspectFlip.OnClicked -= OnFlipClicked;
            onFinish?.Invoke();
        }

        protected virtual void ShowInspectHints(bool includeRightMouse)
        {
            if (MultiHintController.Instance == null)
                return;


            MultiHintController.Instance.Show(MultiHintController.PanelNames.RightMouse, "InspectFlip");
        }

        protected void OnFlipClicked()
        {
            isFlipped = !isFlipped;


            if (inspectableObject != null)
            {
                inspectableObject.SetFlipState(isFlipped);
            }

            Debug.Log($"[InspectSessionCore] Flip состояние изменено: {isFlipped}");
        }

        protected void CalculateObjectScreenSize()
        {
            if (cam == null || go == null)
            {
                objectScreenSize = Vector2.one;
                return;
            }


            Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
            bool hasBounds = false;

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                foreach (var renderer in renderers)
                {
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            else
            {
                var colliders = go.GetComponentsInChildren<Collider>();
                foreach (var collider in colliders)
                {
                    if (!hasBounds)
                    {
                        bounds = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                objectScreenSize = Vector2.one;
                return;
            }


            Vector3[] corners = new Vector3[8];
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            corners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
            corners[1] = center + new Vector3(extents.x, -extents.y, -extents.z);
            corners[2] = center + new Vector3(-extents.x, extents.y, -extents.z);
            corners[3] = center + new Vector3(extents.x, extents.y, -extents.z);
            corners[4] = center + new Vector3(-extents.x, -extents.y, extents.z);
            corners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
            corners[6] = center + new Vector3(-extents.x, extents.y, extents.z);
            corners[7] = center + new Vector3(extents.x, extents.y, extents.z);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (var corner in corners)
            {
                Vector3 screenPoint = cam.WorldToScreenPoint(corner);
                min.x = Mathf.Min(min.x, screenPoint.x);
                min.y = Mathf.Min(min.y, screenPoint.y);
                max.x = Mathf.Max(max.x, screenPoint.x);
                max.y = Mathf.Max(max.y, screenPoint.y);
            }


            Vector2 screenSizePixels = max - min;


            objectScreenSize = new Vector2(
                screenSizePixels.x / Screen.width,
                screenSizePixels.y / Screen.height
            );

            if (showDebugInfo)
            {
                Debug.Log(
                    $"[InspectSessionCore] Object screen size: {objectScreenSize}, aspect ratio: {objectScreenSize.x / objectScreenSize.y:F2}");
            }
        }
    }
}