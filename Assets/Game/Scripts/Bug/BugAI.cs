using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Bug
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class BugAI : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Параметры перемещения")]
        [Tooltip("Радиус блуждания вокруг текущей позиции")]
        public float wanderRadius = 10f;

        [Tooltip("Как часто выбирать новую точку (сек)")]
        public float repathInterval = 1.5f;

        [Header("Надёжность NavMesh")]
        [Tooltip("Радиус поиска ближайшей точки NavMesh при возврате из инспекции/включении")]
        public float reattachRadius = 2f;

        [Header("Access Control")]
        [Tooltip("Can this bug be inspected regardless of zone restrictions?")]
        [SerializeField] private bool alwaysAccessible = false;
        #endregion

        #region State
        private NavMeshAgent agent;
        private Animator anim;

        private float nextRepathTime;
        private bool manuallyDisabled;

        private float spawnTime;

        // --- ЗОНЫ И ДОСТУП ---
        private readonly HashSet<BugAccessZone> zones = new HashSet<BugAccessZone>();
        private InspectableObject inspectable; // целевой флаг canInspect будет управляться отсюда
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            anim  = GetComponent<Animator>();
            spawnTime = Time.time;

            inspectable = GetComponent<InspectableObject>();
            if (inspectable == null)
            {
                Debug.LogWarning($"[BugAI] На {name} нет InspectableObject — управление canInspect работать не будет");
            }
        }

        private void Start()
        {
            // Если жук уже стоит внутри зон на старте — зарегистрируемся
            CheckForAccessZone();
            RecomputeAndApplyCanInspect();
        }

        private void OnEnable()
        {
            EnsureAgentOnNavMesh();
        }

        private void Update()
        {
            if (!AgentReady() || manuallyDisabled)
            {
                UpdateAnimator(0f);
                return;
            }

            if (Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + repathInterval;
                PickNewRandomPoint();
            }

            UpdateAnimator(agent.velocity.magnitude);
        }
        #endregion

        #region Gameplay API
        public string GetBugType()
        {
            string n = gameObject.name;
            return n.Replace("(Clone)", "").Trim();
        }

        public float GetTimeSinceSpawn()
        {
            return Time.time - spawnTime;
        }

        // ==== НОВОЕ: API для зон ====

        /// <summary>Жук вошёл в зону.</summary>
        public void RegisterAccessZone(BugAccessZone zone)
        {
            if (zone == null) return;
            if (zones.Add(zone))
                RecomputeAndApplyCanInspect();
        }

        /// <summary>Жук вышел из зоны.</summary>
        public void UnregisterAccessZone(BugAccessZone zone)
        {
            if (zone == null) return;
            if (zones.Remove(zone))
                RecomputeAndApplyCanInspect();
        }

        /// <summary>Зона сообщает: «моя доступность изменилась».</summary>
        public void NotifyZoneAccessibilityChanged()
        {
            RecomputeAndApplyCanInspect();
        }

        // Оставляем для обратной совместимости (если где-то еще зовётся).
        // Ничего не устанавливаем напрямую — просто пересчитываем.
        public void SetAccessible(bool _ignored) => RecomputeAndApplyCanInspect();

        public void DisableAI(bool disable)
        {
            if (disable) OnInspectStart();
            else OnInspectEnd();
        }

        public void OnInspectStart()
        {
            manuallyDisabled = true;

            if (agent)
            {
                if (agent.enabled && agent.isOnNavMesh)
                    agent.isStopped = true;

                agent.enabled = false;
            }

            UpdateAnimator(0f);
        }

        public void OnInspectEnd()
        {
            if (agent)
            {
                AttachToNavMeshIfNeeded();
                agent.enabled = true;
                if (!agent.isOnNavMesh)
                    AttachToNavMeshIfNeeded();
                agent.isStopped = false;
            }

            manuallyDisabled = false;
            nextRepathTime   = Time.time + 0.1f;
        }
        #endregion

        #region Movement
        private void PickNewRandomPoint()
        {
            if (!AgentReady()) return;

            var origin = transform.position;
            var random = Random.insideUnitSphere * wanderRadius + origin;
            random.y = origin.y;

            if (NavMesh.SamplePosition(random, out var hit, wanderRadius, NavMesh.AllAreas))
                SetDestinationSafe(hit.position);
            else
                nextRepathTime = Time.time + 0.5f;
        }

        private float EstimatePathLength(Vector3 target)
        {
            if (!AgentReady()) return 0f;

            var path = new NavMeshPath();
            if (!agent.CalculatePath(target, path) || path.status == NavMeshPathStatus.PathInvalid)
                return 0f;

            float len = 0f;
            var c = path.corners;
            for (int i = 1; i < c.Length; i++)
                len += Vector3.Distance(c[i - 1], c[i]);
            return len;
        }

        private void SetDestinationSafe(Vector3 pos)
        {
            if (!AgentReady()) return;
            if (!agent.SetDestination(pos))
                nextRepathTime = Time.time + 0.3f;
            else
                agent.isStopped = false;
        }

        private bool AgentReady() => agent && agent.enabled && agent.isOnNavMesh;

        private void EnsureAgentOnNavMesh()
        {
            if (!agent) return;
            if (agent.enabled && !agent.isOnNavMesh)
                AttachToNavMeshIfNeeded();
        }

        private void AttachToNavMeshIfNeeded()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, Mathf.Max(0.25f, reattachRadius), NavMesh.AllAreas))
            {
                if (!agent.enabled) agent.enabled = true;
                agent.Warp(hit.position);
            }
        }

        private void UpdateAnimator(float speed)
        {
            if (!anim) return;
            anim.SetFloat("Speed", speed);
        }
        #endregion

        #region Zones → CanInspect
        private void RecomputeAndApplyCanInspect()
        {
            // 1) агрегируем доступ по всем зонам (AND)
            bool zonesAllow = true;
            foreach (var z in zones)
            {
                if (z == null) continue;
                if (!z.IsAccessible)
                {
                    zonesAllow = false;
                    break;
                }
            }

            // 2) учитываем alwaysAccessible
            bool finalCanInspect = alwaysAccessible || zonesAllow;

            // 3) толкаем в InspectableObject.canInspect
            if (inspectable != null)
                inspectable.SetInspectable(finalCanInspect);

            // (опционально можно включать/выключать подсветку/интеракт-коллайдеры тут же)
        }

        private void CheckForAccessZone()
        {
            // Если при старте уже стоим в нескольких зонах — зарегистрируйтесь во всех
            var zonesInScene = FindObjectsByType<BugAccessZone>(FindObjectsSortMode.None);
            var bugCollider = GetComponent<Collider>();

            foreach (var zone in zonesInScene)
            {
                var zoneCollider = zone ? zone.GetComponent<Collider>() : null;
                if (zoneCollider != null && bugCollider != null)
                {
                    if (zoneCollider.bounds.Intersects(bugCollider.bounds))
                        zone.RefreshBugs(); // это вызовет RegisterAccessZone(this) на нас
                }
            }
        }
        #endregion
    }
}
