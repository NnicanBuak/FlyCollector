using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DG.Tweening;
using Game.Scripts.Bug;
using BugCatching;
using BugData;
using Game.Scripts.Core;

namespace Game.Scripts.CameraInspect
{
    public class BugInspectSession : InspectSessionCore
    {
        private BugAI targetBugAI;
        private BugJarTrap activeJar;
        private bool isCollectMode;
        private bool sealedByJar;
        private bool sealExitScheduled;
        private bool startedAtFocus0;

        public BugInspectSession(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish)
            : base(go, holdPoint, flyTime, onFinish)
        {
            MaxRotationAngle = 90f;
        }

        public override void Begin()
        {
            targetBugAI = go.GetComponent<BugAI>();

            base.Begin();

            InspectFlip.OnClicked += OnFlipClicked;

            sealedByJar = false;
            sealExitScheduled = false;

            var flm = FocusLevelManager.Instance;
            startedAtFocus0 = (flm == null) ? true : (flm.CurrentNestLevel == 0);
            
            Debug.Log($"[BugInspectSession] Begin() - FocusLevelManager: {(flm != null ? "exists" : "null")}, CurrentNestLevel: {(flm != null ? flm.CurrentNestLevel.ToString() : "N/A")}, startedAtFocus0: {startedAtFocus0}");

            if (targetBugAI != null)
            {
                targetBugAI.DisableAI(true);

                // Для жуков всегда разрешаем collectMode
                Debug.Log($"[BugInspectSession] Bug inspection started (always allows collect mode, startedAtFocus0={startedAtFocus0})");
                
                // Показываем хинты только если НЕ на нулевом уровне (т.е. пришли из фокуса)
                if (!startedAtFocus0)
                {
                    ShowInspectHints(true);
                }
            }
            else
            {
                Debug.Log("[BugInspectSession] targetBugAI is NULL - this should not happen for bugs");
            }
        }

        public override void UpdateInput()
        {
            base.UpdateInput();

            bool lmb = inputHandler != null && inputHandler.IsLeftClickPressed();

            // LMB: вход в CollectMode для жуков (только после окончания анимации fly)
            bool noActiveFly = activeFlyTween == null || !activeFlyTween.IsActive();
            bool parentedToHold = go != null && go.transform.parent == holdPoint;
            bool noDotweenOnGo = go == null || !DG.Tweening.DOTween.IsTweening(go.transform);


            // Добавить в начало условия перехода в CollectMode
            if (lmb && (isAnimating || !noActiveFly || !parentedToHold || !noDotweenOnGo || !initialFlyCompleted))
            {
                Debug.Log(
                    $"[BugInspectSession] CollectMode check failed: isAnimating={isAnimating}, noActiveFly={noActiveFly}, parentedToHold={parentedToHold}, noDotweenOnGo={noDotweenOnGo}, initialFlyCompleted={initialFlyCompleted}");
            }

            if (lmb && targetBugAI != null && !isCollectMode && !isAnimating && noActiveFly && parentedToHold && noDotweenOnGo && initialFlyCompleted)
            {
                Debug.Log("[BugInspectSession] LMB pressed - attempting to enter CollectMode");
                TryEnterCollectModeOrFallback();
                return;
            }

            if (isCollectMode && TryJarDirectInteraction(lmb))
                return;
        }

        protected override void HandleInspectObjectControl()
        {
            HandleObjectRotation();
        }

        public override void EndInspectNow()
        {
            if (camFlyRoutine != null && camFlyRoutine.IsActive())
            {
                Debug.Log("[BugInspectSession] EndInspectNow: Stopping camera animation");
                camFlyRoutine.Kill(complete: false);
                camFlyRoutine = null;
            }

            if (activeJar != null && !sealedByJar)
            {
                Debug.Log(
                    $"[BugInspectSession] EndInspectNow: Returning jar immediately (state={activeJar.GetState()})");

                DG.Tweening.DOTween.Kill(activeJar.transform, complete: false);

                activeJar.FlyBack();
            }

            base.EndInspectNow();
        }

