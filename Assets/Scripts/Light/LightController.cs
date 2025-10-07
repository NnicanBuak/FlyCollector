using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Контроллер света с анимациями и эффектами
/// </summary>
public class LightController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Light[] lights;
    [SerializeField] private Renderer[] emissiveRenderers; // Материалы с эмиссией
    
    [Header("Настройки включения/выключения")]
    [SerializeField] private bool canBeControlledBySwitch = true;
    [SerializeField] private bool ignoreSwitchWhenOn = true; // Игнорировать выключатель когда свет включен
    
    [Header("Начальное состояние")]
    [Tooltip("Включен ли свет при старте игры")]
    [SerializeField] private bool startLightOn = false; // Свет выключен по умолчанию
    
    [Header("Анимация затухания")]
    [SerializeField] private float fadeOutDuration = 1.5f;
    [SerializeField] private AnimationCurve fadeOutCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
    [SerializeField] private float bounceAmount = 0.3f; // Сила bounce эффекта
    [SerializeField] private int bounceCount = 2; // Количество bounce
    
    [Header("Анимация включения")]
    [SerializeField] private float fadeInDuration = 0.8f;
    [SerializeField] private AnimationCurve fadeInCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
    
    [Header("Мерцание (Half-Life стиль)")]
    [SerializeField] private bool enableFlickering = true;
    [SerializeField] private float flickerMinInterval = 3f;
    [SerializeField] private float flickerMaxInterval = 8f;
    [SerializeField] private float flickerChance = 0.3f; // Шанс мерцания
    
    // Паттерны мерцания как в Half-Life Source
    [SerializeField] private string[] flickerPatterns = {
        "abcdefghijklmnopqrstuvwxyza", // Плавное мерцание
        "mmamammmmammamamaaamammma", // Быстрое мерцание
        "jklaaabcdefgabcdefg", // Случайное мерцание
        "aaaaaaaazzzzzzzz", // Длинные вспышки
        "zzaazz" // Короткие вспышки
    };
    
    [Header("Случайное выключение")]
    [SerializeField] private float randomShutoffChance = 0.05f; // 5% шанс в секунду
    
    [Header("Звуки")]
    [SerializeField] private AudioClip lightOnSound;
    [SerializeField] private AudioClip lightOffSound;
    [SerializeField] private AudioClip flickerSound;
    [SerializeField] private float soundVolume = 0.7f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private bool isLightOn; // Инициализируется в Awake()
    private bool isAnimating = false;
    private bool isFlickering = false;
    private bool randomShutoffEnabled = false;
    
    private float[] originalIntensities;
    private Color[] originalEmissionColors;
    private AudioSource audioSource;
    
    private Coroutine currentAnimation;
    private Coroutine flickerCoroutine;
    private Coroutine randomShutoffCoroutine;

    // События
    public System.Action<bool> OnLightStateChanged;

    void Awake()
    {
        // Создаем AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        
        // Сохраняем оригинальные значения
        SaveOriginalValues();
        
        // Устанавливаем начальное состояние света
        isLightOn = startLightOn;
        SetLightState(isLightOn, isLightOn ? 1f : 0f);
        
        if (showDebugInfo)
        {
            Debug.Log($"[LightController] Начальное состояние света: {(isLightOn ? "включен" : "выключен")}");
        }
    }

    void Start()
    {
        // Ждем инициализации GameTimer
        StartCoroutine(WaitForGameTimer());
        
        // Запускаем мерцание если свет выключен
        if (!isLightOn && enableFlickering)
        {
            StartFlickering();
        }
    }
    
    System.Collections.IEnumerator WaitForGameTimer()
    {
        // Ждем пока GameTimer не инициализируется
        while (GameTimer.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Подписываемся на события таймера
        GameTimer.Instance.onTwoMinutesLeft.AddListener(OnTwoMinutesLeft);
        
        if (showDebugInfo)
        {
            Debug.Log("[LightController] Подключен к GameTimer");
        }
    }

    void Update()
    {
        // Случайное выключение когда активно
        if (randomShutoffEnabled && isLightOn && !isAnimating)
        {
            if (Random.value < randomShutoffChance * Time.deltaTime)
            {
                TurnOff(true);
            }
        }
    }

    void SaveOriginalValues()
    {
        // Сохраняем интенсивности света
        originalIntensities = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            originalIntensities[i] = lights[i].intensity;
        }
        
        // Сохраняем эмиссионные цвета
        if (emissiveRenderers != null)
        {
            originalEmissionColors = new Color[emissiveRenderers.Length];
            for (int i = 0; i < emissiveRenderers.Length; i++)
            {
                if (emissiveRenderers[i].material.HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[i] = emissiveRenderers[i].material.GetColor("_EmissionColor");
                }
            }
        }
    }

    /// <summary>
    /// Включить свет
    /// </summary>
    public void TurnOn(bool useAnimation = true)
    {
        if (isLightOn && ignoreSwitchWhenOn)
        {
            if (showDebugInfo)
            {
                Debug.Log("[LightController] Свет уже включен, игнорируем");
            }
            return;
        }
        
        if (isAnimating) return;
        
        StopFlickering();
        
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        if (useAnimation)
        {
            currentAnimation = StartCoroutine(FadeInAnimation());
        }
        else
        {
            SetLightState(true, 1f);
            isLightOn = true;
            OnLightStateChanged?.Invoke(true);
        }
        
        PlaySound(lightOnSound);
        
        if (showDebugInfo)
        {
            Debug.Log("[LightController] Свет включен");
        }
    }

    /// <summary>
    /// Выключить свет
    /// </summary>
    public void TurnOff(bool useAnimation = true)
    {
        if (!isLightOn) return;
        if (isAnimating) return;
        
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        if (useAnimation)
        {
            currentAnimation = StartCoroutine(FadeOutAnimation());
        }
        else
        {
            SetLightState(false, 0f);
            isLightOn = false;
            OnLightStateChanged?.Invoke(false);
            
            if (enableFlickering)
            {
                StartFlickering();
            }
        }
        
        PlaySound(lightOffSound);
        
        if (showDebugInfo)
        {
            Debug.Log("[LightController] Свет выключен");
        }
    }

    /// <summary>
    /// Переключить состояние света
    /// </summary>
    public void Toggle()
    {
        if (isLightOn)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }

    /// <summary>
    /// Проверить, можно ли управлять выключателем
    /// </summary>
    public bool CanBeSwitched()
    {
        if (!canBeControlledBySwitch) return false;
        if (isAnimating) return false;
        if (isLightOn && ignoreSwitchWhenOn) return false;
        
        return true;
    }

    IEnumerator FadeInAnimation()
    {
        isAnimating = true;
        float elapsed = 0f;
        
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeInDuration;
            float intensity = fadeInCurve.Evaluate(progress);
            
            SetLightState(true, intensity);
            
            yield return null;
        }
        
        SetLightState(true, 1f);
        isLightOn = true;
        isAnimating = false;
        OnLightStateChanged?.Invoke(true);
    }

    IEnumerator FadeOutAnimation()
    {
        isAnimating = true;
        float elapsed = 0f;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeOutDuration;
            
            // Основная кривая затухания
            float baseIntensity = fadeOutCurve.Evaluate(progress);
            
            // Добавляем bounce эффект
            float bounceIntensity = baseIntensity;
            if (progress > 0.7f) // Bounce в конце
            {
                float bounceProgress = (progress - 0.7f) / 0.3f;
                float bounce = Mathf.Sin(bounceProgress * Mathf.PI * bounceCount) * bounceAmount * (1f - bounceProgress);
                bounceIntensity = Mathf.Clamp01(baseIntensity + bounce);
            }
            
            SetLightState(true, bounceIntensity);
            
            yield return null;
        }
        
        SetLightState(false, 0f);
        isLightOn = false;
        isAnimating = false;
        OnLightStateChanged?.Invoke(false);
        
        if (enableFlickering)
        {
            StartFlickering();
        }
    }

    void SetLightState(bool enabled, float intensity)
    {
        // Устанавливаем состояние источников света
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].enabled = enabled;
            if (enabled)
            {
                lights[i].intensity = originalIntensities[i] * intensity;
            }
        }
        
        // Устанавливаем эмиссионные материалы
        if (emissiveRenderers != null)
        {
            for (int i = 0; i < emissiveRenderers.Length; i++)
            {
                if (emissiveRenderers[i].material.HasProperty("_EmissionColor"))
                {
                    Color emissionColor = enabled ? originalEmissionColors[i] * intensity : Color.black;
                    emissiveRenderers[i].material.SetColor("_EmissionColor", emissionColor);
                }
            }
        }
    }

    void StartFlickering()
    {
        if (!enableFlickering || isLightOn) return;
        
        StopFlickering();
        flickerCoroutine = StartCoroutine(FlickerRoutine());
    }

    void StopFlickering()
    {
        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
        }
        isFlickering = false;
    }

    IEnumerator FlickerRoutine()
    {
        while (!isLightOn)
        {
            // Ждем случайное время
            float waitTime = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(waitTime);
            
            // Проверяем шанс мерцания
            if (Random.value > flickerChance) continue;
            
            isFlickering = true;
            
            // Выбираем случайный паттерн мерцания
            string pattern = flickerPatterns[Random.Range(0, flickerPatterns.Length)];
            
            PlaySound(flickerSound);
            
            // Выполняем паттерн мерцания
            foreach (char c in pattern)
            {
                // Конвертируем символ в интенсивность (a=0, z=1)
                float intensity = (c - 'a') / 25f;
                SetLightState(intensity > 0.1f, intensity);
                
                yield return new WaitForSeconds(0.05f); // 50ms на символ
            }
            
            // Возвращаем в выключенное состояние
            SetLightState(false, 0f);
            isFlickering = false;
            
            yield return new WaitForSeconds(0.5f); // Пауза после мерцания
        }
    }

    void OnTwoMinutesLeft()
    {
        if (showDebugInfo)
        {
            Debug.Log("[LightController] Осталось 2 минуты - активируем случайное выключение");
        }
        
        // Выключаем свет
        TurnOff();
        
        // Активируем случайное выключение
        randomShutoffEnabled = true;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    // Публичные свойства
    public bool IsLightOn => isLightOn;
    public bool IsAnimating => isAnimating;
    public bool IsFlickering => isFlickering;

    void OnDestroy()
    {
        // Отписываемся от событий
        if (GameTimer.Instance != null)
        {
            GameTimer.Instance.onTwoMinutesLeft.RemoveListener(OnTwoMinutesLeft);
        }
    }
}