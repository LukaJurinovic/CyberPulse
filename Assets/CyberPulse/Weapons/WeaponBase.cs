using System;
using System.Collections;
using UnityEngine;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// Base class for all weapons. Handles fire rate gating, magazine/reserve ammo,
    /// reload timing, muzzle flash, and audio. Subclasses implement <see cref="FireProjectile"/>.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _weaponName = "Weapon";

        [Header("Fire Rate")]
        [SerializeField] private float _fireRate = 10f;
        [SerializeField] private bool _isAutomatic = true;

        [Header("Ammo")]
        [SerializeField] private int _magazineSize = 30;
        [SerializeField] private int _reserveAmmo = 90;

        [Header("Reload")]
        [SerializeField] private float _reloadDuration = 1.8f;

        [Header("Muzzle Flash")]
        [SerializeField] private ParticleSystem _muzzleFlash;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _fireClip;
        [SerializeField] private AudioClip _reloadClip;
        [SerializeField] private AudioClip _emptyClip;

        private int _currentAmmo;
        private float _nextFireTime;
        private bool _isReloading;
        private Coroutine _reloadCoroutine;

        // ──────────────────────────────────────────────────────────────────────
        // Public state
        // ──────────────────────────────────────────────────────────────────────

        public string WeaponName  => _weaponName;
        public int CurrentAmmo    => _currentAmmo;
        public int ReserveAmmo    => _reserveAmmo;
        public int MagazineSize   => _magazineSize;
        public bool IsReloading   => _isReloading;
        public bool IsAutomatic   => _isAutomatic;

        /// <summary>Fires when ammo state changes so the HUD can update.</summary>
        public event Action OnAmmoChanged;

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _currentAmmo = _magazineSize;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Public API called by WeaponHolder
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Attempt to fire. Returns false if gated by cooldown, reloading, or empty.</summary>
        public bool TryFire(Transform cameraTransform)
        {
            if (_isReloading) return false;
            if (Time.time < _nextFireTime) return false;

            if (_currentAmmo <= 0)
            {
                PlayAudio(_emptyClip, 0.5f);
                // Auto-reload on empty
                TryReload();
                return false;
            }

            _currentAmmo--;
            _nextFireTime = Time.time + 1f / _fireRate;

            FireProjectile(cameraTransform);
            PlayMuzzleFlash();
            PlayAudio(_fireClip, 1f);
            OnAmmoChanged?.Invoke();
            return true;
        }

        /// <summary>Start a reload if the magazine is not full and reserve ammo exists.</summary>
        public void TryReload()
        {
            if (_isReloading) return;
            if (_currentAmmo == _magazineSize) return;
            if (_reserveAmmo <= 0) return;

            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        /// <summary>Cancel an in-progress reload (e.g., when weapon is switched away).</summary>
        public void CancelReload()
        {
            if (!_isReloading) return;
            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            _isReloading = false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Subclass contract
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Perform the actual shot — raycast or instantiate projectile.</summary>
        protected abstract void FireProjectile(Transform cameraTransform);

        // ──────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator ReloadRoutine()
        {
            _isReloading = true;
            PlayAudio(_reloadClip, 1f);

            yield return new WaitForSeconds(_reloadDuration);

            int needed = _magazineSize - _currentAmmo;
            int taken  = Mathf.Min(needed, _reserveAmmo);
            _currentAmmo  += taken;
            _reserveAmmo  -= taken;
            _isReloading   = false;
            _reloadCoroutine = null;
            OnAmmoChanged?.Invoke();
        }

        private void PlayMuzzleFlash()
        {
            if (_muzzleFlash != null)
                _muzzleFlash.Play();
        }

        private void PlayAudio(AudioClip clip, float volume)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip, volume);
        }
    }
}
