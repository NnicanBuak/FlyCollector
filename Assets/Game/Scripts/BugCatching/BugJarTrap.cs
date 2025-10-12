using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using BugData;
using Bug;

namespace BugCatching
{
    public class BugJarTrap : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Positions")]
        [Tooltip("Target position on table where jar flies to")]
        [SerializeField] private Transform tablePosition;

        [Header("Animation")]
        [Tooltip("Time for jar to fly to/from table")]
        [SerializeField] private float flyTime = 0.5f;

        [Tooltip("Animator for jar animations (optional)")]
        [SerializeField] private Animator animator;

        [Header("Interaction")]
        [Tooltip("InteractableObject that gets enabled when jar is at table")]
        [SerializeField] private InteractableObject interactableObject;

        [Header("Audio")]
        [Tooltip("Sound when jar flies to table")]
        [SerializeField] private AudioClip flyToTableSound;

        [Tooltip("Sound when jar flies back")]
        [SerializeField] private AudioClip flyBackSound;

        [Tooltip("Sound when jar is sealed")]
        [SerializeField] private AudioClip sealSound;

        [SerializeField] private float soundVolume = 1f;

        [Header("Debug")]
        [SerializeField] private bool showDebug = false;
        #endregion

        #region Properties
        public enum State
        {
            Idle,
            FlyingToTable,
            AtTable,
            Sealing,
            Sealed,
            FlyingBack
        }
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        private State currentState = State.Idle;

        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Transform originalParent;

        private GameObject targetBug;
        private string targetBugName;
        private Item targetItem;
        private string targetItemKey;
        private bool consumed;
        private bool suppressCollectHintOnSeal;

        private AudioSource audioSource;
        private Tween flyTween;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            if (interactableObject != null)
            {
                interactableObject.enabled = false;
            }

