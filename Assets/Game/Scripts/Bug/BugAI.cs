using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Scripts.Bug
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class BugAI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Параметры перемещения")]
        [Tooltip("Радиус блуждания вокруг текущей позиции")]
        public float wanderRadius = 10f;

        [Tooltip("Как часто выбирать новую точку (сек)")]
        public float repathInterval = 1.5f;

        [Header("Надёжность NavMesh")]
        [Tooltip("Радиус поиска ближайшей точки NavMesh при возврате из инспекции/включении")]
        public float reattachRadius = 2f;

        [Header("Скорость")]
        [Tooltip("Обычная скорость движения")]
        public float normalSpeed = 3.5f;

        [Tooltip("Скорость на NavMesh Link")]
        public float linkSpeed = 10f;

        [Tooltip("Скорость поворота (градусы в секунду)")]
        public float turnSpeed = 360f;

        [Header("Access Control")]
        [Tooltip("Can this bug be inspected regardless of zone restrictions?")]
        [SerializeField] private bool alwaysAccessible = false;

        [Header("Анимация")]
        [SerializeField] private Animator _anim;
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string isMovingParam = "IsMoving";
        [SerializeField, Tooltip("Порог, выше которого считаем, что движение есть")]
        private float movingThreshold = 0.05f;
        [SerializeField, Tooltip("Сглаживание Speed для Animator.SetFloat")]
        private float speedDamp = 0.1f;

        public Animator Anim
        {
            get => _anim;
            set => _anim = value;
        }

        private Vector3 _lastPos;
        private float _lastSpeed;

        #endregion

        #region State

        private NavMeshAgent _agent;

        private float _nextRepathTime;
        private bool _manuallyDisabled;

        private float _spawnTime;

        // --- ЗОНЫ И ДОСТУП ---
        private readonly HashSet<BugAccessZone> _zones = new HashSet<BugAccessZone>();
        private InspectableObject _inspectable; // целевой флаг canInspect будет управляться отсюда

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // Отключаем автоматическое вращение, чтобы контролировать его вручную
            _agent.updateRotation = false;
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            _lastPos = transform.position;
            _spawnTime = Time.time;

            _inspectable = GetComponent<InspectableObject>();
            if (_inspectable == null)
            {
                // Удален Debug.LogWarning для очистки дебаг сообщений
            }

            ValidateAnimatorParameters();
        }

        private void ValidateAnimatorParameters()
        {
            if (!_anim || !_anim.runtimeAnimatorController) return;

            bool hasSpeed = HasAnimatorParameter(speedParam, AnimatorControllerParameterType.Float);
            bool hasIsMoving = HasAnimatorParameter(isMovingParam, AnimatorControllerParameterType.Bool);
        }

        private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType paramType)
        {
            if (!_anim || !_anim.runtimeAnimatorController) return false;
            
            foreach (var param in _anim.parameters)
            {
                if (param.name == paramName && param.type == paramType)
                    return true;
            }
            return false;
        }

        private void Start()
        {
            _agent.speed = normalSpeed;
            CheckForAccessZone();
            RecomputeAndApplyCanInspect();
        }

        private void OnEnable()
        {
            EnsureAgentOnNavMesh();
        }

        private void Update()
        {
            if (!AgentReady() || _manuallyDisabled)
            {
                UpdateAnimator(0f);
                return;
            }

            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + repathInterval;
                PickNewRandomPoint();
            }

            // Устанавливаем скорость в зависимости от того, на NavMesh Link ли мы
            if (_agent.isOnOffMeshLink)
            {
                _agent.speed = linkSpeed;
            }
            else
            {
                _agent.speed = normalSpeed;
            }

            float speed = ComputeCurrentSpeed();
            UpdateAnimator(speed);

            // Ручной поворот к направлению движения
            if (_agent && _agent.enabled && !_manuallyDisabled && _agent.velocity.sqrMagnitude > 0.01f)
            {
                Vector3 moveDir = _agent.velocity.normalized;
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
        }

        #endregion

        #region Gameplay API

        public string GetBugType()
        {
            string n = gameObject.name;
            return n.Replace("(Clone)", "").Trim();
        }

        public float GetTimeSinceSpawn()
        {
            return Time.time - _spawnTime;
        }

        // ==== НОВОЕ: API для зон ====

        /// <summary>Жук вошёл в зону.</summary>
        public void RegisterAccessZone(BugAccessZone zone)
        {
            if (zone == null) return;
            if (_zones.Add(zone))
                RecomputeAndApplyCanInspect();
        }

        /// <summary>Жук вышел из зоны.</summary>
        public void UnregisterAccessZone(BugAccessZone zone)
        {
            if (zone == null) return;
            if (_zones.Remove(zone))
                RecomputeAndApplyCanInspect();
        }

        /// <summary>Зона сообщает: «моя доступность изменилась».</summary>
        public void NotifyZoneAccessibilityChanged()
        {
            RecomputeAndApplyCanInspect();
        }

        // Оставляем для ��братной совместимости (если где-то еще зовётся).
        // Ничего не устанавливаем напрямую — просто пересчитываем.
        public void SetAccessible(bool ignored) => RecomputeAndApplyCanInspect();

        public void DisableAI(bool disable)
        {
            if (disable) OnInspectStart();
            else OnInspectEnd();
        }

        private float ComputeCurrentSpeed()
        {
            float speed = 0f;

            // 1) Если есть валидный агент — -пробуем его velocity
            if (_agent && _agent.enabled && _agent.isOnNavMesh)
            {
                // Часто agent.velocity == 0 в момент старта или при остановке торможением
                float v = _agent.velocity.magnitude;

                // Если путь есть, но velocity≈0, используем desiredVelocity (частый кейс)
                if (v < 0.01f && (_agent.hasPath || !_agent.isStopped))
                    v = _agent.desiredVelocity.magnitude;

                speed = v;
            }

            // 2) Фолбэк: реальная скорость по дельте позиции (кейс root motion/ручного движения)
            if (speed < 0.01f)
            {
                Vector3 delta = transform.position - _lastPos;
                float dt = Mathf.Max(Time.deltaTime, 1e-5f);
                float posSpeed = delta.magnitude / dt;
                speed = Mathf.Max(speed, posSpeed);
            }

            _lastPos = transform.position;

            // Немного сглаживания вручную, чтобы избежать дрожания флага
            _lastSpeed = Mathf.Lerp(_lastSpeed, speed, 1f - Mathf.Exp(-Time.deltaTime / 0.05f));
            return _lastSpeed;
        }

        private void UpdateAnimator(float velocityMagnitude)
        {
            if (!_anim || !_anim.enabled || !_anim.runtimeAnimatorController) 
            {
                // Удален Debug.LogWarning для очистки дебаг сообщений
                return;
            }

            // Проверяем существование параметров перед установкой
            if (HasAnimatorParameter(speedParam, AnimatorControllerParameterType.Float))
            {
                _anim.SetFloat(speedParam, velocityMagnitude, speedDamp, Time.deltaTime);
            }

            bool moving = velocityMagnitude > movingThreshold;
            
            if (HasAnimatorParameter(isMovingParam, AnimatorControllerParameterType.Bool))
            {
                _anim.SetBool(isMovingParam, moving);
            }

            // Удален Debug.Log для производительности
        }

        public void OnInspectStart()
        {
            _manuallyDisabled = true;

            if (_agent)
            {
                if (_agent.enabled && _agent.isOnNavMesh)
                    _agent.isStopped = true;

                _agent.enabled = false;
            }

            UpdateAnimator(0f);
        }

        public void OnInspectEnd()
        {
            if (_agent)
            {
                AttachToNavMeshIfNeeded();
                _agent.enabled = true;
                if (!_agent.isOnNavMesh)
                    AttachToNavMeshIfNeeded();
                _agent.isStopped = false;
            }

            _manuallyDisabled = false;
            _nextRepathTime = Time.time + 0.1f;
        }

        #endregion

        #region Movement

        private void PickNewRandomPoint()
        {
            if (!AgentReady()) return;

            var origin = transform.position;
            var random = Random.insideUnitSphere * wanderRadius + origin;
            random.y = origin.y;

            if (NavMesh.SamplePosition(random, out var hit, wanderRadius, NavMesh.AllAreas))
                SetDestinationSafe(hit.position);
            else
                _nextRepathTime = Time.time + 0.5f;
        }

        private void SetDestinationSafe(Vector3 pos)
        {
            if (!AgentReady()) return;
            if (!_agent.SetDestination(pos))
                _nextRepathTime = Time.time + 0.3f;
            else
                _agent.isStopped = false;
        }

        private bool AgentReady() => _agent && _agent.enabled && _agent.isOnNavMesh;

        private void EnsureAgentOnNavMesh()
        {
            if (!_agent) return;
            if (_agent.enabled && !_agent.isOnNavMesh)
                AttachToNavMeshIfNeeded();
        }

        private void AttachToNavMeshIfNeeded()
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, Mathf.Max(0.25f, reattachRadius),
                    NavMesh.AllAreas))
            {
                if (!_agent.enabled) _agent.enabled = true;
                _agent.Warp(hit.position);
            }
        }

        #endregion

        #region Zones → CanInspect

        private void RecomputeAndApplyCanInspect()
        {
            // 1) агрегируем доступ по всем зонам (AND)
            bool zonesAllow = true;
            foreach (var z in _zones)
            {
                if (z == null) continue;
                if (!z.IsAccessible)
                {
                    zonesAllow = false;
                    break;
                }
            }

            // 2) учитываем alwaysAccessible
            bool finalCanInspect = alwaysAccessible || zonesAllow;

            // 3) толкаем в InspectableObject.canInspect
            if (_inspectable != null)
                _inspectable.SetInspectable(finalCanInspect);

            // (опционально можно включать/выключать подсветку/интеракт-коллайдеры тут же)
        }

        private void CheckForAccessZone()
        {
            // Если при старт�� уже стоим в нескольких зонах — зарегистрируйтесь во всех
            var zonesInScene = FindObjectsByType<BugAccessZone>(FindObjectsSortMode.None);
            var bugCollider = GetComponent<Collider>();

            foreach (var zone in zonesInScene)
            {
                var zoneCollider = zone ? zone.GetComponent<Collider>() : null;
                if (zoneCollider != null && bugCollider != null)
                {
                    if (zoneCollider.bounds.Intersects(bugCollider.bounds))
                        zone.RefreshBugs(); // это вызовет RegisterAccessZone(this) на нас
                }
            }
        }

        #endregion
    }
}

