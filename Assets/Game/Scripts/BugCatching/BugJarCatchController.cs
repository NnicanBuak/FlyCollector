using System.Collections;
using UnityEngine;
using BugData;

namespace BugCatching
{
    public class BugJarCatchController : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Где внутри банки должен оказаться жук")]
        [SerializeField] private Transform insideJarPoint;

        [Header("Время затяжки/сжатия")]
        [SerializeField] private float shrinkTime = 0.4f;

        [Header("Реестр соответствия жуков предметам")]
        [SerializeField] private BugItemRegistry registry;

        [Header("Авто-скрыть HintUI после захвата")]
        [SerializeField] private bool hideHintOnDone = true;

        [Header("Счетчик банок")]

        [Tooltip("Автоматически декрементировать BugCounter при сборе (если false, вызывающий код должен сделать это)")]
        [SerializeField] private bool autoDecrementJarCounter = false;

        [Header("Debug")]
        [SerializeField] private bool showDebug = false;
        #endregion

        #region Properties
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        private GameObject caughtBug;
        private bool isProcessing;
        #endregion

        #region Public Methods
        public void CatchBug(GameObject bugGO)
        {
            if (isProcessing || !bugGO) return;
            caughtBug = bugGO;
            if (bugGO.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
            if (bugGO.TryGetComponent(out Collider col)) col.enabled = false;
        }

        public void ConfirmCloseJar()
        {
            if (isProcessing || !caughtBug || !insideJarPoint) return;
            StartCoroutine(SuckIntoJarAndAddToInventory());
        }

        public void Cancel()
        {
            if (isProcessing || !caughtBug) return;
            if (caughtBug.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;
            if (caughtBug.TryGetComponent(out Collider col)) col.enabled = true;
            caughtBug = null;
        }
        #endregion

        #region Private Methods
        private IEnumerator SuckIntoJarAndAddToInventory()
        {
            isProcessing = true;
            var bug = caughtBug;

            Vector3 startPos = bug.transform.position;
            Quaternion startRot = bug.transform.rotation;
            Vector3 startScale = bug.transform.localScale;

            float t = 0f;
            while (t < shrinkTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / shrinkTime);
                bug.transform.position = Vector3.Lerp(startPos, insideJarPoint.position, k);
                bug.transform.rotation = Quaternion.Slerp(startRot, insideJarPoint.rotation, k);
                bug.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
                t += Time.deltaTime;
                yield return null;
            }

            string bugFileName = bug.name;


            if (registry && registry.TryGetItem(bugFileName, out var item) && item)
            {
                InventoryManager.Instance?.AddItem(item, 1);
            }


            CaughtBugsRuntime.Instance?.RegisterCaught(bugFileName);


            if (autoDecrementJarCounter && BugCounter.Instance != null)
            {
                BugCounter.Instance.DecrementJars();
                if (showDebug)
                    Debug.Log($"[BugJarCatchController] Jar counter decremented: {BugCounter.Instance.CurrentJars} remaining");
            }

            Destroy(bug);
            caughtBug = null;
            isProcessing = false;

            if (hideHintOnDone && MultiHintController.Instance)
                MultiHintController.Instance.HideAll();
        }
        #endregion

        #region Gizmos
        #endregion
    }
}
