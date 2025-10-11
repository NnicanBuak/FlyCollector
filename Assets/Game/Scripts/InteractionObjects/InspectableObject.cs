using UnityEngine;
using UnityEngine.AI;
using Bug;

public class InspectableObject : MonoBehaviour, IInspectable
{
    [Header("Type")]
    [Tooltip("Mark this object as a bug to enable bug-specific inspect behavior")]
    [SerializeField] private bool isBug = false;

    [Header("Настройки инспекции")] [Tooltip("Можно ли инспектировать этот объект")] [SerializeField]
    private bool canInspect = true;

    [Tooltip("Автоматически начать инспекцию при старте сцены")] [SerializeField]
    private bool inspectOnAwake = false;

    [Header("Ориентация при инспекции")]
    [Tooltip("Включить настройку начальной ориентации при инспекции")]
    [SerializeField]
    private bool useCustomOrientation = false;

    [Tooltip("Начальная ротация объекта при инспекции (в градусах)")] [SerializeField]
    private Vector3 inspectRotation = Vector3.zero;

    [Tooltip("Применить ротацию относительно текущего поворота объекта")] [SerializeField]
    private bool relativeRotation = false;

    [Header("Динамическое позиционирование")]
    [Tooltip("Автоматически настраивать расстояние до камеры в зависимости от размера объекта")]
    [SerializeField]
    private bool dynamicHoldPoint = false;

    [Tooltip("Множитель расстояния (чем больше, тем дальше от камеры)")]
    [SerializeField]
    private float distanceMultiplier = 1.5f;

    [Tooltip("Минимальное расстояние до камеры")]
    [SerializeField]
    private float minDistance = 0.3f;

    [Tooltip("Максимальное расстояние до камеры")]
    [SerializeField]
    private float maxDistance = 2.0f;

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

    [Header("Audio (optional)")]
    [Tooltip("AudioSource для воспроизведения звуков (оставьте пустым для автопоиска или PlayClipAtPoint)")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Громкость звуков инспекции")]
    [SerializeField, Range(0f, 1f)] private float audioVolume = 1f;

    [Tooltip("Звук при наведении на объект")]
    [SerializeField] private AudioClip hoverClip;

    [Tooltip("Звук при начале инспекции")]
    [SerializeField] private AudioClip inspectBeginClip;

    [Tooltip("Звук при завершении инспекции")]
    [SerializeField] private AudioClip inspectEndClip;

    [Header("Debug")] [SerializeField] private bool showDebugInfo = false;

    private Outline outline;


    private NavMeshAgent navMeshAgent;
    private Animator animator;
    private Collider[] childColliders;


    private bool navMeshAgentWasEnabled;
    private bool animatorWasEnabled;
    private bool[] childCollidersWereEnabled;


    private Vector3 savedVelocity;
    private Vector3 savedPosition;
    private bool wasStopped;
    private bool wasOnNavMesh;


    private UnityEngine.AI.NavMeshAgent _agent;
    private BugAI _bugAI;

    private bool _agentStateCached;
    private bool _agentWasEnabled;
    private bool _agentWasStopped;
    private bool _agentUpdatePosWas;
    private bool _agentUpdateRotWas;

    void Awake()
    {

        InitializeOutline();


        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();


        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _bugAI = GetComponent<BugAI>();


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

    public bool IsBug() => isBug;

    void Start()
    {

        if (inspectOnAwake && canInspect)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Автоматический запуск инспекции для {gameObject.name}");
            }


            CameraController cameraController = FindFirstObjectByType<CameraController>();
            if (cameraController != null)
            {
                var startInspectMethod = cameraController.GetType().GetMethod(
                    "StartInspect",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null,
                    new System.Type[] { typeof(GameObject) },
                    null
                );
                if (startInspectMethod != null)
                {
                    startInspectMethod.Invoke(cameraController, new object[] { gameObject });
                }
            }
        }
    }


