using UnityEngine;
using CyberPulse.Systems;
using CyberPulse.Weapons;

namespace CyberPulse.Enemy
{
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyDeathShards))]
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyCylinderLauncher : MonoBehaviour
    {
        [Header("Orbit")]
        [SerializeField] private float _orbitRadius  = 10f;
        [SerializeField] private float _orbitSpeed   = 45f;
        [SerializeField] private float _trackSpeed   = 2.5f;

        [Header("Missile")]
        [SerializeField] private float _fireInterval    = 3f;
        [SerializeField] private int   _missileDamage   = 20;
        [SerializeField] private float _missileSpeed    = 6f;
        [SerializeField] private float _homingStrength  = 1.2f;
        [SerializeField] private float _missileAoe      = 2f;
        [SerializeField] private LayerMask _playerLayer;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Arena Bounds")]
        [SerializeField] private float _arenaBound = 24f;

        private EnemyHealth      _health;
        private EnemySensor      _sensor;
        private EnemyStateVisual _visual;
        private Transform        _target;

        private float _orbitAngle;
        private float _fireTimer;
        private float _attackPulseUntil;
        private bool  _isDead;
        private float _baseY;

        private void Awake()
        {
            _health     = GetComponent<EnemyHealth>();
            _sensor     = GetComponent<EnemySensor>();
            _visual     = GetComponent<EnemyStateVisual>();
            _orbitAngle = Random.Range(0f, 360f);
            _baseY      = transform.position.y;
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

        private void OnPlayerSpotted(Transform t)
        {
            _target = t;
            _visual?.SetMode(EnemyVisualMode.Chase);
        }

        private void OnPlayerLost()
        {
            _target = null;
            _visual?.SetMode(EnemyVisualMode.Idle);
        }

        private void Start() => _health.OnDeath += () => _isDead = true;

        private void Update()
        {
            if (_isDead || _target == null) return;

            _orbitAngle += _orbitSpeed * Time.deltaTime;
            float rad = _orbitAngle * Mathf.Deg2Rad;
            Vector3 orbitPos = _target.position
                + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _orbitRadius;
            orbitPos.y = _baseY;

            transform.position = Vector3.Lerp(transform.position, orbitPos,
                _trackSpeed * Time.deltaTime);

            float b = Mathf.Min(_arenaBound, 24f);
            transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, -b, b),
                _baseY,
                Mathf.Clamp(transform.position.z, -b, b));

            Vector3 dir = (_target.position - transform.position); dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), Time.deltaTime * 5f);

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                _fireTimer = _fireInterval;
                FireMissile();
                _attackPulseUntil = Time.time + 0.6f;
                _visual?.SetMode(EnemyVisualMode.Attack);
            }

            if (_attackPulseUntil > 0f && Time.time > _attackPulseUntil)
            {
                _attackPulseUntil = 0f;
                _visual?.SetMode(EnemyVisualMode.Chase);
            }
        }

        private void FireMissile()
        {
            if (_target == null) return;

            var go = new GameObject("HomingMissile");
            go.transform.position = transform.position + Vector3.up * 1.2f;
            go.transform.rotation = Quaternion.LookRotation(
                (_target.position + Vector3.up - go.transform.position).normalized);

            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale    = new Vector3(0.4f, 0.9f, 0.4f);
            vis.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(vis.GetComponent<CapsuleCollider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor",     new Color(0f, 0.7f, 1f));
            mat.SetColor("_EmissionColor", new Color(0f, 2.4f, 3f));
            mat.EnableKeyword("_EMISSION");
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.42f;

            var proj = go.AddComponent<Projectile>();
            proj.Init(_missileSpeed, _missileDamage, _playerLayer | _groundLayer);
            proj.SetHoming(_target, _homingStrength);
            proj.SetAoeRadius(_missileAoe);

            proj.SetInterceptReward(() =>
            {
                TraceMeter.Instance?.DrainDirect(5f);
                ScoreManager.Instance?.AddInterceptScore();
            });

            var myCol = GetComponent<Collider>();
            if (myCol != null) Physics.IgnoreCollision(col, myCol);

            var lockAudio = go.AddComponent<AudioSource>();
            lockAudio.spatialBlend = 1f;
            lockAudio.dopplerLevel = 0f;
            lockAudio.minDistance  = 2f;
            lockAudio.maxDistance  = 12f;
            lockAudio.playOnAwake  = false;

            var lockOn = go.AddComponent<MissileLockOn>();
            lockOn.Init(_target, lockAudio);
        }
    }
}
