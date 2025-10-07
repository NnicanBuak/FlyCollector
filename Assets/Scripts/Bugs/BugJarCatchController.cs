// BugJarCatchController.cs
// (если используешь) добавлена регистрация пойманного жука в CaughtBugsRuntime.
using System.Collections;
using UnityEngine;

public class BugJarCatchController : MonoBehaviour
{
    [Header("Где внутри банки должен оказаться жук")]
    [SerializeField] private Transform insideJarPoint;

    [Header("Время затяжки/сжатия")]
    [SerializeField] private float shrinkTime = 0.4f;

    [Header("Реестр соответствия жуков предметам")]
    [SerializeField] private BugItemRegistry registry;

    [Header("Авто-скрыть CollectHintUI после захвата")]
    [SerializeField] private bool hideCollectHintOnDone = true;

    private GameObject caughtBug;
    private bool isProcessing;

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

        string bugFileName = bug.TryGetComponent(out BugMeta meta) && !string.IsNullOrWhiteSpace(meta.FileName)
            ? meta.FileName
            : bug.name;

        // 1) В инвентарь (если есть маппинг)
        if (registry && registry.TryGetItem(bugFileName, out var item) && item)
        {
            InventoryManager.Instance?.AddItem(item, 1);
        }

        // 2) В лог пойманных (для сцены результатов)
        CaughtBugsRuntime.Instance?.RegisterCaught(bugFileName);

        Destroy(bug);
        caughtBug = null;
        isProcessing = false;

        if (hideCollectHintOnDone && CollectHintUI.Instance)
            CollectHintUI.Instance.Hide();
    }

    public void Cancel()
    {
        if (isProcessing || !caughtBug) return;
        if (caughtBug.TryGetComponent(out Rigidbody rb)) rb.isKinematic = false;
        if (caughtBug.TryGetComponent(out Collider col)) col.enabled = true;
        caughtBug = null;
    }
}
