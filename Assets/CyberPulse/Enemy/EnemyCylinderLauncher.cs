using UnityEngine;
using CyberPulse.Systems;
using CyberPulse.Weapons;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Cylinder Homing Launcher. Strafe-orbits the player at mid-range and fires
    /// homing missiles every 3s. Missiles arc toward the player, destroy on 1 hit
    /// from player weapons, and deal AoE damage on wall contact.
    /// No NavMesh — transform-based orbit.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyDeathShards))]
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyCylinderLauncher : MonoBehaviour
    {
        [Header("Orbit")]
        [SerializeField] private float _orbitRadius  = 10f;
        [SerializeField] private float _orbitSpeed   = 45f;   // degrees per second
        [SerializeField] private float _trackSpeed   = 2.5f;

        [Header("Missile")]
        [SerializeField] private float _fireInterval    = 3f;
        [SerializeField] private int   _missileDamage   = 20;
        [SerializeField] private float _missileSpeed    = 6f;    // slow enough to shoot down comfortably
        [SerializeField] private float _homingStrength  = 1.2f;  // reduced to compensate for larger target
        [SerializeField] private float _missileAoe      = 2f;    // wall-detonation radius
        [SerializeField] private LayerMask _playerLayer;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Arena Bounds")]
        [SerializeField] private float _arenaBound = 27f;  // clamp keeps enemy inside arena walls

        private EnemyHealth _health;
        private EnemySensor _sensor;
        private Transform   _target;

        private float _orbitAngle;
        private float _fireTimer;
        private bool  _isDead;

        private void Awake()
        {
            _health     = GetComponent<EnemyHealth>();
            _sensor     = GetComponent<EnemySensor>();
            _orbitAngle = Random.Range(0f, 360f);
        }

        private void OnEnable()
        {
            _sensor.OnPlayerSpotted += t => _target = t;
            _sensor.OnPlayerLost    += () => _target = null;
        }

        private void OnDisable()
        {
            _sensor.OnPlayerSpotted -= t => _target = t;
            _sensor.OnPlayerLost    -= () => _target = null;
        }

        private void Start() => _health.OnDeath += () => _isDead = true;

        private void Update()
        {
            if (_isDead || _target == null) return;

            // Strafe orbit around player
            _orbitAngle += _orbitSpeed * Time.deltaTime;
            float rad = _orbitAngle * Mathf.Deg2Rad;
            Vector3 orbitPos = _target.position
                + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _orbitRadius;
            orbitPos.y = 0f;

            transform.position = Vector3.Lerp(transform.position, orbitPos,
                _trackSpeed * Time.deltaTime);

            // Clamp inside arena walls — prevents the enemy from phasing through boundary.
            float b = _arenaBound;
            transform.position = new Vector3(
                Mathf.Clamp(transform.position.x, -b, b),
                0f,
                Mathf.Clamp(transform.position.z, -b, b));

            // Face player
            Vector3 dir = (_target.position - transform.position); dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), Time.deltaTime * 5f);

            // Fire
            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f) { _fireTimer = _fireInterval; FireMissile(); }
        }

        private void FireMissile()
        {
            if (_target == null) return;

            var go = new GameObject("HomingMissile");
            go.transform.position = transform.position + Vector3.up * 1.2f;
            go.transform.rotation = Quaternion.LookRotation(
                (_target.position + Vector3.up - go.transform.position).normalized);

            // Visual — large cyan elongated capsule (readable at range, rewarding to shoot down)
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

            // Intercept reward: shooting this missile down drains trace and refreshes combo.
            proj.SetInterceptReward(() =>
            {
                TraceMeter.Instance?.DrainDirect(5f);
                ScoreManager.Instance?.AddInterceptScore();
            });

            var myCol = GetComponent<Collider>();
            if (myCol != null) Physics.IgnoreCollision(col, myCol);

            // Lock-on proximity beep — accelerates as the missile closes on the player.
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
