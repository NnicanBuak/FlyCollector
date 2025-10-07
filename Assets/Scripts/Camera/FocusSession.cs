using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Класс для управления фокусом камеры на объекте с поддержкой уровней вложенности
/// </summary>
public class FocusSession
{
    private Camera cam;
    private GameObject target;
    private IFocusable focusable;
    private float flyTime;
    private float rotationSpeed;

    private Vector3 originalPos;
    private Quaternion originalRot;
    private Transform originalParent;

    private bool isActive = false;
    private bool isReturning = false;
    private float flyProgress = 0f;

    private Vector3 startPos, targetPos;
    private Quaternion startRot, targetRot;

    private Mouse mouse;
    private Keyboard keyboard;

    // === ПОЛЯ ДЛЯ ВЛОЖЕННОСТИ ===
    private int previousNestLevel;
    private int targetNestLevel;
    
    // === ПОЛЯ ДЛЯ ВЗАИМОДЕЙСТВИЯ ===
    [Header("Настройки взаимодействия")]
    private float interactionRange = 100f;
    private LayerMask interactableLayer = ~0; // Все слои по умолчанию
    
    private GameObject currentHoveredObject;
    private IInteractable currentInteractable;
    
    // Debug
    private bool showDebugInfo = true;

    public FocusSession(Camera cam, GameObject target, IFocusable focusable, float flyTime, 
        float rotationSpeed, float interactionRange = 100f)
    {
        this.cam = cam;
        this.target = target;
        this.focusable = focusable;
        this.flyTime = flyTime;
        this.rotationSpeed = rotationSpeed;
        this.interactionRange = interactionRange;

        mouse = Mouse.current;
        keyboard = Keyboard.current;
    }

    /// <summary>
    /// Установить маску слоев для интерактивных объектов
    /// </summary>
    public void SetInteractableLayer(LayerMask layer)
    {
        interactableLayer = layer;
    }

    /// <summary>
    /// Получить целевой объект фокуса
    /// </summary>
    public GameObject GetTarget()
    {
        return target;
    }

    /// <summary>
    /// Проверить, завершился ли фокус
    /// </summary>
    public bool IsFinished()
    {
        return isReturning && flyProgress >= 1f;
    }

