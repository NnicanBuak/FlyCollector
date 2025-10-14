using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class LightPulseController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Light mainLight;
    [SerializeField] private AudioSource glitchAudio;
    [SerializeField] private CooldownTimer cooldownTimer;

    [Header("Фоновый звук работающей лампы")]
    [SerializeField] private AudioSource humAudio;
    [SerializeField, Min(0f)] private float humFadeOutDuration = 0.25f;
    [SerializeField] private bool stopHumImmediately = false;

    [Header("Интеграция (опционально)")]
    [SerializeField] private LightController lightController;
    [SerializeField] private bool useFlickerOnExternalOff = true;

    [Header("Мигание перед отключением")]
    [Tooltip("Общая длительность хаотичного мигания перед «рывками»")]
    [SerializeField, Min(0f)] private float flickerChaosDuration = 0.8f;
    [Tooltip("Минимальная пауза между хаотичными вспышками")]
    [SerializeField, Min(0.01f)] private float chaosMinInterval = 0.03f;
    [Tooltip("Максимальная пауза между хаотичными вспышками")]
    [SerializeField, Min(0.02f)] private float chaosMaxInterval = 0.12f;
    [Tooltip("Диапазон интенсивности при хаотичном мигании (0..1 умножается на исходную интенсивность)")]
    [SerializeField, Range(0f, 1f)] private float chaosMinIntensity = 0.15f;
    [SerializeField, Range(0f, 1f)] private float chaosMaxIntensity = 1.0f;

    [Header("Финальные «рывки» (stutter)")]
    [Tooltip("Сколько резких рывков перед окончательным гашением")]
    [SerializeField, Min(1)] private int finalJerksCount = 3;
    [Tooltip("Диапазон длительности каждого рывка")]
    [SerializeField, Min(0.01f)] private float jerkMinDuration = 0.05f;
    [SerializeField, Min(0.01f)] private float jerkMaxDuration = 0.14f;
    [Tooltip("Диапазон пауз между рывками")]
    [SerializeField, Min(0.01f)] private float jerkMinGap = 0.04f;
    [SerializeField, Min(0.01f)] private float jerkMaxGap = 0.12f;
    [Tooltip("Интенсивность вспышки на рывке (0..1 умножается на исходную)")]
    [SerializeField, Range(0.1f, 2.0f)] private float jerkFlashIntensityMul = 1.2f;
    [Tooltip("Насколько падать ниже минимума между рывками (0..1 от исходной)")]
    [SerializeField, Range(0f, 1f)] private float jerkDipIntensity = 0.05f;

    [Header("Синхронизация со звуком во время мигания")]
    [Tooltip("Модулировать громкость гула в такт миганию")]
    [SerializeField] private bool stutterHumOnFlicker = true;
    [SerializeField, Range(0f, 1f)]
    private float humChaosDip = 0.35f;

    [Header("Extra (optional)")]
    public UnityEvent OnTick;

    private float startTime;
    private float nextTickTime;
    private Coroutine humFadeRoutine;
    private float humInitialVolume = 1f;

    private bool isShuttingDown = false;
    private Coroutine shutdownRoutine;
    private float lightInitialIntensity = 1f;

    private void OnEnable()
    {
        if (lightController != null)
            lightController.OnLightStateChanged.AddListener(HandleLightStateChanged);
    }

    private void OnDisable()
    {
        if (lightController != null)
            lightController.OnLightStateChanged.RemoveListener(HandleLightStateChanged);
    }

    private void Start()
    {
        if (mainLight != null) lightInitialIntensity = mainLight.intensity;

        startTime = Time.time;
        nextTickTime = startTime + 60f;

        if (humAudio != null) humInitialVolume = humAudio.volume;

        if (mainLight != null && mainLight.enabled)
            StartHum();
    }

    private void Update()
    {
        if (Time.time < nextTickTime) return;

        Tick();

        float elapsed = Time.time - startTime;
        float interval = (elapsed < 120f) ? 60f : 30f;
        nextTickTime += interval;
    }

    private void Tick()
    {
        if (mainLight == null)
        {
            OnTick?.Invoke();
            return;
        }


        if (mainLight.enabled && !isShuttingDown)
        {
            PowerDownWithFlicker();
        }

        OnTick?.Invoke();
    }

    public void PowerDownWithFlicker()
    {
        if (shutdownRoutine != null) StopCoroutine(shutdownRoutine);
        shutdownRoutine = StartCoroutine(FlickerAndPowerDown());
    }

    public void CancelShutdownAndTurnOn()
    {
        if (shutdownRoutine != null) StopCoroutine(shutdownRoutine);
        isShuttingDown = false;
        if (mainLight != null)
        {
            mainLight.enabled = true;
            mainLight.intensity = lightInitialIntensity;
        }

        StartHum();
    }

    private void HandleLightStateChanged(bool isOn)
    {
        if (isOn)
        {
            CancelShutdownAndTurnOn();
        }
        else
        {
            if (useFlickerOnExternalOff && mainLight != null && mainLight.enabled)
                PowerDownWithFlicker();
            else
                InstantOff();
        }
    }

    private void StartHum()
    {
        if (humAudio == null) return;
        if (humFadeRoutine != null) StopCoroutine(humFadeRoutine);
        humFadeRoutine = null;

        humAudio.volume = humInitialVolume;
        if (!humAudio.isPlaying) humAudio.Play();
    }

    private void StopHum()
    {
        if (humAudio == null) return;
        if (!humAudio.isPlaying) return;

        if (stopHumImmediately || humFadeOutDuration <= 0f)
        {
            humAudio.Stop();
            humAudio.volume = humInitialVolume;
            return;
        }

        if (humFadeRoutine != null) StopCoroutine(humFadeRoutine);
        humFadeRoutine = StartCoroutine(FadeOutAndStop(humAudio, humFadeOutDuration));
    }

    private IEnumerator FadeOutAndStop(AudioSource src, float duration)
    {
        float from = src.volume;
        float t = 0f;
        while (t < duration && src != null)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);
            src.volume = humInitialVolume * k;
            yield return null;
        }

        if (src != null)
        {
            src.Stop();
            src.volume = humInitialVolume;
        }

        humFadeRoutine = null;
    }

    private IEnumerator FlickerAndPowerDown()
    {
        if (mainLight == null) yield break;

        isShuttingDown = true;


        mainLight.enabled = true;
        if (lightInitialIntensity <= 0f) lightInitialIntensity = Mathf.Max(0.01f, mainLight.intensity);

        float chaosEndTime = Time.unscaledTime + Mathf.Max(0f, flickerChaosDuration);


        while (Time.unscaledTime < chaosEndTime)
        {
            float target = lightInitialIntensity * Random.Range(chaosMinIntensity, chaosMaxIntensity);
            mainLight.intensity = target;


            if (stutterHumOnFlicker && humAudio != null && humAudio.isPlaying)
            {
                float saved = humAudio.volume;
                humAudio.volume = Mathf.Lerp(0f, humInitialVolume, humChaosDip);
                yield return null;
                humAudio.volume = saved;
            }


            float wait = Random.Range(chaosMinInterval, Mathf.Max(chaosMinInterval, chaosMaxInterval));
            yield return new WaitForSecondsRealtime(wait);
        }


        for (int i = 0; i < finalJerksCount; i++)
        {
            mainLight.intensity = lightInitialIntensity * jerkDipIntensity;
            if (stutterHumOnFlicker && humAudio != null && humAudio.isPlaying)
                humAudio.volume = humInitialVolume * 0.2f;

            yield return new WaitForSecondsRealtime(Random.Range(jerkMinGap, jerkMaxGap));


            mainLight.intensity = lightInitialIntensity * jerkFlashIntensityMul;
            if (stutterHumOnFlicker && humAudio != null && humAudio.isPlaying)
                humAudio.volume = humInitialVolume;

            yield return new WaitForSecondsRealtime(Random.Range(jerkMinDuration, jerkMaxDuration));
        }


        mainLight.intensity = 0f;
        mainLight.enabled = false;


        StopHum();


        if (glitchAudio != null) glitchAudio.Play();
        LogGameTimerStamp();


        if (cooldownTimer != null)
            cooldownTimer.ResetTimer();
        cooldownTimer.StartTimer();

        isShuttingDown = false;
        shutdownRoutine = null;
    }

    private void InstantOff()
    {
        if (mainLight != null)
        {
            mainLight.intensity = 0f;
            mainLight.enabled = false;
        }

        StopHum();
        isShuttingDown = false;
        if (shutdownRoutine != null)
        {
            StopCoroutine(shutdownRoutine);
            shutdownRoutine = null;
        }
    }

    private void LogGameTimerStamp(string context = "Light shutdown glitch")
    {
        try
        {
            var gt = GameTimer.Instance;
            if (gt == null)
            {
                Debug.LogWarning($"[LightPulseController] {context}: GameTimer.Instance == null");
                return;
            }


            float left = 0f, elapsed = 0f, total = 0f;


            try
            {
                left = gt.TimeLeft;
            }
            catch
            {
                try
                {
                    left = gt.GetRemainingTime();
                }
                catch
                {
                }
            }


            try
            {
                elapsed = gt.TimeElapsed;
            }
            catch
            {
                try
                {
                    elapsed = gt.GetElapsedTime();
                }
                catch
                {
                }
            }


            try
            {
                total = gt.TotalTime;
            }
            catch
            {
            }


            if (total <= 0f && left > 0f && elapsed > 0f) total = left + elapsed;
            if (elapsed <= 0f && total > 0f && left >= 0f) elapsed = Mathf.Max(0f, total - left);


            int m = Mathf.FloorToInt(left / 60f);
            int s = Mathf.FloorToInt(left % 60f);
            string prettyLeft = $"{m:00}:{s:00}";

            Debug.Log(
                $"[LightPulseController] {context} → GameTimer: left {prettyLeft} ({left:F2}s), " +
                $"elapsed {elapsed:F2}s, total {total:F2}s");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LightPulseController] {context}: лог не удался: {e.Message}");
        }
    }
}