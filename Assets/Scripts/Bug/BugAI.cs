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

        #region Properties
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        private NavMeshAgent agent;
        private Animator anim;

        private float nextRepathTime;
        private bool manuallyDisabled;

        // Track spawn time for analytics
        private float spawnTime;

        // Access zone management
        private BugAccessZone currentZone;
        private bool isAccessible = true; // Default accessible if not in any zone

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            anim  = GetComponent<Animator>();
            spawnTime = Time.time;
        }

        private void Start()
        {
            // Check if we're inside any BugAccessZone at start
            CheckForAccessZone();
        }

        private void CheckForAccessZone()
        {
            // Find all BugAccessZones in scene and check if we're inside any
            var zones = FindObjectsByType<BugAccessZone>(FindObjectsSortMode.None);
            foreach (var zone in zones)
            {
                // Check if bug collider overlaps with zone collider
                var zoneCollider = zone.GetComponent<Collider>();
                var bugCollider = GetComponent<Collider>();

                if (zoneCollider != null && bugCollider != null)
                {
                    if (zoneCollider.bounds.Intersects(bugCollider.bounds))
                    {
                        zone.RefreshBugs();
                        break; // Only register with one zone
                    }
                }
            }
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

        #region Public Methods
        /// <summary>
        /// Get bug type identifier (GameObject name without "(Clone)" suffix)
        /// Used for analytics and bug identification
        /// </summary>
        public string GetBugType()
        {
            string name = gameObject.name;
            // Remove "(Clone)" suffix added by Unity when instantiating
            return name.Replace("(Clone)", "").Trim();
        }

        /// <summary>
        /// Get time elapsed since bug was spawned (in seconds)
        /// Used for analytics to track catch time
        /// </summary>
        public float GetTimeSinceSpawn()
        {
            return Time.time - spawnTime;
        }

        /// <summary>
        /// Check if this bug is currently accessible for inspection/catching.
        /// Returns true if bug is in accessible zone or alwaysAccessible is true.
        /// </summary>
        public bool IsAccessible()
        {
            return alwaysAccessible || isAccessible;
        }

        /// <summary>
        /// Set bug accessibility (called by BugAccessZone).
        /// </summary>
        public void SetAccessible(bool accessible)
        {
            isAccessible = accessible;
        }

        /// <summary>
        /// Register bug entering an access zone.
        /// </summary>
        public void RegisterAccessZone(BugAccessZone zone)
        {
            currentZone = zone;
            isAccessible = zone.IsAccessible;
        }

        /// <summary>
        /// Unregister bug from access zone.
        /// </summary>
        public void UnregisterAccessZone(BugAccessZone zone)
        {
            if (currentZone == zone)
            {
                currentZone = null;
                isAccessible = true; // Default accessible when not in any zone
            }
        }

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

                agent.isStopped = true;
                agent.enabled   = false;
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

        #region Private Methods
        private void PickNewRandomPoint()
        {
            if (!AgentReady()) return;

            var origin = transform.position;
            var random = Random.insideUnitSphere * wanderRadius + origin;
            random.y = origin.y;

            if (NavMesh.SamplePosition(random, out var hit, wanderRadius, NavMesh.AllAreas))
            {
                SetDestinationSafe(hit.position);
            }
            else
            {
                nextRepathTime = Time.time + 0.5f;
            }
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
            {
                nextRepathTime = Time.time + 0.3f;
            }
            else
            {
                agent.isStopped = false;
            }
        }

        private bool AgentReady()
        {

            return agent && agent.enabled && agent.isOnNavMesh;
        }

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

        #region Gizmos
        #endregion
    }
}