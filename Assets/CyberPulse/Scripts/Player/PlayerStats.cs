using System;
using UnityEngine;

namespace CyberPulse.Player
{
    /// <summary>
    /// Stores and manages player health. Pure data + events — no UI code.
    /// Subscribe to the events from a separate HUD component.
    /// </summary>
    public class PlayerStats : MonoBehaviour, CyberPulse.Combat.IDamageable
    {
        [SerializeField] private int _maxHealth = 100;

        private int _currentHealth;

        // ──────────────────────────────────────────────────────────────────────
        // Events
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Fires when damage is applied. Parameter is the damage amount (positive).</summary>
        public event Action<int> OnDamageTaken;

        /// <summary>Fires when health is restored. Parameter is the actual amount healed.</summary>
        public event Action<int> OnHealed;

        /// <summary>Fires once when <see cref="CurrentHealth"/> first reaches zero.</summary>
        public event Action OnDeath;

        // ──────────────────────────────────────────────────────────────────────
        // Properties
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Current health, always in the range [0, <see cref="MaxHealth"/>].</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>The maximum health value configured in the Inspector.</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>True when <see cref="CurrentHealth"/> is zero.</summary>
        public bool IsDead => _currentHealth <= 0;

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Subtracts <paramref name="amount"/> from current health.
        /// Fires <see cref="OnDamageTaken"/> and, if health reaches zero, <see cref="OnDeath"/>.
        /// Does nothing if the player is already dead.
        /// </summary>
        /// <param name="amount">Positive damage value.</param>
        public void TakeDamage(int amount)
        {
            if (IsDead) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            OnDamageTaken?.Invoke(amount);

            if (_currentHealth == 0)
                OnDeath?.Invoke();
        }

        /// <summary>
        /// Restores health clamped to <see cref="MaxHealth"/>.
        /// Fires <see cref="OnHealed"/> with the actual restored amount.
        /// Does nothing if the player is dead.
        /// </summary>
        /// <param name="amount">Positive heal value.</param>
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
