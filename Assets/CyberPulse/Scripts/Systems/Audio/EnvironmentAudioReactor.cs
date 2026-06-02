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
        [SerializeField] private Color _wallPeakEmit = new Color(0.45f,  1.3f,  3.2f);   // HDR

        // Energy-based palette tint — lerp between cool blue and warm cyan.
        private static readonly Color CoolBase = new Color(0.02f, 0.04f, 0.12f);
        private static readonly Color WarmBase = new Color(0.08f, 0.20f, 0.35f);

        private static readonly int    EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int    BeatPulseID     = Shader.PropertyToID("_CyberBeatPulse");

        private readonly List<Renderer> _reactors = new();
        private Material                _runtimeMat;   // single shared runtime instance
        private float                   _level;        // current pulse level, attack-instant / decay-slow

        private void Start()
        {
            // Collect every wall renderer across all stacked layers. GameObject.Find returns only
            // the first match per name, so in the layered arena it left most walls un-pulsed —
            // gather them all by name instead.
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

            // Clear the global so a stale pulse value doesn't bleed into other scenes.
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

            // Bass is beat-reactive; overall RMS saturates at 1.0 with loud music.
            float raw = Mathf.Clamp01(_analyzer.BassAmplitude * _bassMultiplier);

            // Sharpen the response so quiet stretches stay dark and only real beats
            // climb toward the peak — this is what gives the pulse its contrast.
            float target = Mathf.Pow(raw, _contrastPower);

            // Attack instantly to the target, then decay slowly. The result is a crisp
            // flash on each beat that falls back to darkness instead of a mushy average.
            // Unscaled time keeps pulses locked to the music during slow-mo.
            _level = Mathf.Max(target, _level - _pulseDecay * Time.unscaledDeltaTime);

            Color emit = Color.Lerp(_wallBaseEmit, _wallPeakEmit, _level);
            _runtimeMat.SetColor(EmissionColorID, emit);

            // Publish the pulse globally so GridFloor.shader can flash the floor in sync.
            Shader.SetGlobalFloat(BeatPulseID, _level * _floorPulseGain);
        }
    }
}
