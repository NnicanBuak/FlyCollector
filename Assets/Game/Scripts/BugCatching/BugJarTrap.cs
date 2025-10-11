using System.Collections;
using UnityEngine;
using DG.Tweening;
using BugData;

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

    private AudioSource audioSource;
    private Tween flyTween;
        private const string ANIM_SEAL = "JarSeal";

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

        public void SetTargetBug(GameObject bug)
        {
            targetBug = bug;
            targetBugName = bug != null ? bug.name : null;

            if (showDebug)
            {
                Debug.Log($"[BugJarTrap] Target bug set: {bug?.name}");
            }

            // Update InteractableObject's dynamic Item based on bug name
            UpdateInteractableItemFromRegistry();
        }

        public string GetTargetBugName() => targetBugName;

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


            if (interactableObject != null)
            {
                interactableObject.enabled = false;
            }


            if (animator != null)
            {
                animator.SetTrigger(ANIM_SEAL);
            }


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
            if (string.IsNullOrEmpty(targetBugName) || interactableObject == null) return;

            var registry = FindFirstObjectByType<BugItemRegistry>();
            if (registry == null) return;

            // Prefer variant name
            string baseName = targetBugName.Replace("(Clone)", "").Trim();
            string variantName = baseName + "_Variant";

            if (registry.TryGetItem(variantName, out var item) && item != null)
            {
                SetInteractableItem(item);
                return;
            }

            if (registry.TryGetItem(baseName, out var plainItem) && plainItem != null)
            {
                SetInteractableItem(plainItem);
            }
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
            var bugAI = targetBug.GetComponent<Bug.BugAI>();
            if (bugAI != null)
            {
                string bugType = bugAI.GetBugType();
                float catchTime = bugAI.GetTimeSinceSpawn();
                GAManager.Instance.TrackBugCatchTime(bugType, catchTime);
                GAManager.Instance.TrackBugCaught(bugType);
            }


            var registry = FindFirstObjectByType<BugItemRegistry>();
            if (registry != null && registry.TryGetItem(bugFileName, out var item) && item != null)
            {
                if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AddItem(item, 1);

                    if (showDebug)
                    {
                        Debug.Log($"[BugJarTrap] Added {item.itemName} to inventory");
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
                BugCounter.Instance.DecrementJars();

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
                // Keep showing Put (LMB), hide Collect (RMB)
                MultiHintController.Instance.Show(MultiHintController.PanelNames.LeftMouse);
            }


            yield return new WaitForSeconds(0.2f);

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


            if (BugJarPool.Instance != null)
            {
                BugJarPool.Instance.ReturnJar(this);

                if (showDebug)
                {
                    Debug.Log($"[BugJarTrap] Returned to pool");
                }
            }
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
