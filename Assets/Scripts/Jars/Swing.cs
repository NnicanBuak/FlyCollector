using UnityEngine;

/// <summary>
/// Покачивание объектов на веревке с возможностью толкания
/// Автоматически возвращается к плавному покачиванию после толчка
/// </summary>
public class JarSwing : MonoBehaviour
{
    [Header("Точка якоря (веревка)")]
    [Tooltip("Точка, от которой висит объект. Если не задана - берется позиция объекта + offset")]
    public Transform anchorPoint;
    
    [Tooltip("Смещение точки якоря относительно объекта (если anchorPoint не задана)")]
    public Vector3 anchorOffset = new Vector3(0f, 1f, 0f);
    
    [Tooltip("Показывать линию веревки в редакторе")]
    public bool showRope = true;

    [Header("Начальное покачивание (Perlin Noise)")]
    [Tooltip("Максимальный угол наклона по оси X (вперед-назад)")]
    [Range(0f, 45f)]
    public float ambientMaxAngleX = 8f;
    
    [Tooltip("Максимальный угол наклона по оси Z (влево-вправо)")]
    [Range(0f, 45f)]
    public float ambientMaxAngleZ = 8f;
    
    [Tooltip("Скорость плавного покачивания")]
    [Range(0.1f, 5f)]
    public float ambientSwingSpeed = 1f;

    [Header("Физика толкания")]
    [Tooltip("Максимальный угол при толкании")]
    [Range(0f, 45f)]
    public float maxPushAngle = 25f;
    
    [Tooltip("Затухание после толчка")]
    [Range(0f, 5f)]
    public float damping = 1f;
    
    [Tooltip("Насколько сильно реагировать на толчки")]
    [Range(0f, 50f)]
    public float pushSensitivity = 10f;

    [Header("Возврат к покачиванию")]
    [Tooltip("Время без толчков для возврата к покачиванию (секунды)")]
    [Range(0.1f, 5f)]
    public float returnDelay = 0.8f;
    
    [Tooltip("Скорость перехода к покачиванию")]
    [Range(0.1f, 5f)]
    public float returnSpeed = 2f;

    [Header("Разнобой")]
    [Tooltip("Случайная начальная фаза")]
    public bool randomizePhase = true;
    
    [Tooltip("Случайная вариация скорости (±%)")]
    [Range(0f, 0.5f)]
    public float speedVariation = 0.2f;

    // Состояние
    private enum SwingMode { Ambient, Physics, Returning }
    private SwingMode currentMode = SwingMode.Ambient;
    
    // Физические параметры
    private Vector2 physicsAngularVelocity;
    private Vector2 physicsAngles;
    
    // Ambient параметры
    private float offsetX;
    private float offsetZ;
    private float actualSpeed;
    
    // Возврат
    private float timeSinceLastPush;
    
    private Transform pivotTransform;
    private Rigidbody rb;

    void Start()
    {
        SetupPivot();
        
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        
        if (randomizePhase)
        {
            offsetX = Random.Range(0f, 100f);
            offsetZ = Random.Range(100f, 200f);
        }
        
        actualSpeed = ambientSwingSpeed * Random.Range(1f - speedVariation, 1f + speedVariation);
    }

    void SetupPivot()
    {
        if (anchorPoint != null)
        {
            pivotTransform = anchorPoint;
        }
        else
        {
            GameObject pivotGo = new GameObject($"{gameObject.name}_Anchor");
            pivotGo.transform.position = transform.position + anchorOffset;
            pivotGo.transform.SetParent(transform.parent);
            
            pivotTransform = pivotGo.transform;
            transform.SetParent(pivotTransform);
        }
    }

    void Update()
    {
        if (pivotTransform == null) return;

        float deltaTime = Time.deltaTime;
        timeSinceLastPush += deltaTime;

        switch (currentMode)
        {
            case SwingMode.Ambient:
                UpdateAmbientSwing();
                break;
                
            case SwingMode.Physics:
                UpdatePhysicsSwing(deltaTime);
                
                // Проверка возврата к ambient
                if (timeSinceLastPush >= returnDelay)
                {
                    currentMode = SwingMode.Returning;
                }
                break;
                
            case SwingMode.Returning:
                UpdateReturning(deltaTime);
                break;
        }
    }

    void UpdateAmbientSwing()
    {
        float time = Time.time * actualSpeed;
        
        float angleX = (Mathf.PerlinNoise(time + offsetX, 0f) - 0.5f) * 2f * ambientMaxAngleX;
        float angleZ = (Mathf.PerlinNoise(0f, time + offsetZ) - 0.5f) * 2f * ambientMaxAngleZ;
        
        pivotTransform.localRotation = Quaternion.Euler(angleX, 0f, angleZ);
    }

