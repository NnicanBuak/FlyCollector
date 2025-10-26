using UnityEngine;
using TMPro;

namespace Game.Scripts.UI
{
    /// <summary>
    /// Дублирует TextMeshProUGUI позади оригинала и подставляет в него текст, сгенерированный через TextColorizer.ColorizeBackgroundDuplicate.
    /// Идея: оригинальный TMP рисует буквы, а дубликат (позади) рисует только &lt;mark&gt; фоны — прочий текст в дубликате полностью прозрачный.
    /// </summary>
    [DisallowMultipleComponent]
    public class TMPBackgroundDuplicator : MonoBehaviour
    {
        [Tooltip("Target TextMeshProUGUI. Если не указан, компонент попытается найти его на этом же объекте.")]
        public TextMeshProUGUI target;
        [Tooltip("Автоматическое смещение дубликата по оси Z (отрицательное значение помещает дубликат дальше от камеры).")]
        public float duplicateZOffset = -0.02f;

        [Tooltip("Если включено, дубликат будет помещён на отдельный Canvas с другим sortingOrder — полезно для Screen Space - Overlay, где Z не влияет.")]
        public bool useSeparateCanvas;

        [Tooltip("Если включено, при создании/восстановлении дубликата будут удаляться старые дочерние TMP SubMesh объекты у исходного текста (удаляет остатки, созданные старыми rich-text тегами).")]
        public bool cleanupOldSubMeshes = true;

        [Tooltip("Смещение sortingOrder для дубликата при использовании отдельного Canvas (отрицательное значение помещает дубликат позади).")]
        public int canvasSortingOffset = -1;

        // Ссылка на дубликат
        private TextMeshProUGUI _duplicate;
        private string _lastSourceText;

        /// <summary>
        /// Позволяет программно задать смещение дубликата по Z и применить его немедленно.
        /// </summary>
        public void SetDuplicateZOffset(float zOffset)
        {
            duplicateZOffset = zOffset;
            if (_duplicate == null) EnsureDuplicateExists();
            ApplyDuplicateZOffset();
            UpdateDuplicateText();
        }

        private void OnValidate()
        {
            // В редакторе: если target не назначен, попробем найти TMP на том же объекте
            if (target == null)
                target = GetComponent<TextMeshProUGUI>();

            // Попробуем применить смещение в редакторе сразу
            if (!Application.isPlaying)
            {
                if (target != null)
                {
                    EnsureDuplicateExists();
                    if (_duplicate != null)
                        ApplyDuplicateZOffset();
                }
            }
        }

        private void Awake()
        {
            if (target == null)
                target = GetComponent<TextMeshProUGUI>();

            if (target == null)
            {
                Debug.LogWarning("[TMPBackgroundDuplicator] Нет TextMeshProUGUI в target или на объекте.");
                enabled = false;
                return;
            }
            
            EnsureDuplicateExists();
        }

        private void OnEnable()
        {
            EnsureDuplicateExists();

            RefreshImmediate();
        }

        /// <summary>
        /// Применяет Z-смещение для дубликата относительно позиции целевого RectTransform.
        /// Это гарантирует, что смещение не накапливается при повторных вызовах.
        /// </summary>
        private void ApplyDuplicateZOffset()
        {
            if (target == null || _duplicate == null) return;
            var srcRect = target.rectTransform;
            var dstRect = _duplicate.rectTransform;
            if (srcRect == null || dstRect == null) return;

            var srcLocalPos = srcRect.localPosition;
            dstRect.localPosition = new Vector3(srcLocalPos.x, srcLocalPos.y, srcLocalPos.z + duplicateZOffset);
 
            // Если у дубликата есть дочерние submesh'ы — применим то же смещение к ним (чтобы они не оставались на исходной Z).
            for (int i = 0; i < dstRect.childCount; i++)
            {
                var child = dstRect.GetChild(i);
                var p = child.localPosition;
                child.localPosition = new Vector3(p.x, p.y, srcLocalPos.z + duplicateZOffset);
            }

            // Также удостоверимся, что у target и _duplicate нет оставшихся TMP SubMesh (иногда TMP создаёт их вследствие старых тегов) — удаляем их
            if (cleanupOldSubMeshes)
            {
                CleanupSubMeshes(target.transform);
                CleanupSubMeshes(_duplicate.transform);
            }
        }

