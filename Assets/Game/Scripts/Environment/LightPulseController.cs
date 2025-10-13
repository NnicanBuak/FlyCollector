using UnityEngine;
using UnityEngine.Events;

public class LightPulseController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Light mainLight;
    [SerializeField] private AudioSource glitchAudio;      // звук «выкл»
    [SerializeField] private MonoBehaviour cooldownTimer;  // компонент с методом ResetTimer()

    [Header("Extra (optional)")]
    public UnityEvent OnTick;  // если нужно повесить ещё что-то из инспектора

    private float startTime;
    private float nextTickTime;

    private void Start()
    {
        startTime = Time.time;
        nextTickTime = startTime + 60f; // первая минута
    }

    private void Update()
    {
        if (Time.time < nextTickTime) return;

        Tick(); // сработало

        // планируем следующий интервал:
        float elapsed = Time.time - startTime;
        float interval = (elapsed < 120f) ? 60f : 30f; // до 2-й минуты — раз в минуту, дальше — каждые 30с
        nextTickTime += interval;
    }

    private void Tick()
    {
        // 1) выключаем свет, только если он включён
        if (mainLight != null && mainLight.enabled)
        {
            mainLight.enabled = false;

            // 2) звук — только если реально выключили
            if (glitchAudio != null)
                glitchAudio.Play();
        }

        // 3) сброс таймера (если передан и имеет метод ResetTimer)
        if (cooldownTimer != null)
        {
            var m = cooldownTimer.GetType().GetMethod("ResetTimer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null) m.Invoke(cooldownTimer, null);
        }

        // 4) доп. события
        OnTick?.Invoke();
    }

    // Вызвать из кода, если нужно перезапустить расписание
    public void RestartSchedule()
    {
        startTime = Time.time;
        nextTickTime = startTime + 60f;
    }
}   