    public void Begin()
    {
        originalPos = cam.transform.position;
        originalRot = cam.transform.rotation;
        originalParent = cam.transform.parent;

        // === СОХРАНЯЕМ И УСТАНАВЛИВАЕМ УРОВЕНЬ ВЛОЖЕННОСТИ ===
        if (FocusLevelManager.Instance != null)
        {
            previousNestLevel = FocusLevelManager.Instance.CurrentNestLevel;
            targetNestLevel = focusable.GetTargetNestLevel();
            
            // Устанавливаем новый уровень вложенности
            FocusLevelManager.Instance.SetNestLevel(targetNestLevel, target);
        }

        focusable?.OnFocusStart();

        targetPos = focusable.GetCameraPosition();
        targetRot = focusable.GetCameraRotation();

        startPos = originalPos;
        startRot = originalRot;
        flyProgress = 0f;
        isActive = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"[FocusSession] ▶ Начат фокус на: {target.name}");
        }
    }

    public void Update()
    {
        // Полет к объекту
        if (!isActive)
        {
            flyProgress += Time.deltaTime / flyTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flyProgress));

            cam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            if (flyProgress >= 1f)
            {
                isActive = true;
                if (showDebugInfo)
                {
                    Debug.Log($"[FocusSession] ✓ Фокус активен на: {target.name}");
                }
            }
            return;
        }

        // Возврат к исходной позиции
        if (isReturning)
        {
            flyProgress += Time.deltaTime / flyTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flyProgress));

            cam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            if (flyProgress >= 1f)
            {
                cam.transform.SetParent(originalParent);
                focusable?.OnFocusEnd();
                
                if (showDebugInfo)
                {
                    Debug.Log($"[FocusSession] ✓ Возврат завершен для: {target.name}");
                }
                
                // === ВОЗВРАЩАЕМ ПРЕДЫДУЩИЙ УРОВЕНЬ ВЛОЖЕННОСТИ ===
                if (FocusLevelManager.Instance != null)
                {
                    FocusLevelManager.Instance.GoToPreviousLevel();
                }
            }
            return;
        }

        // === ОБРАБОТКА ВЗАИМОДЕЙСТВИЯ ВО ВРЕМЯ ФОКУСА ===
        HandleInteraction();

        // Вращение камеры вокруг объекта (если не заблокировано)
        if (!focusable.IsCameraPositionLocked() && mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            if (mouseDelta.sqrMagnitude > 0.01f)
            {
                float rotX = mouseDelta.x * rotationSpeed * Time.deltaTime * 60f;
                float rotY = -mouseDelta.y * rotationSpeed * Time.deltaTime * 60f;

                Vector3 focusPoint = focusable.GetFocusCenter();
                cam.transform.RotateAround(focusPoint, Vector3.up, rotX);
                cam.transform.RotateAround(focusPoint, cam.transform.right, rotY);
                cam.transform.LookAt(focusPoint);
            }
        }

        // Выход из фокуса (один шаг назад)
        bool exitPressed = keyboard != null && keyboard[Key.Escape].wasPressedThisFrame;
        bool rightMousePressed = mouse != null && mouse.rightButton.wasPressedThisFrame;

        if (exitPressed || rightMousePressed)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[FocusSession] ▼ Escape/ПКМ нажата на объекте: {target.name}");
            }
            ExitFocus();
        }
    }

    /// <summary>
    /// Обработка взаимодействия с объектами во время фокуса
    /// </summary>
    private void HandleInteraction()
    {
        if (cam == null || mouse == null) return;

        // Создаем луч от центра экрана (или от позиции мыши)
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        // Для взаимодействия от курсора мыши используйте:
        // Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        RaycastHit hit;
        
        // Проверяем попадание луча
        if (Physics.Raycast(ray, out hit, interactionRange, interactableLayer))
        {
            GameObject hitObject = hit.collider.gameObject;
            IInteractable interactable = hitObject.GetComponent<IInteractable>();

            if (interactable != null)
            {
                // Если навели на новый объект
                if (currentHoveredObject != hitObject)
                {
                    // Убираем подсветку с предыдущего
                    if (currentInteractable != null)
                    {
                        currentInteractable.OnHoverExit();
                        if (showDebugInfo)
                        {
                            Debug.Log($"[FocusSession] Hover Exit: {currentHoveredObject.name}");
                        }
                    }

                    // Подсвечиваем новый
                    currentHoveredObject = hitObject;
                    currentInteractable = interactable;
                    currentInteractable.OnHoverEnter();
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[FocusSession] Hover Enter: {hitObject.name}");
                    }
                }

                // Обработка клика для взаимодействия
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"[FocusSession] ⚡ Взаимодействие с: {hitObject.name}");
                    }
                    currentInteractable.OnInteract(cam);
                }
            }
            else
            {
                // Луч попал, но объект не интерактивный
                ClearHoveredObject();
            }
        }
        else
        {
            // Луч не попал ни во что
            ClearHoveredObject();
        }

        // Отладочная визуализация луча
        if (showDebugInfo)
        {
            Debug.DrawRay(ray.origin, ray.direction * interactionRange, Color.yellow);
        }
    }

    /// <summary>
    /// Очистить текущий объект под курсором
    /// </summary>
    private void ClearHoveredObject()
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnHoverExit();
            if (showDebugInfo)
            {
                Debug.Log($"[FocusSession] Hover Exit: {currentHoveredObject.name}");
            }
            currentHoveredObject = null;
            currentInteractable = null;
        }
    }

    /// <summary>
    /// Выход из фокуса
    /// </summary>
    void ExitFocus()
    {
        if (isReturning)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[FocusSession] ⚠ ExitFocus проигнорирован - уже возвращаемся");
            }
            return;
        }

        // Очищаем hover при выходе
        ClearHoveredObject();

        isReturning = true;
        startPos = cam.transform.position;
        startRot = cam.transform.rotation;
        targetPos = originalPos;
        targetRot = originalRot;
        flyProgress = 0f;
        
        if (showDebugInfo)
        {
            Debug.Log($"[FocusSession] ← Начат возврат с объекта: {target.name}");
        }
    }
    
    /// <summary>
    /// Запросить выход из фокуса (можно вызвать извне)
    /// </summary>
    public void RequestExit()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[FocusSession] 🔙 Запрошен принудительный выход из фокуса: {target.name}");
        }
        ExitFocus();
    }

    /// <summary>
    /// Включить/выключить отладочную информацию
    /// </summary>
    public void SetDebugMode(bool enabled)
    {
        showDebugInfo = enabled;
    }
}