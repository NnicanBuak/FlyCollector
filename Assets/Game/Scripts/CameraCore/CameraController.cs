using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using Bug;
using BugCatching;

public class CameraController : MonoBehaviour
{
    [Header("Raycast")]
    public float maxDistance = 30f;
    public LayerMask interactMask = ~0;

    [Header("Inspect (object to camera)")]
    public Transform holdPoint;
    public float inspectFlyTime = 0.25f;

    [Header("Focus (camera to object)")]
    public float focusFlyTime = 0.5f;
    public float focusRotationSpeed = 2f;

    [Header("Gameplay")]
    [SerializeField] private BugJarCatchController bugJarCatchController;
    public BugJarCatchController BugJarCatchController => bugJarCatchController;

    [Header("Mouse Follow")]
    [Tooltip("Enable camera rotation following mouse cursor")]
    public bool enableMouseFollow = true;
    [Tooltip("Maximum camera rotation angle (degrees)")]
    public float mouseFollowAmount = 3f;
    [Tooltip("Mouse follow speed")]
    public float mouseFollowSpeed = 3f;

    [Header("Push Settings")]
    public float pushForce = 5f;
    public AudioClip pushDefaultSound;
    [Range(0f, 1f)] public float pushSoundVolume = 1f;
    public bool playPushSound = true;
    public CameraPushSystem.PushMode pushMode = CameraPushSystem.PushMode.OnHover;
    public CameraPushSystem.PushButton pushButton = CameraPushSystem.PushButton.RightClick;
    [Range(0f, 1f)] public float pushCooldown = 0.3f;

    [Header("Nest Level System")]
    public bool warnUnavailableFocus = true;
    public AudioClip unavailableSound;

    [Header("Events (Inspector)")]
    public BugAIEvent BugAIInspected;
    public BugAIWithFocusEvent BugAIInspectedWhileFocused;

    [Header("Debug")]
    public bool showDebugInfo = true;


    private Camera cam;
    private AudioSource audioSource;
    private CameraRaycaster raycaster;
    private CameraInputHandler inputHandler;
    private CameraMouseFollow mouseFollow;
    private CameraPushSystem pushSystem;
    private CameraFocusManager focusManager;
    private CameraInspectManager inspectManager;
    private CameraHoverState hoverState;


    private Vector3 cameraReturnPos;
    private Quaternion cameraReturnRot;


    [System.Serializable]
    public class BugAIEvent : UnityEvent<BugAI> { }
    [System.Serializable]
    public class BugAIWithFocusEvent : UnityEvent<BugAI, GameObject> { }

    public event System.Action<BugAI> OnBugAIInspected;
    public event System.Action<BugAI, GameObject> OnBugAIInspectedWhileFocused;
    public event System.Action InspectEnded;

    public bool IsAtZeroFocus => focusManager != null && focusManager.IsAtZeroFocus;

    void Awake()
    {
        cam = Camera.main;
        audioSource = GetComponent<AudioSource>();

        if (cam == null)
            Debug.LogError("[CameraController] Main camera not found!");


        raycaster = new CameraRaycaster(cam, maxDistance, interactMask, showDebugInfo);
        inputHandler = new CameraInputHandler();
        mouseFollow = new CameraMouseFollow(cam, mouseFollowAmount, mouseFollowSpeed);
        pushSystem = new CameraPushSystem(pushForce, pushDefaultSound, pushSoundVolume, audioSource, playPushSound, pushCooldown);
        focusManager = new CameraFocusManager(cam, focusFlyTime, focusRotationSpeed, audioSource, unavailableSound, warnUnavailableFocus, showDebugInfo);
        inspectManager = new CameraInspectManager(holdPoint, inspectFlyTime, showDebugInfo);
        hoverState = new CameraHoverState();


        pushSystem.Mode = pushMode;
        pushSystem.Button = pushButton;
        pushSystem.SetCooldown(pushCooldown);


        inspectManager.OnBugAIInspected += (bug) =>
        {
            BugAIInspected?.Invoke(bug);
            OnBugAIInspected?.Invoke(bug);
        };
        inspectManager.OnBugAIInspectedWhileFocused += (bug, focusTarget) =>
        {
            BugAIInspectedWhileFocused?.Invoke(bug, focusTarget);
            OnBugAIInspectedWhileFocused?.Invoke(bug, focusTarget);
        };
        inspectManager.OnInspectEnded += () => InspectEnded?.Invoke();

        if (showDebugInfo)
            Debug.Log($"[CameraController] Initialized OK. MaxDist: {maxDistance}");
    }

    void Start()
    {
        UpdateReturnPosition();
    }

    public void UpdateReturnPosition()
    {
        if (cam == null) return;

        cameraReturnPos = cam.transform.position;
        cameraReturnRot = cam.transform.rotation;
        mouseFollow.UpdateBaseRotation();

        if (showDebugInfo)
            Debug.Log($"[CameraController] Return position updated: {cameraReturnPos}");
    }

    public void SetDebugMode(bool enabled) => showDebugInfo = enabled;

