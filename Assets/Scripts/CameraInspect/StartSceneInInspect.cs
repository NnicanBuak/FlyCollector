using UnityEngine;
using UnityEngine.Events;

public class StartSceneInInspect : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private GameObject targetToInspect;

    [Header("Behaviour")]
    [SerializeField] private bool startOnAwake = true;
    [Tooltip("Подписаться на конец инспекции и запустить таймер/ивент")]
    [SerializeField] private bool startTimerOnInspectEnd = true;

    [Header("Timer hookup (любой компонент/метод)")]
    [Tooltip("Компонент, у которого надо вызвать метод запуска таймера (например, GameTimer, LevelTimer и т.п.). Можно оставить пустым и использовать UnityEvent ниже.")]
    [SerializeField] private MonoBehaviour timerTarget;
    [Tooltip("Имя метода у timerTarget, который нужно вызвать. По умолчанию 'StartTimer'.")]
    [SerializeField] private string timerMethodName = "StartTimer";

    [Header("Alternatively: UnityEvent")]
    [Tooltip("Если не хотите указывать компонент/метод, просто повесьте сюда действия, которые должны выполниться на конец инспекта.")]
    public UnityEvent onInspectEnd;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private bool hasStartedTimer = false;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();

        if (cameraController == null)
        {
            Debug.LogError("[StartSceneInInspect] CameraController не найден");
            enabled = false;
            return;
        }


        cameraController.InspectEnded += HandleInspectEnded;
    }

    private void OnDestroy()
    {
        if (cameraController != null)
            cameraController.InspectEnded -= HandleInspectEnded;
    }

    private void Start()
    {
        if (!startOnAwake) return;

        if (targetToInspect == null)
        {
            Debug.LogError("[StartSceneInInspect] targetToInspect не задан");
            return;
        }


        if (showDebugInfo) Debug.Log("[StartSceneInInspect] Стартуем инспекцию на старте сцены");
        cameraController.StartInspect(targetToInspect);
    }

    private void HandleInspectEnded()
    {
        // Start timer only once after initial inspect
        if (!startTimerOnInspectEnd || hasStartedTimer) return;

        hasStartedTimer = true;

        if (showDebugInfo) Debug.Log("[StartSceneInInspect] Получен конец ПЕРВОЙ инспекции → запуск таймера/ивента");

        bool invoked = false;


        if (timerTarget != null && !string.IsNullOrEmpty(timerMethodName))
        {
            var method = timerTarget.GetType().GetMethod(timerMethodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null && method.GetParameters().Length == 0)
            {
                method.Invoke(timerTarget, null);
                invoked = true;
            }
            else
            {
                Debug.LogWarning($"[StartSceneInInspect] Метод '{timerMethodName}' не найден у {timerTarget.GetType().Name} или имеет параметры.");
            }
        }


        if (!invoked)
        {
            onInspectEnd?.Invoke();
        }
    }
}