        protected override void CompleteInspectReturn()
        {
            if (isCollectMode)
            {
                if (camFlyRoutine != null && camFlyRoutine.IsActive())
                    camFlyRoutine.Kill(complete: true);


                if (cameraController != null)
                {
                    cameraController.ReturnHomeFromCurrent(cameraController.returnHomeTime);


                    if (runner != null)
                    {
                        runner.StartCoroutine(EnsureCameraReturned());
                    }
                }


                cameraController?.FocusManager?.ForceFinishTopWithoutReturn();

                var flm = FocusLevelManager.Instance;
                if (flm != null && flm.CurrentNestLevel != 0)
                    flm.SetNestLevel(0);

                if (cam != null)
                {
                    DG.Tweening.DOTween.Kill(CamFlyTweenId, complete: false);
                    DG.Tweening.DOTween.Kill(CamFlyTweenId, complete: false);
                }


                if (!sealedByJar && targetBugAI != null)
                {
                    DG.Tweening.DOTween.Kill(go.transform, complete: false);


                    if (go != null && originalParent != null)
                    {
                        go.transform.SetParent(originalParent, true);
                    }


                    var navMeshAgent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navMeshAgent != null)
                    {
                        navMeshAgent.enabled = true;
                    }


                    targetBugAI.DisableAI(false);
                    targetBugAI.enabled = true;


                    var localRb = go.GetComponent<Rigidbody>();
                    if (localRb != null)
                    {
                        localRb.isKinematic = false;
                        localRb.detectCollisions = true;
                    }


                    foreach (var c in go.GetComponentsInChildren<Collider>(true))
                    {
                        if (c != null) c.enabled = true;
                    }


                    if (parentColliders != null && selfColliders != null)
                    {
                        foreach (var collider in parentColliders)
                        {
                            if (collider && System.Array.IndexOf(selfColliders, collider) < 0)
                                collider.enabled = true;
                        }
                    }
                }


                if (!sealedByJar && activeJar != null)
                {
                    Debug.Log($"[BugInspectSession] CompleteInspectReturn: activeJar state = {activeJar.GetState()}");
                    DG.Tweening.DOTween.Kill(activeJar.transform, complete: false);
                }


                ClearHoveredObject();

                if (holdPointLocalPosOverridden && holdPoint != null)
                {
                    holdPoint.localPosition = holdPointOriginalLocalPos;
                    holdPointLocalPosOverridden = false;
                }

                if (holdPoint != null)
                {
                    holdPoint.localRotation = holdPointOriginalLocalRot;
                }

                MultiHintController.Instance?.HideAll();

                if (freezeDuringReturn)
                {
                    InteractionFreeze.Pop();
                    freezeDuringReturn = false;
                }

                isAnimating = false;
                isReturning = false;
                hasReturnOverride = false;

                InspectFlip.OnClicked -= OnFlipClicked;


                if (!sealedByJar)
                {
                    activeJar = null;
                }

                onFinish?.Invoke();
                return;
            }


            BugJarTrap jarForReturn = (isCollectMode && activeJar != null) ? activeJar : null;
            bool wasCollectMode = isCollectMode;

            if (wasCollectMode && jarForReturn != null)
            {
                var table = jarForReturn.GetTablePosition();
                Vector3 pos = table != null ? table.position : jarForReturn.transform.position;
                Quaternion rot = table != null ? table.rotation : jarForReturn.transform.rotation;
                returnOverridePos = pos;
                returnOverrideRot = rot;
                hasReturnOverride = true;
            }

            if (sealedByJar)
            {
                base.CompleteInspectReturn();
                activeJar = null;
                return;
            }

            base.CompleteInspectReturn();

            if (!sealedByJar && activeJar != null)
            {
                Debug.Log($"[BugInspectSession] CompleteInspectReturn: activeJar state = {activeJar.GetState()}");

                DG.Tweening.DOTween.Kill(activeJar.transform, complete: false);

                activeJar = null;
            }

            if (!sealedByJar && targetBugAI != null)
            {
                targetBugAI.DisableAI(false);
                targetBugAI.enabled = true;
            }
        }

        protected override void ShowInspectHints(bool includeRightMouse)
        {
            if (MultiHintController.Instance == null)
                return;

            if (isCollectMode)
            {
                MultiHintController.Instance.Show(MultiHintController.PanelNames.RightMouse);
                return;
            }


            MultiHintController.Instance.Show(MultiHintController.PanelNames.LeftMouse,
                MultiHintController.PanelNames.RightMouse, "InspectFlip");
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


            bool ready = go != null && go.transform.parent == holdPoint &&
                         Vector3.Distance(go.transform.localPosition, Vector3.zero) <= 0.02f &&
                         !DG.Tweening.DOTween.IsTweening(go.transform) && !isAnimating;
            if (!ready)
            {
                Debug.Log(
                    $"[BugInspectSession] EnterCollectMode deferred — not ready yet (isAnimating={isAnimating}, parent={(go != null ? go.transform.parent?.name : "null")}, localPos={(go != null ? go.transform.localPosition.ToString() : "null")}, IsTweening={(go != null ? DG.Tweening.DOTween.IsTweening(go.transform).ToString() : "null")})");
                if (runner != null)
                    runner.StartCoroutine(DelayedEnterCollectMode());
                else
                    Debug.LogWarning("[BugInspectSession] Can't delay EnterCollectMode: runner == null");
                return;
            }

            EnterCollectModeInternal();
        }

