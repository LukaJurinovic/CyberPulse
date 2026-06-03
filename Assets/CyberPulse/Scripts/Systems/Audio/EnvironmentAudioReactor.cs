using System.Collections.Generic;
using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Drives emissive intensity on arena wall renderers based on AudioAnalyzer data.
    ///
    /// At Start(), creates a single shared runtime Material instance (copy of the wall mat)
    /// and assigns it to all wall renderers. Then Update() calls SetColor directly on that
    /// instance — more reliable than MaterialPropertyBlock with statically-batched objects.
    ///
    /// t = max(Amplitude * _ampMultiplier, BassAmplitude * _bassMultiplier)
    /// Lerps _EmissionColor from _wallBaseEmit to _wallPeakEmit.
    ///
    /// Peak colour is HDR (values > 1) so Bloom can amplify it.
    /// </summary>
    public class EnvironmentAudioReactor : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioAnalyzer _analyzer;

        [Header("Sensitivity  (tune per music track)")]
        [Tooltip("Bass frequency multiplier. Raise if walls are too dim, lower if always maxed.")]
        [SerializeField] private float _bassMultiplier = 6f;

        [Header("Pulse contrast")]
        [Tooltip("Higher = sharper pulses. Raises the gap between off-beat darkness and on-beat peaks.")]
        [SerializeField, Range(1f, 4f)] private float _contrastPower = 2.6f;
        [Tooltip("How fast a pulse falls back to dark after a beat. Higher = snappier, punchier pulses.")]
        [SerializeField] private float _pulseDecay = 4.5f;
        [Tooltip("Scales the global _CyberBeatPulse value the grid floor reads.")]
        [SerializeField] private float _floorPulseGain = 1f;

        [Header("Emissive colours  (HDR for Bloom interaction)")]
        [SerializeField] private Color _wallBaseEmit = new Color(0.015f, 0.02f, 0.05f);
        [SerializeField] private Color _wallPeakEmit = new Color(0.45f,  1.3f,  3.2f);

        private static readonly Color CoolBase = new Color(0.02f, 0.04f, 0.12f);
        private static readonly Color WarmBase = new Color(0.08f, 0.20f, 0.35f);

        private static readonly int    EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int    BeatPulseID     = Shader.PropertyToID("_CyberBeatPulse");

        private readonly List<Renderer> _reactors = new();
        private Material                _runtimeMat;
        private float                   _level;

        private void Start()
        {
            var all = Object.FindObjectsByType<MeshRenderer>();
            foreach (var r in all)
            {
                string n = r.gameObject.name;
                if (n.StartsWith("Wall") || n == "Ceiling")
                    _reactors.Add(r);
            }

            if (_reactors.Count == 0)
            {
                Debug.LogWarning("[CyberPulse] EnvironmentAudioReactor: no wall renderers found.");
                return;
            }

            _runtimeMat = new Material(_reactors[0].sharedMaterial);
            _runtimeMat.EnableKeyword("_EMISSION");
            _runtimeMat.SetColor(EmissionColorID, _wallBaseEmit);
            foreach (var r in _reactors)
                r.sharedMaterial = _runtimeMat;

            if (SongAnalyzer.Instance != null)
            {
                if (SongAnalyzer.Instance.IsAnalyzed)
                    ApplySongPalette(SongAnalyzer.Instance.Profile);
                else
                    SongAnalyzer.Instance.OnAnalysisComplete += ApplySongPalette;
            }
        }

        private void OnDestroy()
        {
            if (SongAnalyzer.Instance != null)
                SongAnalyzer.Instance.OnAnalysisComplete -= ApplySongPalette;

            Shader.SetGlobalFloat(BeatPulseID, 0f);
        }

        private void ApplySongPalette(SongProfile profile)
        {
            float t = Mathf.Clamp01((profile.AverageEnergy - 0.2f) / 0.6f);
            _wallBaseEmit = Color.Lerp(CoolBase, WarmBase, t);
        }

        private void Update()
        {
            if (_analyzer == null || _runtimeMat == null) return;

            float raw = Mathf.Clamp01(_analyzer.BassAmplitude * _bassMultiplier);

            float target = Mathf.Pow(raw, _contrastPower);

            _level = Mathf.Max(target, _level - _pulseDecay * Time.unscaledDeltaTime);

            Color emit = Color.Lerp(_wallBaseEmit, _wallPeakEmit, _level);
            _runtimeMat.SetColor(EmissionColorID, emit);

            Shader.SetGlobalFloat(BeatPulseID, _level * _floorPulseGain);
        }
    }
}
