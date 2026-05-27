using System.Collections;
using UnityEngine;
using CyberPulse.Weapons;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Triangle Mirror Fighter. Mirrors player capabilities at reduced scale:
    /// fires orange projectiles, has 3 dashes (4s recharge), moves at 65% player speed.
    /// No NavMesh — transform-based movement.
    /// Counter-play: bait the 3 dashes then punish during the 4s recharge window.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyDeathShards))]
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyTriangleMirror : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed        = 3.25f;  // 65% of player ~5 m/s
        [SerializeField] private float _dashSpeed        = 14f;
        [SerializeField] private float _dashDuration     = 0.18f;
        [SerializeField] private int   _dashCharges      = 3;
        [SerializeField] private float _dashRechargeTime = 4f;
        [SerializeField] private float _preferredRange   = 8f;
        [SerializeField] private float _engageRange      = 14f;

        [Header("Projectile")]
        [SerializeField] private float _fireInterval     = 1.8f;
        [SerializeField] private int   _projectileDamage = 12;
        [SerializeField] private float _projectileSpeed  = 16f;
        [SerializeField] private LayerMask _playerLayer;

        private EnemyHealth _health;
        private EnemySensor _sensor;
        private Transform   _target;

        private int   _currentDashes;
        private float _dashRechargeTimer;
        private float _fireTimer;
        private bool  _isDashing;
        private bool  _isDead;

        private void Awake()
        {
            _health        = GetComponent<EnemyHealth>();
            _sensor        = GetComponent<EnemySensor>();
            _currentDashes = _dashCharges;
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

            RechargeDashes();
            Move();

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f) { _fireTimer = _fireInterval; FireProjectile(); }
        }

        private void RechargeDashes()
        {
            if (_currentDashes >= _dashCharges) return;
            _dashRechargeTimer -= Time.deltaTime;
            if (_dashRechargeTimer <= 0f)
                _currentDashes = _dashCharges;
        }

        private void Move()
        {
            if (_isDashing) return;

            float dist = Vector3.Distance(transform.position, _target.position);

            // Dash aggressively toward player while charges remain
            if (_currentDashes > 0 && dist < _engageRange && dist > _preferredRange * 0.5f)
            {
                _currentDashes--;
                if (_currentDashes == 0) _dashRechargeTimer = _dashRechargeTime;
                StartCoroutine(DashRoutine());
                return;
            }

            // Normal: close to preferred range, then strafe
            Vector3 toTarget = (_target.position - transform.position).normalized;
            Vector3 move = dist > _preferredRange
                ? toTarget
                : Vector3.Cross(Vector3.up, toTarget);

            transform.position += move * (_moveSpeed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, 0f, transform.position.z);

            if (toTarget.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(toTarget), Time.deltaTime * 8f);
        }

        private IEnumerator DashRoutine()
        {
            _isDashing = true;
            Vector3 dir = (_target.position - transform.position);
            dir.y = 0; dir.Normalize();
            float elapsed = 0f;
            while (elapsed < _dashDuration)
            {
                transform.position += dir * (_dashSpeed * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _isDashing = false;
        }

        private void FireProjectile()
        {
            if (_target == null) return;

            var go = new GameObject("TriangleProjectile");
            go.transform.position = transform.position + Vector3.up * 1.2f;

            // Aim at player torso
            Vector3 dir = (_target.position + Vector3.up - go.transform.position).normalized;
            go.transform.rotation = Quaternion.LookRotation(dir);

            // Visual — small orange sphere child
            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = Vector3.one * 0.15f;
            Object.Destroy(vis.GetComponent<SphereCollider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor",     new Color(1f, 0.4f, 0.1f));
            mat.SetColor("_EmissionColor", new Color(2f, 0.8f, 0.2f));
            mat.EnableKeyword("_EMISSION");
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Hit collider on root
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.12f;

            var proj = go.AddComponent<Projectile>();
            proj.Init(_projectileSpeed, _projectileDamage, _playerLayer);

            // Prevent projectile from immediately hitting this enemy
            var myCol = GetComponent<Collider>();
            if (myCol != null) Physics.IgnoreCollision(col, myCol);
        }
    }
}
