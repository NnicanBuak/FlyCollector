using UnityEngine;
using System.Collections.Generic;

public class FocusableObject : MonoBehaviour, IFocusable
{
    [Header("=== Система вложенности ===")]
    [Tooltip("Уровень вложенности, на котором доступен этот объект\n0 = базовый уровень (всегда доступен)")]
    [SerializeField] private int requiredNestLevel = 0;
    
    [Tooltip("На какой уровень вложенности переходит камера при фокусе\n0 = возврат к базовому уровню")]
    [SerializeField] private int targetNestLevel = 1;

    [Header("Camera Position")]
    [Tooltip("Transform, определяющий позицию и поворот камеры при фокусе")]
    [SerializeField] private Transform cameraPosition;
    
    [Tooltip("Если не задан cameraPosition, использовать автоматическое позиционирование")]
    [SerializeField] private bool useAutomaticPositioning = true;
    
    [Header("Automatic Positioning (если cameraPosition не задан)")]
    [SerializeField] private float distance = 3f;
    [SerializeField] private Vector3 offset = Vector3.zero;
    
    [Header("Camera Behavior")]
    [Tooltip("Фиксировать камеру в заданной позиции (отключает вращение мышью)")]
    [SerializeField] private bool lockCameraPosition = true;

    [Header("Outline")]
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Color focusHoverColor = new Color(0, 0.831f, 0.404f);
    [SerializeField] private Color focusActiveColor = Color.white;

    [Header("Animation")]
    [Tooltip("Animator для воспроизведения анимаций при фокусе")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Использовать анимацию")]
    [SerializeField] private bool useAnimation = false;
    
    [Tooltip("Имя триггера для анимации при фокусе")]
    [SerializeField] private string focusStartTrigger = "FocusStart";
    
    [Tooltip("Имя триггера для анимации при расфокусе")]
    [SerializeField] private string focusEndTrigger = "FocusEnd";
    
    [Tooltip("Имя триггера для анимации при наведении")]
    [SerializeField] private string hoverEnterTrigger = "HoverEnter";
    
    [Tooltip("Имя триггера для анимации при уходе курсора")]
    [SerializeField] private string hoverExitTrigger = "HoverExit";
    
    [Header("Colliders")]
    [Tooltip("Отключать ли коллайдеры у этого объекта на время фокуса")]
    [SerializeField] private bool disableCollidersOnFocus = false;

    [Tooltip("Затрагивать ли коллайдеры дочерних объектов")]
    [SerializeField] private bool includeChildColliders = true;


    private List<(Collider col, bool wasEnabled)> colliderCache;
    private Outline outline;


    void Awake()
    {
        outline = GetComponent<Outline>();
        
        if (outline != null && useOutline)
        {
            outline.enabled = false;
        }
    }


    
    public int GetRequiredNestLevel()
    {
        return requiredNestLevel;
    }
    
    public int GetTargetNestLevel()
    {
        return targetNestLevel;
    }
    
    public bool IsAvailableAtNestLevel(int currentLevel)
    {
        return currentLevel == requiredNestLevel;
    }



    public void OnFocusHoverEnter()
    {

        int currentLevel = FocusLevelManager.Instance != null ? 
            FocusLevelManager.Instance.CurrentNestLevel : 0;
        
        bool isAvailable = IsAvailableAtNestLevel(currentLevel);
        

        if (isAvailable)
        {
            if (useOutline && outline != null)
            {
                outline.enabled = true;
                outline.OutlineColor = focusHoverColor;
            }

            if (useAnimation && animator != null && !string.IsNullOrEmpty(hoverEnterTrigger))
            {
                animator.SetTrigger(hoverEnterTrigger);
            }
        }
    }

    public void OnFocusHoverExit()
    {
        if (useOutline && outline != null)
        {
            outline.enabled = false;
        }

        if (useAnimation && animator != null && !string.IsNullOrEmpty(hoverExitTrigger))
        {
            animator.SetTrigger(hoverExitTrigger);
        }
    }
    public void OnFocusStart()
    {
        if (useOutline && outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = focusActiveColor;
        }

        if (disableCollidersOnFocus)
        {
            CacheAndSetColliders(enabledState: false);
        }

        if (useAnimation && animator != null && !string.IsNullOrEmpty(focusStartTrigger))
        {
            animator.SetTrigger(focusStartTrigger);
        }
    }

    public void OnFocusEnd()
    {
        if (useOutline && outline != null)
        {
            outline.enabled = false;
        }


        if (disableCollidersOnFocus)
        {
            RestoreColliders();
        }

        if (useAnimation && animator != null && !string.IsNullOrEmpty(focusEndTrigger))
        {
            animator.SetTrigger(focusEndTrigger);
        }
    }

    private void CacheAndSetColliders(bool enabledState)
    {
        if (colliderCache == null) colliderCache = new List<(Collider, bool)>();
        colliderCache.Clear();


        var colliders = includeChildColliders 
            ? GetComponentsInChildren<Collider>(includeInactive: true)
            : GetComponents<Collider>();

        foreach (var col in colliders)
        {

            colliderCache.Add((col, col.enabled));
            col.enabled = enabledState;
        }
    }

    private void RestoreColliders()
    {
        if (colliderCache == null) return;

        foreach (var (col, wasEnabled) in colliderCache)
        {
            if (col != null) col.enabled = wasEnabled;
        }

        colliderCache.Clear();
    }



    public Vector3 GetCameraPosition()
    {
        if (cameraPosition != null)
        {
            return cameraPosition.position;
        }

        if (useAutomaticPositioning)
        {
            Bounds bounds = GetObjectBounds();
            Vector3 center = bounds.center + offset;
            
            Vector3 direction = Camera.main != null ? 
                (Camera.main.transform.position - center).normalized : 
                -transform.forward;
                
            return center + direction * distance;
        }

        return transform.position;
    }


    public Quaternion GetCameraRotation()
    {
        if (cameraPosition != null)
        {
            return cameraPosition.rotation;
        }

        if (useAutomaticPositioning)
        {
            Bounds bounds = GetObjectBounds();
            Vector3 center = bounds.center + offset;
            Vector3 direction = (center - GetCameraPosition()).normalized;
            return Quaternion.LookRotation(direction);
        }

        return Quaternion.LookRotation(transform.position - GetCameraPosition());
    }


    public bool IsCameraPositionLocked()
    {
        return lockCameraPosition;
    }


    public Vector3 GetFocusCenter()
    {
        return GetObjectBounds().center + offset;
    }


    public void SetCameraPosition(Transform cameraTransform)
    {
        cameraPosition = cameraTransform;
    }


    public void SetCameraLock(bool locked)
    {
        lockCameraPosition = locked;
    }

    private Bounds GetObjectBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }


    void OnDrawGizmosSelected()
    {
        if (cameraPosition != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(cameraPosition.position, 0.1f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawLine(cameraPosition.position, 
                cameraPosition.position + cameraPosition.forward * 0.5f);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(cameraPosition.position, GetFocusCenter());
        }
        else if (useAutomaticPositioning)
        {
            Vector3 camPos = GetCameraPosition();
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(camPos, 0.1f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(camPos, GetFocusCenter());
        }
    }
}