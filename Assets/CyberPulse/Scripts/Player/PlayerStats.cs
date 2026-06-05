using System;
using UnityEngine;

namespace CyberPulse.Player
{

    public class PlayerStats : MonoBehaviour, CyberPulse.Combat.IDamageable
    {
        [SerializeField] private int _maxHealth = 100;

        private int _currentHealth;

        public event Action<int> OnDamageTaken;

        public event Action<int> OnHealed;
        public event Action OnDeath;

        public int CurrentHealth => _currentHealth;

        public int MaxHealth => _maxHealth;

        public bool IsDead => _currentHealth <= 0;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (IsDead) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            OnDamageTaken?.Invoke(amount);

            if (_currentHealth == 0)
                OnDeath?.Invoke();
        }

        public void Heal(int amount)
        {
            if (IsDead) return;

            int before = _currentHealth;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            int actual = _currentHealth - before;

            if (actual > 0)
                OnHealed?.Invoke(actual);
        }
    }
}
