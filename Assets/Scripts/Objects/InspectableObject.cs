using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Компонент для объектов, которые можно инспектировать при клике ЛКМ
/// Работает совместно с CameraController
/// Автоматически отключает AI, Animator и коллайдеры детей при инспекции
/// </summary>
public class InspectableObject : MonoBehaviour, IInspectable
{
    [Header("Настройки инспекции")] [Tooltip("Можно ли инспектировать этот объект")] [SerializeField]
    private bool canInspect = true;

    [Header("Ориентация при инспекции")]
    [Tooltip("Включить настройку начальной ориентации при инспекции")]
    [SerializeField]
    private bool useCustomOrientation = false;

    [Tooltip("Начальная ротация объекта при инспекции (в градусах)")] [SerializeField]
    private Vector3 inspectRotation = Vector3.zero;

    [Tooltip("Применить ротацию относительно текущего поворота объекта")] [SerializeField]
    private bool relativeRotation = false;

    [Header("Отключение компонентов при инспекции")]
    [Tooltip("Отключать AI (NavMeshAgent) при инспекции")]
    [SerializeField]
    private bool disableAI = true;

    [Tooltip("Отключать Animator при инспекции")] [SerializeField]
    private bool disableAnimator = true;

    [Tooltip("Отключать коллайдеры детей при инспекции")] [SerializeField]
    private bool disableChildColliders = true;

    [Header("Опциональный Outline")] [Tooltip("Включать ли outline при наведении")] [SerializeField]
    private bool useOutline = true;

    [Tooltip("Outline компонент (оставьте пустым для автопоиска)")] [SerializeField]
    private Outline manualOutline;

    [SerializeField] private Color hoverOutlineColor = Color.white;

    [Header("Debug")] [SerializeField] private bool showDebugInfo = false;

    private Outline outline;

    // Компоненты для отключения при инспекции
    private NavMeshAgent navMeshAgent;
    private MonoBehaviour bugAI;
    private Animator animator;
    private Collider[] childColliders;

    // Состояния компонентов перед инспекцией
    private bool navMeshAgentWasEnabled;
    private bool bugAIWasEnabled;
    private bool animatorWasEnabled;
    private bool[] childCollidersWereEnabled;

    // Данные NavMeshAgent для восстановления
    private Vector3 savedVelocity;
    private Vector3 savedPosition;
    private bool wasStopped;
    private bool wasOnNavMesh;

    void Awake()
    {
        // Инициализируем Outline - сначала пробуем ручное назначение
        InitializeOutline();

        // Кэшируем компоненты
        navMeshAgent = GetComponent<NavMeshAgent>();

        // Ищем BugAI компонент
        MonoBehaviour[] allComponents = GetComponents<MonoBehaviour>();
        foreach (var comp in allComponents)
        {
            if (comp.GetType().Name == "BugAI")
            {
                bugAI = comp;
                break;
            }
        }

        animator = GetComponent<Animator>();

        // Получаем коллайдеры детей (исключая коллайдеры самого объекта)
        if (disableChildColliders)
        {
            Collider[] allColliders = GetComponentsInChildren<Collider>();
            Collider[] selfColliders = GetComponents<Collider>();

            System.Collections.Generic.List<Collider> childCollidersList =
                new System.Collections.Generic.List<Collider>();
            foreach (var col in allColliders)
            {
                bool isSelfCollider = false;
                foreach (var selfCol in selfColliders)
                {
                    if (col == selfCol)
                    {
                        isSelfCollider = true;
                        break;
                    }
                }

                if (!isSelfCollider)
                {
                    childCollidersList.Add(col);
                }
            }

            childColliders = childCollidersList.ToArray();
            childCollidersWereEnabled = new bool[childColliders.Length];

            if (showDebugInfo && childColliders.Length > 0)
            {
                Debug.Log($"[InspectableObject] Найдено {childColliders.Length} коллайдеров детей у {gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Инициализирует Outline компонент - ищет автоматически или использует ручное назначение
    /// </summary>
    private void InitializeOutline()
    {
        // Приоритет: ручное назначение > поиск на объекте > поиск в детях
        if (manualOutline != null)
        {
            outline = manualOutline;
            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Использован ручно назначенный Outline на {outline.gameObject.name}");
            }
        }
        else
        {
            // Автопоиск: сначала на самом объекте
            outline = GetComponent<Outline>();

            // Если не найден, ищем в дочерних объектах
            if (outline == null)
            {
                outline = GetComponentInChildren<Outline>();
                if (outline != null && showDebugInfo)
                {
                    Debug.Log($"[InspectableObject] Найден Outline в дочернем объекте: {outline.gameObject.name}");
                }
            }
            else if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Найден Outline на объекте: {gameObject.name}");
            }
        }

        // Настраиваем Outline
        if (outline != null && useOutline)
        {
            outline.enabled = false;

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Outline инициализирован для {gameObject.name}");
            }
        }
        else if (useOutline && outline == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[InspectableObject] Outline не найден для {gameObject.name}, но useOutline включен");
            }
        }
    }

