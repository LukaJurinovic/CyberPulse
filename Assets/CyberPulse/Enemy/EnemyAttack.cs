using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Melee-range attack. EnemyController calls TryAttack() each frame while in Attack state.
    /// Uses OverlapSphere on the Player layer and delivers damage through IDamageable.
    /// </summary>
    public class EnemyAttack : MonoBehaviour
    {
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _cooldown = 1.5f;
        [SerializeField] private float _range = 2.5f;
        [SerializeField] private LayerMask _playerLayer;

        private float _nextAttackTime;

        /// <summary>Attempt a melee attack. No-ops if still on cooldown.</summary>
        public void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + _cooldown;

            Collider[] hits = Physics.OverlapSphere(transform.position, _range, _playerLayer);
            foreach (Collider hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();
                damageable?.TakeDamage(_damage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _range);
        }
    }
}
