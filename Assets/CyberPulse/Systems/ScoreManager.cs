using System;
using UnityEngine;
using CyberPulse.Enemy;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Tracks score and kill-streak combo multiplier.
    /// Each kill adds <c>_baseKillScore * comboMultiplier</c> points.
    /// Combo increments per kill and resets after <c>_comboResetTime</c> seconds without a kill.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField] private int   _baseKillScore    = 100;
        [SerializeField] private float _comboResetTime   = 3f;
        [SerializeField] private int   _maxCombo         = 8;

        public int   Score         { get; private set; }
        public int   ComboCount    { get; private set; }
        public float ComboTimer    { get; private set; }   // seconds until combo resets

        public event Action<int, int> OnScoreChanged;      // (newScore, pointsAdded)
        public event Action<int>      OnComboChanged;      // (newCombo)

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
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

        // ── Kill event ────────────────────────────────────────────────────────

        private void OnEnemyKilled()
        {
            ComboCount  = Mathf.Min(ComboCount + 1, _maxCombo);
            ComboTimer  = _comboResetTime;
            OnComboChanged?.Invoke(ComboCount);

            int points = _baseKillScore * ComboCount;
            Score += points;
            OnScoreChanged?.Invoke(Score, points);
        }

        private void ResetCombo()
        {
            ComboCount = 0;
            ComboTimer = 0f;
            OnComboChanged?.Invoke(0);
        }
    }
}
