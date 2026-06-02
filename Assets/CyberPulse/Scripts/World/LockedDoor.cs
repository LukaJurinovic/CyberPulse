using System;
using UnityEngine;

namespace CyberPulse.World
{
    /// <summary>
    /// A barrier at the top of a ramp that gates entry to the next layer.
    ///
    /// Starts locked: a solid <see cref="_blocker"/> collider seals the opening and a
    /// glowing <see cref="_panel"/> forcefield shows red. When LayerManager clears the
    /// current layer it calls <see cref="Unlock"/> — the blocker is disabled and the
    /// panel turns green and hides. A trigger collider on this GameObject detects the
    /// player walking through and fires <see cref="OnPlayerPassed"/>, after which
    /// LayerManager advances the active layer and re-seals the door via <see cref="Lock"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LockedDoor : MonoBehaviour
    {
        [SerializeField] private Renderer  _panel;     // forcefield visual spanning the opening
        [SerializeField] private Collider  _blocker;   // solid collider blocking the opening while locked
        [SerializeField] private LayerMask _playerLayer;

        [Header("Colours (HDR for Bloom)")]
        [SerializeField] private Color _lockedColor   = new Color(3.0f, 0.15f, 0.35f, 1f);  // red
        [SerializeField] private Color _unlockedColor = new Color(0.1f, 3.0f,  1.2f,  1f);  // green

        public bool IsLocked { get; private set; } = true;

        /// <summary>Fires once each time the player walks through while unlocked.</summary>
        public event Action OnPlayerPassed;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID     = Shader.PropertyToID("_BaseColor");

        private bool _passed;

        private void Awake() => ApplyVisual(_lockedColor, sealed_: true);

        /// <summary>Re-seal the door (called after the player has ascended past it).</summary>
        public void Lock()
        {
            IsLocked = true;
            _passed  = false;
            if (_blocker != null) _blocker.enabled = true;
            ApplyVisual(_lockedColor, sealed_: true);
        }

        /// <summary>Open the door so the player can walk through to the next layer.</summary>
        public void Unlock()
        {
            IsLocked = false;
            if (_blocker != null) _blocker.enabled = false;
            ApplyVisual(_unlockedColor, sealed_: false);
        }

        public void SetPlayerLayer(LayerMask mask) => _playerLayer = mask;

        private void OnTriggerEnter(Collider other)
        {
            if (IsLocked || _passed) return;
            if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0) return;
            _passed = true;
            OnPlayerPassed?.Invoke();
        }

        private void ApplyVisual(Color c, bool sealed_)
        {
            if (_panel == null) return;
            // Hide the panel entirely when open so the opening reads as passable.
            _panel.enabled = sealed_;
            if (!sealed_) return;

            var block = new MaterialPropertyBlock();
            _panel.GetPropertyBlock(block);
            block.SetColor(EmissionColorID, c);
            block.SetColor(BaseColorID, new Color(c.r * 0.1f, c.g * 0.1f, c.b * 0.1f, 1f));
            _panel.SetPropertyBlock(block);
        }
    }
}
