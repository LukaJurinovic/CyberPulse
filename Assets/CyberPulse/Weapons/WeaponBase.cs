using System;
using System.Collections;
using UnityEngine;
using CyberPulse.Systems;

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

        [Header("SYNC Special")]
        [SerializeField] private float _specialCost = 60f;

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

        /// <summary>Camera transform from the last TryFire call. Subclasses can read this in TriggerSpecial.</summary>
        protected Transform _lastCameraTransform;

        // ──────────────────────────────────────────────────────────────────────
        // Public state
        // ──────────────────────────────────────────────────────────────────────

        public string WeaponName  => _weaponName;
        public float SpecialCost  => _specialCost;
        public int CurrentAmmo    => _currentAmmo;
        public int ReserveAmmo    => _reserveAmmo;
        public int MagazineSize   => _magazineSize;
        public bool IsReloading   => _isReloading;
        public bool IsAutomatic   => _isAutomatic;

        /// <summary>Set by BeatReactor — reduces fire rate to 60% while off-rhythm.</summary>
        public static float RhythmFireMultiplier { get; set; } = 1f;

        /// <summary>Index of the weapon slot last used to fire. ScoreManager reads this for variety bonus.</summary>
        public static int LastFiredWeaponSlotIndex { get; set; } = 0;

        /// <summary>Fires when ammo state changes so the HUD can update.</summary>
        public event Action OnAmmoChanged;

        /// <summary>Fires on any weapon's successful shot. BeatReactor uses this.</summary>
        public static event Action OnAnyWeaponFired;

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
            _lastCameraTransform = cameraTransform;
            _nextFireTime = Time.time + 1f / (_fireRate * RhythmFireMultiplier);

            FireProjectile(cameraTransform);
            PlayMuzzleFlash();
            PlayAudio(_fireClip, 1f);
            OnAmmoChanged?.Invoke();
            OnAnyWeaponFired?.Invoke();
            return true;
        }

        /// <summary>Start a reload if the magazine is not full and reserve ammo exists. Instant on-beat.</summary>
        public void TryReload()
        {
            if (_isReloading) return;
            if (_currentAmmo == _magazineSize) return;
            if (_reserveAmmo <= 0) return;

            if (BeatClock.Instance != null && BeatClock.Instance.IsOnBeat)
            {
                // Instant on-beat reload: play clip immediately at normal pitch as feedback.
                PlayAudio(_reloadClip, 1f);
                ApplyReload();
                return;
            }

            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        /// <summary>Cancel an in-progress reload (e.g., when weapon is switched away).</summary>
        public void CancelReload()
        {
            if (!_isReloading) return;
            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            if (_audioSource != null) { _audioSource.Stop(); _audioSource.pitch = 1f; }
            _isReloading = false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Subclass contract
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Perform the actual shot — raycast or instantiate projectile.</summary>
        protected abstract void FireProjectile(Transform cameraTransform);

        /// <summary>
        /// Called by SyncGauge when the player spends SYNC on this weapon's special.
        /// Override in each weapon subclass. Base is a no-op so subclasses without a
        /// special defined yet compile cleanly.
        /// </summary>
        public virtual void TriggerSpecial() { }

        /// <summary>
        /// Fires one shot bypassing cooldown — for burst specials.
        /// Handles ammo, muzzle flash, audio, and events.
        /// </summary>
        protected void FireBurstShot(Transform cam)
        {
            if (_currentAmmo <= 0) return;
            _currentAmmo--;
            FireProjectile(cam);
            PlayMuzzleFlash();
            PlayAudio(_fireClip, 1f);
            OnAmmoChanged?.Invoke();
            OnAnyWeaponFired?.Invoke();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator ReloadRoutine()
        {
            _isReloading = true;

            // Pitch-shift the clip so it finishes exactly when _reloadDuration expires.
            // This keeps the audio in lockstep with the reload timer regardless of clip length.
            if (_audioSource != null && _reloadClip != null && _reloadDuration > 0f)
            {
                _audioSource.pitch = _reloadClip.length / _reloadDuration;
                _audioSource.clip  = _reloadClip;
                _audioSource.Play();
            }

            yield return new WaitForSeconds(_reloadDuration);

            if (_audioSource != null) _audioSource.pitch = 1f;
            ApplyReload();
        }

        private void ApplyReload()
        {
            int needed       = _magazineSize - _currentAmmo;
            int taken        = Mathf.Min(needed, _reserveAmmo);
            _currentAmmo    += taken;
            _reserveAmmo    -= taken;
            _isReloading     = false;
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
