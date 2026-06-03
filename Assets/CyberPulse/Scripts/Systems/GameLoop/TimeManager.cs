using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberPulse.Systems
{
    /// <summary>
    /// Kill-cam and slow-motion system. EnterSlowMo/ExitSlowMo are called directly
    /// by WeaponHolder (AltFire). Uses unscaled delta so transitions are real-time.
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

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

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

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
            ApplyTimeScale(_killCamTimeScale);
            if (_slowMoVolume != null) _slowMoVolume.weight = 0.7f;

            yield return new WaitForSecondsRealtime(_killCamDuration);

            ApplyTimeScale(1f);
            if (_slowMoVolume != null) _slowMoVolume.weight = 0f;
            IsKillCam       = false;
            _killCamRoutine = null;
        }

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

            ApplyTimeScale(to);
            if (_slowMoVolume != null)
                _slowMoVolume.weight = enteringSlowMo ? 1f : 0f;
            UpdateAudioPitches(to);
        }

        private static void ApplyTimeScale(float scale)
        {
            Time.timeScale      = scale;
            Time.fixedDeltaTime = scale * 0.02f;
        }

        private static void UpdateAudioPitches(float timeScale)
        {
            var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);
            foreach (var src in sources)
            {
                if (src == null) continue;
                if (src.loop) continue;
                src.pitch = timeScale;
            }
        }
    }
}
