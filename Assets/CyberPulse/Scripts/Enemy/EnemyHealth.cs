using System;
using UnityEngine;
using CyberPulse.Combat;
using CyberPulse.Systems;

namespace CyberPulse.Enemy
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private int _maxHealth = 50;
        [SerializeField] private ParticleSystem _hitVFX;
        [SerializeField] private EnemyDeathShards _deathShards;

        [Header("Drops")]
        [SerializeField, Range(0f, 1f)] private float _ammoDropChance = 0.35f;
        [SerializeField] private int _ammoDropAmount = 12;

        private int _currentHealth;
        private bool _isDead;

        public bool IsDead       => _isDead;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth     => _maxHealth;

        public event Action OnDeath;

        public event Action<int> OnDamageTaken;

        public static event Action OnAnyEnemyKilled;

        /// <summary>Override max health before Awake runs (call on inactive GO). Safe to call before SetActive(true).</summary>
        public void InitHealth(int hp) { _maxHealth = hp; }

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        private void Start()
        {
            TraceMeter.RegisterEnemy(this);
        }

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

            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;

            OnDeath?.Invoke();
            OnAnyEnemyKilled?.Invoke();
            _deathShards?.Explode();

            if (_ammoDropAmount > 0 && UnityEngine.Random.value < _ammoDropChance)
                CyberPulse.World.AmmoPickup.Spawn(transform.position, _ammoDropAmount);

            Destroy(gameObject, 2f);
        }
    }
}
