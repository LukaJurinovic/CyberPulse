using System;
using UnityEngine;

namespace CyberPulse.Enemy
{
    public class EnemySensor : MonoBehaviour
    {
        [SerializeField] private float _detectionRange = 15f;
        [SerializeField] private float _fieldOfView = 120f;
        [SerializeField] private LayerMask _playerLayer;
        [SerializeField] private LayerMask _obstructionLayer;
        [SerializeField] private float _checkInterval = 0.2f;

        private float _timer;
        private bool _playerInSight;

        public bool PlayerInSight => _playerInSight;

        public event Action<Transform> OnPlayerSpotted;

        public event Action OnPlayerLost;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _checkInterval;
            CheckForPlayer();
        }

        private void CheckForPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _detectionRange, _playerLayer);
            if (hits.Length == 0) { LosePlayer(); return; }

            Transform target = hits[0].transform;
            Vector3 toPlayer = target.position - transform.position;
            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);

            if (angle > _fieldOfView * 0.5f) { LosePlayer(); return; }

            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            if (Physics.Raycast(eyePos, toPlayer.normalized, toPlayer.magnitude,
                    _obstructionLayer, QueryTriggerInteraction.Ignore))
            {
                LosePlayer();
                return;
            }

            Transform root = target;
            while (root.parent != null) root = root.parent;
            SpotPlayer(root);
        }

        private void SpotPlayer(Transform player)
        {
            if (_playerInSight) return;
            _playerInSight = true;
            OnPlayerSpotted?.Invoke(player);
        }

        private void LosePlayer()
        {
            if (!_playerInSight) return;
            _playerInSight = false;
            OnPlayerLost?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRange);
        }
    }
}
