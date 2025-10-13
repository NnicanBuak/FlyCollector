using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ClipList — простой планировщик для ОДНОГО AudioSource на том же объекте.
/// У каждого элемента настраиваются:
/// - AudioClip
/// - индивидуальная громкость (0..1)
/// - длительность проигрывания (если <=0 — вся длина клипа; если больше — ограничится длиной клипа)
///
/// ВАЖНО ДЛЯ ИНСПЕКТОРА:
/// 1) Имя файла ДОЛЖНО совпадать с именем класса: ClipList.cs
/// 2) Unity не сериализует double — используем float, чтобы элементы корректно отображались в инспекторе.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class ClipList : MonoBehaviour
{
    [Serializable]
    public class Item
    {
        public AudioClip clip;

        [Range(0f, 1f)]
        [Tooltip("Индивидуальная громкость для этого клипа.")]
        public float volume = 1f;

        [Tooltip("Если <= 0 — берётся вся длина клипа. Если больше длины клипа — будет ограничено.")]
        public float durationSeconds = 0f;
    }

    [Header("List")]
    [Tooltip("Список клипов, проигрываемых по порядку.")]
    public List<Item> items = new List<Item>();

    [Tooltip("Повторять список по кругу.")]
    public bool loop = false;

    [Tooltip("Запустить сразу при старте сцены.")]
    public bool playOnStart = true;

    [Header("Scheduling")]
    [Tooltip("Запас времени (сек) перед стартом каждого клипа.")]
    [Min(0f)]
    public float lookAhead = 0.12f;

    private AudioSource _src;
    private float _baseVolume;

    // Индекс следующего элемента
    private int _nextIndex;

    // Идёт ли сейчас запланированное воспроизведение
    private bool _hasActive;

    // Конец текущего клипа в dsp-времени
    private double _currentEndDsp;

    // Запущен ли цикл воспроизведения
    private bool _started;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false; // управляем стартом сами
        _src.loop = false;        // длительность задаём вручную
        _baseVolume = _src.volume;
    }

    void Start()
    {
        if (playOnStart && items.Count > 0)
            StartQueue(true);
    }

    void OnDisable()
    {
        if (_src) _src.volume = _baseVolume;
    }

    void Update()
    {
        if (!_started || items.Count == 0)
            return;

        if (_hasActive)
        {
            var now = AudioSettings.dspTime;
            if (!_src.isPlaying || now >= _currentEndDsp - 0.001)
            {
                _hasActive = false;
                _src.volume = _baseVolume; // вернуть базовую громкость
            }
        }

        if (!_hasActive)
        {
            if (!TryNext(out var next))
            {
                _started = false;
                _src.volume = _baseVolume;
                return;
            }

            var clip = next.clip;
            if (!clip) return;

            var dur = next.durationSeconds <= 0f ? clip.length : next.durationSeconds;
            if (dur > clip.length) dur = clip.length;

            var startDsp = Math.Max(AudioSettings.dspTime + lookAhead, AudioSettings.dspTime + 0.01);
            var endDsp   = startDsp + dur;

            _src.Stop();
            _src.clip = clip;
            _src.volume = Mathf.Clamp01(next.volume);

            _src.PlayScheduled(startDsp);
            _src.SetScheduledEndTime(endDsp);

            _currentEndDsp = endDsp;
            _hasActive = true;
        }
    }

    /// <summary>Запустить очередь. Если forceFromStart = true — начнёт с нуля.</summary>
    public void StartQueue(bool forceFromStart = true)
    {
        if (forceFromStart) _nextIndex = 0;
        _started = true;
        _hasActive = false;
    }

    /// <summary>Остановить воспроизведение (список не очищается), вернуть громкость источника.</summary>
    public void StopQueue()
    {
        _started = false;
        _hasActive = false;
        _src.Stop();
        _src.volume = _baseVolume;
    }

    /// <summary>Очистить список и остановить воспроизведение.</summary>
    public void Clear()
    {
        StopQueue();
        items.Clear();
        _nextIndex = 0;
    }

    /// <summary>Добавить элемент в конец списка.</summary>
    public void Add(AudioClip clip, float volume = 1f, float durationSeconds = 0f)
    {
        if (!clip) return;
        items.Add(new Item { clip = clip, volume = Mathf.Clamp01(volume), durationSeconds = durationSeconds });
    }

    /// <summary>Вставить элемент по индексу.</summary>
    public void Insert(int index, AudioClip clip, float volume = 1f, float durationSeconds = 0f)
    {
        if (!clip) return;
        index = Mathf.Clamp(index, 0, items.Count);
        items.Insert(index, new Item { clip = clip, volume = Mathf.Clamp01(volume), durationSeconds = durationSeconds });
        if (index <= _nextIndex) _nextIndex++;
    }

    /// <summary>Удалить элемент по индексу.</summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= items.Count) return;
        items.RemoveAt(index);
        if (index < _nextIndex) _nextIndex = Mathf.Max(0, _nextIndex - 1);
    }

    public int NextIndex => _nextIndex;
    public int Count => items.Count;

    private bool TryNext(out Item item)
    {
        item = null;

        if (items.Count == 0) return false;

        if (_nextIndex >= items.Count)
        {
            if (!loop) return false;
            _nextIndex = 0;
        }

        item = items[_nextIndex];
        _nextIndex++;
        return item != null && item.clip != null;
    }
}
