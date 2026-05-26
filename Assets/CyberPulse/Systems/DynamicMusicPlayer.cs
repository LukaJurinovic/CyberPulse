using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Crossfades two music AudioSources (ambient stem + action stem) based on the
    /// TraceMeter's normalised value.
    ///
    /// Blend curve:
    ///   0  – blendStart  → ambient full, action silent
    ///   blendStart – blendEnd → linear crossfade
    ///   blendEnd – 1.0        → action full, ambient silent
    ///
    /// Single-stem setup: assign the same AudioClip to both sources. The action
    /// source is held silent until the trace threshold is crossed and its playback
    /// is synced to the ambient source in Start() to prevent phasing artefacts.
    /// When a proper action stem is available, swap in the new clip on _actionSrc.
    /// </summary>
    public class DynamicMusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource _ambientSrc;
        [SerializeField] private AudioSource _actionSrc;

        [Header("Blend thresholds (0 – 1 normalised)")]
        [SerializeField] private float _blendStartNorm = 0.5f;   // trace where crossfade begins
        [SerializeField] private float _blendEndNorm   = 0.8f;   // trace where action is fully in

        [Header("Smoothing")]
        [SerializeField] private float _blendSpeed = 1.5f;       // blend units per second

        private float _blend;   // 0 = ambient only · 1 = action only

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            if (_ambientSrc != null && !_ambientSrc.isPlaying)
                _ambientSrc.Play();

            if (_actionSrc != null)
            {
                // Sync position so same-clip setup doesn't create phasing.
                if (_ambientSrc != null && _actionSrc.clip == _ambientSrc.clip)
                    _actionSrc.timeSamples = _ambientSrc.timeSamples;

                if (!_actionSrc.isPlaying)
                    _actionSrc.Play();

                _actionSrc.volume = 0f; // guarantee silent until crossfade starts
            }
        }

        private void Update()
        {
            if (TraceMeter.Instance == null) return;

            float norm   = TraceMeter.Instance.Normalized;
            float target = Mathf.InverseLerp(_blendStartNorm, _blendEndNorm, norm);
            _blend       = Mathf.MoveTowards(_blend, target, Time.deltaTime * _blendSpeed);

            if (_ambientSrc != null) _ambientSrc.volume = 1f - _blend;
            if (_actionSrc  != null) _actionSrc.volume  = _blend;
        }
    }
}
