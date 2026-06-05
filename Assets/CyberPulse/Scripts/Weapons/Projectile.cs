using System;
using UnityEngine;
using CyberPulse.Combat;

namespace CyberPulse.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _speed         = 60f;
        [SerializeField] private float _lifetime      = 5f;
        [SerializeField] private int   _damage        = 40;
        [SerializeField] private LayerMask _hitMask   = ~0;
        [SerializeField] private float _wallAoeRadius = 0f;

        private Transform _homingTarget;
        private float     _homingStrength;
        private int       _bouncesRemaining;
        private Action    _onIntercept;
        private Rigidbody _rb;
        private bool      _hasHit;

        public void Init(float speed, int damage, LayerMask hitMask)
        {
            _speed = speed; _damage = damage; _hitMask = hitMask;
        }

        public void SetHoming(Transform target, float strength)
        {
            _homingTarget   = target;
            _homingStrength = strength;
        }

        public void SetAoeRadius(float radius) { _wallAoeRadius = radius; }

        public void SetRicochet(int bounces) { _bouncesRemaining = bounces; }

        public void SetInterceptReward(Action callback) { _onIntercept = callback; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity             = false;
            _rb.interpolation          = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void Start()
        {
            _rb.linearVelocity = transform.forward * _speed;
            Destroy(gameObject, _lifetime);
        }

        private void FixedUpdate()
        {
            if (_homingTarget == null || _homingStrength <= 0f || _hasHit) return;
            Vector3 toTarget  = (_homingTarget.position + Vector3.up - transform.position).normalized;
            Vector3 newDir    = Vector3.RotateTowards(_rb.linearVelocity.normalized,
                                    toTarget, _homingStrength * Time.fixedDeltaTime, 0f);
            _rb.linearVelocity = newDir * _speed;
            transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasHit) return;
            if ((_hitMask.value & (1 << collision.gameObject.layer)) == 0) return;

            if (_bouncesRemaining > 0 && collision.collider.GetComponentInParent<IDamageable>() == null)
            {
                _bouncesRemaining--;
                Vector3 reflected      = Vector3.Reflect(_rb.linearVelocity.normalized,
                                             collision.contacts[0].normal);
                _rb.linearVelocity     = reflected * _speed;
                transform.rotation     = Quaternion.LookRotation(_rb.linearVelocity);
                return;
            }

            _hasHit = true;

            if (_wallAoeRadius > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, _wallAoeRadius, _hitMask);
                foreach (var c in hits)
                    c.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
            }
            else
            {
                collision.collider.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
            }

            Destroy(gameObject);
        }

        public bool IsDead => _hasHit;

        public void TakeDamage(int amount)
        {
            if (_hasHit) return;
            _hasHit = true;
            _onIntercept?.Invoke();
            Destroy(gameObject);
        }
    }
}
