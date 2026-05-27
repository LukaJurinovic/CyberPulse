using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CyberPulse.Enemy;
using CyberPulse.Player;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Unified survival/health mechanic. Replaces player HP entirely.
    ///
    /// Fill sources:
    ///   • Passive:       +_passiveFillPerSecond always (paused for _beatStopBeats after any on-beat shot)
    ///   • Enemy hit:     +_tracePerHit per hit on the player
    ///
    /// Drain sources:
    ///   • Kill:          -_drainOnKill per enemy killed
    ///   • Score milestone (every 500 pts): -_drainPerMilestone
    ///
    /// Thresholds: Alert 50%, Critical 80%, Fail 100%.
    /// </summary>
    public class TraceMeter : MonoBehaviour
    {
        public static TraceMeter Instance { get; private set; }

        [Header("Passive Fill")]
        [SerializeField] private float _passiveFillPerSecond = 1.5f;
        [SerializeField] private int   _beatStopBeats        = 4;    // on-beat shot pauses fill for N beats
        [SerializeField] private float _fillMultiplierMax    = 3f;   // fill rate multiplier at end of song (1× at start → this at end)

        [Header("Music Source (for time-scaling)")]
        [SerializeField] private AudioSource _musicSource;

        [Header("Hit / Kill")]
        [SerializeField] private float _tracePerHit  = 8f;    // % added per enemy hit on player
        [SerializeField] private float _drainOnKill  = 12f;   // % drained per kill

        [Header("Score Milestone Drain")]
        [SerializeField] private float _drainPerMilestone = 3f;

        [Header("Audio — optional")]
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private string     _actionVolumeParam = "ActionVolume";

        [Header("Post-Processing — optional")]
        [SerializeField] private Volume _criticalVolume;
        [SerializeField] private float  _criticalVolumeTarget = 0.8f;
        [SerializeField] private float  _volumeLerpSpeed      = 3f;

        [Header("UI — optional")]
        [SerializeField] private Image _fillBar;

        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;

        private const float AlertThreshold    = 50f;
        private const float CriticalThreshold = 80f;

        private float _value;
        private bool  _alertActive;
        private bool  _criticalActive;
        private bool  _failFired;
        private int   _beatStopCountdown;   // beats remaining where passive fill is paused

        private static readonly List<EnemyHealth> _pendingEnemies = new();
        private readonly HashSet<EnemyHealth> _liveEnemies = new();

        // ── Public read-only state ────────────────────────────────────────────

        public float Value      => _value;
        public float Normalized => _value / 100f;
        public int   EnemyCount => _liveEnemies.Count;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            foreach (var e in _pendingEnemies)
                if (e != null) AddEnemy(e);
            _pendingEnemies.Clear();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged += OnPhaseChanged;
            if (BeatClock.Instance != null)
                BeatClock.Instance.OnBeat += OnBeat;
            if (BeatReactor.Instance != null)
                BeatReactor.Instance.OnBeatShot += OnBeatShot;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnMilestoneReached += OnMilestone;
            if (_playerStats != null)
                _playerStats.OnDamageTaken += OnPlayerHit;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged -= OnPhaseChanged;
            if (BeatClock.Instance != null)
                BeatClock.Instance.OnBeat -= OnBeat;
            if (BeatReactor.Instance != null)
                BeatReactor.Instance.OnBeatShot -= OnBeatShot;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.OnMilestoneReached -= OnMilestone;
            if (_playerStats != null)
                _playerStats.OnDamageTaken -= OnPlayerHit;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Purge && _liveEnemies.Count == 0)
                GameManager.Instance?.SetPhase(GamePhase.Extract);
        }

        private float SongProgress()
        {
            if (_musicSource == null || _musicSource.clip == null || _musicSource.clip.samples == 0)
                return 0f;
            return Mathf.Clamp01((float)_musicSource.timeSamples / _musicSource.clip.samples);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_beatStopCountdown <= 0)
            {
                float multiplier = Mathf.Lerp(1f, _fillMultiplierMax, SongProgress());
                _value += _passiveFillPerSecond * multiplier * dt;
            }

            _value = Mathf.Clamp(_value, 0f, 100f);

            HandleThresholds();
            BlendCriticalVolume(dt);
            UpdateUI();
        }

        // ── Public drain API ─────────────────────────────────────────────────

        /// <summary>
        /// Directly drain the trace by <paramref name="percent"/> points (0-100 scale).
        /// Called by missile intercepts and any other non-kill drain sources.
        /// </summary>
        public void DrainDirect(float percent)
        {
            _value = Mathf.Max(0f, _value - percent);
        }

        // ── Enemy registration (called by EnemyHealth) ────────────────────────

        public static void RegisterEnemy(EnemyHealth enemy)
        {
            if (Instance != null) Instance.AddEnemy(enemy);
            else _pendingEnemies.Add(enemy);
        }

        private void AddEnemy(EnemyHealth enemy)
        {
            if (!_liveEnemies.Add(enemy)) return;
            enemy.OnDeath += () => Instance?.OnEnemyKilled(enemy);
        }

        private void OnEnemyKilled(EnemyHealth enemy)
        {
            _liveEnemies.Remove(enemy);
            _value = Mathf.Max(0f, _value - _drainOnKill);
            if (_liveEnemies.Count == 0)
            {
                TimeManager.Instance?.TriggerKillCam();
                if (GameManager.Instance?.CurrentPhase == GamePhase.Purge)
                    GameManager.Instance.SetPhase(GamePhase.Extract);
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnPlayerHit(int _)
        {
            _value = Mathf.Clamp(_value + _tracePerHit, 0f, 100f);
        }

        private void OnBeat()
        {
            if (_beatStopCountdown > 0)
                _beatStopCountdown--;
        }

        private void OnBeatShot()
        {
            _beatStopCountdown = _beatStopBeats;
        }

        private void OnMilestone(int _)
        {
            _value = Mathf.Max(0f, _value - _drainPerMilestone);
        }

        // ── Threshold reactions ───────────────────────────────────────────────

        private void HandleThresholds()
        {
            bool shouldAlert = _value >= AlertThreshold;
            if (shouldAlert != _alertActive)
            {
                _alertActive = shouldAlert;
                SetActionMusicActive(_alertActive);
            }

            _criticalActive = _value >= CriticalThreshold;

            if (!_failFired && _value >= 100f)
            {
                _failFired = true;
                GameManager.Instance?.TriggerFailState();
            }
        }

        private void BlendCriticalVolume(float dt)
        {
            if (_criticalVolume == null || !_criticalVolume) return;
            float target = _criticalActive ? _criticalVolumeTarget : 0f;
            _criticalVolume.weight = Mathf.Lerp(_criticalVolume.weight, target, dt * _volumeLerpSpeed);
        }

        private void SetActionMusicActive(bool active)
        {
            if (_mixer != null)
                _mixer.SetFloat(_actionVolumeParam, active ? 0f : -80f);
        }

        private void UpdateUI()
        {
            if (_fillBar != null)
                _fillBar.fillAmount = Normalized;
        }
    }
}
