using System;
using System.Collections;
using UnityEngine;
using CyberPulse.Systems;

namespace CyberPulse.Weapons
{

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
        [SerializeField] private int _maxReserveAmmo = 300;

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
        private Coroutine _fadeCoroutine;

        protected Transform _lastCameraTransform;

        public string WeaponName  => _weaponName;
        public float SpecialCost  => _specialCost;
        public int CurrentAmmo    => _currentAmmo;
        public int ReserveAmmo    => _reserveAmmo;
        public int MagazineSize   => _magazineSize;
        public bool IsReloading   => _isReloading;
        public bool IsAutomatic   => _isAutomatic;

        public static float RhythmFireMultiplier { get; set; } = 1f;

        public static int LastFiredWeaponSlotIndex { get; set; } = 0;

        public event Action OnAmmoChanged;

        public static event Action OnAnyWeaponFired;

        protected virtual void Awake()
        {
            _currentAmmo = _magazineSize;
        }

        public bool TryFire(Transform cameraTransform)
        {
            if (_isReloading) return false;
            if (Time.time < _nextFireTime) return false;

            if (_currentAmmo <= 0)
            {
                StopFireAudio();
                PlayAudio(_emptyClip, 0.5f);
                TryReload();
                return false;
            }

            _currentAmmo--;
            _lastCameraTransform = cameraTransform;
            _nextFireTime = Time.time + 1f / (_fireRate * RhythmFireMultiplier);

            FireProjectile(cameraTransform);
            PlayMuzzleFlash();
            if (UseLoopedFireAudio()) StartLoopFireAudio(); else PlayAudio(_fireClip, 1f);
            OnAmmoChanged?.Invoke();
            OnAnyWeaponFired?.Invoke();
            return true;
        }

        public void TryReload()
        {
            if (_isReloading) return;
            if (_currentAmmo == _magazineSize) return;
            if (_reserveAmmo <= 0) return;

            if (BeatClock.Instance != null && BeatClock.Instance.IsOnBeat)
            {
                PlayAudio(_reloadClip, 1f);
                ApplyReload();
                return;
            }

            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        public void AddReserveAmmo(int amount)
        {
            if (amount <= 0) return;
            _reserveAmmo = Mathf.Min(_reserveAmmo + amount, _maxReserveAmmo);
            OnAmmoChanged?.Invoke();
        }


        public void CancelReload()
        {
            if (!_isReloading) return;
            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            if (_audioSource != null) { _audioSource.Stop(); _audioSource.pitch = 1f; }
            _isReloading = false;
        }


        protected abstract void FireProjectile(Transform cameraTransform);

        protected virtual bool UseLoopedFireAudio() => false;

        public virtual void TriggerSpecial() { }

        protected void FireBurstShot(Transform cam)
        {
            if (_currentAmmo <= 0) return;
            _currentAmmo--;
            FireProjectile(cam);
            PlayMuzzleFlash();
            if (UseLoopedFireAudio()) StartLoopFireAudio(); else PlayAudio(_fireClip, 1f);
            OnAmmoChanged?.Invoke();
            OnAnyWeaponFired?.Invoke();
        }

        private IEnumerator ReloadRoutine()
        {
            _isReloading = true;

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

        private void StartLoopFireAudio()
        {
            if (_audioSource == null || _fireClip == null) return;
            if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; _audioSource.volume = 1f; }
            if (_audioSource.isPlaying && _audioSource.clip == _fireClip) return;
            _audioSource.volume = 1f;
            _audioSource.clip = _fireClip;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        public void StopFireAudio()
        {
            if (_audioSource == null || !UseLoopedFireAudio()) return;
            if (!_audioSource.isPlaying || _audioSource.clip != _fireClip) return;
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutFireAudio(0.25f));
        }

        private IEnumerator FadeOutFireAudio(float duration)
        {
            float startVol = _audioSource.volume;
            float elapsed = 0f;
            while (elapsed < duration && _audioSource.clip == _fireClip)
            {
                elapsed += Time.unscaledDeltaTime;
                _audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            if (_audioSource.clip == _fireClip)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }
            _audioSource.volume = 1f;
            _fadeCoroutine = null;
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
