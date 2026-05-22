using UnityEngine;
using CyberPulse.Input;

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

        private int _activeIndex;
        private bool _isFiring;

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
        }

        private void Update()
        {
            // Auto-fire: keep calling TryFire each frame while fire is held.
            if (_isFiring && ActiveWeapon != null && ActiveWeapon.IsAutomatic)
                ActiveWeapon.TryFire(_cameraTransform);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Input handlers
        // ──────────────────────────────────────────────────────────────────────

        private void OnFirePressed()
        {
            _isFiring = true;
            // Fire immediately on press; Update() handles auto-fire continuation.
            ActiveWeapon?.TryFire(_cameraTransform);
        }

        private void OnFireReleased() => _isFiring = false;

        private void OnReload() => ActiveWeapon?.TryReload();

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

            ActiveWeapon?.CancelReload();
            _activeIndex = index;
            _isFiring = false;
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
