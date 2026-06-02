using UnityEngine;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Plays a rising-rate beep on an AudioSource when this missile closes within
    /// <see cref="_alertRadius"/> metres of the player. Uses a procedural sine tone —
    /// no audio asset required. Added by EnemyCylinderLauncher to each spawned missile.
    ///
    /// Beep interval shrinks from 0.3s (max range) to 0.08s (point-blank) so the
    /// player can tell how close the missile is by sound alone.
    /// </summary>
    public class MissileLockOn : MonoBehaviour
    {
        [SerializeField] private float _alertRadius  = 6f;
        [SerializeField] private float _beepInterval = 0.30f;

        private Transform   _playerTarget;
        private AudioSource _audio;
        private AudioClip   _toneClip;
        private float       _beepTimer;

        public void Init(Transform player, AudioSource src)
        {
            _playerTarget = player;
            _audio        = src;
            _toneClip     = MakeSineTone(880f, 0.07f, AudioSettings.outputSampleRate);
        }

        private void Update()
        {
            if (_playerTarget == null || _audio == null || _toneClip == null) return;

            float dist = Vector3.Distance(transform.position, _playerTarget.position);
            if (dist > _alertRadius) return;

            _beepTimer -= Time.deltaTime;
            if (_beepTimer > 0f) return;

            float urgency  = 1f - Mathf.Clamp01(dist / _alertRadius);
            _beepTimer     = Mathf.Lerp(_beepInterval, 0.08f, urgency);
            _audio.PlayOneShot(_toneClip, Mathf.Lerp(0.3f, 0.7f, urgency));
        }

        private static AudioClip MakeSineTone(float frequency, float duration, int sampleRate)
        {
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var clip    = AudioClip.Create("LockOnTone", samples, 1, sampleRate, false);
            var data    = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * (1f - t);
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
