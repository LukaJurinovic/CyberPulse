using System;
using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Singleton beat tracker. Reads song position from an AudioSource via
    /// timeSamples / AudioSettings.outputSampleRate (drift-free per plan.md §6).
    ///
    /// BeatPhase 0-1 within each beat. IsOnBeat is true within ±_beatWindowSize
    /// of the beat crossing (i.e. phase &lt; window OR phase &gt; 1 - window).
    ///
    /// OnBeat   — fires on every beat crossing.
    /// OnBeatWindow — fires when entering the leading beat window.
    ///
    /// BPM is set automatically from SongAnalyzer.OnAnalysisComplete, or can be
    /// set manually via Initialize().
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        public static BeatClock Instance { get; private set; }

        [Header("References")]
        [SerializeField] private AudioSource _musicSource;

        [Header("Beat")]
        [SerializeField] private float _defaultBPM = 120f;
        [SerializeField, Range(0.05f, 0.35f)] private float _beatWindowSize = 0.15f;

        // ── Public state ──────────────────────────────────────────────────────

        public float BPM            { get; private set; }
        public float BeatInterval   { get; private set; }   // seconds per beat

        /// <summary>0-1 within the current beat. 0 = beat crossing, 0.5 = halfway.</summary>
        public float BeatPhase      { get; private set; }

        /// <summary>True when within the beat window (phase &lt; window OR phase &gt; 1-window).</summary>
        public bool  IsOnBeat       { get; private set; }

        /// <summary>0 at the beat crossing, 1 halfway between beats.</summary>
        public float DistanceToBeat { get; private set; }

        /// <summary>Fires on every beat crossing (phase wraps 1→0).</summary>
        public event Action OnBeat;

        /// <summary>Fires when entering the leading beat window.</summary>
        public event Action OnBeatWindow;

        private float _prevBeatPhase;
        private bool  _prevOnBeat;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ApplyBPM(_defaultBPM);
        }

        private void Start()
        {
            if (SongAnalyzer.Instance != null)
                SongAnalyzer.Instance.OnAnalysisComplete += OnAnalysisComplete;
        }

        private void OnDestroy()
        {
            if (SongAnalyzer.Instance != null)
                SongAnalyzer.Instance.OnAnalysisComplete -= OnAnalysisComplete;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Set BPM and optional AudioSource at runtime (e.g. from a loading screen).</summary>
        public void Initialize(float bpm, AudioSource source = null)
        {
            ApplyBPM(bpm);
            if (source != null) _musicSource = source;
        }

        public void SetMusicSource(AudioSource source) => _musicSource = source;

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            _prevBeatPhase = BeatPhase;

            float songTime = GetSongTime();
            BeatPhase = BeatInterval > 0f
                ? (songTime % BeatInterval) / BeatInterval
                : 0f;

            // DistanceToBeat: 0 at the crossing, 1 halfway between.
            float d = Mathf.Min(BeatPhase, 1f - BeatPhase);
            DistanceToBeat = d / 0.5f;

            bool nowOnBeat = BeatPhase < _beatWindowSize || BeatPhase > (1f - _beatWindowSize);
            IsOnBeat = nowOnBeat;

            // Beat crossing: phase wrapped from near-1 to near-0
            if (_prevBeatPhase > 0.7f && BeatPhase < 0.3f)
                OnBeat?.Invoke();

            // Beat window entry: leading edge only
            if (!_prevOnBeat && nowOnBeat && BeatPhase < _beatWindowSize)
                OnBeatWindow?.Invoke();

            _prevOnBeat = nowOnBeat;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void ApplyBPM(float bpm)
        {
            BPM          = Mathf.Max(1f, bpm);
            BeatInterval = 60f / BPM;
        }

        private void OnAnalysisComplete(SongProfile profile) => ApplyBPM(profile.BPM);

        private float GetSongTime()
        {
            if (_musicSource != null && _musicSource.isPlaying)
                return (float)_musicSource.timeSamples / AudioSettings.outputSampleRate;
            // Fallback to unscaled real time when no audio source is assigned yet.
            return Time.unscaledTime;
        }
    }
}
