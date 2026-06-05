using System.Collections;
using UnityEngine;
using CyberPulse.Weapons;

namespace CyberPulse.Enemy
{
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyDeathShards))]
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyTriangleMirror : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed        = 3.25f;
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

        private EnemyHealth      _health;
        private EnemySensor      _sensor;
        private ShieldReflector  _shield;
        private EnemyStateVisual _visual;
        private Transform        _target;

        private int   _currentDashes;
        private float _dashRechargeTimer;
        private float _fireTimer;
        private bool  _isDashing;
        private bool  _isDead;
        private float _baseY;

        private void Awake()
        {
            _health        = GetComponent<EnemyHealth>();
            _sensor        = GetComponent<EnemySensor>();
            _visual        = GetComponent<EnemyStateVisual>();
            _currentDashes = _dashCharges;
            _baseY         = transform.position.y;
            BuildShield();
        }

        private void BuildShield()
        {
            var shieldGO = new GameObject("Shield");
            shieldGO.transform.SetParent(transform, false);
            shieldGO.transform.localPosition = new Vector3(0f, 1f, 0.5f);
            var bc = shieldGO.AddComponent<BoxCollider>();
            bc.size    = new Vector3(1.2f, 1.8f, 0.1f);
            bc.isTrigger = false;
            _shield = shieldGO.AddComponent<ShieldReflector>();
            _shield.Init(_playerLayer);
            _shield.IsActive = false;
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

        private void Start() => _health.OnDeath += OnDeath;

        private void OnDeath()
        {
            _isDead = true;
            if (_shield != null) _shield.IsActive = false;
        }

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
            if (_shield != null)
                _shield.IsActive = !_isDashing && _currentDashes > 0;

            if (_isDashing) return;

            float dist = Vector3.Distance(transform.position, _target.position);

            if (_currentDashes > 0 && dist < _engageRange && dist > _preferredRange * 0.5f)
            {
                _currentDashes--;
                if (_currentDashes == 0) _dashRechargeTimer = _dashRechargeTime;
                StartCoroutine(DashRoutine());
                return;
            }

            Vector3 toTarget = (_target.position - transform.position).normalized;
            Vector3 move = dist > _preferredRange
                ? toTarget
                : Vector3.Cross(Vector3.up, toTarget);

            transform.position += move * (_moveSpeed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, _baseY, transform.position.z);

            if (toTarget.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(toTarget), Time.deltaTime * 8f);
        }

        private IEnumerator DashRoutine()
        {
            _isDashing = true;
            _visual?.SetMode(EnemyVisualMode.Attack);
            Vector3 dir = (_target.position - transform.position);
            dir.y = 0; dir.Normalize();
            float elapsed = 0f;
            while (elapsed < _dashDuration)
            {
                transform.position += dir * (_dashSpeed * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, _baseY, transform.position.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _isDashing = false;
            if (_target != null) _visual?.SetMode(EnemyVisualMode.Chase);
        }

        private void FireProjectile()
        {
            if (_target == null) return;

            var go = new GameObject("TriangleProjectile");
            go.transform.position = transform.position + Vector3.up * 1.2f;

            Vector3 dir = (_target.position + Vector3.up - go.transform.position).normalized;
            go.transform.rotation = Quaternion.LookRotation(dir);

            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = Vector3.one * 0.15f;
            Object.Destroy(vis.GetComponent<SphereCollider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor",     new Color(1f, 0.4f, 0.1f));
            mat.SetColor("_EmissionColor", new Color(2f, 0.8f, 0.2f));
            mat.EnableKeyword("_EMISSION");
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.12f;

            var proj = go.AddComponent<Projectile>();
            proj.Init(_projectileSpeed, _projectileDamage, _playerLayer);

            var myCol = GetComponent<Collider>();
            if (myCol != null) Physics.IgnoreCollision(col, myCol);
        }
    }
}
