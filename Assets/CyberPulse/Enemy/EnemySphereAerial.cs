using System.Collections;
using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Sphere Aerial Striker. Hovers at y=5-7, drifts laterally, and charges a
    /// laser-column attack with a ground AoE indicator. Dealing 40% of its max HP
    /// during the charge interrupts the strike.
    /// Does NOT use NavMesh — purely transform-driven.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyDeathShards))]
    [RequireComponent(typeof(EnemySensor))]
    public class EnemySphereAerial : MonoBehaviour
    {
        [Header("Hover")]
        [SerializeField] private float _hoverMin   = 5f;
        [SerializeField] private float _hoverMax   = 7f;
        [SerializeField] private float _driftSpeed = 3f;

        [Header("Charge Attack")]
        [SerializeField] private float _chargeDuration    = 2.5f;
        [SerializeField] private float _attackRadius      = 5f;
        [SerializeField] private int   _attackDamage      = 30;
        [SerializeField] private float _attackCooldown    = 6f;
        [SerializeField] private float _interruptFraction = 0.4f;

        [Header("Layers")]
        [SerializeField] private LayerMask _playerLayer;

        private EnemyHealth _health;
        private EnemySensor _sensor;
        private AudioSource _chargeAudio;
        private AudioClip   _droneClip;

        private enum AerialState { Drift, Charging, Cooldown, Dead }
        private AerialState _state = AerialState.Drift;

        private Transform _target;
        private Vector3   _driftTarget;
        private float     _hoverHeight;
        private float     _cooldownTimer;
        private Coroutine _chargeRoutine;

        private void Awake()
        {
            _health      = GetComponent<EnemyHealth>();
            _sensor      = GetComponent<EnemySensor>();
            _hoverHeight = Random.Range(_hoverMin, _hoverMax);
            _driftTarget = PickDriftTarget();

            // Procedural rising-pitch drone — no audio asset required.
            int sr = AudioSettings.outputSampleRate;
            _droneClip   = MakeDroneTone(220f, 0.5f, sr);
            _chargeAudio = gameObject.AddComponent<AudioSource>();
            _chargeAudio.clip         = _droneClip;
            _chargeAudio.loop         = true;
            _chargeAudio.playOnAwake  = false;
            _chargeAudio.spatialBlend = 1f;
            _chargeAudio.volume       = 0.5f;
            _chargeAudio.minDistance  = 4f;
            _chargeAudio.maxDistance  = 28f;
        }

        private void OnEnable()
        {
            _sensor.OnPlayerSpotted += OnPlayerSpotted;
            _sensor.OnPlayerLost    += OnPlayerLost;
        }

        private void OnDisable()
        {
            _sensor.OnPlayerSpotted -= OnPlayerSpotted;
            _sensor.OnPlayerLost    -= OnPlayerLost;
        }

        private void Start()
        {
            _health.OnDeath += HandleDeath;
            var p = transform.position;
            transform.position = new Vector3(p.x, _hoverHeight, p.z);
        }

        private void Update()
        {
            switch (_state)
            {
                case AerialState.Drift:    UpdateDrift();    break;
                case AerialState.Cooldown: UpdateCooldown(); break;
            }
        }

        private void UpdateDrift()
        {
            // Smooth hover height
            var pos = transform.position;
            float ty = Mathf.Lerp(pos.y, _hoverHeight, Time.deltaTime * 2f);

            // Drift horizontally toward target
            var flatDest = new Vector3(_driftTarget.x, ty, _driftTarget.z);
            transform.position = Vector3.MoveTowards(
                new Vector3(pos.x, ty, pos.z), flatDest, _driftSpeed * Time.deltaTime);

            if (Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(_driftTarget.x,       0, _driftTarget.z)) < 1f)
                _driftTarget = PickDriftTarget();

            if (_target == null) return;

            // Face player horizontally
            Vector3 dir = _target.position - transform.position; dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), Time.deltaTime * 3f);

            // Begin charge when player is within range
            float flatDist = new Vector2(
                transform.position.x - _target.position.x,
                transform.position.z - _target.position.z).magnitude;
            if (flatDist < 20f)
            {
                _state = AerialState.Charging;
                _chargeRoutine = StartCoroutine(ChargeRoutine());
            }
        }

        private void UpdateCooldown()
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f) _state = AerialState.Drift;
        }

        private IEnumerator ChargeRoutine()
        {
            int hpAtStart = _health.CurrentHealth;

            Vector3 groundPos = _target != null
                ? new Vector3(_target.position.x, 0f, _target.position.z)
                : new Vector3(transform.position.x, 0f, transform.position.z);

            var indicator = AoEIndicator.Create(groundPos, _attackRadius, _chargeDuration);

            // Rising pitch tone — starts low, peaks at 2× on strike.
            _chargeAudio.pitch = 0.5f;
            _chargeAudio.Play();

            float elapsed     = 0f;
            bool  interrupted = false;
            while (elapsed < _chargeDuration)
            {
                if (_health.IsDead) { _chargeAudio.Stop(); yield break; }
                elapsed += Time.deltaTime;
                _chargeAudio.pitch = Mathf.Lerp(0.5f, 2.2f, elapsed / _chargeDuration);
                if (hpAtStart - _health.CurrentHealth >= _health.MaxHealth * _interruptFraction)
                {
                    interrupted = true;
                    break;
                }
                yield return null;
            }

            _chargeAudio.Stop();
            if (indicator != null) Destroy(indicator.gameObject);

            if (!interrupted)
            {
                Collider[] hits = Physics.OverlapSphere(groundPos + Vector3.up, _attackRadius, _playerLayer);
                foreach (var c in hits)
                    c.GetComponentInParent<IDamageable>()?.TakeDamage(_attackDamage);
            }

            _state         = AerialState.Cooldown;
            _cooldownTimer = _attackCooldown;
            _chargeRoutine = null;
        }

        private static AudioClip MakeDroneTone(float frequency, float duration, int sampleRate)
        {
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var clip    = AudioClip.Create("ChargeDrone", samples, 1, sampleRate, false);
            var data    = new float[samples];
            for (int i = 0; i < samples; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * 0.6f;
            clip.SetData(data, 0);
            return clip;
        }

        private Vector3 PickDriftTarget() =>
            new Vector3(Random.Range(-20f, 20f), _hoverHeight, Random.Range(-20f, 20f));

        private void OnPlayerSpotted(Transform player) => _target = player;
        private void OnPlayerLost()                    => _target = null;

        private void HandleDeath()
        {
            _state = AerialState.Dead;
            if (_chargeRoutine != null) StopCoroutine(_chargeRoutine);
        }
    }
}
