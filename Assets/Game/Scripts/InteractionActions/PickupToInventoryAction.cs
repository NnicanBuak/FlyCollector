using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BugData;

public class PickupToInventoryAction : InteractionActionBase
{
    [Header("�������")]
    [SerializeField] private Item item;
    [SerializeField] private int quantity = 1;

    [Header("��������/��������")]
    [SerializeField] private bool waitForAnimator = true;
    [SerializeField] private string waitStateName = "";  // ���� �������, ������� ��������� ����� ������
    [SerializeField] private float fallbackWait = 0.5f;  // ���� �������� ���

    [Header("�������")]
    [SerializeField] private ParticleSystem pickupEffect;
    [SerializeField] private AudioClip pickupSound;

    [Header("��������")]
    [SerializeField] private bool destroyWholeGameObject = true;
    [SerializeField] private GameObject onlyThisObject; // ���� ����� ������� ���������� GO

    [Header("Debug")]
    [SerializeField] private bool showDebug;

    public override IEnumerator Execute(InteractionContext ctx)
    {
        if (InventoryManager.Instance == null || quantity <= 0)
            yield break;

        // Try to get Item dynamically from BugJarTrap
        Item targetItem = TryGetDynamicItem(ctx);

        // Fallback to static item if dynamic loading failed
        if (targetItem == null)
        {
            targetItem = item;
        }

        if (targetItem == null)
        {
            Debug.LogWarning("[PickupToInventoryAction] No Item specified (static or dynamic)");
            yield break;
        }

        // ��������� ���� �� �����
        if (!InventoryManager.Instance.HasSpace(targetItem, quantity))
            yield break;

        // ��������� �������� (���� ����)
        if (waitForAnimator)
        {
            var anim = ctx.Animator ? ctx.Animator : ctx.Transform.GetComponentInChildren<Animator>();
            if (anim && !string.IsNullOrEmpty(waitStateName))
            {
                // ������� �����
                float t = 0f;
                while (t < 2f && !anim.GetCurrentAnimatorStateInfo(0).IsName(waitStateName))
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                // ������� ����������
                if (anim.GetCurrentAnimatorStateInfo(0).IsName(waitStateName))
                {
                    while (anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.99f)
                        yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(fallbackWait);
            }
        }

        // ������/����
        if (pickupEffect) Object.Instantiate(pickupEffect, ctx.Transform.position, Quaternion.identity).Play();
        if (pickupSound)  AudioSource.PlayClipAtPoint(pickupSound, ctx.Transform.position);

        // �������� � ���������
        bool ok = InventoryManager.Instance.AddItem(targetItem, quantity);
        if (showDebug) Debug.Log($"[Act_PickupToInventory] Add {targetItem.itemName} x{quantity} = {ok}");

        // ������� ������
        if (ok)
        {
            if (destroyWholeGameObject)
                Object.Destroy(ctx.GameObject);
            else if (onlyThisObject)
                Object.Destroy(onlyThisObject);
        }
    }

    /// <summary>
    /// Try to load Item dynamically from BugJarTrap's targetBugName.
    /// Uses BugItemRegistry to find Item by bug name with _Variant suffix.
    /// </summary>
    private Item TryGetDynamicItem(InteractionContext ctx)
    {
        // First, check if InteractableObject carries a dynamic Item payload
        if (ctx.Object is InteractableObject io)
        {
            var dyn = io.GetDynamicItem();
            if (dyn != null)
                return dyn;
        }

        // Try to find BugJarTrap on this GameObject or parent
        var jarTrap = ctx.GameObject?.GetComponent<BugCatching.BugJarTrap>();
        if (jarTrap == null && ctx.Transform != null)
        {
            jarTrap = ctx.Transform.GetComponentInParent<BugCatching.BugJarTrap>();
        }

        if (jarTrap == null)
        {
            if (showDebug)
                Debug.Log("[PickupToInventoryAction] No BugJarTrap found, using static Item");
            return null;
        }

        var cachedItem = jarTrap.GetTargetItem();
        if (cachedItem != null)
            return cachedItem;

        string bugName = jarTrap.GetTargetBugName();
        if (string.IsNullOrEmpty(bugName))
        {
            if (showDebug)
                Debug.LogWarning("[PickupToInventoryAction] BugJarTrap has no targetBugName");
            return null;
        }

        var registry = BugItemRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[PickupToInventoryAction] BugItemRegistry not available at runtime!");
            return null;
        }

        var candidates = new List<string>(4);
        candidates.Add(bugName);

        string trimmed = bugName.Replace("(Clone)", "").Trim();
        if (!string.Equals(trimmed, bugName, System.StringComparison.Ordinal))
            candidates.Add(trimmed);

        if (jarTrap.GetTargetBug() != null && jarTrap.GetTargetBug().TryGetComponent<Bug.BugAI>(out var ai))
        {
            string bugType = ai.GetBugType();
            if (!string.IsNullOrWhiteSpace(bugType))
                candidates.Add(bugType);
        }

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (registry.TryGetItem(candidate, out var directItem) && directItem != null)
                return directItem;

            string variantKey = candidate.EndsWith("_Variant", System.StringComparison.OrdinalIgnoreCase)
                ? candidate
                : $"{candidate}_Variant";
            if (registry.TryGetItem(variantKey, out var variantItem) && variantItem != null)
                return variantItem;
        }

        Debug.LogWarning($"[PickupToInventoryAction] Failed to resolve Item in registry for bug '{bugName}'");
        return null;
    }
}
