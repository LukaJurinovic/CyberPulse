using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Cube Splitter. Slow, aggressive, seeks directly toward the player.
    /// On death, spawns 2 half-scale Splitter_Small cubes that are faster and melee-only.
    /// Small cubes do NOT split further.
    /// No NavMesh — Vector3.MoveTowards seek.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyDeathShards))]
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyCubeSplitter : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed    = 2f;
        [SerializeField] private float _attackRange  = 1.8f;
        [SerializeField] private int   _meleeDamage  = 15;
        [SerializeField] private float _meleeCooldown = 1.2f;
        [SerializeField] private LayerMask _playerLayer;

        [Header("Split")]
        [SerializeField] private bool _isSplit = false;   // true on small cubes

        private EnemyHealth _health;
        private EnemySensor _sensor;
        private Transform   _target;

        private float _meleeTimer;
        private bool  _isDead;

        // ── Public init (used when spawning small cubes at runtime) ──────────

        /// <summary>Configure this instance as a split-spawn before SetActive(true).</summary>
        public void InitAsSplit(Transform playerTarget, LayerMask playerLayer)
        {
            _isSplit     = true;
            _moveSpeed   = 5f;      // small cubes are faster
            _meleeDamage = 8;
            _target      = playerTarget;
            _playerLayer = playerLayer;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _sensor = GetComponent<EnemySensor>();
        }

        private void OnEnable()
        {
            _sensor.OnPlayerSpotted += t => _target = t;
            // Split cubes keep their injected target even when sensor loses sight
            _sensor.OnPlayerLost    += () => { if (!_isSplit) _target = null; };
        }

        private void OnDisable()
        {
            _sensor.OnPlayerSpotted -= t => _target = t;
            _sensor.OnPlayerLost    -= () => { if (!_isSplit) _target = null; };
        }

        private void Start()
        {
            _health.OnDeath += HandleDeath;
        }

        private void Update()
        {
            if (_isDead || _target == null) return;

            float dist = Vector3.Distance(transform.position, _target.position);

            if (dist > _attackRange)
            {
                Vector3 dir = (_target.position - transform.position); dir.y = 0; dir.Normalize();
                transform.position += dir * (_moveSpeed * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), Time.deltaTime * 6f);
            }
            else
            {
                _meleeTimer -= Time.deltaTime;
                if (_meleeTimer <= 0f)
                {
                    _meleeTimer = _meleeCooldown;
                    Collider[] hits = Physics.OverlapSphere(transform.position, _attackRange, _playerLayer);
                    foreach (var c in hits)
                        c.GetComponentInParent<IDamageable>()?.TakeDamage(_meleeDamage);
                }
            }
        }

        // ── Death → spawn splits ──────────────────────────────────────────────

        private void HandleDeath()
        {
            _isDead = true;
            if (!_isSplit) SpawnSplits();
        }

        private void SpawnSplits()
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = (i == 0 ? Vector3.right : Vector3.left) * 0.6f;

                var go = new GameObject("CubeSplitter_Small");
                go.SetActive(false);  // defer Awake until fully configured

                go.transform.position = transform.position + Vector3.up * 0.3f + offset;

                // Visual cube (half scale of parent)
                var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vis.transform.SetParent(go.transform, false);
                vis.transform.localScale = Vector3.one * 0.5f;
                Object.Destroy(vis.GetComponent<BoxCollider>());
                var mat = new Material(
                    Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.SetColor("_BaseColor",     new Color(0.6f, 0.1f, 0.8f));
                mat.SetColor("_EmissionColor", new Color(1.2f, 0.2f, 1.6f));
                mat.EnableKeyword("_EMISSION");
                vis.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var col = go.AddComponent<BoxCollider>();
                col.size = Vector3.one * 0.5f;

                // Minimal HitVFX
                var vfxGO = new GameObject("HitVFX");
                vfxGO.transform.SetParent(go.transform, false);
                var ps = vfxGO.AddComponent<ParticleSystem>();
                ConfigureSmallHitVFX(ps);

                // Components — Awake is deferred (GO inactive)
                var health   = go.AddComponent<EnemyHealth>();
                var shards   = go.AddComponent<EnemyDeathShards>();
                go.AddComponent<EnemySensor>();   // sensor is a no-op for splits; target injected below
                var splitter = go.AddComponent<EnemyCubeSplitter>();

                health.InitHealth(25);
                shards.SetRenderer(vis.GetComponent<Renderer>());
                splitter.InitAsSplit(_target, _playerLayer);

                go.SetActive(true);
            }
        }

        private static void ConfigureSmallHitVFX(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration      = 0.1f;
            main.loop          = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor    = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.1f, 1f));
            main.maxParticles  = 12;
            main.playOnAwake   = false;
            main.stopAction    = ParticleSystemStopAction.Disable;
            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
        }
    }
}
