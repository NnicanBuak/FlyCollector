using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Bug;

namespace BugCatching
{
    [DisallowMultipleComponent]
    public class BugManager : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Настройки производительности")]
        [Min(0)] [SerializeField] private int maxActiveBugs = 30;
        [Min(0f)] [SerializeField] private float distanceToActivate = 20f;
        [Min(0.02f)] [SerializeField] private float updateFrequency = 0.5f;

        [Header("Кого считать центром внимания")]
        [Tooltip("Если оставить пустым, менеджер попробует взять Camera.main")]
        [SerializeField] private Transform target;

        [Header("Поиск и авто‑обновление списка жуков")]
        [SerializeField] private bool refreshBugsOnEnable = true;
        [SerializeField] private bool autoRescan = false;
        [SerializeField] private float rescanInterval = 5f;

        [Header("Отладка")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColorActive = new Color(0f, 1f, 0.2f, 0.15f);
        #endregion

        #region Properties
        private class BugEntry
        {
            public BugAI ai;
            public Transform t;
            public NavMeshAgent agent;
            public bool managedActive;
            public float lastDistSq;
        }
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        private readonly List<BugEntry> _bugs = new List<BugEntry>(128);
        private Transform _focus;
        private float _optTimer;
        private float _rescanTimer;

        private void OnEnable()
        {
            ResolveFocus();
            if (refreshBugsOnEnable)
                RebuildBugList();


            ForceOptimizeNow();
        }

        private void Update()
        {

            if (!_focus)
                ResolveFocus();


            _optTimer += Time.deltaTime;
            if (_optTimer >= Mathf.Max(0.02f, updateFrequency))
            {
                _optTimer = 0f;
                OptimizeBugs();
            }


            if (autoRescan)
            {
                _rescanTimer += Time.deltaTime;
                if (_rescanTimer >= Mathf.Max(1f, rescanInterval))
                {
                    _rescanTimer = 0f;
                    RebuildBugList();
                }
            }
        }

        private void OnDisable()
        {

            for (int i = 0; i < _bugs.Count; i++)
            {
                var b = _bugs[i];
                if (b != null && b.agent)
                    b.agent.enabled = true;
                if (b != null)
                    b.managedActive = true;
            }
        }
        #endregion

        #region Public Methods
        public void RebuildBugList()
        {
            _bugs.Clear();


#if UNITY_2023_1_OR_NEWER
            var found = Object.FindObjectsByType<BugAI>(FindObjectsSortMode.None);
#else
            var found = Object.FindObjectsOfType<BugAI.BugAI>(true);
#endif
            foreach (var ai in found)
            {
                if (ai == null) continue;
                var agent = ai.GetComponent<NavMeshAgent>();
                if (!agent) continue;

                _bugs.Add(new BugEntry
                {
                    ai = ai,
                    t = ai.transform,
                    agent = agent,
                    managedActive = agent.enabled,
                    lastDistSq = float.PositiveInfinity
                });
            }
        }

        public void ForceOptimizeNow()
        {
            _optTimer = 0f;
            OptimizeBugs();
        }
        #endregion

        #region Private Methods
        private void ResolveFocus()
        {
            if (target)
            {
                _focus = target;
                return;
            }

            var cam = Camera.main;
            _focus = cam ? cam.transform : null;
        }

        private void OptimizeBugs()
        {
            if (_bugs.Count == 0) return;
            if (!_focus) return;


            var focusPos = _focus.position;
            for (int i = 0; i < _bugs.Count; i++)
            {
                var b = _bugs[i];
                if (b == null || b.t == null)
                {
                    continue;
                }
                b.lastDistSq = (b.t.position - focusPos).sqrMagnitude;
            }


            _bugs.Sort((a, b) => a.lastDistSq.CompareTo(b.lastDistSq));


            float distSq = distanceToActivate <= 0f ? 0f : distanceToActivate * distanceToActivate;

            int activated = 0;
            for (int i = 0; i < _bugs.Count; i++)
            {
                var entry = _bugs[i];
                if (entry == null || !entry.agent) continue;

                bool withinDistance = entry.lastDistSq <= distSq;
                bool withinQuota = activated < maxActiveBugs;
                bool shouldBeActive = withinDistance && withinQuota;

                if (entry.managedActive != shouldBeActive)
                {
                    SetAgentActive(entry, shouldBeActive);
                }

                if (shouldBeActive)
                    activated++;
            }
        }

        private static void SetAgentActive(BugEntry entry, bool active)
        {

            entry.agent.enabled = active;
            entry.managedActive = active;



            if (!active)
            {
                var anim = entry.ai ? entry.ai.GetComponent<Animator>() : null;
                if (anim)
                {

                    anim.SetFloat("Speed", 0f);
                }
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Transform f = target ? target : (Camera.main ? Camera.main.transform : null);
            if (!f) return;
            Gizmos.color = gizmoColorActive;
            Gizmos.DrawWireSphere(f.position, distanceToActivate);
        }
        #endregion
    }
}
