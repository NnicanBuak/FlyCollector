using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Text;
using UnityEngine.Localization.Settings;

namespace Game.Scripts.UI
{
    /// <summary>
    /// Режим колоризации текста
    /// </summary>
    public enum ColorizationMode
    {
        /// <summary>Изменяет цвет текста</summary>
        TextColor,
        /// <summary>Изменяет цвет фона текста</summary>
        BackgroundColor,
        /// <summary>Добавляет цветной кружок перед текстом</summary>
        ColoredCircle
    }

    /// <summary>
    /// Универсальная система колоризации текста, работающая с LocalizationService.
    /// Автоматически загружает цвета из Resources/Localization и поддерживает префиксы light-/dark-.
    /// </summary>
    public static class TextColorizer
    {
        // Вспомогательный класс для хранения данных о форме цвета
        private class ColorFormData
        {
            public string HexCode { get; set; }
            public string BaseForm { get; set; }

            public ColorFormData(string hexCode, string baseForm)
            {
                HexCode = hexCode;
                BaseForm = baseForm;
            }
        }

        private static Dictionary<string, string> _colorNameToHex;
        private static Regex _colorsRegex;
        private static string _currentLocale;
        private static bool _initialized;

        // Новые поля для работы со склонениями
        private static Dictionary<string, string> _colorFormToBase; // Для связи склонённых форм с основной
        private static Dictionary<string, List<string>> _russianAdjectiveEndings;

        // Добавлено: кэш проверки русской локали
        private static bool IsRussianLocale => !string.IsNullOrEmpty(_currentLocale) &&
                                               _currentLocale.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Словарь с hex-кодами цветов (универсальные, не требуют локализации)
        /// Очищено от двойных ключей: оставлены только light-* и dark-*
        /// </summary>
        private static readonly Dictionary<string, string> ColorHexCodes = new Dictionary<string, string>
        {
            // Базовые
            { "red", "#772522" },
            { "green", "#346938" },
            { "blue", "#1E2ED3" },
            { "yellow", "#F4D865" },
            { "orange", "#B95C37" },
            { "purple", "#801078" },
            { "pink", "#E9B0D0" },
            { "black", "#2C2E34" },
            { "white", "#EEEBEA" },
            { "brown", "#884802" },
            { "gray", "#7C736E" },
            { "grey", "#7C736E" },
            { "gold", "#C3B013" },
            { "golden", "#C3B013" },
            { "silver", "#BEBABC" },
            { "teal", "#3B9187" },
            { "emerald", "#52B670" },
            { "malachite", "#52B670" },
            { "iron", "#3A403A" },
            { "chromic", "#BEBABC" },
            { "ruby", "#CF628C" },
            { "sapphire", "#313EA8" },
            { "bone", "#E0D0C3" },
            { "skeleton", "#E0D0C3" },
            { "lightskin", "#FAD7CB" },
            { "bronze", "#EA9457" },
            { "darkgreen", "#22862A" },
            { "dark", "#2C2E34" },

            // light- варианты
            { "light-red", "#DBB2AD" },
            { "light-green", "#BDED9E" },
            { "light-blue", "#AAC9E3" },
            { "light-yellow", "#E5E6CC" },
            { "light-orange", "#E8BC97" },
            { "light-purple", "#C893E4" },
            { "light-pink", "#E9B0D0" },
            { "light-brown", "#EA9457" },
            { "light-gray", "#D9D8D6" },
            { "light-grey", "#D9D8D6" },
            { "light-bronze", "#AAA460" },
            { "light-gold", "#FCEEB2" },
            { "light-teal", "#8FE8CB" },

            // dark- варианты
            { "dark-red", "#9B2F2C" },
            { "dark-green", "#22862A" },
            { "dark-blue", "#1926A9" },
            { "dark-yellow", "#938A0D" },
            { "dark-orange", "#DD6B3B" },
            { "dark-purple", "#660B83" },
            { "dark-pink", "#940E6B" },
            { "dark-brown", "#62321F" },
            { "dark-gray", "#3A403A" },
            { "dark-grey", "#3A403A" },
            { "dark-teal", "#183F46" },
            { "dark-iron", "#2C2E34" }
        };

