using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using Game.Scripts.UI;
using UnityEngine.Localization.Settings;

namespace Game.Scripts.BugData
{

    public class BugListTMP : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Список текстовых компонентов для отображения целей (по одному на каждую цель)")]
        [SerializeField] private List<TMP_Text> _targetTextList = new List<TMP_Text>();

        [Tooltip("Префаб GameObject для эффекта зачёркивания")]
        [SerializeField] private GameObject _strikethroughPrefab;

        [Header("Материалы")]
        [SerializeField] private string _coloredMaterialName = "DotGothic16-Regular Outline";
        
        [Header("Формат ото��ражения")]
        [Tooltip("Процент к базовому размеру шрифта колонки для описания.")]
        [SerializeField, Min(1)] private int _descriptionSizePercent = 125;
        [Tooltip("Подсвечивать слова-цвета соответствующими цветами в тексте")]
        [SerializeField] private bool _highlightColors = true;
        [Tooltip("Режим колоризации: цвет текста или цвет фона")]
        [SerializeField] private ColorizationMode _colorizationMode = ColorizationMode.TextColor;

        private List<GameObject> _strikethroughInstances = new List<GameObject>();

        private void Awake()
        {
            foreach (var textComponent in _targetTextList)
            {
                if (textComponent != null)
                {
                    textComponent.richText = true;
                }
            }
            
            EnsureStrikethroughInstances();
        }

