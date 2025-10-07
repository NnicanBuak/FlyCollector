// InspectToJarRMB.cs — если нужен переход в режим банки по ПКМ (под New Input System)
using UnityEngine;
using UnityEngine.InputSystem;

public class InspectToJarRMB : MonoBehaviour
{
    [SerializeField] private CameraController cam;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private BugJarCatchController jar; // используем контроллер, чтобы регистрировать пойманных

    private void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        cam = FindFirstObjectByType<CameraController>();
        jar = FindFirstObjectByType<BugJarCatchController>();
#else
        cam = FindObjectOfType<CameraController>();
        jar = FindObjectOfType<BugJarCatchController>();
#endif
        if (cam) holdPoint = cam.holdPoint;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || jar == null || holdPoint == null) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            Transform t = holdPoint.childCount > 0 ? holdPoint.GetChild(0) : null;
            if (!t) return;

            bool isBug = t.GetComponent<BugAI>() != null || t.GetComponent<BugMeta>() != null;
            if (!isBug) return;

            jar.CatchBug(t.gameObject);
        }
    }
}