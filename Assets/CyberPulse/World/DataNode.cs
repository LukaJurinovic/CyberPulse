using System;
using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.World
{
    /// <summary>
    /// Shootable data node. One hit activates it; activation registers with DataNodeManager.
    /// IDamageable means any weapon hits it automatically — no extra input binding needed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DataNode : MonoBehaviour, IDamageable
    {
        [Header("Visuals")]
        [SerializeField] private Renderer      _renderer;
        [SerializeField] private Light         _nodeLight;
        [SerializeField] private ParticleSystem _activateVFX;

        [Header("Colors")]
        [SerializeField] private Color _idleEmissive     = new Color(0f, 2.4f, 2.5f, 1f);   // cyan HDR
        [SerializeField] private Color _siphonedEmissive = new Color(0f, 3f,   0.5f, 1f);   // green HDR

        public bool IsDead => _siphoned;

        /// <summary>Fires once when the node is activated by any hit.</summary>
        public event Action OnSiphoned;

        private bool _siphoned;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            ApplyEmissive(_idleEmissive);
        }

        private void Start()
        {
            DataNodeManager.Register(this);
        }

        // ── IDamageable ───────────────────────────────────────────────────────

        public void TakeDamage(int amount)
        {
            if (_siphoned) return;
            Activate();
        }

        // ── Activation ────────────────────────────────────────────────────────

        private void Activate()
        {
            _siphoned = true;

            ApplyEmissive(_siphonedEmissive);

            if (_nodeLight != null)
            {
                _nodeLight.color     = Color.green;
                _nodeLight.intensity = 6f;
            }

            if (_activateVFX != null)
                _activateVFX.Play();

            // Disable collider so it can't be hit again and stops blocking bullets.
            GetComponent<Collider>().enabled = false;

            OnSiphoned?.Invoke();
        }

        private void ApplyEmissive(Color color)
        {
            if (_renderer == null) return;
            // MaterialPropertyBlock avoids creating a new material instance per node.
            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorID, color);
            _renderer.SetPropertyBlock(block);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _siphoned ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
