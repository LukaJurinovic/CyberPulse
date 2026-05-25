using System;
using UnityEngine;
using UnityEngine.AI;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// State machine driving enemy locomotion: Idle → Patrol → Chase → Attack → Dead.
    /// Subscribes to EnemySensor events for player detection and EnemyHealth.OnDeath for death.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemySensor))]
    [RequireComponent(typeof(EnemyAttack))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyController : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Attack, Dead }

        [Header("Patrol")]
        [SerializeField] private Transform[] _patrolPoints;
        [SerializeField] private float _patrolSpeed = 2.5f;
        [SerializeField] private float _patrolWaitTime = 2f;

        [Header("Chase")]
        [SerializeField] private float _chaseSpeed = 5f;

        [Header("Attack")]
        [SerializeField] private float _attackRange = 2f;

        private NavMeshAgent _agent;
        private EnemySensor _sensor;
        private EnemyAttack _attack;
        private EnemyHealth _health;

        private State _state = State.Idle;
        private Transform _target;
        private int _patrolIndex;
        private float _waitTimer;

        /// <summary>Current state of the enemy state machine.</summary>
        public State CurrentState => _state;

        /// <summary>Fires whenever the state changes. Parameter is the new state.</summary>
        public event Action<State> OnStateChanged;

        private void Awake()
        {
            _agent  = GetComponent<NavMeshAgent>();
            _sensor = GetComponent<EnemySensor>();
            _attack = GetComponent<EnemyAttack>();
            _health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _sensor.OnPlayerSpotted += HandlePlayerSpotted;
            _sensor.OnPlayerLost    += HandlePlayerLost;
            _health.OnDeath         += HandleDeath;
        }

        private void OnDisable()
        {
            _sensor.OnPlayerSpotted -= HandlePlayerSpotted;
            _sensor.OnPlayerLost    -= HandlePlayerLost;
            _health.OnDeath         -= HandleDeath;
        }

        private void Start() => BeginPatrol();

        private void Update()
        {
            switch (_state)
            {
                case State.Idle:   UpdateIdle();   break;
                case State.Patrol: UpdatePatrol(); break;
                case State.Chase:  UpdateChase();  break;
                case State.Attack: UpdateAttack(); break;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // State updates
        // ──────────────────────────────────────────────────────────────────────

        private void UpdateIdle()
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f) BeginPatrol();
        }

        private void UpdatePatrol()
        {
            if (!_agent.isOnNavMesh) return;
            if (_agent.pathPending) return;
            if (!_agent.hasPath) return;
            if (_agent.remainingDistance > _agent.stoppingDistance) return;

            _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
            _waitTimer = _patrolWaitTime;
            SetState(State.Idle);
        }

        private void UpdateChase()
        {
            if (_target == null) { BeginPatrol(); return; }

            if (Vector3.Distance(transform.position, _target.position) <= _attackRange)
            {
                _agent.ResetPath();
                SetState(State.Attack);
            }
            else if (_agent.isOnNavMesh)
            {
                _agent.SetDestination(_target.position);
            }
        }

        private void UpdateAttack()
        {
            if (_target == null) { BeginPatrol(); return; }

            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist > _attackRange * 1.25f)
            {
                _agent.speed = _chaseSpeed;
                SetState(State.Chase);
                return;
            }

            Vector3 dir = _target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), Time.deltaTime * 8f);

            _attack.TryAttack();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private void BeginPatrol()
        {
            if (_patrolPoints == null || _patrolPoints.Length == 0 || !_agent.isOnNavMesh)
            {
                _waitTimer = _patrolWaitTime;
                SetState(State.Idle);
                return;
            }
            _agent.speed = _patrolSpeed;
            _agent.SetDestination(_patrolPoints[_patrolIndex].position);
            SetState(State.Patrol);
        }

        private void SetState(State next)
        {
            if (_state == next) return;
            _state = next;
            OnStateChanged?.Invoke(next);
        }

        private void HandlePlayerSpotted(Transform player)
        {
            _target = player;
            _agent.speed = _chaseSpeed;
            SetState(State.Chase);
        }

        private void HandlePlayerLost()
        {
            _target = null;
            BeginPatrol();
        }

        private void HandleDeath()
        {
            SetState(State.Dead);
            _agent.enabled = false;
        }
    }
}