        /// <summary>
        /// Инициализирует систему колоризации для текущей локали.
        /// </summary>
        public static void Initialize()
        {
            if (LocalizationSettings.SelectedLocale == null)
            {
                Debug.LogWarning("[TextColorizer] Unity Localization не инициализирована");
                return;
            }

            string locale = LocalizationSettings.SelectedLocale.Identifier.Code;

            // Повторная инициализация только при смене локали
            if (_initialized && _currentLocale == locale && _colorNameToHex != null)
                return;

            _currentLocale = locale;
            _colorNameToHex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _colorFormToBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Инициализация словаря склонений для русского языка
            InitializeRussianAdjectives();

            // Загружаем базовые цвета
            LoadColorsFromLocalization();

            // Добавлено: дополнительные русские синонимы (например, "голубой" как light-blue)
            if (IsRussianLocale)
            {
                AddRussianSynonyms();
            }

            // Генерируем формы склонений для русского языка
            if (IsRussianLocale)
            {
                GenerateRussianColorForms();
                GeneratePluralForms();
                GenerateRussianAdverbForms(); // Новое: наречные формы вроде "бело", "коричнево", "оранжево"
            }

            // Строим regex для поиска цветов в тексте
            BuildColorsRegex();

            _initialized = true;
            Debug.Log(
                $"[TextColorizer] Инициализирован для локали '{locale}', загружено {_colorNameToHex.Count} цветов");
        }

        /// <summary>
        /// Загружает данные о цветах из Unity Localization.
        /// </summary>
        private static void LoadColorsFromLocalization()
        {
            if (LocalizationSettings.StringDatabase == null)
            {
                Debug.LogWarning("[TextColorizer] StringDatabase не найдена");
                return;
            }

            // Список всех базовых цветов и их вариаций из BUGS.txt
            var baseColors = new[]
            {
                "red", "green", "blue", "yellow", "orange", "purple", "pink",
                "black", "white", "brown", "gray", "grey", "gold", "golden", "silver",
                "teal", "emerald", "malachite", "iron", "chromic", "ruby", "sapphire",
                "bone", "skeleton", "lightskin", "bronze", "darkgreen"
            };

            var prefixes = new[] { "", "light-", "dark-", "deep-" };
            var compoundColors = new[]
            {
                "swamp-green", "grey-yellow", "blue-purple", "red-orange", "orange-green",
                "teal-brown", "purple-yellow", "chromic-pink", "white-blue", "white-pink",
                "white-purple", "white-teal", "white-green", "white-yellow", "teal-blue",
                "red-blue", "green-blue", "yellow-green", "orange-brown",
                "purple-green", "blue-white", "teal-pink", "purple-orange", "brown-green",
                "white-brown", "red-purple", "green-brown", "blue-orange", "yellow-purple",
                "pink-orange", "teal-orange", "green-orange", "blue-brown", "yellow-brown",
                "pink-purple", "teal-green", "green-purple", "brown-purple", "orange-teal",
                "pink-brown", "blue-teal", "yellow-teal", "pink-teal", "purple-teal",
                "brown-teal", "grey-teal", "yellow-pink", "orange-pink", "green-pink",
                "blue-pink", "brown-pink", "purple-pink", "red-pink",
                "green-red", "yellow-red", "orange-red", "purple-red",
                "brown-red", "teal-red", "grey-red", "white-red", "black-red",
                "green-black", "blue-black", "yellow-black", "orange-black", "purple-black",
                "brown-black", "teal-black", "grey-black", "white-black", "pink-black",
                "red-black", "green-white", "blue-white", "yellow-white", "orange-white",
                "purple-white", "brown-white", "teal-white", "grey-white", "pink-white",
                "black-white", "red-grey", "green-grey", "yellow-grey",
                "orange-grey", "purple-grey", "brown-grey", "teal-grey", "pink-grey",
                "black-grey", "white-grey"
            };

            // Загружаем базовые цвета с префиксами
            foreach (var prefix in prefixes)
            {
                foreach (var baseColor in baseColors)
                {
                    string colorKey = prefix + baseColor;
                    if (ColorHexCodes.ContainsKey(colorKey))
                    {
                        LoadColorFromLocalization(colorKey);
                    }
                }
            }

            // Загружаем составные цвета (включая с префиксами)
            foreach (var compoundColor in compoundColors)
            {
                // Сначала без префикса — только если есть hex-код
                bool loaded = false;
                if (ColorHexCodes.ContainsKey(compoundColor))
                {
                    loaded = LoadColorFromLocalization(compoundColor);
                }

                // Если перевод составного цвета отсутствует, попробуем синтезировать его (для ru-локали)
                if (!loaded && IsRussianLocale && compoundColor.Contains('-'))
                {
                    TrySynthesizeRussianCompound(compoundColor);
                }

                // Проверяем варианты с префиксами для составных цветов (только если есть hex)
                foreach (var prefix in new[] { "light-", "dark-", "deep-" })
                {
                    string prefixed = prefix + compoundColor;
                    if (ColorHexCodes.ContainsKey(prefixed))
                    {
                        LoadColorFromLocalization(prefixed);
                    }
                }
            }
        }

