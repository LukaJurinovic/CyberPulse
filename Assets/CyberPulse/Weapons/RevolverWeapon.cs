using UnityEngine;
using CyberPulse.Combat;
using CyberPulse.Systems;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// High-damage single-shot revolver. 6-round cylinder; each reload swaps the
    /// whole cylinder. On-beat shots deal 1.5× damage via WeaponBase read of BeatClock.
    /// Slow fire rate rewards deliberate, rhythmic play.
    /// Special (P3 ricochet) is a no-op until implemented.
    /// </summary>
    public class RevolverWeapon : WeaponBase
    {
        [Header("Revolver")]
        [SerializeField] private int   _damage    = 75;
        [SerializeField] private float _range     = 200f;
        [SerializeField] private float _onBeatDamageMultiplier = 1.5f;
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Ricochet Special")]
        [SerializeField] private int   _ricochetDamage    = 80;
        [SerializeField] private float _ricochetSpeed     = 18f;
        [SerializeField] private float _ricochetAoeRadius = 3f;
        [SerializeField] private int   _ricochetBounces   = 2;

        protected override void FireProjectile(Transform cameraTransform)
        {
            bool onBeat = BeatClock.Instance != null && BeatClock.Instance.IsOnBeat;
            int  damage = onBeat
                ? Mathf.RoundToInt(_damage * _onBeatDamageMultiplier)
                : _damage;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _range, _hitMask, QueryTriggerInteraction.Ignore))
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
        }

        // ── Ricochet special: slow projectile that bounces off up to 2 walls ──

        public override void TriggerSpecial()
        {
            if (_lastCameraTransform == null) return;

            var go = new GameObject("RicochetBullet");
            go.transform.position = _lastCameraTransform.position
                                  + _lastCameraTransform.forward * 0.5f;
            go.transform.rotation = _lastCameraTransform.rotation;

            var col    = go.AddComponent<SphereCollider>();
            col.radius = 0.06f;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = Vector3.one * 0.12f;
            Object.Destroy(vis.GetComponent<SphereCollider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor",     new Color(1f, 0.85f, 0.1f));
            mat.SetColor("_EmissionColor", new Color(3f, 2.2f,  0.2f));
            mat.EnableKeyword("_EMISSION");
            vis.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var proj = go.AddComponent<Projectile>();
            proj.Init(_ricochetSpeed, _ricochetDamage, _hitMask);
            proj.SetRicochet(_ricochetBounces);
            proj.SetAoeRadius(_ricochetAoeRadius);
        }
    }
}
