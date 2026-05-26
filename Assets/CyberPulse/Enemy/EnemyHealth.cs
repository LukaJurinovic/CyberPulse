using System;
using UnityEngine;
using CyberPulse.Combat;
using CyberPulse.Systems;

namespace CyberPulse.Enemy
{
    /// <summary>Tracks enemy hit points, implements IDamageable, fires OnDeath when depleted.</summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private int _maxHealth = 50;
        [SerializeField] private ParticleSystem _hitVFX;
        [SerializeField] private EnemyDeathShards _deathShards;

        private int _currentHealth;
        private bool _isDead;

        public bool IsDead       => _isDead;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth     => _maxHealth;

        /// <summary>Fires once when health first reaches zero.</summary>
        public event Action OnDeath;

        /// <summary>Fires each time damage is applied; parameter is the damage amount.</summary>
        public event Action<int> OnDamageTaken;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        private void Start()
        {
            TraceMeter.RegisterEnemy(this);
        }

        /// <summary>Subtract amount from health. Fires OnDamageTaken, then OnDeath if health reaches zero.</summary>
        public void TakeDamage(int amount)
        {
            if (_isDead) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            OnDamageTaken?.Invoke(amount);
            _hitVFX?.Play();

            if (_currentHealth == 0)
                Die();
        }

        private void Die()
        {
            _isDead = true;
            OnDeath?.Invoke();
            _deathShards?.Explode();
            Destroy(gameObject, 2f);
        }
    }
}
