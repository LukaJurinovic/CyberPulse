using System.Collections.Generic;
using UnityEngine;
using CyberPulse.Combat;
using CyberPulse.Enemy;
using CyberPulse.Systems;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// 8-pellet pump shotgun. Off-beat: wide 12° spread. On-beat: spread tightens
    /// to 60% (4.8°) for a precision blast. 2-round pump mag; rewards close-quarters
    /// aggression synced to the beat.
    /// Special: wide blast (30° cone, 16 rays) that knocks enemies back and hops the player upward.
    /// </summary>
    public class ShotgunWeapon : WeaponBase
    {
        [Header("Shotgun")]
        [SerializeField] private int   _pelletsPerShot   = 8;
        [SerializeField] private int   _damagePerPellet  = 15;
        [SerializeField] private float _spreadAngle      = 12f;
        [SerializeField] private float _onBeatSpreadMult = 0.4f;  // 1 - 0.6 tighten = 40% of base
        [SerializeField] private float _range            = 60f;
        [SerializeField] private LayerMask _hitMask      = ~0;

        [Header("Knockdown Special")]
        [SerializeField] private float _knockbackForce = 12f;
        [SerializeField] private float _playerHopForce = 5f;

        protected override void FireProjectile(Transform cameraTransform)
        {
            bool  onBeat = BeatClock.Instance != null && BeatClock.Instance.IsOnBeat;
            float spread = onBeat ? _spreadAngle * _onBeatSpreadMult : _spreadAngle;

            for (int i = 0; i < _pelletsPerShot; i++)
            {
                Vector3 dir = Quaternion.Euler(
                    Random.Range(-spread, spread),
                    Random.Range(-spread, spread),
                    0f) * cameraTransform.forward;

                Ray ray = new Ray(cameraTransform.position, dir);
                if (Physics.Raycast(ray, out RaycastHit hit, _range, _hitMask, QueryTriggerInteraction.Ignore))
                    hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(_damagePerPellet);
            }
        }

        // ── Knockdown special: wide blast + enemy knockback + player hop ──

        public override void TriggerSpecial()
        {
            if (_lastCameraTransform == null) return;

            float blastSpread = _spreadAngle * 2.5f;
            var   alreadyHit  = new HashSet<GameObject>();

            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(
                    Random.Range(-blastSpread, blastSpread),
                    Random.Range(-blastSpread, blastSpread),
                    0f) * _lastCameraTransform.forward;

                Ray ray = new Ray(_lastCameraTransform.position, dir);
                if (!Physics.Raycast(ray, out RaycastHit hit, _range, _hitMask,
                        QueryTriggerInteraction.Ignore)) continue;

                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(_damagePerPellet);

                // Knockback — add receiver lazily so enemies don't require the component pre-attached.
                var kb = hit.collider.GetComponentInParent<KnockbackReceiver>();
                if (kb == null)
                {
                    var healthRoot = hit.collider.GetComponentInParent<EnemyHealth>();
                    if (healthRoot != null)
                        kb = healthRoot.gameObject.AddComponent<KnockbackReceiver>();
                }

                if (kb != null && alreadyHit.Add(kb.gameObject))
                {
                    Vector3 push = hit.transform.position - _lastCameraTransform.position;
                    push.y = 0.3f;
                    kb.Apply(push.normalized * _knockbackForce);
                }
            }

            // Recoil hop — push player upward for a small airborne hop.
            var rb = GetComponentInParent<Rigidbody>();
            if (rb != null)
                rb.AddForce(Vector3.up * _playerHopForce, ForceMode.Impulse);
        }
    }
}
