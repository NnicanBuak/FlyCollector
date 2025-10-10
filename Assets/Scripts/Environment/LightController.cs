using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(Light))]
public class LightController : MonoBehaviour
{
    private Light _light;

    [Header("Параметры света")]
    [SerializeField] private float maxIntensity = 2f;
    [SerializeField] private float minIntensity = 0.2f;
    [SerializeField] private bool enableLightOnTimerStart = true;
    [SerializeField] private bool disableLightOnTimerEnd = true;

    [Header("Доступность переключения")]
    [Tooltip("Можно ли внешне переключать свет (LightSwitch и т.п.)")]
    [SerializeField] private bool canBeSwitched = true;

    [Header("Пульсация в последние 2 минуты")]
    [SerializeField] private bool pulseOnLastTwoMinutes = true;
    [SerializeField, Min(0f)] private float pulseSpeed = 6f;


    [Tooltip("Вызывается при смене состояния света (true = включен)")]
    public UnityEvent<bool> OnLightStateChanged = new UnityEvent<bool>();


    public bool CanBeSwitched
    {
        get => canBeSwitched;
        set => canBeSwitched = value;
    }

    public bool IsLightOn => _light != null && _light.enabled;

    private bool _pulsing;

    void Awake()
    {
        _light = GetComponent<Light>();
    }

    void Update()
    {
        if (_pulsing && _light != null)
        {

            float s = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, s);
        }
    }




    public void TurnOn(bool invokeEvent = true)
    {
        if (!_light) return;
        if (!IsLightOn)
        {
            _light.enabled = true;
            if (!_pulsing) _light.intensity = maxIntensity;
            if (invokeEvent) OnLightStateChanged?.Invoke(true);
        }
    }


    public void TurnOff(bool invokeEvent = true)
    {
        if (!_light) return;
        if (IsLightOn)
        {
            _light.intensity = minIntensity;
            _light.enabled = false;
            if (invokeEvent) OnLightStateChanged?.Invoke(false);
        }
    }


    public void Toggle()
    {
        if (CanBeSwitched) { if (IsLightOn) TurnOff(); else TurnOn(); }
    }



    private IEnumerator SubscribeWhenTimerReady()
    {
        yield return new WaitUntil(() => GameTimer.Instance != null);
        TrySubscribeToTimer();


        HandleTimeUpdate(GameTimer.Instance.CurrentTime);
        if (GameTimer.Instance.IsRunning && enableLightOnTimerStart)
            TurnOn(false);
    }

    private void TrySubscribeToTimer()
    {
        if (GameTimer.Instance == null) return;

        GameTimer.Instance.OnMinutePassed.AddListener(HandleMinutePassed);
        GameTimer.Instance.OnTimeUpdate.AddListener(HandleTimeUpdate);
        GameTimer.Instance.OnTimerStart.AddListener(HandleTimerStart);
        GameTimer.Instance.OnTimerEnd.AddListener(HandleTimerEnd);

#if UNITY_EDITOR
        Debug.Log("[LightController] Подписался на события GameTimer.", this);
#endif
    }

    private void TryUnsubscribeFromTimer()
    {
        if (GameTimer.Instance == null) return;

        GameTimer.Instance.OnMinutePassed.RemoveListener(HandleMinutePassed);
        GameTimer.Instance.OnTimeUpdate.RemoveListener(HandleTimeUpdate);
        GameTimer.Instance.OnTimerStart.RemoveListener(HandleTimerStart);
        GameTimer.Instance.OnTimerEnd.RemoveListener(HandleTimerEnd);

#if UNITY_EDITOR
        Debug.Log("[LightController] Отписался от событий GameTimer.", this);
#endif
    }




    private void HandleMinutePassed()
    {
        if (pulseOnLastTwoMinutes && GameTimer.Instance != null)
        {

            _pulsing = (Mathf.CeilToInt(GameTimer.Instance.CurrentTime / 60f) <= 2);
        }
    }


    private void HandleTimeUpdate(float timeLeft)
    {
        if (_light == null) return;

        if (_pulsing) return;


        float t = 0f;
        if (GameTimer.Instance != null && GameTimer.Instance.TotalTime > 0f)
            t = Mathf.Clamp01(1f - (timeLeft / GameTimer.Instance.TotalTime));

        if (IsLightOn)
            _light.intensity = Mathf.Lerp(maxIntensity, minIntensity, t);
        else
            _light.intensity = minIntensity;
    }

    private void HandleTimerStart()
    {
        _pulsing = false;
        if (enableLightOnTimerStart) TurnOn();
    }

    private void HandleTimerEnd()
    {
        _pulsing = false;
        if (disableLightOnTimerEnd) TurnOff();
        else
        {

            if (_light) _light.intensity = minIntensity;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxIntensity = Mathf.Max(0f, maxIntensity);
        minIntensity = Mathf.Clamp(minIntensity, 0f, maxIntensity);
    }
#endif
}