        private System.Collections.IEnumerator DelayedEnterCollectMode()
        {
            int ticks = 0;
            while (ticks < 120)
            {
                if (go != null && go.transform.parent == holdPoint &&
                    Vector3.Distance(go.transform.localPosition, Vector3.zero) <= 0.02f &&
                    !DG.Tweening.DOTween.IsTweening(go.transform) && !isAnimating)
                {
                    break;
                }

                ticks++;
                yield return null;
            }

            Debug.Log(
                $"[BugInspectSession] DelayedEnterCollectMode waking up after {ticks} ticks — isAnimating={isAnimating}, parent={(go != null ? go.transform.parent?.name : "null")}, localPos={(go != null ? go.transform.localPosition.ToString() : "null")}");
            EnterCollectModeInternal();
        }

        private void EnterCollectModeInternal()
        {
            isCollectMode = true;
            Debug.Log("[BugInspectSession] EnterCollectMode internal — entering collect mode now.");

            cameraController?.ExitAllFocus(true);


            ShowInspectHints(false);

            TrySummonJarToTable();


            if (activeJar != null)
            {
                activeJar.TriggerOpen();
                var jarItem = activeJar.GetTargetItem();
                if (jarItem != null)
                {
                    activeJar.SetInteractableItem(jarItem);
                }
                else
                {
                    var registry = BugItemRegistry.Instance;
                    if (registry != null)
                    {
                        string bugKey = targetBugAI != null
                            ? targetBugAI.GetBugType()
                            : (go != null ? go.name : string.Empty);
                        if (!string.IsNullOrEmpty(bugKey) && registry.TryGetItem(bugKey, out var item) && item != null)
                        {
                            activeJar.SetInteractableItem(item);
                        }
                        else if (!string.IsNullOrEmpty(bugKey) && Debug.isDebugBuild)
                        {
                            Debug.LogWarning($"[BugInspectSession] BugItemRegistry mapping not found for '{bugKey}'");
                        }
                    }
                }


                var io = activeJar.GetInteractable();
                if (io != null)
                {
                    io.SetCanInteract(true);
                }


                var table = activeJar.GetTablePosition();
                returnOverridePos = table != null ? table.position : activeJar.transform.position;
                returnOverrideRot = table != null ? table.rotation : activeJar.transform.rotation;
                hasReturnOverride = true;
            }

            MoveCameraToCollectPose();


            var flm = FocusLevelManager.Instance;
            if (flm != null)
            {
                if (flm.CurrentNestLevel != 0)
                    flm.SetNestLevel(0);
            }


            System.Action onBothAnimationsComplete = () =>
            {
                ParentBugToJarIfPossible(go.transform);
                StartBugCollectMovement();
            };

            bool cameraAnimationComplete = camFlyRoutine == null || !camFlyRoutine.IsActive();
            bool bugAnimationComplete = !isAnimating;

            if (cameraAnimationComplete && bugAnimationComplete)
            {
                onBothAnimationsComplete();
            }
            else if (cameraAnimationComplete && !bugAnimationComplete)
            {
                WaitForBugAnimation(onBothAnimationsComplete);
            }
            else if (!cameraAnimationComplete && bugAnimationComplete)
            {
                camFlyRoutine.OnComplete(() => onBothAnimationsComplete());
            }
            else
            {
                WaitForBothAnimations(onBothAnimationsComplete);
            }
        }