        // Возвращает true, если загрузка удалась
        private static bool LoadColorFromLocalization(string colorKey)
        {
            if (LocalizationSettings.StringDatabase == null) return false;

            // Получаем переведённое название цвета
            string translatedName = LocalizationSettings.StringDatabase.GetLocalizedString("color", colorKey);
            if (string.IsNullOrEmpty(translatedName) || translatedName == $"color.{colorKey}")
            {
                Debug.LogWarning($"[TextColorizer] Перевод не найден для {colorKey}");
                return false; // Пропускаем, если перевод не найден
            }

            // Получаем hex-код из встроенного словаря (не из локализации!)
            if (!ColorHexCodes.TryGetValue(colorKey, out string hexCode))
            {
                Debug.LogWarning($"[TextColorizer] Hex-код не найден для {colorKey}");
                return false; // Пропускаем, если hex не найден
            }

            // Сохраняем маппинг: переведённое название -> hex
            _colorNameToHex[translatedName] = hexCode;

            // Также сохраняем английское название для универсальности
            string englishName = colorKey.ToUpperInvariant().Replace("-", " ");
            _colorNameToHex[englishName] = hexCode;

            // Сохраняем оригинальный ключ (например, "light-red") для поддержки в тексте
            _colorNameToHex[colorKey] = hexCode;

            Debug.Log($"[TextColorizer] Загружен цвет: {colorKey} -> '{translatedName}' -> {hexCode}");
            return true;
        }