        private void OnEnable()
        {
            if (BugList.Instance != null)
            {
                BugList.Instance.TargetsChanged += OnTargetsChanged;
                RebuildDisplay();
            }
            
            BugList.InstanceChanged += OnBugListInstanceChanged;
            
            // Подписываемся на смену локали
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDisable()
        {
            if (BugList.Instance != null)
            {
                BugList.Instance.TargetsChanged -= OnTargetsChanged;
            }
            
            BugList.InstanceChanged -= OnBugListInstanceChanged;
            
            // Отписываемся от смены локали
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnBugListInstanceChanged(BugList newInstance)
        {
            if (newInstance != null)
            {
                newInstance.TargetsChanged += OnTargetsChanged;
                RebuildDisplay();
            }
        }

        private void OnTargetsChanged()
        {
            RebuildDisplay();
        }

        private void OnValidate()
        {
            if (_targetTextList != null)
            {
                foreach (var textComponent in _targetTextList)
                {
                    if (textComponent != null)
                    {
                        textComponent.richText = true;
                    }
                }
            }
            
            // Обновляем экземпляры зачёркивания в редакторе
            if (Application.isPlaying)
            {
                EnsureStrikethroughInstances();
            }

            RebuildDisplay();
        }

        private void RebuildDisplay()
        {
            if (BugList.Instance == null) return;

            EnsureTargetTextListSize();
            EnsureStrikethroughInstances();

            if (BugList.Instance.Targets == null || BugList.Instance.Targets.Count == 0)
            {
                ClearAllTargetTexts();
                HideAllStrikethroughs();
                return;
            }

            var pairs = BugList.Instance.Targets.Select(key =>
            {
                if (BugList.Instance.TryGetMeta(key, out var t, out var d))
                    return (key, title: t, desc: d, collected: BugList.Instance.IsCollected(key));
                else
                    return (key, title: key, desc: "(no description)", collected: BugList.Instance.IsCollected(key));
            }).ToList();

            for (int i = 0; i < pairs.Count && i < _targetTextList.Count; i++)
            {
                var pair = pairs[i];
                var textComponent = _targetTextList[i];

                if (textComponent != null)
                {
                    string formattedText = FormatItem(pair.title, pair.desc, pair.collected);
                    textComponent.text = formattedText;
                    
                    // Обновляем зачёркивание
                    UpdateStrikethrough(i, textComponent, pair.collected);
                }
            }

            for (int i = pairs.Count; i < _targetTextList.Count; i++)
            {
                if (_targetTextList[i] != null)
                {
                    _targetTextList[i].text = string.Empty;
                }
                
                // Скрываем неиспользуемые зачёркивания
                if (i < _strikethroughInstances.Count && _strikethroughInstances[i] != null)
                {
                    _strikethroughInstances[i].SetActive(false);
                }
            }
        }

        private void EnsureTargetTextListSize()
        {
            if (_targetTextList == null)
            {
                _targetTextList = new List<TMP_Text>();
            }

            int requiredCount = BugList.Instance?.Targets?.Count ?? 0;



            while (_targetTextList.Count < requiredCount)
            {
                _targetTextList.Add(null);
            }
        }

        private void ClearAllTargetTexts()
        {
            foreach (var textComponent in _targetTextList)
            {
                if (textComponent != null)
                {
                    textComponent.text = string.Empty;
                }
            }
        }

        private void EnsureStrikethroughInstances()
        {
            if (_strikethroughPrefab == null) return;
            
            if (_strikethroughInstances == null)
            {
                _strikethroughInstances = new List<GameObject>();
            }

            int requiredCount = _targetTextList?.Count ?? 0;

            // Создаём недостающие экземпляры
            while (_strikethroughInstances.Count < requiredCount)
            {
                int currentIndex = _strikethroughInstances.Count;
                Transform parent = currentIndex < _targetTextList.Count && _targetTextList[currentIndex] != null 
                    ? _targetTextList[currentIndex].transform.parent 
                    : transform;
                    
                GameObject instance = Instantiate(_strikethroughPrefab, parent);
                instance.SetActive(false);
                _strikethroughInstances.Add(instance);
            }

            // Удаляем лишние экземпляры
            while (_strikethroughInstances.Count > requiredCount)
            {
                int lastIndex = _strikethroughInstances.Count - 1;
                if (_strikethroughInstances[lastIndex] != null)
                {
                    Destroy(_strikethroughInstances[lastIndex]);
                }
                _strikethroughInstances.RemoveAt(lastIndex);
            }
        }

        private void UpdateStrikethrough(int index, TMP_Text textComponent, bool isCollected)
        {
            if (_strikethroughPrefab == null || index >= _strikethroughInstances.Count) return;

            GameObject strikethrough = _strikethroughInstances[index];
            if (strikethrough == null) return;

            Transform strikethroughTransform = strikethrough.transform;
            RectTransform textRect = textComponent.GetComponent<RectTransform>();

            if (strikethroughTransform != null && textRect != null)
            {
                // Устанавливаем того же родителя
                if (strikethroughTransform.parent != textRect.parent)
                {
                    strikethroughTransform.SetParent(textRect.parent, false);
                }
                
                // Копируем локальную позицию текста
                strikethroughTransform.localPosition = textRect.localPosition;
                
                
                // Применяем трансформации после установки позиции
                var animatedLine = strikethrough.GetComponent<AnimatedLineRendererUI>();
                if (animatedLine != null)
                {
                    animatedLine.ApplyTransformations(textRect.localPosition);
                }
                
                // Устанавливаем в иерархии сразу после текстового элемента
                strikethroughTransform.SetSiblingIndex(textRect.GetSiblingIndex() + 1);
            }
            
            // Показываем зачёркивание только для собранных целей (активируем ПОСЛЕ позиционирования)
            strikethrough.SetActive(isCollected);
        }

        private void HideAllStrikethroughs()
        {
            foreach (var strikethrough in _strikethroughInstances)
            {
                if (strikethrough != null)
                {
                    strikethrough.SetActive(false);
                }
            }
        }

        private string HighlightColorsIn(string text)
        {
            if (!_highlightColors || string.IsNullOrEmpty(text)) return text;

            string colored = TextColorizer.Colorize(text, _colorizationMode);

            // Добавляем материал только вокруг цветных слов
            if (_colorizationMode == ColorizationMode.TextColor)
            {
                string materialOpen = $"<material=\"{_coloredMaterialName}\">";
                string materialClose = "</material>";
                colored = Regex.Replace(colored, @"(<color=[^>]+>.*?</color>)", $"{materialOpen}$1{materialClose}");
            }

            return colored;
        }

        private string FormatItem(string title, string desc, bool collected)
        {
            string titleCapsItalic = $"<line-height=110%><i>{(title ?? string.Empty).ToUpperInvariant()}</i>";
            string descCaps = (desc ?? string.Empty).ToUpperInvariant();
            string colored = HighlightColorsIn(descCaps);
            string formattedDesc = $"<size={_descriptionSizePercent}%><b>{colored}</b></size>\n</line-height>";

            string result = $"{titleCapsItalic}\n{formattedDesc}";
            
            return result;
        }

        private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            // Переинициализируем TextColorizer для новой локали
            TextColorizer.Initialize();
            
            // Перестраиваем отображение
            RebuildDisplay();
        }
    }
}
