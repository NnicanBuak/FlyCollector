using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;

namespace Game.Scripts.Localization
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class LanguageSelector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Dropdown dropdown;

        [Header("Options")]
        [Tooltip("Использовать нативные имена CultureInfo (например, \"Русский (Россия)\"). " +
                 "Если выключить — будет использоваться LocaleName или код.")]
        [SerializeField] private bool useNativeNames = true;

        [Tooltip("Ключ для сохранения выбранного языка в PlayerPrefs.")]
        [SerializeField] private string saveKey = "Localization.Language";

        private List<Locale> _locales;

        private void Awake()
        {
            if (dropdown == null)
                dropdown = GetComponent<TMP_Dropdown>();

            if (dropdown == null)
            {
                Debug.LogError("LanguageSelector: TMP_Dropdown не найден!");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            StartCoroutine(InitializeLocalizationRoutine());
        }

        private IEnumerator InitializeLocalizationRoutine()
        {
            // Ждём инициализации локализации
            var initOp = LocalizationSettings.InitializationOperation;
            if (!initOp.IsDone)
            {
                yield return initOp;
            }

            // Получаем доступ к локалям после инициализации
            if (LocalizationSettings.AvailableLocales == null)
            {
                Debug.LogError("LanguageSelector: AvailableLocales is null. Проверьте настройки локализации.");
                yield break;
            }

            _locales = LocalizationSettings.AvailableLocales.Locales;
            
            if (_locales == null || _locales.Count == 0)
            {
                Debug.LogWarning("LanguageSelector: Нет доступных локалей. Проверьте Localization Settings > Available Locales.");
                dropdown.ClearOptions();
                yield break;
            }

            BuildOptions();
            dropdown.RefreshShownValue();

            // Восстановить сохранённый выбор (если есть)
            if (PlayerPrefs.HasKey(saveKey))
            {
                var savedCode = PlayerPrefs.GetString(saveKey);
                var saved = FindByCode(savedCode);
                if (saved != null)
                    LocalizationSettings.SelectedLocale = saved;
            }

            // Проставить текущий индекс без вызова событий
            var currentLocale = LocalizationSettings.SelectedLocale;
            var currentIndex = GetIndexByCode(currentLocale?.Identifier.Code);
            dropdown.SetValueWithoutNotify(currentIndex);

            // Подписки
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private void OnDisable()
        {
            if (dropdown != null)
                dropdown.onValueChanged.RemoveListener(OnDropdownChanged);

            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void BuildOptions()
        {
            dropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>(_locales.Count);

            foreach (var loc in _locales)
            {
                string label;
                if (useNativeNames)
                {
                    // Используем нативное имя языка из CultureInfo
                    var culture = loc.Identifier.CultureInfo;
                    label = culture != null ? culture.NativeName : loc.LocaleName;
                }
                else
                {
                    // Используем короткий код языка в верхнем регистре (EN, RU)
                    label = loc.Identifier.Code.Split('-')[0].ToUpperInvariant();
                }
                
                options.Add(new TMP_Dropdown.OptionData(label));
            }

            dropdown.AddOptions(options);
        }

        private Locale FindByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            return _locales.FirstOrDefault(l => l.Identifier.Code == code);
        }

        private int GetIndexByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0;

            for (int i = 0; i < _locales.Count; i++)
            {
                if (_locales[i].Identifier.Code == code)
                    return i;
            }
            return 0;
        }

        private void OnDropdownChanged(int index)
        {
            if (index < 0 || index >= _locales.Count) return;

            var chosen = _locales[index];

            if (LocalizationSettings.SelectedLocale == null ||
                LocalizationSettings.SelectedLocale.Identifier.Code != chosen.Identifier.Code)
            {
                LocalizationSettings.SelectedLocale = chosen;
                PlayerPrefs.SetString(saveKey, chosen.Identifier.Code);
                PlayerPrefs.Save();
            }
        }

        private void OnSelectedLocaleChanged(Locale newLocale)
        {
            var ix = GetIndexByCode(newLocale?.Identifier.Code);
            dropdown.SetValueWithoutNotify(ix);
            dropdown.RefreshShownValue();
        }
    }
}