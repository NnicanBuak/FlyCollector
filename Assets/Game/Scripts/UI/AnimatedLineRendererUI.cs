using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.Scripts.UI
{
    public class AnimatedLineRendererUI : MonoBehaviour
    {
        [Header("Элементы UI")]
        [SerializeField] private List<GameObject> lineObjects = new List<GameObject>();

        [Header("Настройки анимации")]
        [SerializeField] private float delayBetweenLines = 0.2f;
        [SerializeField] private float drawDurationPerLine = 1.0f;
        [SerializeField] private Ease drawEaseType = Ease.Linear;

        [Header("Опции")]
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool clearOnDisable = true;
        [SerializeField] private bool flipOnNegativeY = true;
        [SerializeField] private float yOffset = 0.01f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        private Sequence _currentSequence;
        private readonly List<Vector3[]> _originalPositions = new List<Vector3[]>();
        private readonly List<float> _animationValues = new List<float>();
        private bool _isInitialized;

        private void Awake()
        {
            ApplyTransformations();
        }

        private void Start()
        {
            InitializeLines();
        }

        private void OnEnable()
        {
            if (showDebugLogs)
                Debug.Log(
                    $"[AnimatedLineRendererUI] OnEnable, playOnEnable={playOnEnable}, positions={_originalPositions.Count}");


            if (!_isInitialized)
            {
                InitializeLines();
            }

            if (playOnEnable && _originalPositions.Count > 0)
            {
                PlayAnimation();
            }
        }

        private void OnDisable()
        {
            if (showDebugLogs)
                Debug.Log("[AnimatedLineRendererUI] OnDisable");

            _currentSequence?.Kill();
            _currentSequence = null;

            if (clearOnDisable)
            {
                ResetAllLines();
            }
        }

        public void SetLines(List<GameObject> lines)
        {
            if (showDebugLogs)
                Debug.Log($"[AnimatedLineRendererUI] SetLines called with {lines?.Count ?? 0} lines");

            lineObjects = lines ?? new List<GameObject>();
            _originalPositions.Clear();
            _animationValues.Clear();
            
            // Применяем трансформации перед сохранением позиций
            ApplyTransformations();

            foreach (var lineGo in lineObjects)
            {
                if (lineGo == null) continue;

                var lineRenderer = lineGo.GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    if (showDebugLogs)
                        Debug.LogWarning($"[AnimatedLineRendererUI] LineRenderer not found on {lineGo.name}");
                    continue;
                }


                bool wasActive = lineGo.activeSelf;
                if (!wasActive)
                    lineGo.SetActive(true);


                var positions = new Vector3[lineRenderer.positionCount];
                lineRenderer.GetPositions(positions);

                _originalPositions.Add(positions);
                _animationValues.Add(0f);

                if (showDebugLogs)
                    Debug.Log(
                        $"[AnimatedLineRendererUI] Saved {positions.Length} positions for {lineGo.name}, worldSpace={lineRenderer.useWorldSpace}");


                if (!wasActive)
                    lineGo.SetActive(wasActive);
            }

            if (showDebugLogs)
                Debug.Log($"[AnimatedLineRendererUI] Total saved positions: {_originalPositions.Count}");


            if (playOnEnable && gameObject.activeInHierarchy && _originalPositions.Count > 0)
            {
                PlayAnimation();
            }
        }

        public void PlayAnimation()
        {
            if (showDebugLogs)
                Debug.Log(
                    $"[AnimatedLineRendererUI] PlayAnimation called, lines={lineObjects.Count}, positions={_originalPositions.Count}");

            _currentSequence?.Kill();
            ResetAllLines();

            _currentSequence = DOTween.Sequence();

            for (int i = 0; i < lineObjects.Count; i++)
            {
                var lineGo = lineObjects[i];
                int index = i;

                if (lineGo == null || index >= _originalPositions.Count)
                {
                    if (showDebugLogs)
                        Debug.LogWarning($"[AnimatedLineRendererUI] Skipping line {index}: null or no positions");
                    continue;
                }

                if (!lineGo.activeSelf)
                    lineGo.SetActive(true);

                var lineRenderer = lineGo.GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    if (showDebugLogs)
                        Debug.LogWarning($"[AnimatedLineRendererUI] Skipping line {index}: no LineRenderer");
                    continue;
                }

                var originalPositions = _originalPositions[index];
                if (originalPositions.Length == 0)
                {
                    if (showDebugLogs)
                        Debug.LogWarning($"[AnimatedLineRendererUI] Skipping line {index}: empty positions");
                    continue;
                }

                _animationValues[index] = 0f;

                if (showDebugLogs)
                    Debug.Log($"[AnimatedLineRendererUI] Adding animation for line {index}, points: {originalPositions.Length}");

                // Устанавливаем все точки сразу в первую позицию
                lineRenderer.positionCount = originalPositions.Length;
                var currentPositions = new Vector3[originalPositions.Length];
                for (int j = 0; j < originalPositions.Length; j++)
                {
                    currentPositions[j] = originalPositions[0];
                }
                lineRenderer.SetPositions(currentPositions);

                // Анимируем прогресс от 0 до 1
                _currentSequence.Append(DOTween.To(
                        () => _animationValues[index],
                        x =>
                        {
                            _animationValues[index] = x;

                            if (lineRenderer != null && lineRenderer.positionCount == originalPositions.Length)
                            {
                                var tempPositions = new Vector3[originalPositions.Length];
                                
                                for (int j = 0; j < originalPositions.Length; j++)
                                {
                                    if (j == 0)
                                    {
                                        // Первая точка всегда на своем месте
                                        tempPositions[j] = originalPositions[0];
                                    }
                                    else
                                    {
                                        // Каждая следующая точка интерполируется от предыдущей к целевой
                                        // В начале анимации (x=0) все точки находятся на позиции первой точки
                                        // В конце (x=1) все точки на своих целевых позициях
                                        float pointProgress = Mathf.Clamp01((x * originalPositions.Length) - j + 1);
                                        tempPositions[j] = Vector3.Lerp(originalPositions[j - 1], originalPositions[j], pointProgress);
                                    }
                                }

                                lineRenderer.SetPositions(tempPositions);
                            }
                        },
                        1f,
                        drawDurationPerLine)
                    .SetEase(drawEaseType));

                if (index < lineObjects.Count - 1)
                {
                    _currentSequence.AppendInterval(delayBetweenLines);
                }
            }

            _currentSequence.OnComplete(() =>
            {
                if (showDebugLogs)
                    Debug.Log("[AnimatedLineRendererUI] Animation sequence completed");
            });
        }

        public void ResetAllLines()
        {
            if (showDebugLogs)
                Debug.Log("[AnimatedLineRendererUI] ResetAllLines called");

            for (int i = 0; i < lineObjects.Count; i++)
            {
                var lineGo = lineObjects[i];
                if (lineGo == null) continue;

                var lineRenderer = lineGo.GetComponent<LineRenderer>();
                if (lineRenderer == null) continue;

                // Устанавливаем все точки в первую позицию
                if (i < _originalPositions.Count && _originalPositions[i].Length > 0)
                {
                    lineRenderer.positionCount = _originalPositions[i].Length;
                    var resetPositions = new Vector3[_originalPositions[i].Length];
                    for (int j = 0; j < resetPositions.Length; j++)
                    {
                        resetPositions[j] = _originalPositions[i][0];
                    }
                    lineRenderer.SetPositions(resetPositions);
                }
                else
                {
                    lineRenderer.positionCount = 0;
                }
            }

            for (int i = 0; i < _animationValues.Count; i++)
            {
                _animationValues[i] = 0f;
            }
        }

        public void ShowAllLines()
        {
            if (showDebugLogs)
                Debug.Log("[AnimatedLineRendererUI] ShowAllLines called");

            for (int i = 0; i < lineObjects.Count; i++)
            {
                var lineGo = lineObjects[i];
                if (lineGo == null || i >= _originalPositions.Count) continue;

                var lineRenderer = lineGo.GetComponent<LineRenderer>();
                if (lineRenderer == null) continue;

                var originalPositions = _originalPositions[i];
                lineRenderer.positionCount = originalPositions.Length;
                lineRenderer.SetPositions(originalPositions);

                if (i < _animationValues.Count)
                    _animationValues[i] = originalPositions.Length;
            }
        }

        private void OnDestroy()
        {
            _currentSequence?.Kill();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyTransformations();
            }
        }

        private void InitializeLines()
        {
            _originalPositions.Clear();
            _animationValues.Clear();

            foreach (var lineGo in lineObjects)
            {
                if (lineGo == null) continue;

                var lineRenderer = lineGo.GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    if (showDebugLogs)
                        Debug.LogWarning($"[AnimatedLineRendererUI] LineRenderer not found on {lineGo.name}");
                    continue;
                }


                bool wasActive = lineGo.activeSelf;
                if (!wasActive)
                    lineGo.SetActive(true);


                var positions = new Vector3[lineRenderer.positionCount];
                lineRenderer.GetPositions(positions);

                _originalPositions.Add(positions);
                _animationValues.Add(0f);

                if (showDebugLogs)
                    Debug.Log(
                        $"[AnimatedLineRendererUI] Saved {positions.Length} positions for {lineGo.name}, worldSpace={lineRenderer.useWorldSpace}");


                if (!wasActive)
                    lineGo.SetActive(wasActive);
            }

            if (showDebugLogs)
                Debug.Log($"[AnimatedLineRendererUI] Total saved positions: {_originalPositions.Count}");

            _isInitialized = true;
        }

        public void ApplyTransformations()
        {
            Vector3 currentLocalPosition = transform.localPosition;
            
            // Применяем flip только если текущая Y позиция отрицательная
            if (flipOnNegativeY && currentLocalPosition.y < 0)
            {
                var rotation = transform.localEulerAngles;
                rotation.z = 180f;
                transform.localEulerAngles = rotation;
            }
            else
            {
                var rotation = transform.localEulerAngles;
                rotation.z = 0f;
                transform.localEulerAngles = rotation;
            }
            
                // Применяем Y offset всегда
            if (!Mathf.Approximately(yOffset, 0f))
            {
                Vector3 localPos = transform.localPosition;
                localPos.y = currentLocalPosition.y + (currentLocalPosition.y < 0 ? -yOffset : yOffset);
                transform.localPosition = localPos;
            }
            
            if (showDebugLogs)
                Debug.Log($"[AnimatedLineRendererUI] Transformations applied: currentY={currentLocalPosition.y}, finalPos={transform.localPosition}, rot={transform.localEulerAngles}");
        }

        public void ApplyTransformations(Vector3 textPosition)
        {
            // Применяем flip только если Y позиция текста отрицательная
            if (flipOnNegativeY && textPosition.y < 0)
            {
                var rotation = transform.localEulerAngles;
                rotation.z = 180f;
                transform.localEulerAngles = rotation;
            }
            else
            {
                var rotation = transform.localEulerAngles;
                rotation.z = 0f;
                transform.localEulerAngles = rotation;
            }
            
            // Применяем Y offset всегда
            if (!Mathf.Approximately(yOffset, 0f))
            {
                Vector3 localPos = transform.localPosition;
                localPos.y = textPosition.y + (textPosition.y < 0 ? -yOffset : yOffset);
                transform.localPosition = localPos;
            }
            
            if (showDebugLogs)
                Debug.Log($"[AnimatedLineRendererUI] Transformations applied: textY={textPosition.y}, finalPos={transform.localPosition}, rot={transform.localEulerAngles}");
        }
    }
}
