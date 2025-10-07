using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class InspectSession
{
    GameObject go;
    Transform holdPoint;
    float flyTime;
    System.Action onFinish;

    Transform originalParent;
    Vector3 origPos, origScale;
    Quaternion origRot;
    Quaternion initialInspectRotation;
    Rigidbody rb;
    Collider[] cols;
    Collider[] parentColliders;
    InspectableObject inspectableObject;

    bool isReturning;
    MonoBehaviour runner;
    
    private Mouse mouse;
    private Keyboard keyboard;
    
    private Camera cam;
    
    // === НОВОЕ: Сохраняем текущую позицию камеры для возврата ===
    private Vector3 cameraReturnPos;
    private Quaternion cameraReturnRot;
    
    private float interactionRange = 100f;
    private LayerMask interactableLayer = ~0;
    
    private GameObject currentHoveredObject;
    private IInteractable currentInteractable;
    
    private BugAI targetBugAI;
    
    private bool showDebugInfo = true;

    public InspectSession(GameObject go, Transform holdPoint, float flyTime, System.Action onFinish)
    {
        this.go = go; 
        this.holdPoint = holdPoint; 
        this.flyTime = flyTime; 
        this.onFinish = onFinish;
        
        if (holdPoint != null)
        {
            runner = holdPoint.GetComponentInParent<MonoBehaviour>();
            if (runner == null)
            {
                runner = holdPoint.GetComponent<MonoBehaviour>();
            }
        }
        
        if (runner == null)
        {
            cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null) runner = cam.GetComponent<MonoBehaviour>();
        }
        
        if (runner == null)
        {
            var cameraController = Object.FindFirstObjectByType<CameraController>();
            if (cameraController != null) runner = cameraController;
        }
        
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        }
        
        mouse = Mouse.current;
        keyboard = Keyboard.current;
    }

    public void Begin()
    {
        if (go == null || holdPoint == null)
        {
            Debug.LogError("[InspectSession] GameObject или holdPoint равны null!");
            onFinish?.Invoke();
            return;
        }

        if (runner == null)
        {
            Debug.LogError("[InspectSession] Не удалось найти MonoBehaviour для запуска корутины!");
            onFinish?.Invoke();
            return;
        }

        // === ВАЖНО: Сохраняем ТЕКУЩУЮ позицию камеры ===
        // Это позиция, к которой вернется камера после инспекции
        // Если мы в фокусе - это будет позиция фокуса
        if (cam != null)
        {
            cameraReturnPos = cam.transform.position;
            cameraReturnRot = cam.transform.rotation;
            
            if (showDebugInfo)
            {
                Debug.Log($"[InspectSession] 📷 Сохранена текущая позиция камеры: {cameraReturnPos}");
            }
        }

        // Сохраняем состояние объекта
        originalParent = go.transform.parent;
        origPos = go.transform.position;
        origRot = go.transform.rotation;
        origScale = go.transform.localScale;

        rb = go.GetComponent<Rigidbody>();
        if (rb) 
        { 
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero; 
            rb.isKinematic = true; 
        }

        targetBugAI = go.GetComponent<BugAI>();
        if (targetBugAI != null)
        {
            targetBugAI.DisableAI();
            if (showDebugInfo)
            {
                Debug.Log($"[InspectSession] 🐛 BugAI отключен на {go.name}");
            }
        }

        parentColliders = go.GetComponents<Collider>();
        foreach (var c in parentColliders)
        {
            c.enabled = false;
        }
        
        if (showDebugInfo && parentColliders.Length > 0)
        {
            Debug.Log($"[InspectSession] Отключено {parentColliders.Length} коллайдеров на {go.name}");
        }

        inspectableObject = go.GetComponent<InspectableObject>();
        if (inspectableObject != null)
        {
            inspectableObject.OnInspectBegin();
            
            if (showDebugInfo)
            {
                Debug.Log($"[InspectSession] Вызван OnInspectBegin() для {go.name}");
            }
        }

        var inspectable = go.GetComponent<IInspectable>();
        Quaternion targetRotation = (inspectable != null && inspectable.UsesCustomOrientation()) 
            ? inspectable.GetInspectRotation() 
            : holdPoint.rotation;

        runner.StartCoroutine(Fly(go.transform, holdPoint.position, targetRotation, flyTime, () =>
        {
            go.transform.SetParent(holdPoint, true);
            initialInspectRotation = go.transform.rotation;
        }));
    }

    /// <summary>
    /// Обновление ввода. Возвращает true, если нужно выйти из фокуса (ПКМ)
    /// </summary>
    public bool UpdateInput(bool exitPressed)
    {
        if (go == null) return false;

        // Обрабатываем взаимодействие и проверяем, был ли клик обработан
        bool interactionHandled = HandleInteraction();

        if (mouse != null)
        {
            Vector2 mousePos = mouse.position.ReadValue();
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 mouseOffset = mousePos - screenCenter;
            
            float normalizedX = mouseOffset.x / (Screen.width * 0.5f);
            float normalizedY = mouseOffset.y / (Screen.height * 0.5f);
            
            normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
            normalizedY = Mathf.Clamp(normalizedY, -1f, 1f);
            
            float targetAngleY = normalizedX * 180f;
            float targetAngleX = -normalizedY * 45f;
            
            Quaternion targetDelta = Quaternion.Euler(targetAngleX, targetAngleY, 0f);
            Quaternion targetRotation = initialInspectRotation * targetDelta;
            
            float smoothSpeed = 10f * Time.deltaTime;
            go.transform.rotation = Quaternion.Slerp(go.transform.rotation, targetRotation, smoothSpeed);
        }

        if (mouse != null && holdPoint != null)
        {
            Vector2 scrollDelta = mouse.scroll.ReadValue();
            float scroll = scrollDelta.y / 120f;
            
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float screenScale = Mathf.Min(Screen.width, Screen.height) / 1080f;
                float zoomAmount = scroll * 0.1f * screenScale;
                
                var currentPos = holdPoint.localPosition;
                float newZ = Mathf.Clamp(currentPos.z - zoomAmount, 0.2f, 2.0f);
                holdPoint.localPosition = new Vector3(currentPos.x, currentPos.y, newZ);
            }
        }

        // === ЛОГИКА ВЫХОДА ===
        
        // ПКМ - выход из ВСЕХ фокусов, но инспекция продолжается
        bool rightMousePressed = mouse != null && mouse.rightButton.wasPressedThisFrame;
        if (rightMousePressed)
        {
            if (showDebugInfo)
            {
                Debug.Log("[InspectSession] ПКМ - запрос выхода из ВСЕХ фокусов (возврат к уровню 0), инспекция продолжается");
            }
            return true; // Сигнал для CameraController - выйти из ВСЕХ фокусов
        }
        
        // Escape - выход из Inspect
        if (exitPressed)
        {
            if (showDebugInfo)
            {
                Debug.Log("[InspectSession] Выход по Escape");
            }
            Return();
            return false;
        }
        
        // ЛКМ по пустоте (не по IInteractable) - выход из Inspect
        bool leftMousePressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (leftMousePressed && !interactionHandled)
        {
            if (showDebugInfo)
            {
                Debug.Log("[InspectSession] Выход по ЛКМ (клик в пустоту)");
            }
            Return();
            return false;
        }
        
        return false; // Продолжаем Inspect
    }

    private bool HandleInteraction()
    {
        if (cam == null || mouse == null) return false;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, interactionRange, interactableLayer))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            if (showDebugInfo)
            {
                Debug.Log($"[InspectSession] Raycast попал в: {hitObject.name}");
            }
            
            IInteractable interactable = FindInteractable(hitObject);

            if (interactable != null)
            {
                GameObject interactableObject = (interactable as MonoBehaviour)?.gameObject;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[InspectSession] IInteractable найден на: {interactableObject?.name}");
                }

                if (currentHoveredObject != interactableObject)
                {
                    if (currentInteractable != null)
                    {
                        currentInteractable.OnHoverExit();
                        if (showDebugInfo)
                        {
                            Debug.Log($"[InspectSession] Hover Exit: {currentHoveredObject.name}");
                        }
                    }

                    currentHoveredObject = interactableObject;
                    currentInteractable = interactable;
                    currentInteractable.OnHoverEnter();
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[InspectSession] Hover Enter: {interactableObject.name}");
                    }
                }

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[InspectSession] Взаимодействие с: {interactableObject.name}");
                    }
                    currentInteractable.OnInteract(cam);
                    return true; // Взаимодействие обработано!
                }
                
                return false; // Hover, но не клик
            }
            else
            {
                ClearHoveredObject();
            }
        }
        else
        {
            ClearHoveredObject();
        }

        if (showDebugInfo)
        {
            if (Physics.Raycast(ray, out hit, interactionRange, interactableLayer))
            {
                Debug.DrawLine(ray.origin, hit.point, Color.green);
                Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.red);
            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * interactionRange, Color.yellow);
            }
        }
        
        return false; // Никакого взаимодействия
    }

    private IInteractable FindInteractable(GameObject obj)
    {
        IInteractable interactable = obj.GetComponent<IInteractable>();
        if (interactable != null) return interactable;

        interactable = obj.GetComponentInParent<IInteractable>();
        if (interactable != null) return interactable;

        interactable = obj.GetComponentInChildren<IInteractable>();
        return interactable;
    }

    private void ClearHoveredObject()
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnHoverExit();
            if (showDebugInfo && currentHoveredObject != null)
            {
                Debug.Log($"[InspectSession] Hover Exit: {currentHoveredObject.name}");
            }
            currentHoveredObject = null;
            currentInteractable = null;
        }
    }

    void Return()
    {
        if (isReturning || go == null || runner == null) return;
        isReturning = true;

        ClearHoveredObject();

        go.transform.SetParent(null, true);
        
        // === ВАЖНОЕ ИСПРАВЛЕНИЕ: Возвращаем ОБЪЕКТ на место И камеру в сохраненную позицию ===
        runner.StartCoroutine(ReturnBoth());
    }

    IEnumerator ReturnBoth()
    {
        if (go == null) yield break;

        // Летим объектом назад
        Vector3 objFromPos = go.transform.position;
        Quaternion objFromRot = go.transform.rotation;
        
        // И одновременно возвращаем камеру (если она есть)
        Vector3 camFromPos = cam != null ? cam.transform.position : Vector3.zero;
        Quaternion camFromRot = cam != null ? cam.transform.rotation : Quaternion.identity;
        
        float elapsed = 0f;
        
        while (elapsed < flyTime)
        {
            float k = Mathf.SmoothStep(0, 1, elapsed / flyTime);
            
            // Двигаем объект
            if (go != null)
            {
                go.transform.position = Vector3.Lerp(objFromPos, origPos, k);
                go.transform.rotation = Quaternion.Slerp(objFromRot, origRot, k);
            }
            
            // Двигаем камеру обратно
            if (cam != null)
            {
                cam.transform.position = Vector3.Lerp(camFromPos, cameraReturnPos, k);
                cam.transform.rotation = Quaternion.Slerp(camFromRot, cameraReturnRot, k);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Финальная установка позиций
        if (go != null)
        {
            go.transform.position = origPos;
            go.transform.rotation = origRot;
            go.transform.SetParent(originalParent, true);
            
            if (rb) rb.isKinematic = false;
            
            foreach (var c in parentColliders) 
            {
                if (c != null) c.enabled = true;
            }
            
            if (targetBugAI != null)
            {
                targetBugAI.EnableAI();
                if (showDebugInfo)
                {
                    Debug.Log($"[InspectSession] 🐛 BugAI включен обратно на {go.name}");
                }
            }
            
            if (showDebugInfo && parentColliders.Length > 0)
            {
                Debug.Log($"[InspectSession] Включено {parentColliders.Length} коллайдеров обратно на {go.name}");
            }
            
            if (inspectableObject != null)
            {
                inspectableObject.OnInspectEnd();
                
                if (showDebugInfo)
                {
                    Debug.Log($"[InspectSession] Вызван OnInspectEnd() для {go.name}");
                }
            }
        }
        
        // Камера уже на месте
        if (cam != null)
        {
            cam.transform.position = cameraReturnPos;
            cam.transform.rotation = cameraReturnRot;
            
            if (showDebugInfo)
            {
                Debug.Log($"[InspectSession] 📷 Камера возвращена в позицию: {cameraReturnPos}");
            }
        }
        
        onFinish?.Invoke();
    }

    IEnumerator Fly(Transform t, Vector3 toPos, Quaternion toRot, float time, System.Action after)
    {
        if (t == null) yield break;

        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;
        float elapsed = 0f;
        
        while (elapsed < time && t != null)
        {
            float k = Mathf.SmoothStep(0, 1, elapsed / time);
            t.position = Vector3.Lerp(fromPos, toPos, k);
            t.rotation = Quaternion.Slerp(fromRot, toRot, k);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (t != null)
        {
            t.position = toPos; 
            t.rotation = toRot;
        }
        
        after?.Invoke();
    }

    public void SetInteractableLayer(LayerMask layer)
    {
        interactableLayer = layer;
    }

    public void SetDebugMode(bool enabled)
    {
        showDebugInfo = enabled;
    }
}