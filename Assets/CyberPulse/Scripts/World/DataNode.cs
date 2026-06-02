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

        [Header("Layered progression")]
        [Tooltip("If set, the node starts hidden (renderer/light/collider off) until its ArenaLayer reveals it.")]
        [SerializeField] private bool _startHidden;

        public bool IsDead   => _siphoned;
        public bool IsHidden { get; private set; }

        /// <summary>Fires once when the node is activated by any hit.</summary>
        public event Action OnSiphoned;

        private bool _siphoned;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            ApplyEmissive(_idleEmissive);
            if (_startHidden) Hide();
        }

        private void Start()
        {
            // In the layered scene the owning ArenaLayer collects and tracks nodes itself,
            // so only fall back to the global manager when there is no layer parent.
            if (GetComponentInParent<ArenaLayer>() == null)
                DataNodeManager.Register(this);
        }

        // ── Hidden state (driven by ArenaLayer) ───────────────────────────────

        /// <summary>Hide the node — disables its renderer, light and collider.</summary>
        public void Hide()    => SetVisible(false);

        /// <summary>Reveal a hidden node so it can be siphoned.</summary>
        public void Reveal()  => SetVisible(true);

        private void SetVisible(bool visible)
        {
            IsHidden = !visible;
            if (_renderer != null) _renderer.enabled = visible;
            if (_nodeLight != null) _nodeLight.enabled = visible;
            // Don't re-enable the collider once siphoned (it's been consumed).
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = visible && !_siphoned;
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
