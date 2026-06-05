using UnityEngine;

namespace CyberPulse.Enemy
{
    public class KnockbackReceiver : MonoBehaviour
    {
        private Vector3 _velocity;

        public void Apply(Vector3 impulse)
        {
            _velocity = impulse;
        }

        private void Update()
        {
            if (_velocity.sqrMagnitude < 0.01f) return;
            transform.position += _velocity * Time.deltaTime;
            _velocity           = Vector3.Lerp(_velocity, Vector3.zero, Time.deltaTime * 5f);
        }
    }
}
