using UnityEngine;
using CyberPulse.Player;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Drives GlitchRendererFeature._GlitchStrength on player damage.
    /// Spikes to _peakStrength, then decays to 0 at _decaySpeed per second.
    /// </summary>
    public class GlitchController : MonoBehaviour
    {
        [SerializeField] private GlitchRendererFeature _feature;
        [SerializeField] private PlayerStats           _playerStats;

        [Header("Glitch Feel")]
        [SerializeField] private float _peakStrength = 0.07f;
        [SerializeField] private float _decaySpeed   = 3.5f;

        private float _strength;

        private void Start()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken += OnDamage;
        }

        private void OnDestroy()
        {
            if (_playerStats != null)
                _playerStats.OnDamageTaken -= OnDamage;
        }

        private void Update()
        {
            if (_strength <= 0f) return;
            _strength = Mathf.Max(0f, _strength - Time.deltaTime * _decaySpeed);
            _feature?.SetStrength(_strength);
        }

        private void OnDamage(int _)
        {
            _strength = _peakStrength;
            _feature?.SetStrength(_strength);
        }
    }
}
