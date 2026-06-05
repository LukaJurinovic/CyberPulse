using System;
using UnityEngine;
using CyberPulse.Enemy;
using CyberPulse.Player;
using CyberPulse.Weapons;

namespace CyberPulse.Systems
{
    public class BeatReactor : MonoBehaviour
    {
        public static BeatReactor Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerController _controller;
        [SerializeField] private DashAbility      _dash;
        [SerializeField] private PlayerStats      _playerStats;
        [SerializeField] private WeaponHolder      _weaponHolder;

        [Header("Off-Rhythm Penalty")]
        [SerializeField] private float _offRhythmDelay      = 2f;
        [SerializeField] private float _offRhythmMoveScale  = 0.7f;
        [SerializeField] private float _offRhythmFireScale  = 0.6f;

        [Header("SYNC Values")]
        [SerializeField] private float _syncPerBeatShot  = 8f;
        [SerializeField] private float _syncPerBeatKill  = 25f;
        [SerializeField] private float _syncPerBeatDash  = 5f;
        [SerializeField] private float _syncLostOnDamage = 10f;

        [Header("Ammo Reward")]
        [Tooltip("Reserve ammo refunded to the active weapon for a kill landed on the beat.")]
        [SerializeField] private int _ammoPerBeatKill = 6;

        public bool  IsOffRhythm    { get; private set; }

        /// <summary>Fires every frame while in off-rhythm state. Arg = deltaTime (unscaled).</summary>
        public event Action<float> OnOffRhythmTick;

        /// <summary>Fires on a kill that happened within the beat window.</summary>
        public event Action OnBeatKill;

        /// <summary>Fires whenever a weapon shot lands within the beat window. TraceMeter uses this to pause fill.</summary>
        public event Action OnBeatShot;

        private float _timeSinceLastOnBeatAction;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            WeaponBase.OnAnyWeaponFired += HandleWeaponFired;

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
                _weaponHolder?.ActiveWeapon?.AddReserveAmmo(_ammoPerBeatKill);
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

            IsOffRhythm = false;
            if (_controller != null)
                _controller.RhythmMultiplier = 1f;
        }
    }
}