    public void OnHoverEnter()
    {
        if (!canInspect) return;

        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] Наведение на {gameObject.name}");
        }

        if (useOutline && outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = hoverOutlineColor;
        }
    }

    public void OnHoverExit()
    {
        if (!canInspect) return;

        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] Уход с {gameObject.name}");
        }

        if (useOutline && outline != null)
        {
            outline.enabled = false;
        }
    }

    public void OnInspect(Camera camera)
    {
        if (!canInspect)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[InspectableObject] Инспекция отключена для {gameObject.name}");
            }

            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] ✓ Начало инспекции {gameObject.name}");
        }
    }

    /// <summary>
    /// Вызывается при начале инспекции - отключает AI, Animator и коллайдеры
    /// </summary>
    public void OnInspectBegin()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] OnInspectBegin для {gameObject.name}");
        }

        // Правильно отключаем NavMeshAgent
        if (disableAI && navMeshAgent != null)
        {
            navMeshAgentWasEnabled = navMeshAgent.enabled;

            if (navMeshAgentWasEnabled)
            {
                // Сохраняем состояние NavMeshAgent
                savedVelocity = navMeshAgent.velocity;
                savedPosition = transform.position;
                wasStopped = navMeshAgent.isStopped;
                wasOnNavMesh = navMeshAgent.isOnNavMesh;

                // Останавливаем агента перед отключением
                if (navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.isStopped = true;
                    navMeshAgent.ResetPath();
                }

                // Отключаем NavMeshAgent
                navMeshAgent.enabled = false;

                if (showDebugInfo)
                {
                    Debug.Log(
                        $"[InspectableObject] NavMeshAgent корректно отключен. Позиция сохранена: {savedPosition}");
                }
            }
        }

        // Отключаем BugAI (если есть)
        if (disableAI && bugAI != null)
        {
            bugAIWasEnabled = bugAI.enabled;
            if (bugAIWasEnabled)
            {
                // Пытаемся вызвать DisableAI если метод существует
                var disableMethod = bugAI.GetType().GetMethod("DisableAI");
                if (disableMethod != null)
                {
                    disableMethod.Invoke(bugAI, null);
                }

                bugAI.enabled = false;

                if (showDebugInfo)
                {
                    Debug.Log($"[InspectableObject] BugAI отключен");
                }
            }
        }

        // Отключаем Animator
        if (disableAnimator && animator != null)
        {
            animatorWasEnabled = animator.enabled;
            if (animatorWasEnabled)
            {
                animator.enabled = false;

                if (showDebugInfo)
                {
                    Debug.Log($"[InspectableObject] Animator отключен");
                }
            }
        }

        // Отключаем коллайдеры детей
        if (disableChildColliders && childColliders != null)
        {
            for (int i = 0; i < childColliders.Length; i++)
            {
                if (childColliders[i] != null)
                {
                    childCollidersWereEnabled[i] = childColliders[i].enabled;
                    childColliders[i].enabled = false;
                }
            }

            if (showDebugInfo && childColliders.Length > 0)
            {
                Debug.Log($"[InspectableObject] Отключено {childColliders.Length} коллайдеров детей");
            }
        }
    }

    /// <summary>
    /// Вызывается при окончании инспекции - восстанавливает все компоненты
    /// </summary>
    public void OnInspectEnd()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] OnInspectEnd для {gameObject.name}");
        }

        // Правильно восстанавливаем NavMeshAgent
        if (disableAI && navMeshAgent != null && navMeshAgentWasEnabled)
        {
            // Восстанавливаем позицию перед включением NavMeshAgent
            transform.position = savedPosition;

            // Включаем NavMeshAgent
            navMeshAgent.enabled = true;

            // Ждем один кадр, чтобы NavMeshAgent инициализировался
            StartCoroutine(RestoreNavMeshAgentState());

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] NavMeshAgent включен обратно. Позиция восстановлена: {savedPosition}");
            }
        }

        // Восстанавливаем BugAI (если есть)
        if (disableAI && bugAI != null && bugAIWasEnabled)
        {
            bugAI.enabled = true;

            // Пытаемся вызвать EnableAI если метод существует
            var enableMethod = bugAI.GetType().GetMethod("EnableAI");
            if (enableMethod != null)
            {
                enableMethod.Invoke(bugAI, null);
            }

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] BugAI включен обратно");
            }
        }

        // Восстанавливаем Animator
        if (disableAnimator && animator != null && animatorWasEnabled)
        {
            animator.enabled = true;

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Animator включен обратно");
            }
        }

        // Восстанавливаем коллайдеры детей
        if (disableChildColliders && childColliders != null)
        {
            for (int i = 0; i < childColliders.Length; i++)
            {
                if (childColliders[i] != null && childCollidersWereEnabled[i])
                {
                    childColliders[i].enabled = true;
                }
            }

            if (showDebugInfo && childColliders.Length > 0)
            {
                Debug.Log($"[InspectableObject] Включено {childColliders.Length} коллайдеров детей обратно");
            }
        }
    }

    /// <summary>
    /// Корутина для восстановления состояния NavMeshAgent после включения
    /// </summary>
    private System.Collections.IEnumerator RestoreNavMeshAgentState()
    {
        // Ждем один кадр, чтобы NavMeshAgent полностью инициализировался
        yield return null;

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            // Проверяем, что агент на NavMesh
            if (!navMeshAgent.isOnNavMesh)
            {
                // Пытаемся найти ближайшую точку на NavMesh
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;

                    if (showDebugInfo)
                    {
                        Debug.Log($"[InspectableObject] Объект {gameObject.name} перемещен на NavMesh: {hit.position}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[InspectableObject] Не удалось найти NavMesh рядом с {gameObject.name}!");
                }
            }

            // Восстанавливаем состояние остановки
            navMeshAgent.isStopped = wasStopped;

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Состояние NavMeshAgent восстановлено для {gameObject.name}");
            }
        }
    }

    public Quaternion GetInspectRotation()
    {
        if (!useCustomOrientation)
        {
            return transform.rotation;
        }

        if (relativeRotation)
        {
            return transform.rotation * Quaternion.Euler(inspectRotation);
        }
        else
        {
            return Quaternion.Euler(inspectRotation);
        }
    }

    public bool UsesCustomOrientation()
    {
        return useCustomOrientation;
    }

    public void SetInspectable(bool value)
    {
        canInspect = value;

        if (!value && outline != null)
        {
            outline.enabled = false;
        }
    }

    public bool CanInspect()
    {
        return canInspect;
    }

    public void SetHoverColor(Color color)
    {
        hoverOutlineColor = color;
    }

    public void SetInspectOrientation(Vector3 rotation, bool relative = false)
    {
        useCustomOrientation = true;
        inspectRotation = rotation;
        relativeRotation = relative;
    }

    /// <summary>
    /// Устанавливает Outline компонент вручную
    /// </summary>
    /// <param name="newOutline">Новый Outline компонент</param>
    public void SetOutline(Outline newOutline)
    {
        manualOutline = newOutline;
        outline = newOutline;

        if (outline != null && useOutline)
        {
            outline.enabled = false;

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Outline установлен вручную для {gameObject.name}");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!useCustomOrientation) return;

        Quaternion targetRot = relativeRotation
            ? transform.rotation * Quaternion.Euler(inspectRotation)
            : Quaternion.Euler(inspectRotation);

        Gizmos.matrix = Matrix4x4.TRS(transform.position, targetRot, Vector3.one);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(Vector3.zero, Vector3.right * 0.3f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(Vector3.zero, Vector3.up * 0.3f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.3f);
    }
}