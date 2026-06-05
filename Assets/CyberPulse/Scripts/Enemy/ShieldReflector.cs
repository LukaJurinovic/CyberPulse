using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.Enemy
{
    public class ShieldReflector : MonoBehaviour
    {
        [SerializeField] private int _reflectDivisor = 2;

        private LayerMask _playerLayer;

        public bool IsActive { get; set; }

        public void Init(LayerMask playerLayer) => _playerLayer = playerLayer;

        public bool TryDeflect(Ray incomingRay, RaycastHit shieldHit, int damage)
        {
            if (!IsActive) return false;

            Vector3 reflected = Vector3.Reflect(incomingRay.direction, shieldHit.normal);
            Ray returnRay = new Ray(shieldHit.point + shieldHit.normal * 0.05f, reflected);

            if (Physics.Raycast(returnRay, out RaycastHit playerHit, 200f, _playerLayer,
                    QueryTriggerInteraction.Ignore))
                playerHit.collider.GetComponentInParent<IDamageable>()
                    ?.TakeDamage(Mathf.Max(1, damage / _reflectDivisor));

            return true;
        }
    }
}
