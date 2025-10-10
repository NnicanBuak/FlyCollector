using UnityEngine;
using UnityEngine.UI;

namespace Audio
{
    /// <summary>
    /// Controls master volume via UI Slider and persists settings
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class VolumeSliderUI : MonoBehaviour
    {
        [Header("Sprites by % ranges")]
        [Tooltip("Sprite (0-10%)")]   [SerializeField] private Sprite sprite1;
        [Tooltip("Sprite (10-20%)")] [SerializeField] private Sprite sprite2;
        [Tooltip("Sprite (20-30%)")] [SerializeField] private Sprite sprite3;
        [Tooltip("Sprite (30-40%)")] [SerializeField] private Sprite sprite4;
        [Tooltip("Sprite (40-50%)")] [SerializeField] private Sprite sprite5;
        [Tooltip("Sprite (50-60%)")] [SerializeField] private Sprite sprite6;
        [Tooltip("Sprite (60-70%)")] [SerializeField] private Sprite sprite7;
        [Tooltip("Sprite (70-80%)")] [SerializeField] private Sprite sprite8;
        [Tooltip("Sprite (80-90%)")] [SerializeField] private Sprite sprite9;
        [Tooltip("Sprite (90-95%)")] [SerializeField] private Sprite sprite10;
        [Tooltip("Sprite (95-97%)")] [SerializeField] private Sprite sprite11;
        [Tooltip("Sprite (97-100%)")][SerializeField] private Sprite sprite12;

        [Header("Thresholds (fractions of 1.0)")]
        [SerializeField] private float[] thresholds = new float[]
        {
            0.10f, 0.20f, 0.30f, 0.40f, 0.50f, 0.60f, 0.70f, 0.80f, 0.90f, 0.95f, 0.97f
        };

        [Header("Settings")]
        [Tooltip("Default volume (0..1) if there is no saved value yet")]
        [Range(0f, 1f)] [SerializeField] private float defaultVolume = 0.5f;

        [Tooltip("PlayerPrefs key used to store the volume")]
        [SerializeField] private string saveKey = "Audio.MasterVolume";

        [Tooltip("Show debug logs in Console")]
        [SerializeField] private bool showDebug = false;

        [Header("UI")]
        [Tooltip("Image to show volume icon sprite")]
        [SerializeField] private Image volumeIcon;

        #region Properties
        public float CurrentVolume => _slider != null ? _slider.value : defaultVolume;
        #endregion

        #region Events
        public event System.Action<float> OnVolumeChanged;
        #endregion

        #region Unity Lifecycle
        private Slider _slider;

        private void Awake()
        {
            _slider = GetComponent<Slider>();

            if (_slider == null)
            {
                Debug.LogError("[VolumeSlider] Slider component not found!");
                return;
            }

            // Configure slider range
            _slider.minValue = 0f;
            _slider.maxValue = 1f;

            // Load saved volume or use default
            float savedVolume = PlayerPrefs.GetFloat(saveKey, defaultVolume);
            _slider.value = savedVolume;
            ApplyVolume(savedVolume);

            // Subscribe to value changes
            _slider.onValueChanged.AddListener(OnSliderChanged);

            if (showDebug)
            {
                Debug.Log($"[VolumeSlider] Initialized with volume: {savedVolume}");
            }
        }

        private void OnDestroy()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
        }

        private void Start()
        {
            // Apply saved volume to ensure it affects all scenes
            ApplyVolume(_slider.value);
        }
        #endregion

        #region Public Methods
        /// <summary>Set volume programmatically</summary>
        public void SetVolume(float volume)
        {
            if (_slider != null)
            {
                _slider.value = Mathf.Clamp01(volume);
            }
        }

        /// <summary>Get current volume value</summary>
        public float GetVolume()
        {
            return _slider != null ? _slider.value : defaultVolume;
        }

        /// <summary>Reset to default volume</summary>
        public void ResetToDefault()
        {
            SetVolume(defaultVolume);
        }
        #endregion

        #region Private Methods
        private void OnSliderChanged(float value)
        {
            ApplyVolume(value);
            SaveVolume(value);
            OnVolumeChanged?.Invoke(value);
        }

        private void ApplyVolume(float volume)
        {
            // AudioListener.volume affects ALL sounds globally across all scenes
            AudioListener.volume = Mathf.Clamp01(volume);
            UpdateVolumeIcon(volume);

            if (showDebug)
            {
                Debug.Log($"[VolumeSlider] Global volume set to: {volume} (affects all AudioSources)");
            }
        }


        private void UpdateVolumeIcon(float volume)
        {
            if (volumeIcon == null) return;

            // Локальный массив спрайтов по порядку интервалов
            Sprite[] sprites =
            {
                sprite1,  // 0–10%
                sprite2,  // 10–20%
                sprite3,  // 20–30%
                sprite4,  // 30–40%
                sprite5,  // 40–50%
                sprite6,  // 50–60%
                sprite7,  // 60–70%
                sprite8,  // 70–80%
                sprite9,  // 80–90%
                sprite10, // 90–95%
                sprite11, // 95–97%
                sprite12  // 97–100%
            };

            // Безопасная проверка: thresholds должно быть на 1 меньше, чем спрайтов
            if (thresholds == null || thresholds.Length != sprites.Length - 1)
            {
                Debug.LogWarning("[VolumeSlider] 'thresholds' length must be sprites.Length - 1 (ожидается 11 порогов).");
                int fallbackIndex = Mathf.Clamp(Mathf.FloorToInt(volume * sprites.Length), 0, sprites.Length - 1);
                Sprite fb = sprites[fallbackIndex];
                if (fb != null && volumeIcon.sprite != fb) volumeIcon.sprite = fb;
                return;
            }

            // Находим индекс по порогам: каждый порог — нижняя граница следующего спрайта
            int index = 0;
            while (index < thresholds.Length && volume >= thresholds[index])
                index++;

            // Клэмп и установка
            index = Mathf.Clamp(index, 0, sprites.Length - 1);
            Sprite targetSprite = sprites[index];

            if (targetSprite != null && volumeIcon.sprite != targetSprite)
            {
                volumeIcon.sprite = targetSprite;

                if (showDebug)
                    Debug.Log($"[VolumeSlider] Icon -> {targetSprite.name} (volume: {volume:F2}, index: {index})");
            }
        }

        private void SaveVolume(float volume)
        {
            PlayerPrefs.SetFloat(saveKey, volume);
            PlayerPrefs.Save();

            if (showDebug)
            {
                Debug.Log($"[VolumeSlider] Volume saved: {volume}");
            }
        }
        #endregion

        #region Gizmos
        #endregion
    }
}
