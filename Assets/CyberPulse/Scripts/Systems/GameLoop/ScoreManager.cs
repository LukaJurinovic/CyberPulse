using System;
using UnityEngine;
using CyberPulse.Enemy;
using CyberPulse.Weapons;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Tracks score and combo multiplier.
    ///
    /// Combo rules:
    ///   • Kill with same weapon as last kill    → +1 combo
    ///   • Kill with different weapon (hot-swap) → +2 combo (variety bonus)
    ///   • Combo resets after _comboResetTime seconds without a kill
    ///
    /// Score milestones (every _scoreMilestone pts) fire OnMilestoneReached for TraceMeter drain.
    /// Weapon specials call AddSpecialScore() — score is added and combo timer refreshed.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField] private int   _baseKillScore    = 100;
        [SerializeField] private float _comboResetTime   = 5f;   // extended from 3s
        [SerializeField] private int   _maxCombo         = 8;
        [SerializeField] private int   _scoreMilestone   = 500;
        [SerializeField] private int   _specialScore     = 50;   // score per special activation

        public int   Score         { get; private set; }
        public int   ComboCount    { get; private set; }
        public float ComboTimer    { get; private set; }

        public event Action<int, int> OnScoreChanged;      // (newScore, pointsAdded)
        public event Action<int>      OnComboChanged;      // (newCombo)
        public event Action<int>      OnMilestoneReached;  // (milestoneNumber) — fires each time score crosses next milestone

        private int _nextMilestone;
        private int _lastKillWeaponIndex = -1;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _nextMilestone = _scoreMilestone;
        }

        private void Start()
        {
            EnemyHealth.OnAnyEnemyKilled += OnEnemyKilled;
        }

        private void OnDestroy()
        {
            EnemyHealth.OnAnyEnemyKilled -= OnEnemyKilled;
        }

        private void Update()
        {
            if (ComboCount > 0)
            {
                ComboTimer -= Time.deltaTime;
                if (ComboTimer <= 0f)
                    ResetCombo();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called when the player shoots down a homing missile.
        /// Adds half-kill score scaled by combo and refreshes the combo window without incrementing count.
        /// </summary>
        public void AddInterceptScore()
        {
            int points = (_baseKillScore / 2) * Mathf.Max(1, ComboCount);
            AddPoints(points);
            if (ComboCount > 0) ComboTimer = _comboResetTime;   // keep streak alive
        }

        /// <summary>Called by SyncGauge when a weapon special fires. Adds score and refreshes combo.</summary>
        public void AddSpecialScore()
        {
            if (ComboCount == 0) return;  // special only scores while in a combo
            int points = _specialScore * ComboCount;
            AddPoints(points);
            ComboTimer = _comboResetTime;  // refresh combo window
        }

        // ── Kill scoring ──────────────────────────────────────────────────────

        private void OnEnemyKilled()
        {
            int currentWeapon = WeaponBase.LastFiredWeaponSlotIndex;
            int comboIncrement = (currentWeapon != _lastKillWeaponIndex && _lastKillWeaponIndex >= 0) ? 2 : 1;
            _lastKillWeaponIndex = currentWeapon;

            ComboCount = Mathf.Min(ComboCount + comboIncrement, _maxCombo);
            ComboTimer = _comboResetTime;
            OnComboChanged?.Invoke(ComboCount);

            int points = _baseKillScore * ComboCount;
            AddPoints(points);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private void AddPoints(int points)
        {
            Score += points;
            OnScoreChanged?.Invoke(Score, points);
            CheckMilestones();
        }

        private void CheckMilestones()
        {
            while (Score >= _nextMilestone)
            {
                OnMilestoneReached?.Invoke(_nextMilestone);
                _nextMilestone += _scoreMilestone;
            }
        }

        private void ResetCombo()
        {
            ComboCount = 0;
            ComboTimer = 0f;
            OnComboChanged?.Invoke(0);
        }
    }
}
