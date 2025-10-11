using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugCatching
{
    public class BugJarPool : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Debug")]
        [SerializeField] private bool showDebug = false;
        #endregion

        #region Properties
        public static BugJarPool Instance { get; private set; }

        public bool HasAvailableJars => availableJars.Count > 0;

        public int AvailableJarCount => availableJars.Count;

        public int BusyJarCount => busyJars.Count;

        public int TotalJarCount => allJars.Count;
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        private List<BugJarTrap> allJars = new();
        private HashSet<BugJarTrap> availableJars = new();
        private HashSet<BugJarTrap> busyJars = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;

            DiscoverAllJars();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DiscoverAllJars();

            if (showDebug)
                Debug.Log($"[BugJarPool] Scene loaded: {scene.name}, rediscovered jars");
        }
        #endregion

        #region Public Methods
        public void DiscoverAllJars()
        {
            allJars.Clear();
            availableJars.Clear();
            busyJars.Clear();


            var foundJars = FindObjectsByType<BugJarTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var jar in foundJars)
            {
                allJars.Add(jar);
                availableJars.Add(jar);
            }

            if (showDebug)
                Debug.Log($"[BugJarPool] Discovered {allJars.Count} jars in scene");
        }

        public BugJarTrap GetAvailableJar()
        {
            if (availableJars.Count == 0)
            {
                if (showDebug)
                    Debug.LogWarning("[BugJarPool] No available jars in pool!");
                return null;
            }

            var jar = availableJars.First();
            availableJars.Remove(jar);
            busyJars.Add(jar);

            if (showDebug)
                Debug.Log($"[BugJarPool] Jar taken from pool: {jar.name}. Available: {availableJars.Count}, Busy: {busyJars.Count}");

            return jar;
        }

        public void ReturnJar(BugJarTrap jar)
        {
            if (jar == null)
            {
                Debug.LogWarning("[BugJarPool] Tried to return null jar!");
                return;
            }

            if (!allJars.Contains(jar))
            {
                Debug.LogWarning($"[BugJarPool] Tried to return jar not in pool: {jar.name}");
                return;
            }

            if (!busyJars.Contains(jar))
            {
                if (showDebug)
                    Debug.LogWarning($"[BugJarPool] Jar {jar.name} was not marked as busy, but returning anyway");
            }

            busyJars.Remove(jar);
            availableJars.Add(jar);

            if (showDebug)
                Debug.Log($"[BugJarPool] Jar returned to pool: {jar.name}. Available: {availableJars.Count}, Busy: {busyJars.Count}");
        }

        public void ResetAllJars()
        {
            availableJars.Clear();
            busyJars.Clear();

            foreach (var jar in allJars)
            {
                availableJars.Add(jar);
            }

            if (showDebug)
                Debug.Log($"[BugJarPool] All {allJars.Count} jars reset to available state");
        }
        #endregion

        #region Private Methods
        #endregion

        #region Gizmos
        #endregion
    }
}
