using UnityEngine;

/// <summary>
/// Выключатель света для работы с LightController
/// </summary>
public class LightSwitch : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private LightController[] lightControllers;
    [SerializeField] private Renderer switchRenderer;
    
    [Header("Материалы выключателя")]
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField] private Material disabledMaterial;
    
    [Header("Звуки")]
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip blockedSound; // Звук когда нельзя переключить
    [SerializeField] private float soundVolume = 1f;
    
    [Header("Анимация выключателя")]
    [SerializeField] private Transform switchHandle; // Рычаг выключателя для анимации
    [SerializeField] private Vector3 onRotation = new Vector3(-15, 0, 0);
    [SerializeField] private Vector3 offRotation = new Vector3(15, 0, 0);
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Состояние")]
    [SerializeField] private bool switchPosition = false; // false = выключен, true = включен
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private InteractableObject interactable;
    private AudioSource audioSource;
    private bool isAnimating = false;
    private Coroutine handleAnimationCoroutine;

    void Start()
    {
        // Получаем компонент InteractableObject
        interactable = GetComponent<InteractableObject>();
        
        if (interactable == null)
        {
            Debug.LogError("[LightSwitch] Добавьте InteractableObject на объект!");
            return;
        }
        
        // Создаем AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        
        // Подписываемся на события контроллеров света
        foreach (var controller in lightControllers)
        {
            if (controller != null)
            {
                controller.OnLightStateChanged += OnLightStateChanged;
            }
        }
        
        // Применяем начальное состояние
        UpdateSwitchVisuals(false);
    }

    /// <summary>
    /// Этот метод вызывается через UnityEvent в InteractableObject
    /// </summary>
    public void OnSwitchActivated()
    {
        if (isAnimating)
        {
            if (showDebugInfo)
            {
                Debug.Log("[LightSwitch] Выключатель анимируется, игнорируем");
            }
            return;
        }
        
        // Проверяем, можем ли мы управлять хотя бы одним светом
        bool canControl = false;
        foreach (var controller in lightControllers)
        {
            if (controller != null && controller.CanBeSwitched())
            {
                canControl = true;
                break;
            }
        }
        
        if (!canControl)
        {
            // Не можем управлять ни одним светом
            PlaySound(blockedSound);
            
            if (showDebugInfo)
            {
                Debug.Log("[LightSwitch] Не могу управлять светом - заблокировано");
            }
            return;
        }
        
        // Включаем только выключенные источники света
        bool anyLightTurnedOn = false;
        foreach (var controller in lightControllers)
        {
            if (controller != null && !controller.IsLightOn && controller.CanBeSwitched())
            {
                controller.TurnOn();
                anyLightTurnedOn = true;
            }
        }
        
        if (anyLightTurnedOn)
        {
            switchPosition = true;
            PlaySound(clickSound);
            AnimateHandle(true);
            
            if (showDebugInfo)
            {
                Debug.Log("[LightSwitch] Свет включен через выключатель");
            }
        }
        else
        {
            // Все источники света уже включены
            PlaySound(blockedSound);
            
            if (showDebugInfo)
            {
                Debug.Log("[LightSwitch] Все источники света уже включены");
            }
        }
    }

    /// <summary>
    /// Обработчик изменения состояния света
    /// </summary>
    void OnLightStateChanged(bool isOn)
    {
        // Проверяем состояние всех источников света
        bool anyLightOn = false;
        foreach (var controller in lightControllers)
        {
            if (controller != null && controller.IsLightOn)
            {
                anyLightOn = true;
                break;
            }
        }
        
        // Обновляем позицию выключателя в зависимости от состояния света
        bool newSwitchPosition = anyLightOn;
        
        if (switchPosition != newSwitchPosition)
        {
            switchPosition = newSwitchPosition;
            AnimateHandle(switchPosition);
            UpdateSwitchVisuals(switchPosition);
            
            if (showDebugInfo)
            {
                Debug.Log($"[LightSwitch] Позиция выключателя изменена: {(switchPosition ? "включен" : "выключен")}");
            }
        }
    }

    /// <summary>
    /// Анимация рычага выключателя
    /// </summary>
    void AnimateHandle(bool toOnPosition)
    {
        if (switchHandle == null) return;
        
        if (handleAnimationCoroutine != null)
        {
            StopCoroutine(handleAnimationCoroutine);
        }
        
        handleAnimationCoroutine = StartCoroutine(AnimateHandleCoroutine(toOnPosition));
    }

    System.Collections.IEnumerator AnimateHandleCoroutine(bool toOnPosition)
    {
        isAnimating = true;
        
        Vector3 startRotation = switchHandle.localEulerAngles;
        Vector3 targetRotation = toOnPosition ? onRotation : offRotation;
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float curveValue = animationCurve.Evaluate(progress);
            
            Vector3 currentRotation = Vector3.Lerp(startRotation, targetRotation, curveValue);
            switchHandle.localEulerAngles = currentRotation;
            
            yield return null;
        }
        
        switchHandle.localEulerAngles = targetRotation;
        isAnimating = false;
    }

    /// <summary>
    /// Обновить визуалы выключателя
    /// </summary>
    void UpdateSwitchVisuals(bool isOn)
    {
        if (switchRenderer == null) return;
        
        // Проверяем, можем ли мы управлять хотя бы одним светом
        bool canControl = false;
        foreach (var controller in lightControllers)
        {
            if (controller != null && controller.CanBeSwitched())
            {
                canControl = true;
                break;
            }
        }
        
        Material materialToUse;
        if (!canControl)
        {
            materialToUse = disabledMaterial;
        }
        else
        {
            materialToUse = isOn ? onMaterial : offMaterial;
        }
        
        if (materialToUse != null)
        {
            switchRenderer.material = materialToUse;
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    /// <summary>
    /// Принудительно установить позицию выключателя
    /// </summary>
    public void SetSwitchPosition(bool position)
    {
        switchPosition = position;
        AnimateHandle(position);
        UpdateSwitchVisuals(position);
    }

    /// <summary>
    /// Получить текущую позицию выключателя
    /// </summary>
    public bool GetSwitchPosition()
    {
        return switchPosition;
    }

    /// <summary>
    /// Проверить, активен ли выключатель
    /// </summary>
    public bool IsSwitchActive()
    {
        foreach (var controller in lightControllers)
        {
            if (controller != null && controller.CanBeSwitched())
            {
                return true;
            }
        }
        return false;
    }

    void Update()
    {
        // Обновляем визуалы каждый кадр для отображения актуального состояния
        UpdateSwitchVisuals(switchPosition);
    }

    void OnDestroy()
    {
        // Отписываемся от событий
        foreach (var controller in lightControllers)
        {
            if (controller != null)
            {
                controller.OnLightStateChanged -= OnLightStateChanged;
            }
        }
    }
}