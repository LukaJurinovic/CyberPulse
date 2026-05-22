using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// Self-propelled projectile. Moves via Rigidbody, deals damage on first solid hit,
    /// then destroys itself. Also self-destructs after <c>_lifetime</c> seconds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 60f;
        [SerializeField] private float _lifetime = 5f;
        [SerializeField] private int _damage = 40;
        [SerializeField] private LayerMask _hitMask = ~0;

        private Rigidbody _rb;
        private bool _hasHit;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void Start()
        {
            _rb.linearVelocity = transform.forward * _speed;
            Destroy(gameObject, _lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasHit) return;
            if ((_hitMask.value & (1 << collision.gameObject.layer)) == 0) return;

            _hasHit = true;
            collision.collider.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
