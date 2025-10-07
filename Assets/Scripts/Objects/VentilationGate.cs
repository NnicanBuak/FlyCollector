using UnityEngine;
using UnityEngine.Events;

public class VentilationTagGate : MonoBehaviour
{
    [Header("Что проверяем (ровно 4 объекта)")]
    [SerializeField] private GameObject[] targets = new GameObject[4];

    [Header("Условие")]
    [SerializeField] private string requiredTag = "Open";
    [Tooltip("Проверять каждый кадр. Если выключено — проверяем раз в checkInterval секунд.")]
    [SerializeField] private bool checkEveryFrame = true;
    [SerializeField, Min(0.02f)] private float checkInterval = 0.2f;

    [Header("Действия при открытии")]
    [SerializeField] private Animator ventAnimator;
    [SerializeField] private string openTrigger = "Open";
    [SerializeField] private GameObject ventToEnable;

    [Header("Опции")]
    [Tooltip("Открыть один раз и больше не закрывать")]
    [SerializeField] private bool openOnce = true;

    [Header("События")]
    public UnityEvent OnAllOpen;
    public UnityEvent OnNotAllOpen;

    private bool isOpen;       // текущее состояние (открыто)
    private float timer;       // для таймерной проверки

    private void Update()
    {
        if (checkEveryFrame)
        {
            TickCheck();
        }
        else
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = checkInterval;
                TickCheck();
            }
        }
    }

    private void TickCheck()
    {
        bool allOk = AllTargetsHaveRequiredTag();

        if (allOk)
        {
            if (!isOpen)
            {
                OpenVent();
                OnAllOpen?.Invoke();
                if (openOnce) isOpen = true;
                else isOpen = true; // открылось в любом случае
            }
        }
        else
        {
            // если хотим «закрывать» при потере условия и openOnce выключен — делаем это тут
            if (!openOnce && isOpen)
            {
                CloseVentIfConfigured();
                isOpen = false;
            }
            OnNotAllOpen?.Invoke();
        }
    }

    private bool AllTargetsHaveRequiredTag()
    {
        if (targets == null || targets.Length != 4) return false;

        for (int i = 0; i < targets.Length; i++)
        {
            var go = targets[i];
            if (go == null) return false;
            if (!go.CompareTag(requiredTag)) return false;
        }
        return true;
    }

    private void OpenVent()
    {
        if (ventAnimator != null && !string.IsNullOrEmpty(openTrigger))
            ventAnimator.SetTrigger(openTrigger);

        if (ventToEnable != null)
            ventToEnable.SetActive(true);
    }

    private void CloseVentIfConfigured()
    {
        // Если нужно — добавь сюда закрывающую логику (сброс триггера, другой триггер, выключение объекта и т.п.)
        // Пример:
        // if (ventAnimator != null) ventAnimator.ResetTrigger(openTrigger);
        // if (ventToEnable != null) ventToEnable.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Убедимся, что всегда 4 слота
        if (targets == null || targets.Length != 4)
        {
            var old = targets;
            targets = new GameObject[4];
            if (old != null)
            {
                for (int i = 0; i < Mathf.Min(4, old.Length); i++)
                    targets[i] = old[i];
            }
        }

        if (checkInterval < 0.02f) checkInterval = 0.02f;
    }
#endif
}
