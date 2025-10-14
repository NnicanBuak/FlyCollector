using UnityEngine;

namespace Audio
{
    public class MusicOnTimer : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Таймер, который шлёт событие старта (например ваш GameTimer).")]
        [SerializeField] private GameTimer timer;

        [Tooltip("Список клипов, который должен стартовать после запуска таймера.")]
        [SerializeField] private ClipList clipList;

        [Header("Музыка ДО запуска таймера")]
        [Tooltip("AudioSource для фоновой музыки до старта таймера (отдельный источник!).")]
        [SerializeField] private AudioSource preLoopSource;

        [Tooltip("Клип, который крутится в цикле ДО старта таймера.")]
        [SerializeField] private AudioClip preLoopClip;

        [Tooltip("Автоматически запустить фоновый луп при старте сцены.")]
        [SerializeField] private bool playPreLoopOnStart = true;

        private bool switched = false;

        private void Reset()
        {
            // Если добавляете скрипт в редакторе — попытаемся автонайти зависимости
            if (!timer) timer = FindFirstObjectByType<GameTimer>();
            if (!clipList) clipList = FindFirstObjectByType<ClipList>();
            if (!preLoopSource) preLoopSource = gameObject.AddComponent<AudioSource>();
        }

        private void Awake()
        {
            if (!timer) Debug.LogWarning("[MusicOnTimer] Не назначен GameTimer");
            if (!clipList) Debug.LogWarning("[MusicOnTimer] Не назначен ClipList");
            if (!preLoopSource) Debug.LogWarning("[MusicOnTimer] Не назначен preLoopSource");

            // Готовим луп до старта таймера
            if (preLoopSource)
            {
                preLoopSource.playOnAwake = false;
                preLoopSource.loop = true;
            }
        }

        private void OnEnable()
        {
            if (timer) timer.OnTimerStart.AddListener(OnTimerStarted);
        }

        private void OnDisable()
        {
            if (timer) timer.OnTimerStart.RemoveListener(OnTimerStarted);
        }

        private void Start()
        {
            // Очень важно: чтобы очередь НЕ стартовала раньше времени
            if (clipList) clipList.playOnStart = false;

            if (playPreLoopOnStart && preLoopSource && preLoopClip)
            {
                preLoopSource.clip = preLoopClip;
                preLoopSource.Play();
            }
        }

        // Коллбек старта таймера — переключаем музыку
        private void OnTimerStarted()
        {
            if (switched) return;
            switched = true;

            // 1) Остановить фоновый луп
            if (preLoopSource && preLoopSource.isPlaying)
                preLoopSource.Stop();

            // 2) Запустить очередь из ClipList
            if (clipList)
                clipList.StartQueue(true);
            else
                Debug.LogWarning("[MusicOnTimer] ClipList не назначен — нечего запускать.");
        }
    }
}