    void UpdatePhysicsSwing(float deltaTime)
    {
        // Гравитационная сила (возвращает к центру)
        float gravity = 20f;
        Vector2 restoring = -physicsAngles * gravity * deltaTime;
        
        // Затухание
        Vector2 dampingForce = -physicsAngularVelocity * damping * deltaTime;
        
        // Применяем силы
        physicsAngularVelocity += restoring + dampingForce;
        
        // Обновляем углы
        physicsAngles += physicsAngularVelocity * deltaTime;
        
        // Ограничиваем углы
        physicsAngles.x = Mathf.Clamp(physicsAngles.x, -maxPushAngle, maxPushAngle);
        physicsAngles.y = Mathf.Clamp(physicsAngles.y, -maxPushAngle, maxPushAngle);
        
        // Применяем ротацию
        pivotTransform.localRotation = Quaternion.Euler(physicsAngles.x, 0f, physicsAngles.y);
    }

    void UpdateReturning(float deltaTime)
    {
        // Получаем целевой угол ambient
        float time = Time.time * actualSpeed;
        float targetAngleX = (Mathf.PerlinNoise(time + offsetX, 0f) - 0.5f) * 2f * ambientMaxAngleX;
        float targetAngleZ = (Mathf.PerlinNoise(0f, time + offsetZ) - 0.5f) * 2f * ambientMaxAngleZ;
        
        // Плавно интерполируем от текущих физических углов к ambient
        physicsAngles.x = Mathf.Lerp(physicsAngles.x, targetAngleX, deltaTime * returnSpeed);
        physicsAngles.y = Mathf.Lerp(physicsAngles.y, targetAngleZ, deltaTime * returnSpeed);
        
        // Затухаем скорость
        physicsAngularVelocity = Vector2.Lerp(physicsAngularVelocity, Vector2.zero, deltaTime * returnSpeed);
        
        pivotTransform.localRotation = Quaternion.Euler(physicsAngles.x, 0f, physicsAngles.y);
        
        // Проверка завершения возврата
        if (physicsAngularVelocity.magnitude < 0.1f && 
            Mathf.Abs(physicsAngles.x - targetAngleX) < 0.5f &&
            Mathf.Abs(physicsAngles.y - targetAngleZ) < 0.5f)
        {
            currentMode = SwingMode.Ambient;
        }
    }

    /// <summary>
    /// Толкнуть объект (вызывается из PushableObject при ховере)
    /// </summary>
    public void Push(Vector3 worldDirection, float force)
    {
        if (pivotTransform == null) return;

        // Переключаем в физический режим если были в ambient
        if (currentMode == SwingMode.Ambient)
        {
            // Берем текущие углы как начальные для физики
            Vector3 currentEuler = pivotTransform.localRotation.eulerAngles;
            physicsAngles.x = NormalizeAngle(currentEuler.x);
            physicsAngles.y = NormalizeAngle(currentEuler.z);
            physicsAngularVelocity = Vector2.zero;
        }
        
        currentMode = SwingMode.Physics;
        timeSinceLastPush = 0f;

        // Преобразуем мировое направление в локальное
        Vector3 localDir = pivotTransform.InverseTransformDirection(worldDirection);
        
        // Добавляем импульс
        physicsAngularVelocity.x += localDir.z * force * pushSensitivity;
        physicsAngularVelocity.y += localDir.x * force * pushSensitivity;
        
        // Ограничиваем скорость
        physicsAngularVelocity.x = Mathf.Clamp(physicsAngularVelocity.x, -maxPushAngle * 2f, maxPushAngle * 2f);
        physicsAngularVelocity.y = Mathf.Clamp(physicsAngularVelocity.y, -maxPushAngle * 2f, maxPushAngle * 2f);
    }

    float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    void OnDrawGizmos()
    {
        if (!showRope) return;

        Vector3 anchor = anchorPoint != null ? anchorPoint.position : transform.position + anchorOffset;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(anchor, transform.position);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(anchor, 0.05f);
    }

    void OnDrawGizmosSelected()
    {
        if (!showRope) return;

        Vector3 anchor = anchorPoint != null ? anchorPoint.position : transform.position + anchorOffset;
        
        Gizmos.color = Color.cyan;
        
        Vector3 toObject = transform.position - anchor;
        float distance = toObject.magnitude;
        
        float angle = Mathf.Max(ambientMaxAngleX, ambientMaxAngleZ, maxPushAngle);
        
        Gizmos.DrawLine(anchor, anchor + Quaternion.Euler(angle, 0, 0) * Vector3.down * distance);
        Gizmos.DrawLine(anchor, anchor + Quaternion.Euler(-angle, 0, 0) * Vector3.down * distance);
        Gizmos.DrawLine(anchor, anchor + Quaternion.Euler(0, 0, angle) * Vector3.down * distance);
        Gizmos.DrawLine(anchor, anchor + Quaternion.Euler(0, 0, -angle) * Vector3.down * distance);
    }
}