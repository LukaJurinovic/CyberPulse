using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberPulse.Input
{
    /// <summary>
    /// ScriptableObject that wraps the generated <see cref="PlayerInputActions"/> and
    /// exposes C# events for every player action.
    /// Create one asset via Assets > Create > CyberPulse > Input Reader and assign
    /// it to every component that needs input — no singletons required.
    /// </summary>
    [CreateAssetMenu(menuName = "CyberPulse/Input Reader")]
    public class InputReader : ScriptableObject, PlayerInputActions.IPlayerActions
    {
        private PlayerInputActions _actions;

        public event Action<Vector2> MoveInput;
        public event Action<Vector2> LookInput;
        public event Action JumpInput;
        public event Action DashInput;

        public event Action FirePressed;
        public event Action FireReleased;
        public event Action AltFirePressed;
        public event Action AltFireReleased;
        public event Action ReloadInput;
        public event Action<Vector2> WeaponScrollInput;
        public event Action WeaponSlot1Input;
        public event Action WeaponSlot2Input;
        public event Action WeaponSlot3Input;

        private void OnEnable()
        {
            if (_actions == null)
            {
                _actions = new PlayerInputActions();
                _actions.Player.SetCallbacks(this);
            }
            _actions.Player.Enable();
        }

        private void OnDisable()
        {
            _actions?.Player.Disable();
        }

        void PlayerInputActions.IPlayerActions.OnMove(InputAction.CallbackContext context)
            => MoveInput?.Invoke(context.ReadValue<Vector2>());

        void PlayerInputActions.IPlayerActions.OnLook(InputAction.CallbackContext context)
            => LookInput?.Invoke(context.ReadValue<Vector2>());

        void PlayerInputActions.IPlayerActions.OnJump(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                JumpInput?.Invoke();
        }

        void PlayerInputActions.IPlayerActions.OnDash(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                DashInput?.Invoke();
        }

        void PlayerInputActions.IPlayerActions.OnFire(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed) FirePressed?.Invoke();
            else if (context.phase == InputActionPhase.Canceled) FireReleased?.Invoke();
        }

        void PlayerInputActions.IPlayerActions.OnAltFire(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed) AltFirePressed?.Invoke();
            else if (context.phase == InputActionPhase.Canceled) AltFireReleased?.Invoke();
        }

        void PlayerInputActions.IPlayerActions.OnReload(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                ReloadInput?.Invoke();
        }

        void PlayerInputActions.IPlayerActions.OnWeaponScroll(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                WeaponScrollInput?.Invoke(context.ReadValue<Vector2>());
        }

        void PlayerInputActions.IPlayerActions.OnWeaponSlot1(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                WeaponSlot1Input?.Invoke();
        }

        void PlayerInputActions.IPlayerActions.OnWeaponSlot2(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                WeaponSlot2Input?.Invoke();
        }

        void PlayerInputActions.IPlayerActions.OnWeaponSlot3(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
                WeaponSlot3Input?.Invoke();
        }
    }
}
