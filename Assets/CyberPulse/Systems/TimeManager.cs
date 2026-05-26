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

        [Header("Kill-Cam")]
        [SerializeField] private float _killCamTimeScale = 0.05f;
        [SerializeField] private float _killCamDuration  = 0.8f;

        public bool IsSlowMo   { get; private set; }
        public bool IsKillCam  { get; private set; }

        private Coroutine _lerpRoutine;
        private Coroutine _killCamRoutine;

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

        /// <summary>
        /// Hard-freezes time to 5% for 0.8s on last-enemy kill, then snaps back.
        /// Cancels any active slow-mo lerp so the two effects don't fight.
        /// </summary>
        public void TriggerKillCam()
        {
            if (IsKillCam) return;
            if (_lerpRoutine != null) { StopCoroutine(_lerpRoutine); _lerpRoutine = null; }
            IsSlowMo  = false;
            IsKillCam = true;
            _killCamRoutine = StartCoroutine(KillCamRoutine());
        }

        private IEnumerator KillCamRoutine()
        {
            // Kill-cam is a purely visual slow-down — audio pitch is intentionally
            // NOT changed here so music and SFX continue at normal speed.
            ApplyTimeScale(_killCamTimeScale);
            if (_slowMoVolume != null) _slowMoVolume.weight = 0.7f;

            yield return new WaitForSecondsRealtime(_killCamDuration);

            ApplyTimeScale(1f);
            if (_slowMoVolume != null) _slowMoVolume.weight = 0f;
            IsKillCam       = false;
            _killCamRoutine = null;
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
                if (src == null) continue;
                // Skip looping sources (background music) — they should always
                // play at normal pitch so the music never drops out during slow-mo
                // or kill-cam. One-shot SFX (non-looping) are pitch-shifted as usual.
                if (src.loop) continue;
                src.pitch = timeScale;
            }
        }
    }
}
