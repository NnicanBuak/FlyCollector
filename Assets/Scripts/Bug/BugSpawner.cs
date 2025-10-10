using UnityEngine;
using UnityEngine.AI;
using BugData;

namespace Bug
{
    public class BugSpawner : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Настройки спавна")]
        [Tooltip("Случайное смещение от центра точки спавна (радиус)")]
        [SerializeField] private float randomOffset = 0f;

        [Tooltip("Максимальное количество жуков, которое может заспавнить этот спавнер (0 = без лимита)")]
        [SerializeField] private int maxSpawnCount = 0;

        [Header("NavMesh")]
        [Tooltip("Ограничивать спавн только участками NavMesh")]
        [SerializeField] private bool constrainToNavMesh = true;

        [Tooltip("Макс. расстояние поиска ближайшей точки NavMesh от сэмплируемой позиции")]
        [SerializeField] private float sampleMaxDistance = 2f;

        [Tooltip("Макс. число попыток найти валидную точку на NavMesh внутри радиуса")]
        [SerializeField] private int maxSampleTries = 10;

        [Tooltip("Маска областей NavMesh (по умолчанию все)")]
        [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmo = true;
        [SerializeField] private Color gizmoColor = Color.green;
        #endregion

        #region Properties
        /// <summary>
        /// Current number of bugs spawned by this spawner
        /// </summary>
        public int CurrentSpawnCount { get; private set; }

        /// <summary>
        /// Check if spawner can spawn more bugs (respects maxSpawnCount if > 0)
        /// </summary>
        public bool CanSpawn => maxSpawnCount <= 0 || CurrentSpawnCount < maxSpawnCount;

        /// <summary>
        /// Maximum spawn count (0 means unlimited)
        /// </summary>
        public int MaxSpawnCount => maxSpawnCount;
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        #endregion

        #region Public Methods
        public GameObject SpawnBug(string bugKey, BugPrefabRegistry registry)
        {
            if (!CanSpawn)
            {
                Debug.LogWarning($"[BugSpawner] Spawner '{name}' достиг лимита спавна ({CurrentSpawnCount}/{maxSpawnCount})");
                return null;
            }

            if (registry == null)
            {
                Debug.LogError("[BugSpawner] BugPrefabRegistry не передан! Убедитесь, что он назначен в BugSpawnerManager.");
                return null;
            }

            if (!registry.TryGetPrefab(bugKey, out var prefab))
            {
                Debug.LogError($"[BugSpawner] Не найден префаб для жука '{bugKey}' в реестре");
                return null;
            }


            Vector3 desired = transform.position;
            if (randomOffset > 0f)
            {
                Vector2 r = Random.insideUnitCircle * randomOffset;
                desired += new Vector3(r.x, 0f, r.y);
            }


            Vector3 spawnPosition = desired;
            if (constrainToNavMesh)
            {
                if (!TryGetPointOnNavMesh(desired, out spawnPosition))
                {

                    if (!TryGetPointOnNavMesh(transform.position, out spawnPosition))
                    {
                        Debug.LogWarning("[BugSpawner] Не удалось найти точку на NavMesh. Спавним в исходной позиции без привязки.");
                        spawnPosition = desired;
                    }
                }
            }


            var go = Instantiate(prefab, spawnPosition, transform.rotation);
            go.name = bugKey;

            CurrentSpawnCount++;

            return go;
        }

        /// <summary>
        /// Reset spawn counter (useful for respawning or restarting)
        /// </summary>
        public void ResetSpawnCount()
        {
            CurrentSpawnCount = 0;
        }

        /// <summary>
        /// Decrement spawn counter when bug is destroyed/removed
        /// </summary>
        public void DecrementSpawnCount()
        {
            if (CurrentSpawnCount > 0)
                CurrentSpawnCount--;
        }
        #endregion

        #region Private Methods
        private bool TryGetPointOnNavMesh(Vector3 basePos, out Vector3 result)
        {


            if (randomOffset > 0f)
            {
                for (int i = 0; i < Mathf.Max(1, maxSampleTries); i++)
                {
                    Vector3 candidate = basePos;
                    if (i > 0)
                    {
                        Vector2 r = Random.insideUnitCircle * randomOffset;
                        candidate = new Vector3(basePos.x + r.x, basePos.y, basePos.z + r.y);
                    }

                    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleMaxDistance, navMeshAreaMask))
                    {
                        result = hit.position;
                        return true;
                    }
                }
            }
            else
            {
                if (NavMesh.SamplePosition(basePos, out NavMeshHit hit, sampleMaxDistance, navMeshAreaMask))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = Vector3.zero;
            return false;
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmos()
        {
            if (!showDebugGizmo) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.2f);

            if (randomOffset > 0f)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
                Gizmos.DrawWireSphere(transform.position, randomOffset);
            }
        }
        #endregion
    }
}
