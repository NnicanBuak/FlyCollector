using UnityEngine;

/// <summary>
/// Компонент для банки, которая ловит жуков при инспекции
/// Привязывается к GameObject банки
/// </summary>
public class BugJarTrap : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Целевая позиция для банки (обычно на столе/поверхности)")]
    [SerializeField] private Transform targetPosition;
    
    [Tooltip("Время перелёта банки")]
    [SerializeField] private float flyTime = 0.5f;
    
    [Tooltip("Автоматически деактивировать банку после ловли")]
    [SerializeField] private bool deactivateAfterCatch = false;
    
    [Header("Звуки")]
    [Tooltip("Звук ловли жука")]
    [SerializeField] private AudioClip catchSound;
    
    [SerializeField] private float catchSoundVolume = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private AudioSource audioSource;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;
    private bool isActive = false;
    
    // Для отслеживания активной инспекции с жуком
    private static BugJarTrap activeInstance;
    private GameObject caughtBug;

    void Awake()
    {
        // Сохраняем исходную позицию банки
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalParent = transform.parent;
        
        // Ищем или создаём AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D звук
    }

    void OnEnable()
    {
        // Подписываемся на события инспекции
        // Ищем все InspectableObject на сцене
        InspectableObject[] inspectables = FindObjectsByType<InspectableObject>(FindObjectsSortMode.None);
        
        foreach (var inspectable in inspectables)
        {
            // Проверяем, есть ли у объекта BugAI
            if (inspectable.GetComponent<BugAI>() != null)
            {
                // Используем события Unity если они есть, или делаем свою систему
                // Для простоты будем проверять в Update
                if (showDebugInfo)
                {
                    Debug.Log($"[BugJarTrap] Найден объект с BugAI: {inspectable.gameObject.name}");
                }
            }
        }
    }

    void Update()
    {
        // Проверяем, есть ли активная инспекция жука
        CheckForBugInspection();
    }

    /// <summary>
    /// Проверяет, инспектируется ли сейчас жук
    /// </summary>
    void CheckForBugInspection()
    {
        if (isActive) return; // Уже активна
        if (targetPosition == null) return;

        // Ищем активную InspectSession через CameraController
        var cameraController = FindFirstObjectByType<CameraController>();
        if (cameraController == null) return;

        // Получаем holdPoint из CameraController
        var holdPointField = cameraController.GetType().GetField("holdPoint", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        if (holdPointField == null) return;
        
        Transform holdPoint = holdPointField.GetValue(cameraController) as Transform;
        if (holdPoint == null || holdPoint.childCount == 0)
        {
            // Жука нет в инспекции - скрываем UI подсказку
            if (CollectHintUI.Instance != null)
            {
                CollectHintUI.Instance.Hide();
            }
            return;
        }

        // Проверяем первый дочерний объект holdPoint
        Transform inspectedObject = holdPoint.GetChild(0);
        if (inspectedObject == null)
        {
            if (CollectHintUI.Instance != null)
            {
                CollectHintUI.Instance.Hide();
            }
            return;
        }

        // Проверяем, есть ли у инспектируемого объекта BugAI
        BugAI bugAI = inspectedObject.GetComponent<BugAI>();
        if (bugAI != null)
        {
            // Жук в инспекции - показываем UI подсказку
            if (CollectHintUI.Instance != null)
            {
                CollectHintUI.Instance.Show();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[BugJarTrap] 🐛 Обнаружен жук в инспекции: {inspectedObject.name}");
            }
            
            // Пока не ловим - ждем ПКМ от игрока
            // CatchBug будет вызван когда сработает логика ПКМ
        }
        else
        {
            // Не жук - скрываем подсказку
            if (CollectHintUI.Instance != null)
            {
                CollectHintUI.Instance.Hide();
            }
        }
    }

    /// <summary>
    /// Ловит жука - перемещает банку на целевую позицию
    /// </summary>
    public void CatchBug(GameObject bug)
    {
        if (isActive) return;
        
        isActive = true;
        caughtBug = bug;
        activeInstance = this;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BugJarTrap] 🎯 Ловим жука! Банка летит к: {targetPosition.position}");
        }
        
        // Воспроизводим звук
        if (catchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(catchSound, catchSoundVolume);
        }
        
        // Запускаем корутину перелёта
        StartCoroutine(FlyToTarget());
    }

    /// <summary>
    /// Корутина перелёта банки к целевой позиции
    /// </summary>
    System.Collections.IEnumerator FlyToTarget()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        Vector3 targetPos = targetPosition.position;
        Quaternion targetRot = targetPosition.rotation;
        
        float elapsed = 0f;
        
        while (elapsed < flyTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyTime);
            
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Финальная позиция
        transform.position = targetPos;
        transform.rotation = targetRot;
        transform.SetParent(targetPosition, true);
        
        if (showDebugInfo)
        {
            Debug.Log($"[BugJarTrap] ✓ Банка на месте! Жук пойман: {caughtBug?.name}");
        }
        
        // Деактивируем банку если нужно
        if (deactivateAfterCatch)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Вернуть банку в исходную позицию
    /// </summary>
    public void ResetJar()
    {
        if (!isActive) return;
        
        StopAllCoroutines();
        
        transform.SetParent(originalParent);
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        
        isActive = false;
        caughtBug = null;
        
        if (activeInstance == this)
        {
            activeInstance = null;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[BugJarTrap] 🔄 Банка возвращена в исходную позицию");
        }
    }

    void OnDisable()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (targetPosition != null)
        {
            // Показываем линию к целевой позиции
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetPosition.position);
            
            // Показываем целевую позицию
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(targetPosition.position, 0.1f);
            
            // Стрелка направления
            Gizmos.color = Color.red;
            Vector3 forward = targetPosition.forward * 0.3f;
            Gizmos.DrawRay(targetPosition.position, forward);
        }
    }
}