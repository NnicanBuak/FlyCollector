using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class CameraController : MonoBehaviour
{
    [Header("Raycast")]
    public float maxDistance = 30f;
    public LayerMask interactMask = ~0;

    [Header("Inspect (объект к камере)")]
    public Transform holdPoint;
    public float inspectFlyTime = 0.25f;

    [Header("Focus (камера к объекту)")]
    public float focusFlyTime = 0.5f;
    
    [Header("Вращение при фокусе (если не заблокировано)")]
    public float focusRotationSpeed = 2f;

    [Header("Толкание объектов")]
    public float pushForce = 10f;
    public AudioClip pushSound;
    public float pushSoundVolume = 0.5f;
    
    [Tooltip("Режим толкания")]
    public enum PushMode { OnClick, OnHover }
    public PushMode pushMode = PushMode.OnHover;
    
    [Tooltip("Кнопка для откидывания (только для режима OnClick)")]
    public enum PushButton { LeftClick, RightClick }
    public PushButton pushButton = PushButton.RightClick;
    
    [Tooltip("Задержка между толчками при ховере (секунды)")]
    [Range(0f, 1f)]
    public float pushCooldown = 0.3f;

    [Header("=== Система вложенности ===")]
    [Tooltip("Показывать предупреждение при попытке взаимодействия с недоступным объектом")]
    public bool showUnavailableWarning = true;
    
    [Tooltip("Звук недоступного взаимодействия")]
    public AudioClip unavailableSound;

    // ==== События для BugAI (типизированные, чтобы подписываться в Inspector) ====
    [System.Serializable] public class BugAIEvent : UnityEvent<BugAI> { }
    [System.Serializable] public class BugAIWithFocusEvent : UnityEvent<BugAI, GameObject> { }

    [Header("Events (Inspector)")]
    public BugAIEvent BugAIInspected;                       // вызывается при инспекте объекта с BugAI
    public BugAIWithFocusEvent BugAIInspectedWhileFocused;  // вызывается, если инспект BugAI происходит во время активного фокуса

    [Header("Debug")]
    public bool showDebugInfo = true;

    private IInspectable hoveredInspectable;
    private IInteractable hoveredInteractable;
    private IFocusable hoveredFocusable;
    private IPushable hoveredPushable;

    private GameObject lastPushedObject;
    private float lastPushTime;

    private InspectSession currentInspect;
    
    // ==== C#-события (если удобнее подписываться кодом) ====
    public event System.Action<BugAI> OnBugAIInspected;
    public event System.Action<BugAI, GameObject> OnBugAIInspectedWhileFocused;

    // === СТЕК ФОКУСОВ ДЛЯ ВЛОЖЕННОСТИ ===
    private System.Collections.Generic.Stack<FocusSession> focusStack = new System.Collections.Generic.Stack<FocusSession>();

    // === ФЛАГ ДЛЯ ОБНОВЛЕНИЯ ПОЗИЦИИ ВОЗВРАТА ===
    private bool pendingReturnPositionUpdate = false;

    private Camera cam;

    private Mouse mouse;
    private Keyboard keyboard;
    private AudioSource audioSource;
    
    // --- Return transform for camera ---
    private Vector3 cameraReturnPos;
    private Quaternion cameraReturnRot;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam) cam = Camera.main;

        if (!cam)
        {
            Debug.LogError("[CameraController] Камера не найдена!");
            return;
        }

        mouse = Mouse.current;
        keyboard = Keyboard.current;

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] Инициализация OK. MaxDist: {maxDistance}");
        }
    }

    /// <summary>
    /// Обновить позицию возврата камеры (вызывается после закрытия фокусов)
    /// </summary>
    public void UpdateReturnPosition()
    {
        if (cam != null)
        {
            cameraReturnPos = cam.transform.position;
            cameraReturnRot = cam.transform.rotation;
        
            if (showDebugInfo)
            {
                Debug.Log($"[CameraController] 🔄 Обновлена позиция возврата камеры: {cameraReturnPos}");
            }
        }
    }

    public void SetDebugMode(bool enabled)
    {
        showDebugInfo = enabled;
    }
    
    void Update()
    {
        // Inspect блокирует всё остальное
        if (currentInspect != null)
        {
            bool escPressed  = keyboard != null && keyboard[Key.Escape].wasPressedThisFrame;
            bool rmbPressed  = mouse    != null && mouse.rightButton.wasPressedThisFrame;

            // Передаём в InspectSession сигнал выхода как по Esc, так и по RMB
            // (предполагается, что InspectSession сам корректно завершит анимацию и вызовет onFinish)
            currentInspect.UpdateInput(escPressed || rmbPressed);

            // Жёсткий выход по Esc: очищаем все фокусы
            if (escPressed && focusStack.Count > 0)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[CameraController] 🔙 ESC в инспекции! Закрываем ВСЕ фокусы ({focusStack.Count} шт)");
                }

                while (focusStack.Count > 0)
                {
                    FocusSession focus = focusStack.Pop();
                    focus.RequestExit();

                    if (showDebugInfo)
                    {
                        Debug.Log($"[CameraController] Закрыт фокус на: {focus.GetTarget().name}");
                    }
                }

                // После закрытия всех фокусов обновим позицию возврата
                pendingReturnPositionUpdate = true;
            }

            // По RMB ничего специально не чистим:
            // - Inspect завершится (через UpdateInput)
            // - стек фокусов сохранён → камера вернётся к текущему фокусу автоматически
            return;
        }

        // Обновляем текущий фокус из стека
        if (focusStack.Count > 0)
        {
            FocusSession currentFocus = focusStack.Peek();
            currentFocus.Update();
            
            // Проверяем, завершился ли фокус
            if (currentFocus.IsFinished())
            {
                focusStack.Pop();
                
                if (showDebugInfo)
                {
                    Debug.Log($"[CameraController] ✓ Фокус завершен. Осталось в стеке: {focusStack.Count}");
                }
            }
        }
        
        // После закрытия всех фокусов обновляем позицию возврата камеры
        if (pendingReturnPositionUpdate && focusStack.Count == 0 && currentInspect != null)
        {
            pendingReturnPositionUpdate = false;
            UpdateReturnPosition();

            if (showDebugInfo)
            {
                Debug.Log("[CameraController] ✓ Позиция возврата камеры обновлена");
            }
        }

        UpdateNormalMode();
    }
    
    void UpdateNormalMode()
    {
        Vector3 mousePosition = mouse != null ? mouse.position.ReadValue() : Input.mousePosition;
        Ray ray = cam.ScreenPointToRay(mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        RaycastHit? validHit = null;
        GameObject currentFocusedObject = null;
        
        // Получаем текущий сфокусированный объект
        if (focusStack.Count > 0)
        {
            currentFocusedObject = focusStack.Peek().GetTarget();
        }
        
        foreach (var hit in hits)
        {
            if (currentFocusedObject != null && 
                (hit.collider.gameObject == currentFocusedObject || 
                hit.collider.transform.IsChildOf(currentFocusedObject.transform)))
            {
                var focusableOnHit = hit.collider.GetComponentInParent<IFocusable>();
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
        
        if (validHit.HasValue)
        {
            RaycastHit hit = validHit.Value;
            
            Debug.DrawLine(ray.origin, hit.point, Color.green);
            
            if (showDebugInfo)
            {
                Debug.Log($"[CameraController] Raycast попал в: {hit.collider.gameObject.name}");
            }

            var inspectable = hit.collider.GetComponentInParent<IInspectable>();
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            var focusable = hit.collider.GetComponentInParent<IFocusable>();
            var pushable = hit.collider.GetComponentInParent<IPushable>();

            // IInspectable и IInteractable hover - ВСЕГДА (нужны для Inspect в фокусе)
            if (inspectable != hoveredInspectable)
            {
                hoveredInspectable?.OnHoverExit();
                hoveredInspectable = inspectable;
                hoveredInspectable?.OnHoverEnter();
            }

            if (interactable != hoveredInteractable)
            {
                hoveredInteractable?.OnHoverExit();
                hoveredInteractable = interactable;
                hoveredInteractable?.OnHoverEnter();
            }

            // IPushable hover - только вне фокуса
            if (focusStack.Count == 0)
            {
                if (pushable != hoveredPushable)
                {
                    hoveredPushable?.OnPushHoverExit();
                    hoveredPushable = pushable;
                    
                    if (hoveredPushable != null)
                    {
                        hoveredPushable.OnPushHoverEnter();
                        
                        if (pushMode == PushMode.OnHover)
                        {
                            GameObject pushTarget = ((MonoBehaviour)hoveredPushable).gameObject;
                            bool canPush = lastPushedObject != pushTarget || 
                                        (Time.time - lastPushTime) >= pushCooldown;
                            
                            if (canPush)
                            {
                                if (showDebugInfo)
                                {
                                    Debug.Log($"[CameraController] Толкание при ховере: {pushTarget.name}");
                                }
                                
                                PushObject(pushTarget, hit.point, ray.direction);
                                lastPushedObject = pushTarget;
                                lastPushTime = Time.time;
                            }
                        }
                    }
                }
            }

            // Focusable hover - ВСЕГДА
            if (focusable != hoveredFocusable)
            {
                hoveredFocusable?.OnFocusHoverExit();
                hoveredFocusable = focusable;
                hoveredFocusable?.OnFocusHoverEnter();
            }

            bool mouseClicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool keyPressed = keyboard != null && keyboard[Key.E].wasPressedThisFrame;

            bool pushButtonPressed = false;
            if (pushMode == PushMode.OnClick && focusStack.Count == 0)
            {
                if (pushButton == PushButton.LeftClick)
                {
                    pushButtonPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
                }
                else
                {
                    pushButtonPressed = mouse != null && mouse.rightButton.wasPressedThisFrame;
                }
            }

            // ПРИОРИТЕТ 1: Откидывание (только вне фокуса)
            if (pushButtonPressed)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[CameraController] Попытка откинуть: {hit.collider.gameObject.name}");
                }
                PushObject(hit.collider.gameObject, hit.point, ray.direction);
            }
            // ПРИОРИТЕТ 2: Взаимодействия
            else if ((mouseClicked || keyPressed) && (pushMode != PushMode.OnClick || pushButton != PushButton.LeftClick))
            {
                bool handled = false;
                
                // 1. IInteractable (работает всегда)
                if (interactable != null)
                {
                    StartInteraction(((MonoBehaviour)interactable).gameObject);
                    handled = true;
                }
                // 2. IInspectable (работает всегда)
                else if (inspectable != null && holdPoint != null)
                {
                    StartInspect(((MonoBehaviour)inspectable).gameObject);
                    handled = true;
                }
                // 3. IFocusable (работает всегда)
                else if (focusable != null)
                {
                    StartFocus(((MonoBehaviour)focusable).gameObject);
                    handled = true;
                }

                if (showDebugInfo && !handled)
                {
                    Debug.Log($"[CameraController] Взаимодействие не обработано для {hit.collider.gameObject.name}");
                }
            }
        }
        else
        {
            Debug.DrawLine(ray.origin, ray.origin + ray.direction * maxDistance, Color.red);
            
            // Очищаем hover (IInspectable и IInteractable всегда, IPushable только вне фокуса)
            hoveredInspectable?.OnHoverExit();
            hoveredInspectable = null;

            hoveredInteractable?.OnHoverExit();
            hoveredInteractable = null;

            if (focusStack.Count == 0)
            {
                hoveredPushable?.OnPushHoverExit();
                hoveredPushable = null;
            }

            hoveredFocusable?.OnFocusHoverExit();
            hoveredFocusable = null;
        }
    }
    
    void StartFocus(GameObject target)
    {
        var focusable = target.GetComponent<IFocusable>();
        if (focusable == null)
        {
            Debug.LogWarning($"[CameraController] IFocusable не найден на {target.name}");
            return;
        }

        int currentNestLevel = FocusLevelManager.Instance != null ? 
            FocusLevelManager.Instance.CurrentNestLevel : 0;
        
        if (!focusable.IsAvailableAtNestLevel(currentNestLevel))
        {
            if (showDebugInfo)
            {
                Debug.Log($"[CameraController] Объект {target.name} недоступен на уровне {currentNestLevel}");
            }

            if (showUnavailableWarning)
            {
                if (unavailableSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(unavailableSound);
                }
                
                Debug.LogWarning($"Объект доступен только на уровне вложенности {focusable.GetRequiredNestLevel()}");
            }
            
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] ═══ НАЧАЛО ФОКУСА ═══");
            Debug.Log($"[CameraController] Фокус на: {target.name}");
            Debug.Log($"[CameraController] Текущий уровень: {currentNestLevel} → целевой: {focusable.GetTargetNestLevel()}");
            Debug.Log($"[CameraController] Стек фокусов ДО: {focusStack.Count}");
        }

        var newFocus = new FocusSession(
            cam, 
            target,
            focusable,
            focusFlyTime, 
            focusRotationSpeed
        );
        
        focusStack.Push(newFocus);
        newFocus.Begin();
        
        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] Стек фокусов ПОСЛЕ: {focusStack.Count}");
            Debug.Log($"[CameraController] ═══════════════════");
        }
    }

    void PushObject(GameObject target, Vector3 hitPoint, Vector3 direction)
    {
        var pushSession = new PushSession(
            target, 
            direction, 
            pushForce, 
            hitPoint, 
            pushSound, 
            pushSoundVolume, 
            audioSource,
            showDebugInfo
        );
        
        pushSession.Execute();

        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] Откинут объект: {target.name}");
        }
    }

    void StartInteraction(GameObject target)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] Взаимодействие (IInteractable): {target.name}");
        }

        var interactable = target.GetComponent<IInteractable>();
        if (interactable != null)
        {
            interactable.OnInteract(cam);
        }
        else
        {
            Debug.LogWarning($"[CameraController] IInteractable не найден на {target.name}");
        }
    }

    void StartInspect(GameObject target)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] Инспекция (IInspectable): {target.name}");
        }

        // Триггеры для BugAI
        var bug = target.GetComponentInParent<BugAI>(); // или GetComponent<BugAI>(), если точно на самом объекте
        if (bug != null)
        {
            // 1) Всегда: BugAI пошёл в инспект
            OnBugAIInspected?.Invoke(bug);
            BugAIInspected?.Invoke(bug);

            // 2) Если в этот момент камера находится в фокусе на каком-то объекте
            if (focusStack.Count > 0)
            {
                var currentFocusTarget = focusStack.Peek().GetTarget();
                OnBugAIInspectedWhileFocused?.Invoke(bug, currentFocusTarget);
                BugAIInspectedWhileFocused?.Invoke(bug, currentFocusTarget);

                if (showDebugInfo)
                {
                    Debug.Log($"[CameraController] 📌 BugAI инспектируется, пока фокус на: {currentFocusTarget.name}");
                }
            }
        }

        if (FocusLevelManager.Instance != null && !FocusLevelManager.Instance.HasEverInteracted)
        {
            FocusLevelManager.Instance.TriggerFirstInteraction("Inspect");
        }

        currentInspect = new InspectSession(target, holdPoint, inspectFlyTime, onFinish: () => currentInspect = null);
        currentInspect.Begin();
    }
}
