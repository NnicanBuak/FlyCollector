using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bug
{
    public class BugSpawnerManager : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Префабы жуков")]
        [Tooltip("Реестр префабов жуков для спавна")]
        [SerializeField] private BugPrefabRegistry prefabRegistry;

        [Header("Настройки спавна")]
        [Tooltip("Автоматически спавнить жуков при старте")]
        [SerializeField] private bool spawnOnStart = true;

        [Tooltip("Задержка перед спавном (в секундах)")]
        [SerializeField] private float spawnDelay = 0f;

        [Header("Распределение")]
        [Tooltip("Равномерно распределять жуков по спавнерам (иначе случайное распределение)")]
        [SerializeField] private bool distributeEvenly = true;

        [Tooltip("Разрешить спавн нескольких жуков на одном спавнере")]
        [SerializeField] private bool allowMultiplePerSpawner = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        #endregion

        #region Properties
        #endregion

        #region Events
        #endregion

        #region Unity Lifecycle
        private BugSpawner[] spawners;
        private List<GameObject> spawnedBugs = new List<GameObject>();

        private void Start()
        {
            if (spawnOnStart)
            {
                if (spawnDelay > 0f)
                    Invoke(nameof(SpawnAllBugs), spawnDelay);
                else
                    SpawnAllBugs();
            }
        }
        #endregion

        #region Public Methods
        public void SpawnAllBugs()
        {

            if (prefabRegistry == null)
            {
                Debug.LogError("[BugSpawnerManager] BugPrefabRegistry не назначен! Назначьте его в Inspector.");
                return;
            }


            if (TargetBugsRuntime.Instance == null || TargetBugsRuntime.Instance.BugsToSpawn == null)
            {
                Debug.LogError("[BugSpawnerManager] TargetBugsRuntime не найден или список жуков для спавна пуст!");
                return;
            }

            List<string> bugsToSpawn = TargetBugsRuntime.Instance.BugsToSpawn;
            if (bugsToSpawn.Count == 0)
            {
                Debug.LogWarning("[BugSpawnerManager] Список жуков для спавна пуст!");
                return;
            }


            spawners = Object.FindObjectsByType<BugSpawner>(FindObjectsSortMode.None);
            if (spawners == null || spawners.Length == 0)
            {
                Debug.LogError("[BugSpawnerManager] Не найдено ни одного BugSpawner на сцене!");
                return;
            }


            int maxCapacity = CalculateTotalSpawnCapacity();
            int maxBugs = allowMultiplePerSpawner ? Mathf.Min(bugsToSpawn.Count, maxCapacity) : Mathf.Min(bugsToSpawn.Count, spawners.Length);
            List<string> bugsToSpawnClamped = bugsToSpawn.Take(maxBugs).ToList();

            if (showDebugInfo)
            {
                Debug.Log($"[BugSpawnerManager] Найдено {spawners.Length} спавнеров (макс. ёмкость: {maxCapacity})");
                Debug.Log($"[BugSpawnerManager] Нужно заспавнить {bugsToSpawn.Count} жуков (макс: {maxBugs})");

                if (maxBugs < bugsToSpawn.Count)
                {
                    Debug.LogWarning($"[BugSpawnerManager] Недостаточно мест для спавна! Будет заспавнено только {maxBugs} из {bugsToSpawn.Count} жуков. " +
                                    $"Увеличьте лимиты спавнеров, добавьте больше спавнеров или включите allowMultiplePerSpawner.");
                }
            }


            spawnedBugs.Clear();


            if (distributeEvenly)
                SpawnEvenly(bugsToSpawnClamped);
            else
                SpawnRandomly(bugsToSpawnClamped);

            if (showDebugInfo)
            {
                int targetsCount = TargetBugsRuntime.Instance.Targets?.Count ?? 0;
                Debug.Log($"[BugSpawnerManager] ✓ Заспавнено {spawnedBugs.Count} жуков (целевых для ловли: {targetsCount})");
            }
        }

        public void DespawnAllBugs()
        {
            foreach (var bug in spawnedBugs)
            {
                if (bug != null)
                    Destroy(bug);
            }

            spawnedBugs.Clear();
            ResetAllSpawners();

            if (showDebugInfo)
            {
                Debug.Log("[BugSpawnerManager] Все жуки удалены");
            }
        }

        public List<GameObject> GetSpawnedBugs() => spawnedBugs;

        public int GetSpawnedBugsCount() => spawnedBugs.Count(b => b != null);

        /// <summary>
        /// Reset spawn counters on all spawners
        /// </summary>
        public void ResetAllSpawners()
        {
            if (spawners == null) return;

            foreach (var spawner in spawners)
            {
                if (spawner != null)
                    spawner.ResetSpawnCount();
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Calculate total spawn capacity considering spawner limits
        /// </summary>
        private int CalculateTotalSpawnCapacity()
        {
            int capacity = 0;
            foreach (var spawner in spawners)
            {
                if (spawner.MaxSpawnCount > 0)
                    capacity += spawner.MaxSpawnCount;
                else
                    return int.MaxValue; // If any spawner has unlimited capacity, total is unlimited
            }
            return capacity > 0 ? capacity : int.MaxValue;
        }

        private void SpawnEvenly(List<string> bugs)
        {
            int spawnerIndex = 0;
            int attempts = 0;
            int maxAttempts = bugs.Count * spawners.Length; // Prevent infinite loop

            foreach (string bugKey in bugs)
            {
                bool spawned = false;

                // Try to find available spawner
                while (!spawned && attempts < maxAttempts)
                {
                    BugSpawner spawner = spawners[spawnerIndex];

                    if (spawner.CanSpawn)
                    {
                        GameObject bug = spawner.SpawnBug(bugKey, prefabRegistry);

                        if (bug != null)
                        {
                            spawnedBugs.Add(bug);
                            spawned = true;

                            if (showDebugInfo)
                            {
                                Debug.Log($"[BugSpawnerManager] Заспавнен '{bugKey}' на спавнере {spawner.name} ({spawner.CurrentSpawnCount}/{spawner.MaxSpawnCount})");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[BugSpawnerManager] Не удалось заспавнить '{bugKey}' на спавнере {spawner.name}");
                        }
                    }

                    spawnerIndex = (spawnerIndex + 1) % spawners.Length;
                    attempts++;
                }

                if (!spawned)
                {
                    Debug.LogWarning($"[BugSpawnerManager] Не удалось найти доступный спавнер для '{bugKey}' (все спавнеры заполнены)");
                }
            }
        }

        private void SpawnRandomly(List<string> bugs)
        {

            HashSet<BugSpawner> usedSpawners = new HashSet<BugSpawner>();

            foreach (string bugKey in bugs)
            {
                BugSpawner spawner = null;


                if (allowMultiplePerSpawner)
                {
                    // Find spawners that can still spawn
                    var availableSpawners = spawners.Where(s => s.CanSpawn).ToArray();

                    if (availableSpawners.Length == 0)
                    {
                        Debug.LogWarning($"[BugSpawnerManager] Не найдено доступных спавнеров для '{bugKey}' (все достигли лимита)");
                        continue;
                    }

                    spawner = availableSpawners[Random.Range(0, availableSpawners.Length)];
                }
                else
                {

                    var availableSpawners = spawners.Where(s => !usedSpawners.Contains(s) && s.CanSpawn).ToArray();

                    if (availableSpawners.Length == 0)
                    {
                        Debug.LogWarning($"[BugSpawnerManager] Не хватает доступных спавнеров для жука '{bugKey}' (нужно больше спавнеров или включите allowMultiplePerSpawner)");
                        continue;
                    }

                    spawner = availableSpawners[Random.Range(0, availableSpawners.Length)];
                    usedSpawners.Add(spawner);
                }


                GameObject bug = spawner.SpawnBug(bugKey, prefabRegistry);

                if (bug != null)
                {
                    spawnedBugs.Add(bug);

                    if (showDebugInfo)
                    {
                        Debug.Log($"[BugSpawnerManager] Заспавнен '{bugKey}' на спавнере {spawner.name} ({spawner.CurrentSpawnCount}/{spawner.MaxSpawnCount})");
                    }
                }
                else
                {
                    Debug.LogWarning($"[BugSpawnerManager] Не удалось заспавнить '{bugKey}' на спавнере {spawner.name}");
                }
            }
        }
        #endregion

        #region Gizmos
        #endregion
    }
}
