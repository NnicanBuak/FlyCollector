using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class BugManager : MonoBehaviour
{
    [Header("Настройки производительности")]
    [SerializeField] private int maxActiveBugs = 30; // Максимум активных жуков одновременно
    [SerializeField] private float distanceToActivate = 20f; // Расстояние активации
    [SerializeField] private float updateFrequency = 1f; // Как часто проверять (сек)
    
    private List<BugAI> allBugs = new List<BugAI>();
    private Transform cameraTransform;
    private float updateTimer;
    
    void Start()
    {
        // Находим камеру
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            cameraTransform = mainCam.transform;
        }
        
        // Собираем всех жуков в сцене
        BugAI[] bugs = Object.FindObjectsByType<BugAI>(FindObjectsSortMode.None);
        allBugs.AddRange(bugs);
        
        Debug.Log($"BugManager управляет {allBugs.Count} жуками");
    }
    
    void Update()
    {
        updateTimer += Time.deltaTime;
        
        if (updateTimer >= updateFrequency)
        {
            updateTimer = 0f;
            OptimizeBugs();
        }
    }
    
    void OptimizeBugs()
    {
        if (cameraTransform == null || allBugs.Count == 0) return;
        
        // Создаем список жуков с расстояниями
        List<BugDistanceData> bugDistances = new List<BugDistanceData>();
        
        foreach (var bug in allBugs)
        {
            if (bug == null) continue;
            
            // === ПРОПУСКАЕМ РУЧНО ОТКЛЮЧЕННЫХ ЖУКОВ ===
            if (bug.IsManuallyDisabled())
            {
                continue; // Не управляем ими
            }
            
            float distance = Vector3.Distance(cameraTransform.position, bug.transform.position);
            bugDistances.Add(new BugDistanceData { bug = bug, distance = distance });
        }
        
        // Сортируем по расстоянию
        bugDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        // Активируем ближайших жуков
        for (int i = 0; i < bugDistances.Count; i++)
        {
            NavMeshAgent agent = bugDistances[i].bug.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                // Активируем только ближайших
                bool shouldBeActive = (i < maxActiveBugs) && (bugDistances[i].distance < distanceToActivate);
                
                if (agent.enabled != shouldBeActive)
                {
                    agent.enabled = shouldBeActive;
                }
            }
        }
    }
    
    // Вспомогательная структура
    private struct BugDistanceData
    {
        public BugAI bug;
        public float distance;
    }
    
    // Показать статистику в редакторе
    void OnGUI()
    {
        if (Application.isEditor && allBugs.Count > 0)
        {
            int activeCount = 0;
            int manuallyDisabledCount = 0;
            
            foreach (var bug in allBugs)
            {
                if (bug != null)
                {
                    if (bug.IsManuallyDisabled())
                    {
                        manuallyDisabledCount++;
                    }
                    else
                    {
                        NavMeshAgent agent = bug.GetComponent<NavMeshAgent>();
                        if (agent != null && agent.enabled) activeCount++;
                    }
                }
            }
        }
    }
}