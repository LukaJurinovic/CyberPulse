using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Analyses an AudioClip offline (energy-based onset detection) to extract BPM,
    /// duration, and an energy timeline used by ProceduralLevelGenerator.
    ///
    /// Call AnalyzeClip() at any time; subscribe to OnAnalysisComplete for the result.
    /// With a music AudioSource wired, analysis starts automatically in Start().
    /// </summary>
    public class SongAnalyzer : MonoBehaviour
    {
        public static SongAnalyzer Instance { get; private set; }

        [Header("Music Source (auto-detects clip in Start)")]
        [SerializeField] private AudioSource _musicSource;

        [Header("BPM Detection")]
        [SerializeField] private int   _windowSize        = 2048;   // samples (~46ms at 44100 Hz)
        [SerializeField] private float _energyThreshold   = 0.01f;  // absolute floor for onset candidates
        [SerializeField] private float _onsetThreshFactor = 0.5f;   // fraction of avg energy for dynamic threshold

        [Header("Energy Timeline")]
        [SerializeField] private float _timelineSampleInterval = 0.5f;  // seconds per timeline bucket

        [Header("Manual Override")]
        [SerializeField] private float _manualBPM = 0f;  // 0 = auto-detect; >0 = skip analysis

        public SongProfile Profile    { get; private set; }
        public bool         IsAnalyzed { get; private set; }

        public event Action<SongProfile> OnAnalysisComplete;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_musicSource != null && _musicSource.clip != null)
                AnalyzeClip(_musicSource.clip);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void AnalyzeClip(AudioClip clip)
        {
            if (clip == null)
            {
                UseDefault(60f);
                return;
            }

            if (_manualBPM > 0f)
            {
                // Build a minimal profile with the override BPM and no timeline.
                Profile = new SongProfile
                {
                    BPM            = _manualBPM,
                    BeatInterval   = 60f / _manualBPM,
                    Duration       = (float)clip.samples / clip.frequency,
                    AverageEnergy  = 0.5f,
                    EnergyVariance = 0.3f,
                    EnergyTimeline = new float[0],
                    PeakEnergy     = 0.5f,
                };
                IsAnalyzed = true;
                OnAnalysisComplete?.Invoke(Profile);
                Debug.Log($"[SongAnalyzer] Manual BPM override: {_manualBPM}");
                return;
            }

            StartCoroutine(AnalyzeRoutine(clip));
        }

        // ── Analysis coroutine ────────────────────────────────────────────────

        private IEnumerator AnalyzeRoutine(AudioClip clip)
        {
            int   sampleRate   = clip.frequency;
            int   channels     = clip.channels;
            int   totalSamples = clip.samples;
            float duration     = (float)totalSamples / sampleRate;

            // ── 1. Get raw samples and downmix to mono ────────────────────────
            float[] raw  = new float[totalSamples * channels];
            clip.GetData(raw, 0);

            float[] mono = new float[totalSamples];
            if (channels == 1)
            {
                Array.Copy(raw, mono, totalSamples);
            }
            else
            {
                float invCh = 1f / channels;
                for (int i = 0; i < totalSamples; i++)
                {
                    float sum = 0f;
                    for (int c = 0; c < channels; c++)
                        sum += raw[i * channels + c];
                    mono[i] = sum * invCh;
                }
            }
            raw = null;
            yield return null;

            // ── 2. Compute RMS energy per window ─────────────────────────────
            int     numWindows     = totalSamples / _windowSize;
            float[] windowEnergy   = new float[numWindows];
            float   sumEnergy      = 0f;
            float   maxEnergy      = 0f;

            for (int w = 0; w < numWindows; w++)
            {
                float sumSq = 0f;
                int   start = w * _windowSize;
                int   end   = start + _windowSize;
                for (int i = start; i < end; i++)
                    sumSq += mono[i] * mono[i];
                float rms = Mathf.Sqrt(sumSq / _windowSize);
                windowEnergy[w] = rms;
                sumEnergy += rms;
                if (rms > maxEnergy) maxEnergy = rms;

                if (w % 200 == 0) yield return null;
            }

            float avgEnergy       = numWindows > 0 ? sumEnergy / numWindows : 0f;
            float dynamicThresh   = Mathf.Max(_energyThreshold, avgEnergy * _onsetThreshFactor);
            float windowDuration  = (float)_windowSize / sampleRate;

            // ── 3. Detect onset peaks ─────────────────────────────────────────
            var onsetTimes = new List<float>(256);
            for (int w = 1; w < numWindows - 1; w++)
            {
                float e = windowEnergy[w];
                if (e > dynamicThresh && e >= windowEnergy[w - 1] && e >= windowEnergy[w + 1])
                    onsetTimes.Add(w * windowDuration);
            }
            yield return null;

            // ── 4. Inter-onset interval analysis → BPM ───────────────────────
            float bpm = DetectBPM(onsetTimes);

            // ── 5. Build energy timeline (sampled every _timelineSampleInterval) ──
            int   timelineLen      = Mathf.Max(1, Mathf.CeilToInt(duration / _timelineSampleInterval));
            int   samplesPerBucket = Mathf.RoundToInt(_timelineSampleInterval * sampleRate);
            float[] timeline       = new float[timelineLen];

            for (int t = 0; t < timelineLen; t++)
            {
                int   start = t * samplesPerBucket;
                int   end   = Mathf.Min(start + samplesPerBucket, totalSamples);
                if (start >= totalSamples) break;
                float sumSq = 0f;
                int   count = end - start;
                for (int i = start; i < end; i++)
                    sumSq += mono[i] * mono[i];
                timeline[t] = count > 0 ? Mathf.Sqrt(sumSq / count) : 0f;
            }
            yield return null;

            // ── 6. Compute variance ───────────────────────────────────────────
            float varianceSumSq = 0f;
            for (int t = 0; t < timelineLen; t++)
            {
                float diff = timeline[t] - avgEnergy;
                varianceSumSq += diff * diff;
            }
            float variance = timelineLen > 0 ? Mathf.Sqrt(varianceSumSq / timelineLen) : 0f;

            // ── 7. Publish result ─────────────────────────────────────────────
            Profile = new SongProfile
            {
                BPM            = bpm,
                BeatInterval   = 60f / bpm,
                Duration       = duration,
                AverageEnergy  = avgEnergy,
                EnergyVariance = variance,
                EnergyTimeline = timeline,
                PeakEnergy     = maxEnergy,
            };
            IsAnalyzed = true;
            OnAnalysisComplete?.Invoke(Profile);
            Debug.Log($"[SongAnalyzer] Done: BPM={bpm:F0}, duration={duration:F1}s, " +
                      $"avgEnergy={avgEnergy:F3}, {onsetTimes.Count} onsets detected.");
        }

        // ── BPM from IOIs ─────────────────────────────────────────────────────

        private float DetectBPM(List<float> onsetTimes)
        {
            if (onsetTimes.Count < 4) return 120f;

            // Bucket onset intervals into BPM histogram (5-BPM resolution, 60-200 range).
            var histogram = new Dictionary<int, int>(32);

            for (int i = 1; i < onsetTimes.Count; i++)
            {
                float ioi = onsetTimes[i] - onsetTimes[i - 1];
                // Only consider IOIs in the 30-300 BPM range.
                if (ioi < 0.2f || ioi > 2.0f) continue;

                int rawBPM = Mathf.RoundToInt(60f / ioi);

                // Vote for the raw BPM and its 2× harmonic (avoids half-time traps).
                foreach (int candidate in new[] { rawBPM, rawBPM * 2 })
                {
                    if (candidate < 60 || candidate > 200) continue;
                    int bucket = (candidate / 5) * 5;
                    if (!histogram.TryGetValue(bucket, out int v)) v = 0;
                    histogram[bucket] = v + 1;
                }
            }

            if (histogram.Count == 0) return 120f;

            int bestBucket = 120, bestVotes = 0;
            foreach (var kvp in histogram)
            {
                if (kvp.Value > bestVotes)
                {
                    bestVotes  = kvp.Value;
                    bestBucket = kvp.Key;
                }
            }

            // Prefer mid-tempo (80-160 BPM): add a small bias toward that range.
            foreach (var kvp in histogram)
            {
                if (kvp.Key >= 80 && kvp.Key <= 160 && kvp.Value >= bestVotes - 2 && kvp.Value > 0)
                {
                    bestBucket = kvp.Key;
                    break;
                }
            }

            return Mathf.Clamp(bestBucket, 60, 200);
        }

        // ── Fallback ──────────────────────────────────────────────────────────

        private void UseDefault(float duration)
        {
            Profile    = SongProfile.Fallback(duration);
            IsAnalyzed = true;
            OnAnalysisComplete?.Invoke(Profile);
            Debug.Log("[SongAnalyzer] No clip — using fallback profile (120 BPM).");
        }
    }
}
