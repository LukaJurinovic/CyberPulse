using UnityEngine;
using CyberPulse.Input;
using CyberPulse.Systems;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// Manages the equipped weapon array. Handles all weapon-related input forwarding,
    /// weapon switching (scroll wheel + number keys), and passes the camera transform
    /// to the active weapon for firing direction.
    /// Attach to the Player root. Weapons should be children of a WeaponSocket transform.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private InputReader _input;
        [SerializeField] private Transform _cameraTransform;

        [Header("Weapons")]
        [SerializeField] private WeaponBase[] _weapons;

        [Header("Rhythm")]
        [SerializeField] private float _offBeatSwitchDelay = 0.4f;

        private int   _activeIndex;
        private bool  _isFiring;
        private bool  _switchPending;
        private int   _pendingIndex;
        private float _switchTimer;

        // ──────────────────────────────────────────────────────────────────────
        // Public state
        // ──────────────────────────────────────────────────────────────────────

        public WeaponBase ActiveWeapon => _weapons != null && _weapons.Length > 0
            ? _weapons[_activeIndex]
            : null;

        public int ActiveIndex => _activeIndex;

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            ActivateWeaponAt(_activeIndex, silent: true);
        }

        private void OnEnable()
        {
            _input.FirePressed        += OnFirePressed;
            _input.FireReleased       += OnFireReleased;
            _input.ReloadInput        += OnReload;
            _input.WeaponScrollInput  += OnWeaponScroll;
            _input.WeaponSlot1Input   += () => SwitchTo(0);
            _input.WeaponSlot2Input   += () => SwitchTo(1);
            _input.WeaponSlot3Input   += () => SwitchTo(2);
            _input.AltFirePressed     += OnAltFirePressed;
        }

        private void OnDisable()
        {
            _input.FirePressed        -= OnFirePressed;
            _input.FireReleased       -= OnFireReleased;
            _input.ReloadInput        -= OnReload;
            _input.WeaponScrollInput  -= OnWeaponScroll;
            _input.WeaponSlot1Input   -= () => SwitchTo(0);
            _input.WeaponSlot2Input   -= () => SwitchTo(1);
            _input.WeaponSlot3Input   -= () => SwitchTo(2);
            _input.AltFirePressed     -= OnAltFirePressed;
        }

        private void Update()
        {
            if (_switchPending)
            {
                _switchTimer -= Time.deltaTime;
                if (_switchTimer <= 0f)
                {
                    _switchPending = false;
                    ExecuteSwitch(_pendingIndex);
                }
            }

            if (_isFiring && ActiveWeapon != null && ActiveWeapon.IsAutomatic)
            {
                WeaponBase.LastFiredWeaponSlotIndex = _activeIndex;
                ActiveWeapon.TryFire(_cameraTransform);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Input handlers
        // ──────────────────────────────────────────────────────────────────────

        private void OnFirePressed()
        {
            _isFiring = true;
            WeaponBase.LastFiredWeaponSlotIndex = _activeIndex;
            ActiveWeapon?.TryFire(_cameraTransform);
        }

        private void OnFireReleased() => _isFiring = false;

        private void OnReload() => ActiveWeapon?.TryReload();

        private void OnAltFirePressed()
        {
            if (ActiveWeapon != null)
                SyncGauge.Instance?.TrySpend(ActiveWeapon.SpecialCost, ActiveWeapon);
        }

        private void OnWeaponScroll(Vector2 scroll)
        {
            if (_weapons == null || _weapons.Length == 0) return;
            int dir = scroll.y > 0f ? -1 : 1;
            int next = (_activeIndex + dir + _weapons.Length) % _weapons.Length;
            SwitchTo(next);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Weapon switching
        // ──────────────────────────────────────────────────────────────────────

        private void SwitchTo(int index)
        {
            if (_weapons == null || index >= _weapons.Length || index < 0) return;
            if (index == _activeIndex) return;

            bool onBeat = BeatClock.Instance != null && BeatClock.Instance.IsOnBeat;
            if (onBeat)
            {
                _switchPending = false;
                ExecuteSwitch(index);
            }
            else
            {
                _switchPending = true;
                _pendingIndex  = index;
                _switchTimer   = _offBeatSwitchDelay;
            }
        }

        private void ExecuteSwitch(int index)
        {
            ActiveWeapon?.CancelReload();
            _activeIndex = index;
            _isFiring    = false;
            ActivateWeaponAt(_activeIndex, silent: false);
        }

        private void ActivateWeaponAt(int index, bool silent)
        {
            if (_weapons == null) return;
            for (int i = 0; i < _weapons.Length; i++)
            {
                if (_weapons[i] != null)
                    _weapons[i].gameObject.SetActive(i == index);
            }
        }
    }
}