    void Update()
    {

        if (inspectManager.IsInspecting)
        {
            // Only ESC exits inspect mode; RMB is handled internally by InspectSession
            bool exitPressed = inputHandler.IsEscapePressed();
            inspectManager.Update(exitPressed);
            return;
        }


        if (focusManager.FocusDepth > 0)
        {
            focusManager.Update();


            if (focusManager.IsAnimating())
            {
                mouseFollow.UpdateBaseRotation();
                return;
            }


            bool escPressed = inputHandler.IsEscapePressed();
            bool rmbPressed = inputHandler.IsRightClickPressed();
            focusManager.TryRequestExit(escPressed, rmbPressed);


        }


        if (focusManager.ShouldUpdateReturnPosition())
        {
            focusManager.ClearPendingReturnUpdate();
            UpdateReturnPosition();
            if (showDebugInfo)
                Debug.Log("[CameraController] Return position updated after focus exit");
        }


        HandleNormalMode();


        if (enableMouseFollow && !inspectManager.IsInspecting && cam != null)
        {
            mouseFollow.Update(Mouse.current);
        }
    }

    private void HandleNormalMode()
    {
        GameObject currentFocusedObject = focusManager.GetCurrentFocusedObject();
        RaycastHit? validHit = raycaster.PerformRaycast(currentFocusedObject, out Ray ray);

        if (validHit.HasValue)
        {
            RaycastHit hit = validHit.Value;


            var inspectable = hit.collider.GetComponentInParent<IInspectable>();
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            var focusable = hit.collider.GetComponentInParent<IFocusable>();
            var pushable = hit.collider.GetComponentInParent<IPushable>();


            hoverState.UpdateInspectable(inspectable);
            hoverState.UpdateInteractable(interactable);
            hoverState.UpdateFocusable(focusable);
            hoverState.UpdatePushable(pushable);


            if (pushable != null)
            {
                pushSystem.TryPushOnHover(pushable, hit.point, ray.direction);
            }


            bool lmb = inputHandler.IsLeftClickPressed();
            bool rmb = inputHandler.IsRightClickPressed();
            bool enter = inputHandler.IsEnterPressed();


            if (pushSystem.TryPushOnClick(pushable, hit.point, ray.direction, lmb, rmb, focusManager.FocusDepth))
            {
                return;
            }


            if ((lmb || enter) && (pushSystem.Mode != CameraPushSystem.PushMode.OnClick || pushSystem.Button != CameraPushSystem.PushButton.LeftClick))
            {
                bool handled = false;

                if (interactable != null)
                {
                    StartInteraction(((MonoBehaviour)interactable).gameObject);
                    handled = true;
                }
                else if (inspectable != null && holdPoint != null)
                {
                    StartInspect(((MonoBehaviour)inspectable).gameObject);
                    handled = true;
                }
                else if (focusable != null)
                {
                    StartFocus(((MonoBehaviour)focusable).gameObject);
                    handled = true;
                }

                if (!handled && showDebugInfo)
                    Debug.Log("[CameraController] No interaction available on click");
            }
        }
        else
        {

            hoverState.ClearAll();
        }
    }

    private void StartInteraction(GameObject target)
    {
        if (showDebugInfo)
            Debug.Log($"[CameraController] Interaction (IInteractable): {target.name}");

        var interactable = target.GetComponent<IInteractable>();
        if (interactable != null)
            interactable.OnInteract(cam);
        else
            Debug.LogWarning($"[CameraController] IInteractable not found on {target.name}");
    }

    public void StartInspect(GameObject target)
    {
        StartInspect(target, null);
    }

    public void StartInspect(GameObject target, System.Action onFinish)
    {
        if (showDebugInfo)
            Debug.Log($"[CameraController] Starting inspect: {target.name}");

        GameObject currentFocusTarget = focusManager.GetCurrentFocusedObject();
        inspectManager.TryStartInspect(target, cam, currentFocusTarget, onFinish);
    }

    private void StartFocus(GameObject target)
    {
        if (focusManager.TryStartFocus(target))
        {
            mouseFollow.UpdateBaseRotation();
        }
    }

    public void ExitAllFocus()
    {
        StartCoroutine(focusManager.ExitAllFocusCoroutine((pos, rot) =>
        {
            cameraReturnPos = pos;
            cameraReturnRot = rot;
            mouseFollow.UpdateBaseRotation();
        }));
    }


    public void FocusToPoint(Transform point, bool allowReturn = true, float flyTimeOverride = -1f)
    {
        if (cam == null || point == null) return;
        if (allowReturn) UpdateReturnPosition();

        float t = (flyTimeOverride > 0f) ? flyTimeOverride : focusFlyTime;
        StartCoroutine(FlyCameraTo(point.position, point.rotation, t));
    }

    private System.Collections.IEnumerator FlyCameraTo(Vector3 pos, Quaternion rot, float time)
    {
        if (cam == null) yield break;

        Vector3 startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;
        float t = 0f;

        while (t < time)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            cam.transform.position = Vector3.Lerp(startPos, pos, k);
            cam.transform.rotation = Quaternion.Slerp(startRot, rot, k);
            t += Time.deltaTime;
            yield return null;
        }

        cam.transform.SetPositionAndRotation(pos, rot);
    }
}
