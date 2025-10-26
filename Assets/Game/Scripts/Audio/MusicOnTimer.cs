using UnityEngine;
using System.Collections;

namespace Game.Scripts.Audio
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

        [Tooltip("Громкость для preLoopClip (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float preLoopVolume = 1f;

        [Tooltip("Автоматически запустить фоновый луп при старте сцены.")]
        [SerializeField] private bool playPreLoopOnStart = true;

        [Header("Настройки бесшовного переключения")]
        [Tooltip("Предварительно загружать музыку из треклиста")]
        [SerializeField] private bool preloadClipList = true;


        private bool switched = false;

        private AudioSource nextTrackSource;
        private bool isNextTrackReady = false;

        private void Reset()
        {
            // Если добавляете скрипт в редакторе — попытаемся автонайти зависимости
            if (!timer) timer = FindFirstObjectByType<GameTimer>();
            if (!clipList) clipList = FindFirstObjectByType<ClipList>();
            if (!preLoopSource) preLoopSource = gameObject.AddComponent<AudioSource>();
            if (!nextTrackSource) nextTrackSource = gameObject.AddComponent<AudioSource>();
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
                preLoopSource.volume = preLoopVolume;
            }

            if (!nextTrackSource) nextTrackSource = gameObject.AddComponent<AudioSource>();
            nextTrackSource.playOnAwake = false;
            nextTrackSource.volume = 0f;

            if (preloadClipList && clipList)
            {
                PreloadNextClip();
            }
        }

        private void PreloadNextClip()
        {
            if (clipList)
            {
                var nextItem = clipList.GetUpcomingItem();
                if (nextItem != null && nextItem.clip)
                {
                    nextTrackSource.clip = nextItem.clip;
                    nextTrackSource.volume = nextItem.volume;
                    isNextTrackReady = true;
                }
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
                preLoopSource.volume = preLoopVolume;
                preLoopSource.Play();
            }
        }

        // Коллбек старта таймера — переключаем музыку
        private void OnTimerStarted()
        {
            if (switched) return;
            switched = true;

            // Если preLoopSource играет, ждём окончания текущего цикла
            if (preLoopSource && preLoopSource.isPlaying && preLoopClip)
            {
                // Отключаем зацикливание, чтобы текущий цикл доиграл до конца
                preLoopSource.loop = false;

                // Запускаем корутину ожидания
                StartCoroutine(WaitForPreLoopToFinish());
            }
            else
            {
                // Если не играет или нет клипа - сразу запускаем треклист
                StartClipList();
            }
        }

        private IEnumerator WaitForPreLoopToFinish()
        {
            // Ждём пока preLoopSource доиграет текущий цикл
            while (preLoopSource && preLoopSource.isPlaying)
            {
                yield return null;
            }

            // Когда закончил играть - запускаем треклист
            StartClipList();
        }

        private void StartClipList()
        {
            if (preLoopSource && preLoopSource.isPlaying)
            {
                preLoopSource.Stop();
            }

            if (isNextTrackReady)
            {
                nextTrackSource.Play();
                clipList.NotifyFirstTrackStarted();
            }
            else
            {
                if (clipList)
                    clipList.StartQueue(true);
                else
                    Debug.LogWarning("[MusicOnTimer] ClipList не назначен — нечего запускать.");
            }
        }
    }
}