    private void InitializeOutline()
    {

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

            outline = GetComponent<Outline>();


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

        // Check bug accessibility (if this is a bug)
        if (_bugAI != null && !_bugAI.IsAccessible())
        {
            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Bug {gameObject.name} is not accessible at current focus level");
            }
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] Наведение на {gameObject.name}");
        }

        if (useOutline && outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = hoverOutlineColor;
        }

        PlayAudio(hoverClip);
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

    public bool OnInspect(Camera camera)
    {
        if (!canInspect)
        {
            if (showDebugInfo)
                Debug.LogWarning($"[InspectableObject] Инспекция отключена для {gameObject.name}");
            return false;
        }

        // Check bug accessibility (if this is a bug)
        if (_bugAI != null && !_bugAI.IsAccessible())
        {
            if (showDebugInfo)
                Debug.LogWarning($"[InspectableObject] Bug {gameObject.name} is not accessible at current focus level");
            return false;
        }

        if (showDebugInfo)
            Debug.Log($"[InspectableObject] ✓ Начало инспекции {gameObject.name}");


        if (_bugAI != null && _agent != null && !_agentStateCached)
        {

            _agentWasEnabled   = _agent.enabled;
            _agentWasStopped   = _agent.isStopped;
            _agentUpdatePosWas = _agent.updatePosition;
            _agentUpdateRotWas = _agent.updateRotation;
            _agentStateCached  = true;

            if (_agent.enabled)
            {
                _agent.isStopped = true;
                _agent.updatePosition = false;
                _agent.updateRotation = false;
            }

            _agent.enabled = false;

            if (showDebugInfo)
                Debug.Log($"[InspectableObject] NavMeshAgent временно отключён у {gameObject.name}");
        }

        return true;
    }



    public void OnInspectBegin()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] OnInspectBegin для {gameObject.name}");
        }

        PlayAudio(inspectBeginClip);


        if (disableAI && navMeshAgent != null)
        {
            navMeshAgentWasEnabled = navMeshAgent.enabled;

            if (navMeshAgentWasEnabled)
            {

                savedVelocity = navMeshAgent.velocity;
                savedPosition = transform.position;
                wasStopped = navMeshAgent.isStopped;
                wasOnNavMesh = navMeshAgent.isOnNavMesh;


                if (navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.isStopped = true;
                    navMeshAgent.ResetPath();
                }


                navMeshAgent.enabled = false;

                if (showDebugInfo)
                {
                    Debug.Log(
                        $"[InspectableObject] NavMeshAgent корректно отключен. Позиция сохранена: {savedPosition}");
                }
            }
        }


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


    public void OnInspectEnd()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[InspectableObject] OnInspectEnd для {gameObject.name}");
        }

        PlayAudio(inspectEndClip);


        if (disableAI && navMeshAgent != null && navMeshAgentWasEnabled)
        {

            transform.position = savedPosition;


            navMeshAgent.enabled = true;


            StartCoroutine(RestoreNavMeshAgentState());

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] NavMeshAgent включен обратно. Позиция восстановлена: {savedPosition}");
            }
        }

        if (_bugAI != null && _agent != null && _agentStateCached)
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);

            _agent.updatePosition = _agentUpdatePosWas;
            _agent.updateRotation = _agentUpdateRotWas;
            _agent.isStopped      = _agentWasStopped;

            if (!_agentWasEnabled)
                _agent.enabled = false;

            _agentStateCached = false;

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] BugAI включен обратно");
            }
        }


        if (disableAnimator && animator != null && animatorWasEnabled)
        {
            animator.enabled = true;

            if (showDebugInfo)
            {
                Debug.Log($"[InspectableObject] Animator включен обратно");
            }
        }


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


    private System.Collections.IEnumerator RestoreNavMeshAgentState()
    {

        yield return null;

        if (navMeshAgent != null && navMeshAgent.enabled)
        {

            if (!navMeshAgent.isOnNavMesh)
            {

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


    public bool UsesDynamicHoldPoint()
    {
        return dynamicHoldPoint;
    }


    public float GetDynamicDistance()
    {
        if (!dynamicHoldPoint)
            return 0.5f;


        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;


        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (hasBounds)
                bounds.Encapsulate(r.bounds);
            else
            {
                bounds = r.bounds;
                hasBounds = true;
            }
        }


        if (!hasBounds)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                if (hasBounds)
                    bounds.Encapsulate(c.bounds);
                else
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
            }
        }

        if (!hasBounds)
        {
            if (showDebugInfo)
                Debug.LogWarning($"[InspectableObject] Не удалось вычислить bounds для {gameObject.name}");
            return 0.5f;
        }


        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);


        float distance = maxSize * distanceMultiplier;


        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        if (showDebugInfo)
            Debug.Log($"[InspectableObject] Динамическое расстояние для {gameObject.name}: {distance:F2} (размер: {maxSize:F2})");

        return distance;
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


    public void SetInspectOnAwake(bool value)
    {
        inspectOnAwake = value;
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _bugAI = GetComponent<BugAI>();
    }


    private void PlayAudio(AudioClip clip)
    {
        if (!clip) return;

        var source = audioSource ? audioSource : GetComponent<AudioSource>();
        if (source)
        {
            source.PlayOneShot(clip, audioVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, audioVolume);
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