        private void WaitForBugAnimation(System.Action onComplete)
        {
            if (runner != null)
            {
                runner.StartCoroutine(WaitForBugAnimationCoroutine(onComplete));
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        private void WaitForBothAnimations(System.Action onComplete)
        {
            if (runner != null)
            {
                runner.StartCoroutine(WaitForBothAnimationsCoroutine(onComplete));
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        private System.Collections.IEnumerator WaitForBugAnimationCoroutine(System.Action onComplete)
        {
            while (isAnimating)
            {
                yield return null;
            }

            onComplete?.Invoke();
        }

        private System.Collections.IEnumerator WaitForBothAnimationsCoroutine(System.Action onComplete)
        {
            while (isAnimating)
            {
                yield return null;
            }


            while (camFlyRoutine != null && camFlyRoutine.IsActive())
            {
                yield return null;
            }

            onComplete?.Invoke();
        }

        private void ExitCollectMode()
        {
            if (!isCollectMode) return;
            isCollectMode = false;


            if (activeJar != null)
            {
                Debug.Log($"[BugInspectSession] ExitCollectMode: activeJar state = {activeJar.GetState()}");
                var io = activeJar.GetInteractable();
                if (io != null) io.SetCanInteract(false);

                DG.Tweening.DOTween.Kill(activeJar.transform, complete: false);
                activeJar.TriggerClose();
                activeJar.FlyBack();
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
                var flm = FocusLevelManager.Instance;
                if (flm != null)
                    flm.SetNestLevel(0);


                cameraController.FocusToPoint(cameraController.CollectModeCameraPose, allowReturn: false,
                    flyTimeOverride: 1.0f);

                if (camFlyRoutine != null && camFlyRoutine.IsActive()) camFlyRoutine.Kill();
                camFlyRoutine = DG.Tweening.DOTween.Sequence()
                    .SetId(CamFlyTweenId)
                    .SetTarget(cam).AppendInterval(1.0f);
                return;
            }

            if (cam == null || holdPoint == null) return;


            Vector3 offset = -holdPoint.forward * 0.5f + holdPoint.up * 0.2f;
            Vector3 dstPos = holdPoint.position + offset;
            Quaternion dstRot = Quaternion.LookRotation((holdPoint.position - dstPos).normalized, Vector3.up);
            float dstFov = cam.orthographic ? 0f : Mathf.Clamp(50f, 25f, 60f);

            if (camFlyRoutine != null && camFlyRoutine.IsActive()) camFlyRoutine.Kill();
            camFlyRoutine = StartCamFly(dstPos, dstRot, dstFov, 1.0f);
        }

        private DG.Tweening.Tween StartCamFly(Vector3 pos, Quaternion rot, float fov, float time)
        {
            if (cam == null) return null;
            var t = cam.transform;
            var seq = DG.Tweening.DOTween.Sequence()
                .SetId(CamFlyTweenId)
                .SetTarget(cam);
            seq.Join(t.DOMove(pos, time).SetEase(DG.Tweening.Ease.InOutSine))
                .Join(t.DORotateQuaternion(rot, time).SetEase(DG.Tweening.Ease.InOutSine));
            if (!cam.orthographic)
                seq.Join(cam.DOFieldOfView(fov, time).SetEase(DG.Tweening.Ease.InOutSine));
            return seq;
        }

        private Item ResolveBugRegistryItem(GameObject bug, out string matchedKey)
        {
            matchedKey = null;
            if (bug == null) return null;

            var registry = BugItemRegistry.Instance;
            if (registry == null) return null;

            var candidates = new List<string>(4);
            string bugName = bug.name;
            if (!string.IsNullOrWhiteSpace(bugName))
            {
                candidates.Add(bugName);
                string trimmed = bugName.Replace("(Clone)", "").Trim();
                if (!string.Equals(trimmed, bugName, System.StringComparison.Ordinal))
                    candidates.Add(trimmed);
            }

            if (targetBugAI != null)
            {
                string bugType = targetBugAI.GetBugType();
                if (!string.IsNullOrWhiteSpace(bugType))
                    candidates.Add(bugType);
            }

            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (!seen.Add(candidate)) continue;

                if (registry.TryGetItem(candidate, out var item) && item != null)
                {
                    matchedKey = candidate;
                    return item;
                }
            }

            return null;
        }

        private bool TryAcquireJar(out BugJarTrap jar)
        {
            jar = null;


            if (activeJar != null)
            {
                jar = activeJar;
                return true;
            }

            if (BugCounter.Instance == null)
            {
                Debug.LogError("[BugInspectSession] BugCounter.Instance == null");
                return false;
            }

            if (!BugCounter.Instance.HasAnyJars)
            {
                Debug.LogWarning("[BugInspectSession] Нет доступных банок.");
                return false;
            }

            if (BugJarPool.Instance == null)
            {
                Debug.LogError("[BugInspectSession] BugJarPool.Instance == null");
                return false;
            }


            jar = BugJarPool.Instance.GetAvailableJar();
            if (jar == null)
            {
                Debug.LogWarning("[BugInspectSession] Не удалось получить банку из пула.");
                return false;
            }


            string matchedKey;
            Item matchedItem = ResolveBugRegistryItem(go, out matchedKey);
            jar.SetTargetBug(go, matchedItem, matchedKey);

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

            activeJar.SetSuppressCollectHintOnSeal(true);

            Debug.Log($"[BugInspectSession] Target bug '{go?.name}' set on jar. Flying jar to TABLE...");

            activeJar.FlyToTable();
        }

        private void FallbackNoJar()
        {
            ShowInspectHints(true);
        }

        private bool ShouldShowRightMouseHint()
        {
            if (BugCounter.Instance != null && BugCounter.Instance.HasAnyJars &&
                BugJarPool.Instance != null && BugJarPool.Instance.HasAvailableJars)
                return true;


            if (cameraController != null)
                return !cameraController.IsAtZeroFocus;

            var flm = FocusLevelManager.Instance;
            if (flm != null)
                return flm.CurrentNestLevel > 0;

            return !startedAtFocus0;
        }

        private Transform ResolveBugTablePosition(GameObject bug)
        {
            if (bug == null) return null;

            var mbs = bug.GetComponents<MonoBehaviour>();
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


            var child = bug.transform.Find("TablePosition");
            if (child != null) return child;
            child = bug.transform.Find("tablePosition");
            if (child != null) return child;

            return null;
        }

        private void ParentBugToJarIfPossible(Transform bugTransform)
        {
            if (bugTransform == null || activeJar == null) return;


            Transform jarAttach = ResolveAttachPoint(activeJar);
            if (jarAttach == null) return;


            bugTransform.SetParent(jarAttach, worldPositionStays: true);
        }

        private Transform ResolveAttachPoint(BugJarTrap jar)
        {
            if (jar == null) return null;


            return jar.AttachParent;
        }

        private bool TryJarDirectInteraction(bool lmb)
        {
            if (activeJar == null || !isCollectMode)
                return false;

            var io = activeJar.GetInteractable();
            var jarObject = io != null ? io.gameObject : null;
            if (io == null || !io.isActiveAndEnabled)
            {
                if (currentHoveredObject != null && currentHoveredObject == jarObject)
                    ClearHoveredObject();


                ShowInspectHints(false);
                return false;
            }

            if (cam == null) cam = Camera.main;
            if (cam == null)
                return false;

            bool hoveringJar = false;
            if (inputHandler != null)
            {
                Vector2 pos = inputHandler.GetMousePosition();
                Ray ray = cam.ScreenPointToRay(pos);
                if (Physics.Raycast(ray, out var hit, interactionRange, interactableLayer))
                {
                    if (hit.collider.GetComponentInParent<BugJarTrap>() == activeJar)
                    {
                        hoveringJar = true;
                    }
                    else
                    {
                        var hitInteractable = hit.collider.GetComponentInParent<IInteractable>();
                        if (ReferenceEquals(hitInteractable, io))
                            hoveringJar = true;
                    }
                }
            }
            else
            {
                hoveringJar = true;
            }

            if (!hoveringJar)
            {
                if (currentHoveredObject != null && currentHoveredObject == jarObject)
                    ClearHoveredObject();
                else

                    ShowInspectHints(false);
                return false;
            }

            if (jarObject != currentHoveredObject)
            {
                ClearHoveredObject();
                currentHoveredObject = jarObject;
                currentInteractable = io;
                currentInteractable.OnHoverEnter();
            }


            ShowInspectHints(true);

            if (lmb)
            {
                PrepareBugForSealing();
                io.OnInteract(cam);
                if (activeJar != null && activeJar.GetState() == BugJarTrap.State.Sealing)
                {
                    HandleJarSealed();
                }

                return true;
            }

            return true;
        }

        private void PrepareBugForSealing()
        {
            if (!isCollectMode || go == null)
                return;

            var jar = ResolveActiveJarFallback();
            if (jar == null) return;

            Transform jarAttach = ResolveAttachPoint(jar);
            if (jarAttach == null) return;

            float duration = Mathf.Clamp(jar.FlyDuration * 0.4f, 0.05f, 0.4f);

            if (activeFlyTween != null && activeFlyTween.IsActive())
            {
                activeFlyTween.Kill();
                activeFlyTween = null;
            }

            go.transform.SetParent(jarAttach, worldPositionStays: true);

            Vector3 localTarget = Vector3.zero;
            Quaternion localRotTarget = Quaternion.identity;

            activeFlyTween = DG.Tweening.DOTween.Sequence()
                .SetTarget(go.transform)
                .Join(go.transform.DOLocalMove(localTarget, duration).SetEase(Ease.InOutSine))
                .Join(go.transform.DOLocalRotateQuaternion(localRotTarget, duration).SetEase(Ease.InOutSine))
                .OnComplete(() =>
                {
                    if (go == null) return;
                    go.transform.localPosition = localTarget;
                    go.transform.localRotation = localRotTarget;
                    activeFlyTween = null;
                });


            var ai = go.GetComponent<BugAI>();
            if (ai) ai.enabled = false;

            var localRb = go.GetComponent<Rigidbody>();
            if (localRb)
            {
                localRb.isKinematic = true;
                localRb.detectCollisions = false;
            }

            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        private void HandleJarSealed()
        {
            if (!isCollectMode) return;

            isCollectMode = false;
            sealedByJar = true;

            if (activeJar != null)
            {
                activeJar.TriggerClose();
                var io = activeJar.GetInteractable();
                if (io != null)
                {
                    io.SetCanInteract(false);
                }
            }


            MultiHintController.Instance?.HideAll();


            // Останавливаем анимацию камеры
            if (cam != null)
            {
                DG.Tweening.DOTween.Kill(CamFlyTweenId, complete: false);
            }

            // Останавливаем анимацию жука
            if (activeFlyTween != null && activeFlyTween.IsActive())
            {
                activeFlyTween.Kill(complete: true);
                activeFlyTween = null;
            }

            if (cameraController != null)
            {
                cameraController.ReturnHomeFromCurrent(cameraController.returnHomeTime);


                ScheduleFinishAfterCameraReturn();
            }
            else
            {
                ScheduleFinishAfterSeal();
            }
        }

        private void ScheduleFinishAfterCameraReturn()
        {
            if (sealExitScheduled)
                return;

            sealExitScheduled = true;

            if (runner != null)
            {
                runner.StartCoroutine(FinishAfterCameraReturnRoutine());
            }
            else
            {
                FinishAfterSealImmediate();
            }
        }

        private IEnumerator FinishAfterCameraReturnRoutine()
        {
            // Ждем завершения анимации жука если она еще активна
            float bugAnimTimeout = 2.0f;
            float bugAnimElapsed = 0f;
            while (activeFlyTween != null && activeFlyTween.IsActive() && bugAnimElapsed < bugAnimTimeout)
            {
                bugAnimElapsed += Time.deltaTime;
                yield return null;
            }

            if (activeFlyTween != null && activeFlyTween.IsActive())
            {
                Debug.LogWarning("[BugInspectSession] Bug animation timeout - forcing completion");
                activeFlyTween.Kill(complete: true);
                activeFlyTween = null;
            }

            yield return new WaitForSeconds(0.1f);

            if (cameraController != null)
            {
                float timeout = cameraController.returnHomeTime + 1.0f;
                float elapsed = 0f;

                while (elapsed < timeout)
                {
                    var isReturningHomeField = cameraController.GetType().GetField("isReturningHome",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (isReturningHomeField != null)
                    {
                        bool isReturningHome = (bool)isReturningHomeField.GetValue(cameraController);
                        if (!isReturningHome)
                        {
                            break;
                        }
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }


            EndInspectNow();
        }

        private void FinishAfterSealImmediate()
        {
            EndInspectNow();
        }

        private void ScheduleFinishAfterSeal()
        {
            ScheduleFinishAfterCameraReturn();
        }

        private BugJarTrap ResolveActiveJarFallback()
        {
            return activeJar;
        }

        private IEnumerator EnsureCameraReturned()
        {
            yield return new WaitForSeconds(0.1f);


            if (cameraController != null)
            {
                var isReturningHomeField = cameraController.GetType().GetField("isReturningHome",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (isReturningHomeField != null && !(bool)isReturningHomeField.GetValue(cameraController))
                {
                    cameraController.ReturnHomeFromCurrent(cameraController.returnHomeTime);
                }
            }
        }

        protected new void OnFlipClicked()
        {
            base.OnFlipClicked();

            MultiHintController.Instance?.Show(MultiHintController.PanelNames.LeftMouse,
                MultiHintController.PanelNames.RightMouse, "InspectFlip");
        }
    }
}

