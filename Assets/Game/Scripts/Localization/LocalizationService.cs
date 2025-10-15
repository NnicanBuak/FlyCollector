using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Game.Scripts.Localization
{
    public class LocalizationService : MonoBehaviour, ILocalizationService
    {
        public static LocalizationService Instance { get; private set; }

        [SerializeField] string defaultLocale = "en";
        const string PREF_KEY = "game.locale";

        Dictionary<string, string> _strings = new();
        string _current;

        public string CurrentLocale => _current;
        public event Action<string> LanguageChanged;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            string initial = null;

            // 1) строковый ключ от селектора
            var selectorKey = "Localization.Language";
            if (PlayerPrefs.HasKey(selectorKey)) {
                // Selector may save either a string (locale name) or an int (index). Try string first.
                var selectorLocale = PlayerPrefs.GetString(selectorKey, "");
                if (!string.IsNullOrEmpty(selectorLocale)) {
                    initial = selectorLocale;
                } else {
                    // treat value as index
                    int idx = PlayerPrefs.GetInt(selectorKey, -1);
                    if (idx >= 0) {
                        var tas = Resources.LoadAll<TextAsset>("Localization/strings").OrderBy(t => t.name).ToArray();
                        if (tas.Length > 0) {
                            if (idx < tas.Length) initial = tas[idx].name;
                            else {
                                initial = tas[0].name;
                                Debug.LogWarning($"LocalizationService: saved index {idx} (key={selectorKey}) is out of range, falling back to {initial}");
                            }

                            // Миграция: сохраняем строковое имя обратно в ключ селектора, чтобы в будущем использовать строковый формат
                            try {
                                PlayerPrefs.SetString(selectorKey, initial);
                                PlayerPrefs.Save();
                                Debug.Log($"LocalizationService: migrated selector index {idx} -> name '{initial}' (key={selectorKey})");
                            } catch (Exception ex) {
                                Debug.LogWarning($"LocalizationService: failed to migrate selector index to string: {ex.Message}");
                            }
                        }
                    }
                }
            }
            else
            {
                // 2) возможные ключи-индексы (другие реализации селектора могли использовать другие ключи)
                string[] idxKeys = new[] { "Localization.LanguageIndex", "Localization.SelectedIndex", "Localization.SelectedLocaleIndex", "localization.index" };
                int foundIndex = -1;
                string foundIdxKey = null;
                foreach (var k in idxKeys) {
                    if (PlayerPrefs.HasKey(k)) { foundIndex = PlayerPrefs.GetInt(k, -1); foundIdxKey = k; break; }
                }

                if (foundIndex >= 0) {
                    var tas = Resources.LoadAll<TextAsset>("Localization/strings").OrderBy(t => t.name).ToArray();
                    if (tas.Length > 0 && foundIndex < tas.Length) {
                        initial = tas[foundIndex].name;
                    }
                    else if (tas.Length > 0) {
                        initial = tas[0].name;
                        Debug.LogWarning($"LocalizationService: saved index {foundIndex} (key={foundIdxKey}) is out of range, falling back to {initial}");
                    }

                    // Если индекс пришёл из альтернативного ключа — мигрируем значение в основной ключ selectorKey
                    try {
                        PlayerPrefs.SetString(selectorKey, initial);
                        PlayerPrefs.Save();
                        Debug.Log($"LocalizationService: migrated legacy index key '{foundIdxKey}' -> selector '{selectorKey}' with value '{initial}'");
                    } catch (Exception ex) {
                        Debug.LogWarning($"LocalizationService: failed to migrate legacy index key {foundIdxKey}: {ex.Message}");
                    }

                }
            }

            if (string.IsNullOrEmpty(initial))
            {
                var saved = PlayerPrefs.GetString(PREF_KEY, "");
                initial = string.IsNullOrEmpty(saved) ? DetectSystemLocaleOrDefault() : saved;
            }

            SetLocale(initial);
        }

        string DetectSystemLocaleOrDefault()
        {
            var sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var ta = Resources.Load<TextAsset>($"Localization/strings/{sys}");
            return ta ? sys : defaultLocale;
        }

        public void SetLocale(string locale)
        {
            if (string.Equals(_current, locale, StringComparison.OrdinalIgnoreCase)) return;
            _current = locale.ToLowerInvariant();
            LoadStringTable(_current);
            PlayerPrefs.SetString(PREF_KEY, _current);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke(_current);
        }

        void LoadStringTable(string locale)
        {
            _strings.Clear();

            var ta = Resources.Load<TextAsset>($"Localization/strings/{locale}");

            if (!ta) ta = Resources.Load<TextAsset>($"Localization/strings/{defaultLocale}");
            if (ta)
            {
                var dict = JsonUtilityWrapper.ToDictionary(ta.text);
                foreach (var kv in dict) _strings[kv.Key] = kv.Value;
            }
            else
            {
                Debug.LogError("String table not found for any locale");
            }
        }

        public string Get(string key, params object[] args)
        {
            if (!_strings.TryGetValue(key, out var raw))
            {
                Debug.LogWarning($"Missing string: {key} for locale {_current}");
                raw = key;
            }

            return args is { Length: > 0 } ? string.Format(raw, args) : raw;
        }

        public T GetAsset<T>(string assetKey) where T : UnityEngine.Object
        {
            var path = $"Localization/assets/{_current}/{assetKey}";
            var obj = Resources.Load<T>(path);
            if (!obj)
            {
                obj = Resources.Load<T>($"Localization/assets/{defaultLocale}/{assetKey}");
                if (!obj)
                    Debug.LogWarning($"Missing asset: {assetKey} for locale {_current} and default {defaultLocale}");
            }

            return obj;
        }
    }

    static class JsonUtilityWrapper
    {
        [Serializable] class KvList
    {
        public List<Entry> entries;
    }

        [Serializable] class Entry
        {
            public string key;
            public string value;
        }

        public static Dictionary<string, string> ToDictionary(string json)
        {
            var dict = new Dictionary<string, string>();
            var tmp = JsonHelper.FromJsonRaw(json);
            foreach (var (k, v) in tmp) dict[k] = v;
            return dict;
        }
    }

    static class JsonHelper
    {
        public static IEnumerable<(string, string)> FromJsonRaw(string json)
        {
            json = json.Trim().TrimStart('{').TrimEnd('}');
            var parts = json.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var kv = p.Split(new[] { ':' }, 2);
                if (kv.Length != 2) continue;
                string k = kv[0].Trim().Trim('"');
                string v = kv[1].Trim().Trim('"');
                yield return (k, v.Replace("\\n", "\n"));
            }
        }
    }
}