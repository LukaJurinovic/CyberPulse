using UnityEngine;
using UnityEngine.Rendering;
using CyberPulse.Enemy;
using CyberPulse.Player;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Drives post-processing reactions to gameplay events:
    ///   - Chromatic Aberration spike when the player takes damage (drives a Volume weight)
    ///   - Mini screen shake on any enemy kill
    ///   - Full screen shake on player damage
    /// Requires a dedicated "Damage" Volume (isGlobal=true, weight starts at 0) whose
    /// profile contains a ChromaticAberration override at intensity 0.85.
    /// </summary>
    public class PostProcessingController : MonoBehaviour
    {
        [Header("Post-Processing")]
        [SerializeField] private Volume _damageVolume;

        [Header("Screen Shake Strengths")]
        [SerializeField] private float _damageShakeIntensity = 0.06f;
        [SerializeField] private float _damageShakeDuration  = 0.25f;
        [SerializeField] private float _killShakeIntensity   = 0.025f;
        [SerializeField] private float _killShakeDuration    = 0.12f;

        [Header("References — wired by builder")]
        [SerializeField] private PlayerStats  _playerStats;
        [SerializeField] private PlayerCamera _playerCamera;

        // CA weight decays from spike → 0 each frame
        private float _damageVolumeWeight;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken += OnPlayerDamaged;

            EnemyHealth.OnAnyEnemyKilled += OnEnemyKilled;
        }

        private void OnDestroy()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken -= OnPlayerDamaged;

            EnemyHealth.OnAnyEnemyKilled -= OnEnemyKilled;
        }

        private void Update()
        {
            // Decay the damage volume weight back to 0 each frame
            _damageVolumeWeight = Mathf.Max(0f, _damageVolumeWeight - Time.deltaTime * 3.5f);
            if (_damageVolume != null)
                _damageVolume.weight = _damageVolumeWeight;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnPlayerDamaged(int amount)
        {
            _damageVolumeWeight = 1f;
            _playerCamera?.Shake(_damageShakeIntensity, _damageShakeDuration);
        }

        private void OnEnemyKilled()
        {
            _playerCamera?.Shake(_killShakeIntensity, _killShakeDuration);
        }
    }
}