        // Попытаться синтезировать русское составное название вида "фиолетово-зелёный" на основе переводов базовых цветов
        private static void TrySynthesizeRussianCompound(string compoundKey)
        {
            try
            {
                if (!ColorHexCodes.TryGetValue(compoundKey, out var hex))
                    return;

                var parts = compoundKey.Split('-');
                if (parts.Length != 2)
                    return; // поддерживаем пары

                // Получаем переводы компонент (например, "фиолетовый", "зелёный")
                string ruPart1 = LocalizationSettings.StringDatabase.GetLocalizedString("color", parts[0]);
                string ruPart2 = LocalizationSettings.StringDatabase.GetLocalizedString("color", parts[1]);
                if (string.IsNullOrEmpty(ruPart1) || ruPart1.StartsWith("color.") ||
                    string.IsNullOrEmpty(ruPart2) || ruPart2.StartsWith("color."))
                {
                    return;
                }

                // Преобразуем первую часть в наречную форму: "фиолетовый" -> "фиолетово"
                string ruAdv1 = ConvertToAdverbialForm(ruPart1);
                string ruCompound = $"{ruAdv1}-{ruPart2}";

                // Добавляем базовую составную форму и все её склонения
                if (!_colorNameToHex.ContainsKey(ruCompound))
                {
                    _colorNameToHex[ruCompound] = hex;
                    _colorFormToBase[ruCompound] = ruCompound; // базовая форма сама на себя
                    Debug.Log($"[TextColorizer] Синтезирован составной цвет: {compoundKey} -> '{ruCompound}' -> {hex}");

                    // Сгенерировать склонения для составного цвета (жен/сред/мн. и падежи)
                    GenerateCompoundColorForms(ruCompound, hex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[TextColorizer] Не удалось синтезировать составной цвет '{compoundKey}': {ex.Message}");
            }
        }

        // Добавление русских синонимов, отсутствующих в таблице локализации (минимальный набор под запрос)
        private static void AddRussianSynonyms()
        {
            // "голубой" маппим на light-blue
            if (ColorHexCodes.TryGetValue("light-blue", out var lightBlueHex))
            {
                const string ruAzure = "голубой";
                if (!_colorNameToHex.ContainsKey(ruAzure))
                {
                    _colorNameToHex[ruAzure] = lightBlueHex;
                    _colorFormToBase[ruAzure] = ruAzure;
                    Debug.Log($"[TextColorizer] Добавлен синоним: '{ruAzure}' -> {lightBlueHex}");
                }
            }

            // "хромовый" как синоним chromic (чтобы работали формы «хромовый/хромовые»)
            if (ColorHexCodes.TryGetValue("chromic", out var chromicHex))
            {
                var chromeAdj = "хромовый";
                if (!_colorNameToHex.ContainsKey(chromeAdj))
                {
                    _colorNameToHex[chromeAdj] = chromicHex;
                    _colorFormToBase[chromeAdj] = chromeAdj;
                    Debug.Log($"[TextColorizer] Добавлен синоним: '{chromeAdj}' -> {chromicHex}");
                }
            }

            // Явные standalone-наречные формы (на случай отсутствия базового прилагательного)
            if (ColorHexCodes.TryGetValue("white", out var whiteHex))
            {
                const string belo = "бело";
                if (!_colorNameToHex.ContainsKey(belo))
                {
                    _colorNameToHex[belo] = whiteHex;
                    _colorFormToBase[belo] = belo;
                }
            }
            if (ColorHexCodes.TryGetValue("brown", out var brownHex))
            {
                const string korichnevo = "коричнево";
                if (!_colorNameToHex.ContainsKey(korichnevo))
                {
                    _colorNameToHex[korichnevo] = brownHex;
                    _colorFormToBase[korichnevo] = korichnevo;
                }
            }
            if (ColorHexCodes.TryGetValue("orange", out var orangeHex))
            {
                const string oranzhevo = "оранжево";
                if (!_colorNameToHex.ContainsKey(oranzhevo))
                {
                    _colorNameToHex[oranzhevo] = orangeHex;
                    _colorFormToBase[oranzhevo] = oranzhevo;
                }
            }
        }

        /// <summary>
        /// Строит regex для поиска всех цветов в тексте.
        /// </summary>
        private static void BuildColorsRegex()
        {
            if (_colorNameToHex == null || _colorNameToHex.Count == 0)
            {
                _colorsRegex = null;
                return;
            }

            // Сортируем по длине (сначала длинные), чтобы "СВЕТЛО-КРАСНЫЙ" матчился раньше "КРАСНЫЙ"
            var patterns = _colorNameToHex.Keys
                .Select(k => Regex.Escape(k))
                .OrderByDescending(s => s.Length)
                .ToArray();

            // Универсальный паттерн для слов: перед и после не word characters
            string pattern = $@"(?<!\w)(?:{string.Join("|", patterns)})(?!\w)";
            try
            {
                _colorsRegex = new Regex(pattern, RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TextColorizer] Ошибка создания regex: {ex.Message}");
                _colorsRegex = null;
            }
        }

        /// <summary>
        /// К��лоризует текст, заменяя названия цветов на TextMeshPro rich text теги.
        /// </summary>
        /// <param name="text">Исходный текст</param>
        /// <returns>Текст с примененной колоризацией</returns>
        public static string Colorize(string text)
        {
            return Colorize(text, ColorizationMode.TextColor);
        }

        /// <summary>
        /// Кол��ризует текст с выбором режима (цвет текста или фона).
        /// </summary>
        /// <param name="text">Исходный текст</param>
        /// <param name="mode">Режим колоризации</param>
        /// <returns>Текст с примененной колоризацией</returns>
        public static string Colorize(string text, ColorizationMode mode)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Инициализируем, если ещё не инициализировано
            if (!_initialized)
                Initialize();

            // Если regex не создан, возвращаем исходный текст
            if (_colorsRegex == null || _colorNameToHex == null)
                return text;

            // Удаляем существующие rich-text теги из входного текста, чтобы избежать конфликтов
            text = SanitizeRichText(text);

            // Заменяем все найденные цвета
            var result = _colorsRegex.Replace(text, match =>
            {
                string colorName = match.Value;

                // Ищем hex-код для найденного цвета (регистронезависимо)
                if (_colorNameToHex.TryGetValue(colorName, out string hexCode))
                {
                    if (mode == ColorizationMode.BackgroundColor)
                    {
                        // Для режима фона текст остаётся без изменений, фон рисуется на дублирующем TMP с ColorizeBackgroundDuplicate
                        return match.Value;
                    }
                    else if (mode == ColorizationMode.ColoredCircle)
                    {
                        return $"<color={hexCode}>■</color> {match.Value}";
                    }
                    else
                    {
                        return $"<color={hexCode}>{match.Value}</color>";
                    }
                }

                return match.Value;
            });

            // Удаляем потенциально проблемные rich-text теги, которые могут создать TMP SubMesh'ы
            return SanitizeRichText(result);
        }

        /// <summary>
        /// Колоризует фон текста, заменяя названия цветов на TextMeshPro mark теги.
        /// </summary>
        /// <param name="text">Исходный текст</param>
        /// <returns>Текст с цветным фоном</returns>
        public static string ColorizeBackground(string text)
        {
            return Colorize(text, ColorizationMode.BackgroundColor);
        }

        /// <summary>
        /// Формирует текст для дублирующего TextMeshPro:
        /// - найденные цветовые слова остаются как &lt;mark=hex&gt;word&lt;/mark&gt;
        /// - все остальные символы оборачиваются в прозрачный цвет, чтобы сохранять метрики и расположение
        /// Этот текст предназначен для отдельного TMP, находящегося сзади оригинального, чтобы mark рисовал фон за буквами.
        /// </summary>
        public static string ColorizeBackgroundDuplicate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (!_initialized)
                Initialize();

            // Если нет данных о цветах — просто скрываем весь текст (чтобы дубликат ничего не рисовал)
            if (_colorsRegex == null || _colorNameToHex == null || _colorNameToHex.Count == 0)
            {
                return WrapTransparent(text);
            }

            var sb = new StringBuilder();
            int lastIndex = 0;
            var matches = _colorsRegex.Matches(text);
            foreach (Match match in matches)
            {
                if (!match.Success) continue;

                // Добавляем сегмент между совпадениями как прозрачный
                if (match.Index > lastIndex)
                {
                    sb.Append(WrapTransparent(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                string matched = match.Value;
                // Найдём hex (регистронезависимо) — ключи слов в словаре могут быть в переводе или английские формы
                if (!_colorNameToHex.TryGetValue(matched, out string hexCode))
                {
                    // Попробуем искать по ключу в словаре с учётом регистра
                    var kv = _colorNameToHex.FirstOrDefault(k =>
                        string.Equals(k.Key, matched, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(kv.Key))
                        hexCode = kv.Value;
                }

                if (!string.IsNullOrEmpty(hexCode))
                {
                    // Оборачиваем слово в <mark>, оставляя текст нетронутым — дубликат рисует фон за оригиналом
                    sb.Append($"<mark={hexCode}>{matched}</mark>");
                }
                else
                {
                    // Если нет hex — делаем прозрачным
                    sb.Append(WrapTransparent(matched));
                }

                lastIndex = match.Index + match.Length;
            }

            // Хвост после последнего совпадения
            if (lastIndex < text.Length)
            {
                sb.Append(WrapTransparent(text.Substring(lastIndex)));
            }

            // Удаляем потенциально проблемные теги из сгенерированного текста
            return SanitizeRichText(sb.ToString());
        }

        /// <summary>
        /// Оборачивает строку в прозрачный цвет, чтобы символы оставались видимыми для расчёта ширины/позиций, но были невидимы.
        /// </summary>
        private static string WrapTransparent(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // TMP поддерживает 8-значный hex (#RRGGBBAA). Используем полностью прозрачный цвет.
            return $"<color=#00000000>{s}</color>";
        }

        /// <summary>
        /// Принудительно обновляет данные колоризации (например, при смене языка).
        /// </summary>
        public static void Refresh()
        {
            _initialized = false;
            _currentLocale = null;
            Initialize();
        }

        /// <summary>
        /// Получает hex-код для названия цвета.
        /// </summary>
        public static bool TryGetColorHex(string colorName, out string hexCode)
        {
            if (!_initialized)
                Initialize();

            if (_colorNameToHex != null && _colorNameToHex.TryGetValue(colorName, out hexCode))
                return true;

            hexCode = null;
            return false;
        }

        /// <summary>
        /// Удаляет потенциально проблемные rich-text теги, которые могут создать TMP SubMesh'ы.
        /// </summary>
        private static string SanitizeRichText(string text)
        {
            // Пока что возвращаем текст как есть. Реализация может быть добавлена позже для удаления определённых тегов.
            return text;
        }

        /// <summary>
        /// Инициализирует словарь с окончаниями русских прилагательных
        /// </summary>
        private static void InitializeRussianAdjectives()
        {
            _russianAdjectiveEndings = new Dictionary<string, List<string>>
            {
                // Расширенный список окончаний для твердой основы (красн-ый)
                {
                    "ый", new List<string>
                    {
                        "ый", "ого", "ому", "ым", "ом", // муж. род
                        "ая", "ой", "ую", // жен. род
                        "ое", // средний род
                        "ые", "ых", "ыми" // множ. число
                    }
                },

                // Окончания для мягкой основы (син-ий)
                {
                    "ий", new List<string>
                    {
                        "ий", "его", "ему", "им", "ем", // муж. род
                        "яя", "ей", "юю", // жен. род
                        "ее", // средний род
                        "ие", "их", "ими" // множ. число
                    }
                },

                // Окончания для основ на ж, ш, ч, щ (рыж-ой)
                {
                    "ой", new List<string>
                    {
                        "ой", "ого", "ому", "ым", "ом", // муж. род
                        "ая", "ей", "ую", // жен. род
                        "ое", // средний род
                        "ые", "ых", "ыми" // множ. число
                    }
                }
            };

            // Добавим поддержку буквы "ё" в окончаниях
            AddYoVariants();
        }

        // Добавляет варианты с буквой "ё" вместо "е"
        private static void AddYoVariants()
        {
            foreach (var pair in _russianAdjectiveEndings)
            {
                var yoVariants = new List<string>();
                foreach (var ending in pair.Value)
                {
                    if (ending.Contains("е"))
                    {
                        yoVariants.Add(ending.Replace("е", "ё"));
                    }
                }

                pair.Value.AddRange(yoVariants);
            }
        }

        /// <summary>
        /// Генерирует все возможные склонения для русских названий цветов
        /// </summary>
        private static void GenerateRussianColorForms()
        {
            // Создаем список уже загруженных цветов
            var baseColors = _colorNameToHex.Keys.Where(k => ContainsCyrillic(k)).ToList();

            // Список форм для добавления после основной итерации
            var formsToAdd = new Dictionary<string, ColorFormData>();

            foreach (var baseColor in baseColors)
            {
                string hexCode = _colorNameToHex[baseColor];

                // Пропускаем английские ключи
                if (!ContainsCyrillic(baseColor))
                    continue;

                // Обработка составных цветов (напр. "темно-синий")
                if (baseColor.Contains('-'))
                {
                    GenerateCompoundColorForms(baseColor, hexCode, formsToAdd);
                    continue;
                }

                // Обработка простых цветовых прилагательных
                foreach (var endingBase in _russianAdjectiveEndings.Keys)
                {
                    if (baseColor.EndsWith(endingBase))
                    {
                        string stem = baseColor.Substring(0, baseColor.Length - endingBase.Length);

                        foreach (var ending in _russianAdjectiveEndings[endingBase])
                        {
                            string form = stem + ending;

                            if (!_colorNameToHex.ContainsKey(form))
                            {
                                _colorNameToHex[form] = hexCode;
                                _colorFormToBase[form] = baseColor;
                                Debug.Log($"[TextColorizer] Добавлена форма: {form} -> {baseColor} -> {hexCode}");

                                // Добавляем вариант с "е" вместо "ё"
                                if (form.Contains("ё"))
                                {
                                    string formWithE = form.Replace("ё", "е");
                                    if (!formsToAdd.ContainsKey(formWithE))
                                    {
                                        formsToAdd[formWithE] = new ColorFormData(hexCode, baseColor);
                                    }
                                }
                            }
                        }

                        break;
                    }
                }
            }

            // Добавляем варианты с "е" после основной итерации
            foreach (var kvp in formsToAdd)
            {
                if (!_colorNameToHex.ContainsKey(kvp.Key))
                {
                    _colorNameToHex[kvp.Key] = kvp.Value.HexCode;
                    _colorFormToBase[kvp.Key] = kvp.Value.BaseForm;
                    Debug.Log(
                        $"[TextColorizer] Добавлен вариант с 'е': {kvp.Key} -> {kvp.Value.BaseForm} -> {kvp.Value.HexCode}");
                }
            }
        }

        /// <summary>
        /// Генерирует формы для составных цветов (напр. "темно-синий", "красно-синий", "светло-оранжево-коричневый")
        /// </summary>
        private static void GenerateCompoundColorForms(string compoundColor, string hexCode,
            Dictionary<string, ColorFormData> formsToAdd = null)
        {
            string[] parts = compoundColor.Split('-');
            if (parts.Length < 2) return; // Нужно хотя бы два компонента

            string lastPart = parts[parts.Length - 1];

            // Ищем окончание для последнего компонента
            foreach (var endingBase in _russianAdjectiveEndings.Keys)
            {
                if (lastPart.EndsWith(endingBase))
                {
                    string stemLast = lastPart.Substring(0, lastPart.Length - endingBase.Length);

                    // Обрабатываем все части составного слова, кроме последней
                    var firstParts = new List<string>();

                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        string part = parts[i];
                        // Оставляем первые части как есть (они уже в правильной форме из локализации)
                        // Например: "светло" остается "светло", "оранжево" остается "оранжево"
                        firstParts.Add(part);
                    }

                    string prefix = string.Join("-", firstParts);
                    if (!string.IsNullOrEmpty(prefix))
                        prefix += "-";

                    // Генерируем все формы склонения для составного цвета
                    foreach (var ending in _russianAdjectiveEndings[endingBase])
                    {
                        string newForm = prefix + stemLast + ending;

                        if (!_colorNameToHex.ContainsKey(newForm))
                        {
                            _colorNameToHex[newForm] = hexCode;
                            _colorFormToBase[newForm] = compoundColor;
                            Debug.Log(
                                $"[TextColorizer] Добавлена составная форма: {newForm} -> {compoundColor} -> {hexCode}");

                            // Добавляем вариант с "е" вместо "ё" (если передан словарь)
                            if (formsToAdd != null && newForm.Contains("ё"))
                            {
                                string formWithE = newForm.Replace("ё", "е");
                                if (!formsToAdd.ContainsKey(formWithE))
                                {
                                    formsToAdd[formWithE] = new ColorFormData(hexCode, compoundColor);
                                }
                            }
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Преобразует прилагательное в наречную форму (для составных цветов)
        /// Например: "красный" -> "красно", "синий" -> "сине", "белый" -> "бело"
        /// </summary>
        private static string ConvertToAdverbialForm(string adjective)
        {
            // Проверяем, не является ли это уже наречной формой или префиксом
            if (adjective.EndsWith("о") || adjective.EndsWith("е"))
                return adjective;

            // Специальные случаи для префиксов
            if (adjective == "светло" || adjective == "тёмно" || adjective == "ярко" ||
                adjective == "тускло" || adjective == "бледно")
                return adjective;

            // Преобразуем окончания прилагательных в наречия
            if (adjective.EndsWith("ый"))
            {
                return adjective.Substring(0, adjective.Length - 2) + "о";
            }
            else if (adjective.EndsWith("ий"))
            {
                return adjective.Substring(0, adjective.Length - 2) + "е";
            }
            else if (adjective.EndsWith("ой"))
            {
                return adjective.Substring(0, adjective.Length - 2) + "о";
            }
            else if (adjective.EndsWith("ая"))
            {
                return adjective.Substring(0, adjective.Length - 2) + "о";
            }
            else if (adjective.EndsWith("яя"))
            {
                return adjective.Substring(0, adjective.Length - 2) + "е";
            }
            else if (adjective.EndsWith("ое") || adjective.EndsWith("ее"))
            {
                return adjective.Substring(0, adjective.Length - 2) + "о";
            }
            else if (adjective.EndsWith("ые") || adjective.EndsWith("ие"))
            {
                return adjective.Substring(0, adjective.Length - 2) + "о";
            }

            // Если не распознали окончание, возвращаем как есть
            return adjective;
        }

        /// <summary>
        /// Генерирует множественное число для всех цветов, где это возможно
        /// </summary>
        private static void GeneratePluralForms()
        {
            var colorsToAdd = new Dictionary<string, string>();

            foreach (var kvp in _colorNameToHex)
            {
                string color = kvp.Key;
                string hex = kvp.Value;

                // Пропускаем английские и уже множественные
                if (!ContainsCyrillic(color) || color.EndsWith("ые") || color.EndsWith("ие") || color.EndsWith("их") ||
                    color.EndsWith("им") || color.EndsWith("ими"))
                    continue;

                // Ищем окончание
                foreach (var endingBase in _russianAdjectiveEndings.Keys)
                {
                    if (color.EndsWith(endingBase))
                    {
                        string stem = color.Substring(0, color.Length - endingBase.Length);

                        // Определяем правильные множественные окончания в зависимости от типа основы
                        List<string> pluralEndings;
                        if (endingBase == "ий")
                        {
                            // Для мягкой основы: синий → синие, синих, синими
                            pluralEndings = new List<string> { "ие", "их", "им", "ими" };
                        }
                        else
                        {
                            // Для твердой основы: красный → красные, красных, красными
                            pluralEndings = new List<string> { "ые", "ых", "ым", "ыми" };
                        }

                        foreach (var pluralEnding in pluralEndings)
                        {
                            string pluralForm = stem + pluralEnding;
                            if (!_colorNameToHex.ContainsKey(pluralForm) && !colorsToAdd.ContainsKey(pluralForm))
                            {
                                colorsToAdd[pluralForm] = hex;
                                _colorFormToBase[pluralForm] = color;
                                Debug.Log(
                                    $"[TextColorizer] Добавлена множественная форма: {pluralForm} -> {color} -> {hex}");
                            }
                        }

                        break;
                    }
                }
            }

            // Добавляем после итерации, чтобы не модифицировать словарь во время итерации
            foreach (var kvp in colorsToAdd)
            {
                _colorNameToHex[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Проверяет наличие кириллических символов в строке
        /// </summary>
        private static bool ContainsCyrillic(string text)
        {
            return text.Any(c => (c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я'));
        }

        /// <summary>
        /// Возвращает базовую форму для любой склонённой формы цвета
        /// </summary>
        public static string GetBaseColorForm(string form)
        {
            if (!_initialized) Initialize();

            return _colorFormToBase.TryGetValue(form, out string baseForm)
                ? baseForm
                : form;
        }

        /// <summary>
        /// Генерирует наречные формы от базовых русских прилагательных цветов (например: белый -> бело, синий -> сине).
        /// Эти формы часто используются как компоненты сложных цветовых слов и могут встречаться отдельно.
        /// </summary>
        private static void GenerateRussianAdverbForms()
        {
            var toAdd = new Dictionary<string, ColorFormData>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _colorNameToHex)
            {
                var name = kvp.Key;
                var hex = kvp.Value;

                // Только кириллица и не составные
                if (!ContainsCyrillic(name) || name.Contains('-'))
                    continue;

                var adv = ConvertToAdverbialForm(name);
                if (adv == name) continue;

                if (!_colorNameToHex.ContainsKey(adv) && !toAdd.ContainsKey(adv))
                {
                    toAdd[adv] = new ColorFormData(hex, name);
                }
            }

            foreach (var kv in toAdd)
            {
                _colorNameToHex[kv.Key] = kv.Value.HexCode;
                _colorFormToBase[kv.Key] = kv.Value.BaseForm;
                Debug.Log($"[TextColorizer] Добавлена наречная форма: {kv.Key} -> {kv.Value.BaseForm} -> {kv.Value.HexCode}");
            }
        }
    }
}
