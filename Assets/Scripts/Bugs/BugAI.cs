using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
public class BugAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private bool wasActive = true;
    private bool isMoving = false;
    private bool isManuallyDisabled = false;

    // --- EVENTS ---
    /// <summary>
    /// Вызывается, когда баг начинает движение к новой цели.
    /// Параметр: позиция цели.
    /// </summary>
    public event Action<Vector3> OnMove;

    [Header("События (настраиваются в Inspector)")]
    /// <summary>
    /// Вызывается когда баг начинает движение. В Inspector можно привязать методы.
    /// </summary>
    [Tooltip("Событие при начале движения")]
    public UnityEvent<Vector3> OnMoveStart;

    /// <summary>
    /// Вызывается когда баг останавливается.
    /// </summary>
    [Tooltip("Событие при остановке")]
    public UnityEvent OnMoveStop;

    [Header("Настройки движения")]
    [SerializeField] private float walkRadius = 10f;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 5f;
    [SerializeField] private float moveSpeed = 1.5f;

    [Header("Оптимизация")]
    [SerializeField] private bool useOptimization = true;
    [SerializeField] private float updateInterval = 0.2f;

    private Vector3 homePosition;
    private float waitTimer;
    private float updateTimer;
    private bool isWaiting;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position;

        agent.speed = moveSpeed;
        agent.acceleration = 4f;
        agent.angularSpeed = 180f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
        agent.avoidancePriority = UnityEngine.Random.Range(40, 60);

        waitTimer = UnityEngine.Random.Range(0f, maxWaitTime);
        isWaiting = true;
    }

    void Update()
    {
        if (useOptimization)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer < updateInterval) return;
            updateTimer = 0f;
        }

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                MoveToRandomPoint();
                isWaiting = false;
            }
        }
        else
        {
            // Проверяем, достиг ли баг цели
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                {
                    // Баг остановился
                    if (isMoving)
                    {
                        isMoving = false;
                        OnMoveStop?.Invoke();
                    }

                    waitTimer = UnityEngine.Random.Range(minWaitTime, maxWaitTime);
                    isWaiting = true;
                }
            }
        }
    }

    void MoveToRandomPoint()
    {
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * walkRadius;
        randomDirection += homePosition;

        if (NavMesh.SamplePosition(randomDirection, out var hit, walkRadius, NavMesh.AllAreas))
        {
            SetDestinationAndNotify(hit.position);
        }
    }

    /// <summary>
    /// Публичный способ инициировать движение извне и получить ивент.
    /// </summary>
    public void MoveTo(Vector3 worldTarget)
    {
        SetDestinationAndNotify(worldTarget);
    }

    /// <summary>
    /// Централизованно ставим цель и уведомляем подписчиков.
    /// </summary>
    private void SetDestinationAndNotify(Vector3 target)
    {
        if (agent == null || !agent.enabled) return;
        
        // Если уже почти там — не дергаем ивент лишний раз
        if (!agent.pathPending && Vector3.Distance(agent.transform.position, target) <= agent.stoppingDistance)
            return;

        agent.SetDestination(target);
        
        // Уведомляем о начале движения
        if (!isMoving)
        {
            isMoving = true;
            OnMoveStart?.Invoke(target);
        }

        // Старый ивент для обратной совместимости
        OnMove?.Invoke(target);
    }

    public void DisableAI()
    {
        if (agent != null)
        {
            wasActive = agent.enabled; // Сохраняем текущее состояние
            isManuallyDisabled = true;
        
            // Останавливаем движение
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        
            // Отключаем компонент
            agent.enabled = false;
        
            Debug.Log($"[BugAI] DisableAI на {gameObject.name}: wasActive={wasActive}, isManuallyDisabled={isManuallyDisabled}");
        }
    }

    public void EnableAI()
    {
        if (agent != null && isManuallyDisabled)
        {
            isManuallyDisabled = false;
        
            // Включаем агента независимо от wasActive (BugManager потом оптимизирует)
            agent.enabled = true;
            agent.isStopped = false;
        
            Debug.Log($"[BugAI] EnableAI на {gameObject.name}: agent.enabled={agent.enabled}, isManuallyDisabled={isManuallyDisabled}");
        
            // Даем BugManager время на оптимизацию
            StartCoroutine(DelayedLogCheck());
        }
    }

    private System.Collections.IEnumerator DelayedLogCheck()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log($"[BugAI] Через 1 сек после EnableAI на {gameObject.name}: agent.enabled={agent.enabled}");
    }
    
    public bool IsManuallyDisabled()
    {
        return isManuallyDisabled;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(homePosition != Vector3.zero ? homePosition : transform.position, walkRadius);
    }

    void OnBecameInvisible()
    {
        if (agent != null && useOptimization && wasActive)
        {
            agent.enabled = false;
        }
    }

    void OnBecameVisible()
    {
        if (agent != null && useOptimization && wasActive)
        {
            agent.enabled = true;
        }
    }
}