using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Game.Scripts.Localization;

namespace Game.Scripts.Localization
{
    public class LocalizationSelector : MonoBehaviour
    {
        [SerializeField] private Dropdown dropdown;
        [SerializeField] private string saveKey = "Localization.Language";

        private TextAsset[] _localeFiles;

        private void Awake()
        {
            if (dropdown == null) dropdown = GetComponent<Dropdown>();

            // Загружаем все таблицы локалей из Resources/Localization/strings и сортируем по имени
            _localeFiles = Resources.LoadAll<TextAsset>("Localization/strings").OrderBy(t => t.name).ToArray();

            if (_localeFiles == null || _localeFiles.Length == 0)
            {
                Debug.LogWarning("LocalizationSelector: no locale files found in Resources/Localization/strings");
                // fallback: оставляем существующий dropdown (если есть) и пытаемся восстановить по строке
                var savedNameFallback = PlayerPrefs.GetString(saveKey, "");
                if (!string.IsNullOrEmpty(savedNameFallback))
                {
                    // если сервис уже доступен — установим
                    var svc = LocalizationService.Instance;
                    if (svc != null) svc.SetLocale(savedNameFallback);
                }
                return;
            }

            // Заполним опции Dropdown отображаемыми именами (используем имя файла как код локали)
            dropdown.options = _localeFiles.Select(t => new Dropdown.OptionData(t.name)).ToList();

            // Выберем значение по сохранённой строке
            var savedLocale = PlayerPrefs.GetString(saveKey, "");
            int index = 0;
            if (!string.IsNullOrEmpty(savedLocale))
            {
                for (int i = 0; i < _localeFiles.Length; i++)
                {
                    if (string.Equals(_localeFiles[i].name, savedLocale, System.StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }

            dropdown.value = index;
            dropdown.onValueChanged.AddListener(OnLanguageChanged);

            // Инициализируем сервис текущим значением (если доступен)
            var initial = _localeFiles[index].name;
            var svcInit = LocalizationService.Instance;
            if (svcInit != null)
                svcInit.SetLocale(initial);
        }

        private void OnLanguageChanged(int index)
        {
            if (_localeFiles == null || index < 0 || index >= _localeFiles.Length) return;

            var locale = _localeFiles[index].name;
            PlayerPrefs.SetString(saveKey, locale);
            PlayerPrefs.Save();

            // Уведомляем сервис
            try
            {
                var svc = LocalizationService.Instance;
                if (svc != null) svc.SetLocale(locale);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"LocalizationSelector: failed to notify LocalizationService: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            dropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        }
    }
}