using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts.Bug
{

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


        [Header("Activation Mode")]
        [Tooltip("Select how this access zone becomes accessible: by nest focus level or when a specific FocusableObject is focused")]
        [SerializeField] private ActivationMode activationMode = ActivationMode.ByFocusLevel;

        [Tooltip("When Activation Mode = ByFocusableObject, the zone is accessible only while this FocusableObject is focused")]
        [SerializeField] private FocusableObject boundFocusableObject;

        private enum ActivationMode
        {
            ByFocusLevel = 0,
            ByFocusableObject = 1
        }

        private readonly HashSet<BugAI> bugsInZone = new HashSet<BugAI>();


        private Collider zoneCollider;


        private bool isAccessible = false;


        private float nextRefreshTime = 0f;

        #region Properties
        public int RequiredFocusLevel => requiredFocusLevel;
        public bool IsAccessible => isAccessible;
        public int BugCount => bugsInZone.Count;
        public string ZoneName => zoneName;
        public FocusableObject BoundFocusableObject => boundFocusableObject;
        public string ActivationModeName => activationMode.ToString();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();


            if (autoSetupCollider && zoneCollider == null)
            {
                zoneCollider = gameObject.AddComponent<BoxCollider>();
            }


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

            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnNestLevelChanged += OnFocusLevelChanged;
                // initial update will consider activation mode inside UpdateAccessibility
                UpdateAccessibility(FocusLevelManager.Instance.CurrentNestLevel);
            }
            else
            {
                Debug.LogWarning($"[BugAccessZone] {zoneName}: FocusLevelManager.Instance is null at Start");
            }

            // Warn if activation mode expects a bound object but none assigned
            if (activationMode == ActivationMode.ByFocusableObject && boundFocusableObject == null)
            {
                Debug.LogWarning($"[BugAccessZone] {zoneName}: ActivationMode is set to ByFocusableObject but Bound FocusableObject is not assigned.");
            }


            RefreshBugs();
            nextRefreshTime = Time.time + refreshInterval;
        }

        private void Update()
        {

            if (autoRefresh && Time.time >= nextRefreshTime)
            {
                RefreshBugs();
                nextRefreshTime = Time.time + refreshInterval;
            }
        }

        private void OnDestroy()
        {

            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.OnNestLevelChanged -= OnFocusLevelChanged;
            }
        }

        private void OnTriggerEnter(Collider other)
        {

            var bugAI = other.GetComponent<BugAI>();
            if (bugAI != null && !bugsInZone.Contains(bugAI))
            {
                bugsInZone.Add(bugAI);
                bugAI.RegisterAccessZone(this);

                if (showDebug)
                {
                    Debug.Log($"[BugAccessZone] {zoneName}: Bug '{bugAI.name}' entered (total: {bugsInZone.Count})");
                }


                UpdateBugAccessibility(bugAI);
            }
        }

        private void OnTriggerExit(Collider other)
        {

            var bugAI = other.GetComponent<BugAI>();
            if (bugAI != null && bugsInZone.Contains(bugAI))
            {
                bugsInZone.Remove(bugAI);
                bugAI.UnregisterAccessZone(this);

                if (showDebug)
                {
                    Debug.Log($"[BugAccessZone] {zoneName}: Bug '{bugAI.name}' exited (total: {bugsInZone.Count})");
                }

                UpdateBugAccessibility(bugAI);
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

            if (activationMode == ActivationMode.ByFocusableObject)
            {
                // Determine current focused GameObject from FocusLevelManager
                var focused = FocusLevelManager.Instance != null ? FocusLevelManager.Instance.GetLastFocusedObject() : null;
                isAccessible = (focused != null && boundFocusableObject != null && focused == boundFocusableObject.gameObject);
            }
            else
            {
                isAccessible = currentLevel >= requiredFocusLevel;
            }

            if (wasAccessible != isAccessible)
            {
                if (showDebug)
                {
                    if (activationMode == ActivationMode.ByFocusableObject)
                    {
                        Debug.Log($"[BugAccessZone] {zoneName}: Accessibility changed to {isAccessible} (bound object: {(boundFocusableObject!=null?boundFocusableObject.name:"<null>")})");
                    }
                    else
                    {
                        Debug.Log($"[BugAccessZone] {zoneName}: Accessibility changed to {isAccessible} (level {currentLevel}/{requiredFocusLevel})");
                    }
                }


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
            bug.NotifyZoneAccessibilityChanged();

            if (showDebug)
            {
                Debug.Log($"[BugAccessZone] {zoneName}: Set bug '{bug.name}' accessible = {isAccessible}");
            }
        }
        #endregion

        #region Public Methods

        public bool ContainsBug(BugAI bug)
        {
            return bugsInZone.Contains(bug);
        }


        public IReadOnlyCollection<BugAI> GetBugsInZone()
        {
            return bugsInZone;
        }


        public void RefreshBugs()
        {
            if (zoneCollider == null)
            {
                Debug.LogWarning($"[BugAccessZone] {zoneName}: Cannot refresh, collider is null");
                return;
            }


            HashSet<BugAI> foundBugs = new HashSet<BugAI>();


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

        public void SetActivationModeByFocusLevel()
        {
            activationMode = ActivationMode.ByFocusLevel;
            UpdateAccessibility(FocusLevelManager.Instance != null ? FocusLevelManager.Instance.CurrentNestLevel : 0);
        }

        public void BindToFocusableObject(FocusableObject focusable)
        {
            boundFocusableObject = focusable;
            UpdateAccessibility(FocusLevelManager.Instance != null ? FocusLevelManager.Instance.CurrentNestLevel : 0);
        }

        public void UnbindFocusableObject()
        {
            boundFocusableObject = null;
            UpdateAccessibility(FocusLevelManager.Instance != null ? FocusLevelManager.Instance.CurrentNestLevel : 0);
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
