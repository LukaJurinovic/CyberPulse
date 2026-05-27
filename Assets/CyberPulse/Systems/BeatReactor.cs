using System;
using UnityEngine;
using CyberPulse.Enemy;
using CyberPulse.Player;
using CyberPulse.Weapons;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Bridges BeatClock to all rhythm-driven gameplay rules:
    ///
    ///   • On-beat shot:   SYNC +8  (damage bonus is applied inside HitscanWeapon)
    ///   • On-beat kill:   SYNC +25
    ///   • On-beat dash:   SYNC +5, cooldown reset (P1 — ready for wiring)
    ///   • Damage taken:   SYNC -10
    ///   • Off-rhythm 2s:  movement speed ×0.7, drain signal → SyncGauge
    ///
    /// References wired by PlayableLevelBuilder.
    /// </summary>
    public class BeatReactor : MonoBehaviour
    {
        public static BeatReactor Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerController _controller;
        [SerializeField] private DashAbility      _dash;
        [SerializeField] private PlayerStats      _playerStats;

        [Header("Off-Rhythm Penalty")]
        [SerializeField] private float _offRhythmDelay      = 2f;   // seconds before penalty kicks in
        [SerializeField] private float _offRhythmMoveScale  = 0.7f; // fraction of normal move speed
        [SerializeField] private float _offRhythmFireScale  = 0.6f; // fraction of normal fire rate

        [Header("SYNC Values")]
        [SerializeField] private float _syncPerBeatShot  = 8f;
        [SerializeField] private float _syncPerBeatKill  = 25f;
        [SerializeField] private float _syncPerBeatDash  = 5f;
        [SerializeField] private float _syncLostOnDamage = 10f;

        // ── Public state ──────────────────────────────────────────────────────

        public bool  IsOffRhythm    { get; private set; }

        /// <summary>Fires every frame while in off-rhythm state. Arg = deltaTime (unscaled).</summary>
        public event Action<float> OnOffRhythmTick;

        /// <summary>Fires on a kill that happened within the beat window.</summary>
        public event Action OnBeatKill;

        /// <summary>Fires whenever a weapon shot lands within the beat window. TraceMeter uses this to pause fill.</summary>
        public event Action OnBeatShot;

        // ── Private ───────────────────────────────────────────────────────────

        private float _timeSinceLastOnBeatAction;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Static weapon fire event — fires on any shot regardless of weapon type.
            WeaponBase.OnAnyWeaponFired += HandleWeaponFired;

            // Enemy kill event — fires on any enemy death.
            EnemyHealth.OnAnyEnemyKilled += HandleEnemyKilled;

            if (_dash != null)
                _dash.OnDashPerformed += HandleDash;

            if (_playerStats != null)
                _playerStats.OnDamageTaken += HandleDamageTaken;
        }

        private void OnDestroy()
        {
            WeaponBase.RhythmFireMultiplier = 1f;
            WeaponBase.OnAnyWeaponFired -= HandleWeaponFired;
            EnemyHealth.OnAnyEnemyKilled -= HandleEnemyKilled;

            if (_dash != null)
                _dash.OnDashPerformed -= HandleDash;

            if (_playerStats != null)
                _playerStats.OnDamageTaken -= HandleDamageTaken;
        }

        private void Update()
        {
            _timeSinceLastOnBeatAction += Time.unscaledDeltaTime;

            bool wasOffRhythm = IsOffRhythm;
            IsOffRhythm = _timeSinceLastOnBeatAction >= _offRhythmDelay;

            if (IsOffRhythm)
            {
                OnOffRhythmTick?.Invoke(Time.unscaledDeltaTime);
                if (_controller != null) _controller.RhythmMultiplier = _offRhythmMoveScale;
                WeaponBase.RhythmFireMultiplier = _offRhythmFireScale;
            }
            else if (wasOffRhythm)
            {
                if (_controller != null) _controller.RhythmMultiplier = 1f;
                WeaponBase.RhythmFireMultiplier = 1f;
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleWeaponFired()
        {
            if (BeatClock.Instance != null && BeatClock.Instance.IsOnBeat)
            {
                RecordOnBeatAction();
                SyncGauge.Instance?.Add(_syncPerBeatShot);
                OnBeatShot?.Invoke();
            }
        }

        private void HandleEnemyKilled()
        {
            if (BeatClock.Instance != null && BeatClock.Instance.IsOnBeat)
            {
                RecordOnBeatAction();
                SyncGauge.Instance?.Add(_syncPerBeatKill);
                OnBeatKill?.Invoke();
            }
        }

        private void HandleDash()
        {
            if (BeatClock.Instance != null && BeatClock.Instance.IsOnBeat)
            {
                RecordOnBeatAction();
                SyncGauge.Instance?.Add(_syncPerBeatDash);
                _dash?.ResetCooldown();
            }
        }

        private void HandleDamageTaken(int _)
        {
            SyncGauge.Instance?.Subtract(_syncLostOnDamage);
        }

        private void RecordOnBeatAction()
        {
            _timeSinceLastOnBeatAction = 0f;

            // Ensure movement is restored immediately on any on-beat action,
            // even if the off-rhythm timer hasn't fully expired yet.
            IsOffRhythm = false;
            if (_controller != null)
                _controller.RhythmMultiplier = 1f;
        }
    }
}
