using UnityEngine;
using UnityEngine.InputSystem;

public class CameraRaycaster
{
    private readonly Camera cam;
    private readonly float maxDistance;
    private readonly LayerMask interactMask;
    private readonly bool showDebug;

    public CameraRaycaster(Camera camera, float maxDistance, LayerMask interactMask, bool showDebug = false)
    {
        this.cam = camera;
        this.maxDistance = maxDistance;
        this.interactMask = interactMask;
        this.showDebug = showDebug;
    }


    public RaycastHit? PerformRaycast(GameObject currentFocusedObject, out Ray ray)
    {
        Vector2 mousePosition = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width / 2f, Screen.height / 2f);

        ray = cam.ScreenPointToRay(mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? validHit = null;

        foreach (var hit in hits)
        {
            if (currentFocusedObject != null &&
                (hit.collider.gameObject == currentFocusedObject ||
                 hit.collider.transform.IsChildOf(currentFocusedObject.transform)))
            {
                var interactableOnHit = hit.collider.GetComponentInParent<IInteractable>();
                var inspectableOnHit = hit.collider.GetComponentInParent<IInspectable>();
                var focusableOnHit = hit.collider.GetComponentInParent<IFocusable>();

                if (interactableOnHit != null || inspectableOnHit != null)
                {
                    validHit = hit;
                    break;
                }
                if (focusableOnHit != null && ((MonoBehaviour)focusableOnHit).gameObject != currentFocusedObject)
                {
                    validHit = hit;
                    break;
                }
                continue;
            }
            validHit = hit;
            break;
        }

        return validHit;
    }
}
