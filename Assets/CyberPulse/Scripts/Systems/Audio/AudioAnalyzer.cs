using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Reads audio data from the AudioListener each frame and exposes smoothed amplitudes.
    ///
    /// Amplitude   — RMS of raw output samples; reliable 0-1 range for any music source.
    /// BassAmplitude — FFT bins 1-5  (~85-430 Hz).  Skips DC bin 0.
    /// MidAmplitude  — FFT bins 8-30 (~680-2580 Hz).
    ///
    /// Used by EnvironmentAudioReactor to drive emissive wall pulsing.
    /// </summary>
    public class AudioAnalyzer : MonoBehaviour
    {
        public static AudioAnalyzer Instance { get; private set; }

        [Header("FFT")]
        [SerializeField] private int       _fftSize      = 256;
        [SerializeField] private int       _outputSize   = 1024;
        [SerializeField] private FFTWindow _fftWindow    = FFTWindow.BlackmanHarris;

        [Header("Smoothing — fast attack keeps beats sharp, slow decay looks natural")]
        [SerializeField] private float _attackSpeed = 30f;   // how fast amplitude rises  (per second)
        [SerializeField] private float _decaySpeed  = 6f;    // how fast amplitude falls (per second)

        /// <summary>RMS of the raw audio output — cleanest overall loudness measure.</summary>
        public float Amplitude     { get; private set; }
        public float BassAmplitude { get; private set; }
        public float MidAmplitude  { get; private set; }

        private float[] _spectrum;
        private float[] _outputSamples;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _spectrum      = new float[_fftSize];
            _outputSamples = new float[_outputSize];
        }

        private void Update()
        {
            // ── RMS amplitude from output data ──────────────────────────────────
            // GetOutputData returns raw samples [-1, 1]; RMS is a reliable
            // 0-1 loudness measure that works for any music file.
            AudioListener.GetOutputData(_outputSamples, 0);
            float sumSq = 0f;
            for (int i = 0; i < _outputSamples.Length; i++)
                sumSq += _outputSamples[i] * _outputSamples[i];
            float rawAmp = Mathf.Sqrt(sumSq / _outputSamples.Length);

            // ── Spectrum (frequency bands) ───────────────────────────────────────
            AudioListener.GetSpectrumData(_spectrum, 0, _fftWindow);

            // Bass: bins 1-5 (~85-430 Hz) — skip bin 0 (DC component)
            float rawBass = 0f;
            for (int i = 1; i <= 5; i++) rawBass += _spectrum[i];
            rawBass /= 5f;

            // Mid: bins 8-30 (~680-2580 Hz)
            float rawMid = 0f;
            for (int i = 8; i <= 30; i++) rawMid += _spectrum[i];
            rawMid /= 23f;

            // ── Peak follower: fast attack, slow decay ────────────────────────────
            // Rising signal (beat hit) snaps up immediately so the wall flash is crisp.
            // Falling signal decays slowly so the glow lingers between beats.
            float dt = Time.deltaTime;
            float ampSpeed  = rawAmp  > Amplitude     ? _attackSpeed : _decaySpeed;
            float bassSpeed = rawBass > BassAmplitude ? _attackSpeed : _decaySpeed;
            float midSpeed  = rawMid  > MidAmplitude  ? _attackSpeed : _decaySpeed;

            Amplitude     = Mathf.Lerp(Amplitude,     rawAmp,  dt * ampSpeed);
            BassAmplitude = Mathf.Lerp(BassAmplitude, rawBass, dt * bassSpeed);
            MidAmplitude  = Mathf.Lerp(MidAmplitude,  rawMid,  dt * midSpeed);

        }
    }
}
