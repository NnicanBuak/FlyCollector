using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Bug
{
    /// <summary>
    /// Controls bug accessibility based on camera focus level.
    /// Attach to NavMeshModifier objects or separate trigger zones.
    /// Bugs in this zone can only be inspected/caught when camera is at required focus level.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BugAccessZone : MonoBehaviour
    {
        [Header("Focus Level Requirement")]
        [Tooltip("Minimum focus level required to access bugs in this zone")]
        [SerializeField] private int requiredFocusLevel = 1;

        [Header("Zone Settings")]
        [Tooltip("Name of this zone for debugging")]
        [SerializeField] private string zoneName = "Bug Zone";

        [Tooltip("Automatically setup BoxCollider as trigger if missing")]
        [SerializeField] private bool autoSetupCollider = true;

        [Header("Auto Refresh")]
        [Tooltip("Automatically scan for new bugs periodically (helps with dynamic spawning)")]
        [SerializeField] private bool autoRefresh = true;
        [SerializeField] private float refreshInterval = 1f;

        [Header("Visual Debug")]
        [SerializeField] private bool showDebug = false;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);
        [SerializeField] private Color gizmoColorActive = new Color(0f, 1f, 0f, 0.3f);

        // Bugs currently in this zone
        private readonly HashSet<BugAI> bugsInZone = new HashSet<BugAI>();

        // Collider for zone detection
        private Collider zoneCollider;

        // Is zone currently accessible based on focus level?
        private bool isAccessible = false;

        // Auto-refresh timer
        private float nextRefreshTime = 0f;

        #region Properties
        public int RequiredFocusLevel => requiredFocusLevel;
        public bool IsAccessible => isAccessible;
        public int BugCount => bugsInZone.Count;
        public string ZoneName => zoneName;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();

            // Auto-setup collider if needed
            if (autoSetupCollider && zoneCollider == null)
            {
                zoneCollider = gameObject.AddComponent<BoxCollider>();
            }

            // Ensure collider is trigger
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
            else
            {
                Debug.LogError($"[BugAccessZone] {zoneName}: No Collider found! Add a Collider component.");
            }
        }

        private void Start()
        {
            // Subscribe to focus level changes
            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnNestLevelChanged += OnFocusLevelChanged;
                UpdateAccessibility(FocusLevelManager.Instance.CurrentNestLevel);
            }
            else
            {
                Debug.LogWarning($"[BugAccessZone] {zoneName}: FocusLevelManager.Instance is null at Start");
            }

            // Scan for bugs already in zone at start
            RefreshBugs();
            nextRefreshTime = Time.time + refreshInterval;
        }

        private void Update()
        {
            // Periodically refresh bugs (helps catch dynamically spawned bugs)
            if (autoRefresh && Time.time >= nextRefreshTime)
            {
                RefreshBugs();
                nextRefreshTime = Time.time + refreshInterval;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from focus level changes
            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnNestLevelChanged -= OnFocusLevelChanged;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if a bug entered the zone
            var bugAI = other.GetComponent<BugAI>();
            if (bugAI != null && !bugsInZone.Contains(bugAI))
            {
                bugsInZone.Add(bugAI);
                bugAI.RegisterAccessZone(this);

                if (showDebug)
                {
                    Debug.Log($"[BugAccessZone] {zoneName}: Bug '{bugAI.name}' entered (total: {bugsInZone.Count})");
                }

                // Update bug accessibility immediately
                UpdateBugAccessibility(bugAI);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Check if a bug left the zone
            var bugAI = other.GetComponent<BugAI>();
            if (bugAI != null && bugsInZone.Contains(bugAI))
            {
                bugsInZone.Remove(bugAI);
                bugAI.UnregisterAccessZone(this);

                if (showDebug)
                {
                    Debug.Log($"[BugAccessZone] {zoneName}: Bug '{bugAI.name}' exited (total: {bugsInZone.Count})");
                }
            }
        }
        #endregion

        #region Focus Level Management
        private void OnFocusLevelChanged(int newLevel)
        {
            UpdateAccessibility(newLevel);
        }

        private void UpdateAccessibility(int currentLevel)
        {
            bool wasAccessible = isAccessible;
            isAccessible = currentLevel >= requiredFocusLevel;

            if (wasAccessible != isAccessible)
            {
                if (showDebug)
                {
                    Debug.Log($"[BugAccessZone] {zoneName}: Accessibility changed to {isAccessible} (level {currentLevel}/{requiredFocusLevel})");
                }

                // Update all bugs in zone
                UpdateAllBugsAccessibility();
            }
        }

        private void UpdateAllBugsAccessibility()
        {
            foreach (var bug in bugsInZone)
            {
                if (bug != null)
                {
                    UpdateBugAccessibility(bug);
                }
            }
        }

        private void UpdateBugAccessibility(BugAI bug)
        {
            bug.SetAccessible(isAccessible);

            if (showDebug)
            {
                Debug.Log($"[BugAccessZone] {zoneName}: Set bug '{bug.name}' accessible = {isAccessible}");
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Check if a specific bug is in this zone.
        /// </summary>
        public bool ContainsBug(BugAI bug)
        {
            return bugsInZone.Contains(bug);
        }

        /// <summary>
        /// Get all bugs currently in this zone.
        /// </summary>
        public IReadOnlyCollection<BugAI> GetBugsInZone()
        {
            return bugsInZone;
        }

        /// <summary>
        /// Manually refresh all bugs in zone (useful for testing).
        /// </summary>
        public void RefreshBugs()
        {
            if (zoneCollider == null)
            {
                Debug.LogWarning($"[BugAccessZone] {zoneName}: Cannot refresh, collider is null");
                return;
            }

            // Don't clear - just add new bugs we don't have yet
            HashSet<BugAI> foundBugs = new HashSet<BugAI>();

            // Find all bugs in trigger volume
            Collider[] overlaps;

            if (zoneCollider is BoxCollider box)
            {
                overlaps = Physics.OverlapBox(
                    transform.TransformPoint(box.center),
                    Vector3.Scale(box.size / 2f, transform.lossyScale),
                    transform.rotation
                );
            }
            else if (zoneCollider is SphereCollider sphere)
            {
                overlaps = Physics.OverlapSphere(
                    transform.TransformPoint(sphere.center),
                    sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z)
                );
            }
            else
            {
                overlaps = Physics.OverlapBox(
                    zoneCollider.bounds.center,
                    zoneCollider.bounds.extents,
                    transform.rotation
                );
            }

            foreach (var col in overlaps)
            {
                var bug = col.GetComponent<BugAI>();
                if (bug != null)
                {
                    foundBugs.Add(bug);

                    // Only register if not already registered
                    if (!bugsInZone.Contains(bug))
                    {
                        bugsInZone.Add(bug);
                        bug.RegisterAccessZone(this);
                        UpdateBugAccessibility(bug);

                        if (showDebug)
                        {
                            Debug.Log($"[BugAccessZone] {zoneName}: Found new bug '{bug.name}' during refresh");
                        }
                    }
                }
            }

            if (showDebug)
            {
                Debug.Log($"[BugAccessZone] {zoneName}: Refreshed - found {foundBugs.Count} bugs, tracking {bugsInZone.Count} total");
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmos()
        {
            if (zoneCollider == null)
                zoneCollider = GetComponent<Collider>();

            if (zoneCollider != null)
            {
                Gizmos.color = Application.isPlaying && isAccessible ? gizmoColorActive : gizmoColor;
                Gizmos.matrix = transform.localToWorldMatrix;

                if (zoneCollider is BoxCollider box)
                {
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (zoneCollider is SphereCollider sphere)
                {
                    Gizmos.DrawSphere(sphere.center, sphere.radius);
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw zone name label
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                $"{zoneName}\nRequired Level: {requiredFocusLevel}\n" +
                (Application.isPlaying ? $"Accessible: {isAccessible}\nBugs: {bugsInZone.Count}" : "")
            );
            #endif
        }
        #endregion
    }
}
