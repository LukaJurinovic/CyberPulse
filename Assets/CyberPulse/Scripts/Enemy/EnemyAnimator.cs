using UnityEngine;
using UnityEngine.AI;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Optional bridge between EnemyController state and an Animator.
    /// Add to enemies that have a skinned rig. Safe to omit on placeholder capsule enemies.
    /// Drives parameters: Speed (float), IsDead (bool), Attack (trigger).
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int DeadHash   = Animator.StringToHash("IsDead");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private Animator _animator;
        private NavMeshAgent _agent;
        private EnemyController _controller;

        private void Awake()
        {
            _animator   = GetComponent<Animator>();
            _agent      = GetComponent<NavMeshAgent>();
            _controller = GetComponent<EnemyController>();
        }

        private void OnEnable()  => _controller.OnStateChanged += OnStateChanged;
        private void OnDisable() => _controller.OnStateChanged -= OnStateChanged;

        private void Update()
        {
            if (_animator == null || _agent == null || !_agent.enabled) return;
            _animator.SetFloat(SpeedHash, _agent.velocity.magnitude);
        }

        private void OnStateChanged(EnemyController.State state)
        {
            if (_animator == null) return;
            _animator.SetBool(DeadHash, state == EnemyController.State.Dead);
            if (state == EnemyController.State.Attack)
                _animator.SetTrigger(AttackHash);
        }
    }
}