            consumed = false;
        }

        private void Start()
        {
            // Save original position AFTER Unity applies prefab overrides from scene
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalParent = transform.parent;

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] {name} initialized at {originalPosition}");
            }
        }
        #endregion

        #region Public Methods
        public float FlyDuration => flyTime;

        public void TriggerOpen()
        {
            if (animator != null)
            {
                animator.SetTrigger("Open");
            }
        }

        public void TriggerClose()
        {
            if (animator != null)
            {
                animator.SetTrigger("Close");
            }
        }

        public void SetTargetBug(GameObject bug, Item item = null, string itemKey = null)
        {
            targetBug = bug;
            targetBugName = bug != null ? bug.name : null;
            targetItem = item;
            targetItemKey = itemKey;
            consumed = false;

            if (bug != null && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Target bug set: {bug?.name}");
            }

            if (targetItem != null)
            {
                SetInteractableItem(targetItem);
            }
            else
            {
                // Update InteractableObject's dynamic Item based on bug name
                UpdateInteractableItemFromRegistry();
            }
        }

        public string GetTargetBugName() => targetBugName;
        public Item GetTargetItem() => targetItem;

        public void FlyToTable()
        {
            if (currentState != State.Idle)
            {
                Debug.LogWarning($"[BugJarTrap] Cannot fly to table - current state: {currentState}");
                return;
            }

            if (tablePosition == null)
            {
                Debug.LogError($"[BugJarTrap] tablePosition is null!");
                return;
            }

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Flying to table: {tablePosition.position}");
            }

            currentState = State.FlyingToTable;
            


            if (flyToTableSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(flyToTableSound, soundVolume);
            }


            if (flyTween != null && flyTween.IsActive()) flyTween.Kill();
            flyTween = DG.Tweening.DOTween.Sequence()
                .Join(transform.DOMove(tablePosition.position, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .Join(transform.DORotateQuaternion(tablePosition.rotation, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .OnComplete(() =>
                {
                    if (showDebug)
                    {
                        Debug.Log($"[BugJarTrap] Arrived at table");
                    }
                    currentState = State.AtTable;
                    if (interactableObject != null)
                    {
                        interactableObject.enabled = true;
                        if (showDebug)
                        {
                            Debug.Log($"[BugJarTrap] InteractableObject enabled");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[BugJarTrap] InteractableObject is null - cannot enable interaction!");
                    }
                });
        }

        /// <summary>
        /// Fly jar to holdPoint (where bug is held) instead of table.
        /// Used when collecting bugs from inspect mode.
        /// </summary>
        public void FlyToHoldPoint(Transform holdPoint, string animTrigger = "Open")
        {
            if (currentState != State.Idle)
            {
                Debug.LogWarning($"[BugJarTrap] Cannot fly to holdPoint - current state: {currentState}");
                return;
            }

            if (holdPoint == null)
            {
                Debug.LogError($"[BugJarTrap] holdPoint is null!");
                return;
            }

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Flying to holdPoint: {holdPoint.position}, trigger: {animTrigger}");
            }

            currentState = State.FlyingToTable; // reuse same state


            if (animator != null && !string.IsNullOrEmpty(animTrigger))
            {
                animator.SetTrigger(animTrigger);
            }


            if (flyToTableSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(flyToTableSound, soundVolume);
            }


            if (flyTween != null && flyTween.IsActive()) flyTween.Kill();
            flyTween = DG.Tweening.DOTween.Sequence()
                .Join(transform.DOMove(holdPoint.position, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .Join(transform.DORotateQuaternion(holdPoint.rotation, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .OnComplete(() =>
                {
                    if (showDebug)
                    {
                        Debug.Log($"[BugJarTrap] Arrived at holdPoint");
                    }
                    currentState = State.AtTable;
                    if (interactableObject != null)
                    {
                        interactableObject.enabled = true;
                        if (showDebug)
                        {
                            Debug.Log($"[BugJarTrap] InteractableObject enabled at holdPoint");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[BugJarTrap] InteractableObject is null - cannot enable interaction!");
                    }
                });
        }

        public void FlyBack()
        {
            if (currentState != State.AtTable)
            {
                Debug.LogWarning($"[BugJarTrap] Cannot fly back - current state: {currentState}");
                return;
            }

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Flying back to original position");
            }

            currentState = State.FlyingBack;


            if (interactableObject != null)
            {
                interactableObject.enabled = false;
            }


            if (flyBackSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(flyBackSound, soundVolume);
            }


            if (flyTween != null && flyTween.IsActive()) flyTween.Kill();
            flyTween = DG.Tweening.DOTween.Sequence()
                .Join(transform.DOMove(originalPosition, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .Join(transform.DORotateQuaternion(originalRotation, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .OnComplete(() =>
                {
                    if (showDebug)
                    {
                        Debug.Log($"[BugJarTrap] Returned to original position");
                    }
                    ResetToIdle();
                });
        }

        public void Seal()
        {
            if (currentState != State.AtTable)
            {
                Debug.LogWarning($"[BugJarTrap] Cannot seal - current state: {currentState}");
                return;
            }

            if (targetBug == null)
            {
                Debug.LogError($"[BugJarTrap] Cannot seal - no target bug!");
                return;
            }

            if (showDebug)
            {
            Debug.Log($"[BugJarTrap] Sealing jar with bug: {targetBug.name}");
        }

            currentState = State.Sealing;
            consumed = true;

            if (BugCounter.Instance != null)
            {
                BugCounter.Instance.DecrementJars();
            }

            if (BugJarPool.Instance != null)
            {
                BugJarPool.Instance.ConsumeJar(this);
            }

            if (interactableObject != null)
            {
                interactableObject.SetCanInteract(false);
            }

            TriggerClose();


            if (sealSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(sealSound, soundVolume);
            }


            StartCoroutine(SealCoroutine());
        }

        public State GetState() => currentState;

        public GameObject GetTargetBug() => targetBug;

        public Transform GetTablePosition() => tablePosition;

        public InteractableObject GetInteractable() => interactableObject;

        public void SetInteractableItem(Item item)
        {
            if (interactableObject != null)
            {
                interactableObject.SetDynamicItem(item);
            }
        }

        /// <summary>
        /// Resolve Item ScriptableObject by targetBugName via BugItemRegistry and assign to InteractableObject.
        /// Tries both "<bug>_Variant" and plain "<bug>" names.
        /// </summary>
        public void UpdateInteractableItemFromRegistry()
        {
            if (interactableObject == null) return;

            if (targetItem != null)
            {
                SetInteractableItem(targetItem);
                return;
            }

            if (string.IsNullOrEmpty(targetBugName)) return;

            var registry = BugItemRegistry.Instance;
            if (registry == null) return;

            string lookupKey = targetBugName;
            if (targetBug != null)
            {
                var bugAI = targetBug.GetComponent<BugAI>();
                if (bugAI != null)
                    lookupKey = bugAI.GetBugType();
            }

            if (!string.IsNullOrEmpty(lookupKey) && registry.TryGetItem(lookupKey, out var item) && item != null)
            {
                targetItem = item;
                targetItemKey = lookupKey;
                SetInteractableItem(item);
            }
            else
            {
                string baseName = targetBugName.Replace("(Clone)", "").Trim();
                if (registry.TryGetItem(baseName, out var fallback) && fallback != null)
                {
                    targetItem = fallback;
                    targetItemKey = baseName;
                    SetInteractableItem(fallback);
                }
            }
        }

        public void SetSuppressCollectHintOnSeal(bool suppress)
        {
            suppressCollectHintOnSeal = suppress;
        }
        #endregion

        #region Private Methods
        private IEnumerator FlyToTableCoroutine()
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            Vector3 targetPos = tablePosition.position;
            Quaternion targetRot = tablePosition.rotation;

            float elapsed = 0f;

            while (elapsed < flyTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / flyTime);

                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                elapsed += Time.deltaTime;
                yield return null;
            }


            transform.position = targetPos;
            transform.rotation = targetRot;

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Arrived at table");
            }


            currentState = State.AtTable;


            if (interactableObject != null)
            {
                interactableObject.enabled = true;

                if (showDebug)
                {
                    Debug.Log($"[BugJarTrap] InteractableObject enabled");
                }
            }
            else
            {
                Debug.LogWarning($"[BugJarTrap] InteractableObject is null - cannot enable interaction!");
            }
        }

        private IEnumerator FlyToHoldPointCoroutine(Transform holdPoint)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            Vector3 targetPos = holdPoint.position;
            Quaternion targetRot = holdPoint.rotation;

            float elapsed = 0f;

            while (elapsed < flyTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / flyTime);

                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                elapsed += Time.deltaTime;
                yield return null;
            }


            transform.position = targetPos;
            transform.rotation = targetRot;

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Arrived at holdPoint");
            }


            currentState = State.AtTable;


            if (interactableObject != null)
            {
                interactableObject.enabled = true;

                if (showDebug)
                {
                    Debug.Log($"[BugJarTrap] InteractableObject enabled at holdPoint");
                }
            }
            else
            {
                Debug.LogWarning($"[BugJarTrap] InteractableObject is null - cannot enable interaction!");
            }
        }

        private IEnumerator FlyBackCoroutine()
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            float elapsed = 0f;

            while (elapsed < flyTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / flyTime);

                transform.position = Vector3.Lerp(startPos, originalPosition, t);
                transform.rotation = Quaternion.Slerp(startRot, originalRotation, t);

                elapsed += Time.deltaTime;
                yield return null;
            }


            transform.position = originalPosition;
            transform.rotation = originalRotation;

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Returned to original position");
            }


            ResetToIdle();
        }

        private IEnumerator SealCoroutine()
        {

            yield return new WaitForSeconds(0.5f);

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Seal complete, processing bug collection...");
            }

            // Track bug catch time for analytics
            string bugFileName = targetBug.name;
            var bugAI = targetBug.GetComponent<BugAI>();
            if (bugAI != null)
            {
                string bugType = bugAI.GetBugType();
                float catchTime = bugAI.GetTimeSinceSpawn();
                GAManager.Instance.TrackBugCatchTime(bugType, catchTime);
                GAManager.Instance.TrackBugCaught(bugType);
            }


            var registry = BugItemRegistry.Instance;
            Item resolvedItem = targetItem;
            string resolvedKey = targetItemKey;

            if (resolvedItem == null && registry != null)
            {
                // Build a list of lookup variants to maximize hit rate.
                var tryKeys = new List<string>(4);

                if (!string.IsNullOrWhiteSpace(bugFileName))
                {
                    tryKeys.Add(bugFileName);

                    // Trim common runtime suffixes/prefixes.
                    var cloneTrimmed = bugFileName.Replace("(Clone)", "").Trim();
                    if (!string.Equals(cloneTrimmed, bugFileName, System.StringComparison.Ordinal))
                        tryKeys.Add(cloneTrimmed);

                    var variantTrimmed = cloneTrimmed.Replace("_Variant", "", System.StringComparison.OrdinalIgnoreCase).TrimEnd('_');
                    if (!string.Equals(variantTrimmed, cloneTrimmed, System.StringComparison.Ordinal))
                        tryKeys.Add(variantTrimmed);
                }

                if (bugAI != null)
                {
                    string bugType = bugAI.GetBugType();
                    if (!string.IsNullOrWhiteSpace(bugType))
                        tryKeys.Add(bugType);
                }

                foreach (var key in tryKeys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (registry.TryGetItem(key, out resolvedItem) && resolvedItem != null)
                    {
                        resolvedKey = key;
                        break;
                    }
                }
            }

            if (resolvedItem != null)
            {
                targetItem = resolvedItem;
                targetItemKey = resolvedKey;
                if (interactableObject != null)
                {
                    interactableObject.SetDynamicItem(resolvedItem);
                }

                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(resolvedItem, 1);

                    if (showDebug)
                    {
                        Debug.Log($"[BugJarTrap] Added {resolvedItem.itemName} to inventory");
                    }
                }
                else
                {
                    Debug.LogWarning($"[BugJarTrap] InventoryManager.Instance is null!");
                }
            }
            else
            {
                Debug.LogWarning($"[BugJarTrap] Could not find Item for bug: {bugFileName}");
            }


            if (CaughtBugsRuntime.Instance != null)
            {
                CaughtBugsRuntime.Instance.RegisterCaught(bugFileName);
            }


            if (BugCounter.Instance != null)
            {
                if (showDebug)
                {
                    Debug.Log($"[BugJarTrap] Jar counter decremented: {BugCounter.Instance.CurrentJars} remaining");
                }
            }


            if (targetBug != null)
            {
                Destroy(targetBug);

                if (showDebug)
                {
                    Debug.Log($"[BugJarTrap] Bug GameObject destroyed");
                }
            }


            if (MultiHintController.Instance != null)
            {
                if (suppressCollectHintOnSeal)
                {
                    MultiHintController.Instance.HideAll();
                }
                else
                {
                    // Keep showing Put (LMB), hide Collect (RMB)
                    MultiHintController.Instance.Show(MultiHintController.PanelNames.LeftMouse);
                }
            }


            yield return new WaitForSeconds(0.2f);

            if (consumed)
            {
                currentState = State.Sealed;
                if (interactableObject != null)
                {
                    interactableObject.SetCanInteract(true);
                }
                yield break;
            }

            currentState = State.FlyingBack;

            if (flyBackSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(flyBackSound, soundVolume);
            }


            // Start tweened fly-back and wait until it completes
            if (flyTween != null && flyTween.IsActive()) flyTween.Kill();
            flyTween = DG.Tweening.DOTween.Sequence()
                .Join(transform.DOMove(originalPosition, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .Join(transform.DORotateQuaternion(originalRotation, flyTime).SetEase(DG.Tweening.Ease.InOutSine))
                .OnComplete(() =>
                {
                    if (showDebug)
                    {
                        Debug.Log($"[BugJarTrap] Returned to original position");
                    }
                    ResetToIdle();
                });
            while (flyTween != null && flyTween.IsActive())
                yield return null;
        }

        private void ResetToIdle()
        {
            currentState = State.Idle;
            targetBug = null;
            targetBugName = null;
            targetItem = null;
            targetItemKey = null;
            consumed = false;
            suppressCollectHintOnSeal = false;

            if (interactableObject != null)
            {
                interactableObject.SetDynamicItem(null);
            }

            if (BugJarPool.Instance != null)
            {
                BugJarPool.Instance.ReturnJar(this);

                if (showDebug)
                {
                    Debug.Log($"[BugJarTrap] Returned to pool");
                }
            }
        }

        private void FinalizeConsumedJar()
        {
            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Consuming jar {name} (removed from pool)");
            }

            if (BugJarPool.Instance != null)
            {
                BugJarPool.Instance.ConsumeJar(this);
            }

            if (interactableObject != null)
            {
                interactableObject.enabled = false;
                interactableObject.SetDynamicItem(null);
            }

            if (flyTween != null && flyTween.IsActive())
            {
                flyTween.Kill();
                flyTween = null;
            }

            transform.SetParent(originalParent, true);
            transform.position = originalPosition;
            transform.rotation = originalRotation;

            currentState = State.Idle;
            targetBug = null;
            targetBugName = null;
            targetItem = null;
            targetItemKey = null;
            consumed = false;
            suppressCollectHintOnSeal = false;

            gameObject.SetActive(false);
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (tablePosition != null)
            {

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, tablePosition.position);


                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(tablePosition.position, 0.1f);


                Gizmos.color = Color.red;
                Vector3 forward = tablePosition.forward * 0.3f;
                Gizmos.DrawRay(tablePosition.position, forward);
            }
        }
        #endregion
    }
}