        private void OnDestroy()
        {
            if (_duplicate != null && Application.isPlaying)
            {
                Destroy(_duplicate.gameObject);
                _duplicate = null;
            }
        }

        private void Update()
        {
            if (target == null) return;

            if (target.text != _lastSourceText)
            {
                UpdateDuplicateText();
            }
        }

        /// <summary>
        /// Принудительно обновляет текст дубликата.
        /// </summary>
        public void RefreshImmediate()
        {
            EnsureDuplicateExists();
            // Применяем Z-смещение заново (на случай, если пользователь изменил значение в инспекторе)
            ApplyDuplicateZOffset();
            // Убедимся, что перед обновлением текста удалены возможные submesh'ы
            if (cleanupOldSubMeshes)
            {
                CleanupSubMeshes(target.transform);
                if (_duplicate != null) CleanupSubMeshes(_duplicate.transform);
            }
            UpdateDuplicateText();
        }

        private void EnsureDuplicateExists()
        {
            if (target == null) return;
            if (_duplicate != null) return;

            // Проверим в дочерних объектах, может уже есть готовый дубликат
            var existing = target.transform.parent?.Find(target.name + "_Background");
            if (existing != null)
            {
                _duplicate = existing.GetComponent<TextMeshProUGUI>();
                if (_duplicate != null)
                {
                    // При восстановлении — опционально удалим старые TMP SubMesh у исходного текста
                    if (cleanupOldSubMeshes)
                        CleanupSubMeshes(target.transform);

                    // Применим корректное Z-смещение на основе позиции target (чтобы не накапливать смещение)
                    ApplyDuplicateZOffset();
                    return;
                }
            }

            // Создаём новый объект-дубликат
            var go = new GameObject(target.name + "_Background");
            // Поместим рядом с target под тем же родителем
            go.transform.SetParent(target.transform.parent, false);
            
            if (useSeparateCanvas)
            {
                // Пытаемся найти Canvas-родителя
                var parentCanvas = target.GetComponentInParent<Canvas>();
                // Добавляем локальный Canvas на дубликат и применяем сортировку
                var canvas = go.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                if (parentCanvas != null)
                {
                    // Копируем режим рендера и камеру, чтобы Canvas выполнял отрисовку в том же пространстве
                    canvas.renderMode = parentCanvas.renderMode;
                    canvas.worldCamera = parentCanvas.worldCamera;
                    canvas.pixelPerfect = parentCanvas.pixelPerfect;
                    canvas.sortingLayerID = parentCanvas.sortingLayerID;
                    canvas.sortingOrder = parentCanvas.sortingOrder + canvasSortingOffset;
                }
                else
                {
                    // Если родительский Canvas не найден, установим безопасные значения
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = canvasSortingOffset;
                }
                // Добавим GraphicRaycaster чтобы Canvas корректно работал в UI (необязательно для рендера, но безопасно)
                go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Скопируем RectTransform параметры
            var srcRect = target.rectTransform;
            var dstRect = go.AddComponent<RectTransform>();
            dstRect.anchorMin = srcRect.anchorMin;
            dstRect.anchorMax = srcRect.anchorMax;
            dstRect.anchoredPosition = srcRect.anchoredPosition;
            dstRect.sizeDelta = srcRect.sizeDelta;
            dstRect.pivot = srcRect.pivot;
            dstRect.localScale = srcRect.localScale;
            dstRect.localRotation = srcRect.localRotation;
            // Применяем смещение по Z чтобы дубликат находился немного дальше от камеры
            dstRect.localPosition = new Vector3(srcRect.localPosition.x, srcRect.localPosition.y, srcRect.localPosition.z + duplicateZOffset);

            // Добавляем TextMeshProUGUI и копируем основные настройки
            var dup = go.AddComponent<TextMeshProUGUI>();
            dup.raycastTarget = false;
            dup.richText = true;

            // Копируем визуальные настройки от оригинала (без текста)
            dup.font = target.font;
            dup.fontSharedMaterial = target.fontSharedMaterial;
            dup.fontSize = target.fontSize;
            dup.enableAutoSizing = target.enableAutoSizing;
            dup.fontSizeMin = target.fontSizeMin;
            dup.fontSizeMax = target.fontSizeMax;
            dup.alignment = target.alignment;
            dup.wordWrappingRatios = target.wordWrappingRatios;
            // Заменяем устаревшее свойство
            dup.textWrappingMode = target.textWrappingMode;
            //dup.enableWordWrapping = target.enableWordWrapping;
            dup.overflowMode = target.overflowMode;
            dup.extraPadding = target.extraPadding;
            dup.isOverlay = target.isOverlay;
            dup.maskable = target.maskable;

            // Помещаем дубликат непосредственно позади target: сохраним индекс target, вставим дубликат на его место, затем подвинем target выше
            int targetIndex = target.transform.GetSiblingIndex();
            // Устанавливаем индекс дубликата равным старому индексу target
            go.transform.SetSiblingIndex(targetIndex);
            // Сдвигаем target выше, чтобы он оказался поверх дубликата
            target.transform.SetSiblingIndex(targetIndex + 1);

            _duplicate = dup;
            // Убедимся, что применили Z-смещение
            ApplyDuplicateZOffset();
            // При создании нового дубликата — опционально удалим старые TMP SubMesh у исходного текста
            if (cleanupOldSubMeshes)
                CleanupSubMeshes(target.transform);
            // Если мы создали отдельный Canvas, убедимся, что RectTransform у дубликата заполняет тот же экранный регион — уже скопировали параметры, этого обычно достаточно
        }

        private void CleanupSubMeshes(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null) continue;
                // Ищем стандартное имя submesh'а, которое создаёт TextMeshPro: содержит "TMP SubMesh"
                if (child.name != null && child.name.Contains("TMP SubMesh"))
                {
                    if (Application.isPlaying)
                        Object.Destroy(child.gameObject);
                    else
                        Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private void UpdateDuplicateText()
        {
            if (target == null) return;
            if (_duplicate == null) EnsureDuplicateExists();
            if (_duplicate == null) return;

            if (cleanupOldSubMeshes)
            {
                // На всякий случай удалим старые submesh'ы у оригинального текста перед обновлением
                CleanupSubMeshes(target.transform);
            }

            string src = target.text ?? string.Empty;
            string newText = TextColorizer.ColorizeBackgroundDuplicate(src);
            if (newText != _duplicate.text)
            {
                _duplicate.text = newText;
                _duplicate.ForceMeshUpdate();
            }

            _lastSourceText = target.text;
        }

        /// <summary>
        /// Программно пересоздаёт дубликат (удаляет старый и создаёт заново). Полезно при тестировании и очистке артефактов.
        /// </summary>
        [ContextMenu("Recreate Duplicate")]
        public void RecreateDuplicate()
        {
            if (_duplicate != null)
            {
                if (Application.isPlaying)
                    Destroy(_duplicate.gameObject);
                else
                    DestroyImmediate(_duplicate.gameObject);
                _duplicate = null;
            }

            EnsureDuplicateExists();
            RefreshImmediate();
            Debug.Log("TMPBackgroundDuplicator: duplicate recreated.");
        }

        /// <summary>
        /// Выводит в консоль информацию для отладки: raw текст, дочерние объекты target и duplicate, наличие TMP SubMesh UI.
        /// </summary>
        [ContextMenu("Log Debug Info")]
        public void LogDebugInfo()
        {
            if (target == null)
            {
                Debug.Log("TMPBackgroundDuplicator: target == null");
                return;
            }

            Debug.Log($"[TMPBackgroundDuplicator] Target.text: '{target.text}'");

            void DumpChildren(Transform parent, string prefix)
            {
                if (parent == null) return;
                for (int i = 0; i < parent.childCount; i++)
                {
                    var c = parent.GetChild(i);
                    bool isSubMesh = c.GetComponent<TMPro.TMP_SubMeshUI>() != null || c.GetComponent<TMPro.TMP_SubMesh>() != null;
                    Debug.Log($"{prefix} Child[{i}] name='{c.name}' pos={c.localPosition} isSubMesh={isSubMesh}");
                }
            }

            Debug.Log("-- Target children --");
            DumpChildren(target.transform, "T");

            if (_duplicate != null)
            {
                Debug.Log("-- Duplicate children --");
                DumpChildren(_duplicate.transform, "D");
            }
            else
            {
                Debug.Log("Duplicate == null");
            }
        }
    }
}
