using UnityEngine;

namespace CyberPulse.World
{
    /// <summary>
    /// Trigger volume that launches the player upward on enter. Placed by
    /// ProceduralArenaGenerator at the base of tall vertical structures so
    /// elevated platforms become reachable without scaling cover blocks.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class JumpPad : MonoBehaviour
    {
        [SerializeField] private float _impulse    = 15f;     // upward velocity (m/s) — clears 6 m towers
        [SerializeField] private float _cooldown   = 0.35f;   // prevents re-launch spam
        [SerializeField] private LayerMask _playerLayer;

        private float _nextReady;

        public void SetPlayerLayer(LayerMask mask) => _playerLayer = mask;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) => TryLaunch(other);
        private void OnTriggerStay (Collider other) => TryLaunch(other);

        private void TryLaunch(Collider other)
        {
            if (Time.time < _nextReady) return;
            if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

            var rb = other.attachedRigidbody;
            if (rb == null) return;

            var v = rb.linearVelocity;
            if (v.y < 0f) v.y = 0f;          // cancel downward velocity for consistent launch height
            rb.linearVelocity = v;
            rb.AddForce(Vector3.up * _impulse, ForceMode.Impulse);

            _nextReady = Time.time + _cooldown;
        }
    }
}
