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

        [Header("Emissive colours  (HDR for Bloom interaction)")]
        [SerializeField] private Color _wallBaseEmit = new Color(0.04f, 0.05f, 0.10f);
        [SerializeField] private Color _wallPeakEmit = new Color(0.3f,  0.9f,  2.5f);   // HDR

        // Energy-based palette tint — lerp between cool blue and warm cyan.
        private static readonly Color CoolBase = new Color(0.02f, 0.04f, 0.12f);
        private static readonly Color WarmBase = new Color(0.08f, 0.20f, 0.35f);

        private static readonly int    EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly string[] WallNames =
            { "Wall_North", "Wall_South", "Wall_East", "Wall_West", "Ceiling" };

        private readonly List<Renderer> _reactors = new();
        private Material                _runtimeMat;   // single shared runtime instance

        private void Start()
        {
            foreach (var wName in WallNames)
            {
                var go = GameObject.Find(wName);
                if (go == null) continue;
                var r = go.GetComponent<Renderer>();
                if (r != null) _reactors.Add(r);
            }

            if (_reactors.Count == 0)
            {
                Debug.LogWarning("[CyberPulse] EnvironmentAudioReactor: no wall renderers found.");
                return;
            }

            // Create one runtime material instance so SetColor works reliably.
            // Using sharedMaterial here reads the original; assigning it back makes
            // each renderer use our runtime copy instead (play-mode only — no asset edit).
            _runtimeMat = new Material(_reactors[0].sharedMaterial);
            _runtimeMat.EnableKeyword("_EMISSION");
            _runtimeMat.SetColor(EmissionColorID, _wallBaseEmit);
            foreach (var r in _reactors)
                r.sharedMaterial = _runtimeMat;

            // Tint base emission to match the song's energy level once analysis is done.
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
        }

        private void ApplySongPalette(SongProfile profile)
        {
            float t = Mathf.Clamp01((profile.AverageEnergy - 0.2f) / 0.6f);
            _wallBaseEmit = Color.Lerp(CoolBase, WarmBase, t);
        }

        private void Update()
        {
            if (_analyzer == null || _runtimeMat == null) return;

            // Bass is beat-reactive; overall RMS saturates at 1.0 with loud music.
            float t = Mathf.Clamp01(_analyzer.BassAmplitude * _bassMultiplier);

            Color emit = Color.Lerp(_wallBaseEmit, _wallPeakEmit, t);
            _runtimeMat.SetColor(EmissionColorID, emit);

        }
    }
}
