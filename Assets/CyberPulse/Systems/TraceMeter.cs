using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CyberPulse.Enemy;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Central tension driver. Fills while enemies are alive, drains on kills.
    /// Drives AudioMixer blend at 50%, PP Volume at 80%, fail state at 100%.
    /// Enemies self-register via <see cref="RegisterEnemy"/>.
    /// </summary>
    public class TraceMeter : MonoBehaviour
    {
        public static TraceMeter Instance { get; private set; }

        [Header("Rates")]
        [SerializeField] private float _fillPerEnemyPerSecond = 2f;   // 4 enemies = 8/sec → ~12s to 100%
        [SerializeField] private float _drainOnKill           = 20f;
        [SerializeField] private float _passiveDrainPerSecond = 1f;

        [Header("Audio — optional")]
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private string     _actionVolumeParam = "ActionVolume";

        [Header("Post-Processing — optional")]
        [SerializeField] private Volume _criticalVolume;
        [SerializeField] private float  _criticalVolumeTarget = 0.8f;
        [SerializeField] private float  _volumeLerpSpeed      = 3f;

        [Header("UI — optional")]
        [SerializeField] private Image _fillBar;

        // Thresholds match plan: Alert 50%, Critical 80%, Fail 100%.
        private const float AlertThreshold    = 50f;
        private const float CriticalThreshold = 80f;

        private float _value;
        private bool  _alertActive;
        private bool  _criticalActive;
        private bool  _failFired;

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
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        // If enemies were killed before Purge started, check immediately on phase entry.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Purge && _liveEnemies.Count == 0)
                GameManager.Instance?.SetPhase(GamePhase.Extract);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_liveEnemies.Count > 0)
                _value += _fillPerEnemyPerSecond * _liveEnemies.Count * dt;
            else
                _value -= _passiveDrainPerSecond * dt;

            _value = Mathf.Clamp(_value, 0f, 100f);

            HandleThresholds();
            BlendCriticalVolume(dt);
            UpdateUI();
        }

        // ── Enemy registration (called by EnemyHealth) ────────────────────────

        public static void RegisterEnemy(EnemyHealth enemy)
        {
            if (Instance != null)
                Instance.AddEnemy(enemy);
            else
                _pendingEnemies.Add(enemy); // TraceMeter not awake yet — queued
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
            if (_liveEnemies.Count == 0 && GameManager.Instance?.CurrentPhase == GamePhase.Purge)
                GameManager.Instance.SetPhase(GamePhase.Extract);
        }

        // ── Threshold reactions ───────────────────────────────────────────────

        private void HandleThresholds()
        {
            // Alert: music blend
            bool shouldAlert = _value >= AlertThreshold;
            if (shouldAlert != _alertActive)
            {
                _alertActive = shouldAlert;
                SetActionMusicActive(_alertActive);
            }

            // Critical: PP volume weight driven in BlendCriticalVolume()
            _criticalActive = _value >= CriticalThreshold;

            // Fail
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

        // -80 dB = effectively silent in an AudioMixer; 0 dB = full level.
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
