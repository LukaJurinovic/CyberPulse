using System;
using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.World
{
    [RequireComponent(typeof(Collider))]
    public class DataNode : MonoBehaviour, IDamageable
    {
        [Header("Visuals")]
        [SerializeField] private Renderer      _renderer;
        [SerializeField] private Light         _nodeLight;
        [SerializeField] private ParticleSystem _activateVFX;

        [Header("Colors")]
        [SerializeField] private Color _idleEmissive     = new Color(0f, 2.4f, 2.5f, 1f);
        [SerializeField] private Color _siphonedEmissive = new Color(0f, 3f,   0.5f, 1f);

        [Header("Layered progression")]
        [Tooltip("If set, the node starts hidden (renderer/light/collider off) until its ArenaLayer reveals it.")]
        [SerializeField] private bool _startHidden;

        public bool IsDead   => _siphoned;
        public bool IsHidden { get; private set; }

        public event Action OnSiphoned;

        private bool _siphoned;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            ApplyEmissive(_idleEmissive);
            if (_startHidden) Hide();
        }

        private void Start()
        {
            if (GetComponentInParent<ArenaLayer>() == null)
                DataNodeManager.Register(this);
        }

        public void Hide()    => SetVisible(false);

        public void Reveal()  => SetVisible(true);

        private void SetVisible(bool visible)
        {
            IsHidden = !visible;
            if (_renderer != null) _renderer.enabled = visible;
            if (_nodeLight != null) _nodeLight.enabled = visible;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = visible && !_siphoned;
        }

        public void TakeDamage(int amount)
        {
            if (_siphoned) return;
            Activate();
        }

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

            GetComponent<Collider>().enabled = false;

            OnSiphoned?.Invoke();
        }

        private void ApplyEmissive(Color color)
        {
            if (_renderer == null) return;
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
