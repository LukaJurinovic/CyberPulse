using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using CyberPulse.Input;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Slow-motion system. Right-click (AltFire) holds 30% time scale with post-processing
    /// and audio pitch responses. Uses unscaled delta so the transition is real-time.
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        [Header("Input")]
        [SerializeField] private InputReader _input;

        [Header("Time")]
        [SerializeField] private float _slowTimeScale  = 0.3f;
        [SerializeField] private float _enterDuration  = 0.15f;
        [SerializeField] private float _exitDuration   = 0.12f;

        [Header("Post-Processing — optional")]
        [SerializeField] private Volume _slowMoVolume;

        public bool IsSlowMo { get; private set; }

        private Coroutine _lerpRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_input != null)
            {
                _input.AltFirePressed  += EnterSlowMo;
                _input.AltFireReleased += ExitSlowMo;
            }
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.AltFirePressed  -= EnterSlowMo;
                _input.AltFireReleased -= ExitSlowMo;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void EnterSlowMo()
        {
            if (IsSlowMo) return;
            IsSlowMo = true;
            RestartLerp(Time.timeScale, _slowTimeScale, _enterDuration, enteringSlowMo: true);
        }

        public void ExitSlowMo()
        {
            if (!IsSlowMo) return;
            IsSlowMo = false;
            RestartLerp(Time.timeScale, 1f, _exitDuration, enteringSlowMo: false);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void RestartLerp(float from, float to, float duration, bool enteringSlowMo)
        {
            if (_lerpRoutine != null) StopCoroutine(_lerpRoutine);
            _lerpRoutine = StartCoroutine(LerpTimeScale(from, to, duration, enteringSlowMo));
        }

        private IEnumerator LerpTimeScale(float from, float to, float duration, bool enteringSlowMo)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t     = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(from, to, t);

                ApplyTimeScale(scale);

                if (_slowMoVolume != null)
                    _slowMoVolume.weight = enteringSlowMo ? t : 1f - t;

                UpdateAudioPitches(scale);

                yield return null;
            }

            // Snap to final values
            ApplyTimeScale(to);
            if (_slowMoVolume != null)
                _slowMoVolume.weight = enteringSlowMo ? 1f : 0f;
            UpdateAudioPitches(to);
        }

        private static void ApplyTimeScale(float scale)
        {
            Time.timeScale      = scale;
            // Always keep fixedDeltaTime in sync — critical for physics correctness.
            Time.fixedDeltaTime = scale * 0.02f;
        }

        private static void UpdateAudioPitches(float timeScale)
        {
            var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);
            foreach (var src in sources)
            {
                if (src != null)
                    src.pitch = timeScale;
            }
        }
    }
}
