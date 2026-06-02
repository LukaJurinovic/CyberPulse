using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.World
{
    /// <summary>
    /// Win-condition trigger. Only fires during the Extract phase so the player
    /// can't skip straight to the exit without completing SIPHON + PURGE.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExitTrigger : MonoBehaviour
    {
        [SerializeField] private LayerMask _playerLayer;

        private bool _triggered;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) => TryTrigger(other);
        private void OnTriggerStay(Collider other)  => TryTrigger(other);

        private void TryTrigger(Collider other)
        {
            if (_triggered) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentPhase != GamePhase.Extract) return;
            if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

            _triggered = true;
            GameManager.Instance.TriggerWinState();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);
            var col = GetComponent<Collider>();
            if (col != null) Gizmos.DrawCube(transform.position, col.bounds.size);
        }
    }
}
