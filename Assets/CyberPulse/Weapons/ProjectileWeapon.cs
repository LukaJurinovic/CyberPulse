using UnityEngine;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// Weapon that instantiates a <see cref="Projectile"/> prefab on fire.
    /// Assign a prefab with a <see cref="Projectile"/> component to <c>_projectilePrefab</c>.
    /// </summary>
    public class ProjectileWeapon : WeaponBase
    {
        [Header("Projectile")]
        [SerializeField] private Projectile _projectilePrefab;
        [SerializeField] private Transform _muzzlePoint;

        protected override void FireProjectile(Transform cameraTransform)
        {
            Transform spawnFrom = _muzzlePoint != null ? _muzzlePoint : cameraTransform;
            Instantiate(_projectilePrefab, spawnFrom.position, cameraTransform.rotation);
        }
    }
}
