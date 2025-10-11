

using System.Collections;
using UnityEngine;

public class PickupToInventoryAction : InteractionActionBase
{
    [Header("Предмет")]
    [SerializeField] private Item item;
    [SerializeField] private int quantity = 1;

    [Header("Ожидание/Анимация")]
    [SerializeField] private bool waitForAnimator = true;
    [SerializeField] private string waitStateName = "";  // если указать, подождёт окончания этого стейта
    [SerializeField] private float fallbackWait = 0.5f;  // если анимации нет

    [Header("Эффекты")]
    [SerializeField] private ParticleSystem pickupEffect;
    [SerializeField] private AudioClip pickupSound;

    [Header("Удаление")]
    [SerializeField] private bool destroyWholeGameObject = true;
    [SerializeField] private GameObject onlyThisObject; // если нужно удалить конкретный GO

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

        // проверить есть ли место
        if (!InventoryManager.Instance.HasSpace(targetItem, quantity))
            yield break;

        // дождаться анимации (если надо)
        if (waitForAnimator)
        {
            var anim = ctx.Animator ? ctx.Animator : ctx.Transform.GetComponentInChildren<Animator>();
            if (anim && !string.IsNullOrEmpty(waitStateName))
            {
                // подождём входа
                float t = 0f;
                while (t < 2f && !anim.GetCurrentAnimatorStateInfo(0).IsName(waitStateName))
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                // подождём завершения
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

        // эффект/звук
        if (pickupEffect) Object.Instantiate(pickupEffect, ctx.Transform.position, Quaternion.identity).Play();
        if (pickupSound)  AudioSource.PlayClipAtPoint(pickupSound, ctx.Transform.position);

        // добавить в инвентарь
        bool ok = InventoryManager.Instance.AddItem(targetItem, quantity);
        if (showDebug) Debug.Log($"[Act_PickupToInventory] Add {targetItem.itemName} x{quantity} = {ok}");

        // удалить объект
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

        string bugName = jarTrap.GetTargetBugName();
        if (string.IsNullOrEmpty(bugName))
        {
            if (showDebug)
                Debug.LogWarning("[PickupToInventoryAction] BugJarTrap has no targetBugName");
            return null;
        }

        // Find BugItemRegistry in scene
        var registry = Object.FindFirstObjectByType<BugData.BugItemRegistry>();
        if (registry == null)
        {
            Debug.LogWarning("[PickupToInventoryAction] BugItemRegistry not found in scene!");
            return null;
        }

        // Try to get Item with _Variant suffix
        string variantName = $"{bugName}_Variant";
        if (registry.TryGetItem(variantName, out var loadedItem) && loadedItem != null)
        {
            if (showDebug)
                Debug.Log($"[PickupToInventoryAction] Loaded dynamic Item: {variantName}");
            return loadedItem;
        }
        else
        {
            Debug.LogWarning($"[PickupToInventoryAction] Failed to find Item in registry: {variantName}");
            return null;
        }
    }
}
