using UnityEngine;

namespace CyberPulse.Enemy
{
    public enum EnemyVisualMode { Idle, Patrol, Chase, Attack, Charging, Dead }

    /// <summary>
    /// Drives the wireframe shader's _EdgeColor per behaviour state via
    /// MaterialPropertyBlock (no material instances). Auto-wires to
    /// EnemyController.OnStateChanged when present; specialized enemies
    /// call SetMode() directly from their own state machines.
    /// </summary>
    public class EnemyStateVisual : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _renderer;

        // HDR colors driving the wireframe shader's _EdgeColor. Hues are deliberately
        // spread across the spectrum so each state reads at a glance: blue (idle) →
        // green (patrol) → orange (chase) → red (attack), with cyan for charging.
        private static readonly Color IdleColor     = new Color(0.3f,  0.7f, 1.6f);
        private static readonly Color PatrolColor   = new Color(0.2f,  1.6f, 0.4f);
        private static readonly Color ChaseColor    = new Color(2.8f,  0.9f, 0.05f);
        private static readonly Color AttackColor   = new Color(3.6f,  0.05f, 0.05f);
        private static readonly Color ChargingColor = new Color(0.2f,  1.4f, 3.2f);
        private static readonly Color DeadColor     = new Color(0.08f, 0.08f, 0.08f);

        [SerializeField, Range(0.03f, 0.6f)]
        private float _fillTint = 0.4f;    // body fill = state colour × this (bright enough that the whole mesh reads the state hue, not just the silhouette edge)

        private const string WireframeShaderName = "CyberPulse/WireframeEnemy";

        private static readonly int EdgeColorID = Shader.PropertyToID("_EdgeColor");
        private static readonly int FillColorID = Shader.PropertyToID("_FillColor");

        private MaterialPropertyBlock _block;
        private EnemyVisualMode       _mode = EnemyVisualMode.Idle;
        private EnemyController       _controller;
        private EnemyHealth           _health;
        private float                 _hitFlashUntil;
        private bool                  _dead;

        // One shared wireframe material across all enemies — per-enemy colour comes from
        // the MaterialPropertyBlock, so sharing costs nothing and keeps batching intact.
        private static Material _sharedWireframe;

        private void Awake()
        {
            _block      = new MaterialPropertyBlock();
            _controller = GetComponent<EnemyController>();
            _health     = GetComponent<EnemyHealth>();
            if (_renderer == null) _renderer = GetComponentInChildren<MeshRenderer>();
            EnsureWireframeMaterial();
        }

        // Enemy prefabs ship with a null material (the builder assigned a transient
        // runtime Material that couldn't serialize into the prefab asset), so spawned
        // wave enemies render with Unity's default grey material — which has no
        // _EdgeColor/_FillColor for our property block to drive. Assign the wireframe
        // material here so every enemy actually shows its state colour.
        private void EnsureWireframeMaterial()
        {
            if (_renderer == null) return;

            var mat = _renderer.sharedMaterial;
            if (mat != null && mat.shader != null && mat.shader.name == WireframeShaderName)
                return; // already a proper wireframe material (e.g. in-scene placed enemies)

            var shared = GetSharedWireframe();
            if (shared != null) _renderer.sharedMaterial = shared;
        }

        private static Material GetSharedWireframe()
        {
            if (_sharedWireframe != null) return _sharedWireframe;

            var shader = Shader.Find(WireframeShaderName);
            if (shader == null) return null;

            _sharedWireframe = new Material(shader) { name = "M_Enemy_Wireframe_Shared" };
            _sharedWireframe.SetColor(EdgeColorID, ChaseColor);
            _sharedWireframe.SetColor(FillColorID, new Color(0.08f, 0.01f, 0.01f, 1f));
            _sharedWireframe.SetFloat(Shader.PropertyToID("_FresnelPower"),  3.5f);
            _sharedWireframe.SetFloat(Shader.PropertyToID("_EdgeWidth"),     0.55f);
            _sharedWireframe.SetFloat(Shader.PropertyToID("_PulseSpeed"),    1.8f);
            _sharedWireframe.SetFloat(Shader.PropertyToID("_PulseAmount"),   0.25f);
            _sharedWireframe.SetFloat(Shader.PropertyToID("_EmissiveScale"), 3.5f);
            return _sharedWireframe;
        }

        private void OnEnable()
        {
            if (_controller != null) _controller.OnStateChanged += OnControllerStateChanged;
            if (_health     != null) { _health.OnDamageTaken += OnHit; _health.OnDeath += OnDeath; }
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.OnStateChanged -= OnControllerStateChanged;
            if (_health     != null) { _health.OnDamageTaken -= OnHit; _health.OnDeath -= OnDeath; }
        }

        private void Start() => ApplyColor(ColorFor(_mode));

        public void SetMode(EnemyVisualMode mode)
        {
            if (_dead) return;
            _mode = mode;
            ApplyColor(ColorFor(mode));
        }

        private void Update()
        {
            // Brief white flash on hit
            if (Time.time < _hitFlashUntil)
            {
                ApplyColor(Color.white * 4f);
                return;
            }

            if (_dead) return;

            // Attack and Charging modes pulse the edge color
            if (_mode == EnemyVisualMode.Attack)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 12f);
                ApplyColor(ColorFor(_mode) * pulse);
            }
            else if (_mode == EnemyVisualMode.Charging)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 7f);
                ApplyColor(ColorFor(_mode) * pulse);
            }
        }

        private void OnControllerStateChanged(EnemyController.State s)
        {
            switch (s)
            {
                case EnemyController.State.Idle:   SetMode(EnemyVisualMode.Idle);   break;
                case EnemyController.State.Patrol: SetMode(EnemyVisualMode.Patrol); break;
                case EnemyController.State.Chase:  SetMode(EnemyVisualMode.Chase);  break;
                case EnemyController.State.Attack: SetMode(EnemyVisualMode.Attack); break;
                case EnemyController.State.Dead:   OnDeath(); break;
            }
        }

        private void OnHit(int _) => _hitFlashUntil = Time.time + 0.08f;

        private void OnDeath()
        {
            _dead = true;
            _mode = EnemyVisualMode.Dead;
            ApplyColor(ColorFor(EnemyVisualMode.Dead));
        }

        private void ApplyColor(Color c)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(EdgeColorID, c);
            // Tint the body fill too so the state colour reads across the whole mesh,
            // not just the thin fresnel silhouette — critical at long sightlines.
            _block.SetColor(FillColorID, new Color(c.r * _fillTint, c.g * _fillTint, c.b * _fillTint, 1f));
            _renderer.SetPropertyBlock(_block);
        }

        private static Color ColorFor(EnemyVisualMode mode) => mode switch
        {
            EnemyVisualMode.Idle     => IdleColor,
            EnemyVisualMode.Patrol   => PatrolColor,
            EnemyVisualMode.Chase    => ChaseColor,
            EnemyVisualMode.Attack   => AttackColor,
            EnemyVisualMode.Charging => ChargingColor,
            EnemyVisualMode.Dead     => DeadColor,
            _                        => ChaseColor,
        };
    }
}
