// BugSpawner.cs
using UnityEngine;

public class BugSpawner : MonoBehaviour
{
    [Header("Точки спавна")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Resources-путь к префабам")]
    [Tooltip("Ищем в Resources/<resourcesBasePath>/<bug>_Variant")]
    [SerializeField] private string resourcesBasePath = "Prefabs/Bugs";

    /// <summary>
    /// Спавнит жука по "файловому" имени: ищет префаб в Resources/Prefabs/Bugs/<name>_Variant
    /// </summary>
    public GameObject SpawnBug(string bugFileName)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[BugSpawner] Нет точек спавна");
            return null;
        }

        string path = $"{resourcesBasePath}/{bugFileName}_Variant";
        var prefab = Resources.Load<GameObject>(path);
        if (!prefab)
        {
            Debug.LogError($"[BugSpawner] Не найден префаб: Resources/{path}");
            return null;
        }

        var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var go = Instantiate(prefab, p.position, p.rotation);

        go.name = bugFileName;
        var meta = go.GetComponent<BugMeta>();
        if (!meta) meta = go.AddComponent<BugMeta>();
        meta.FileName = bugFileName;

        return go;
    }
}