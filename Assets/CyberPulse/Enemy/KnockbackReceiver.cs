using UnityEngine;

namespace CyberPulse.Enemy
{
    /// <summary>
    /// Receives a knockback impulse and applies it as a decaying velocity
    /// displacement each frame. Added lazily by ShotgunWeapon.TriggerSpecial
    /// on the first knockback hit — does not need to be pre-attached.
    /// </summary>
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
