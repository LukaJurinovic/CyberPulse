using System.Collections;
using UnityEngine;
using CyberPulse.Input;

namespace CyberPulse.Player
{
    /// <summary>
    /// FPS camera controller. Attach to the <b>CameraPivot</b> child of the Player root.
    /// Handles mouse/stick look, dynamic FOV, head bob, dash kickback, and wall-slide roll.
    /// The actual <see cref="Camera"/> component must live on the <b>MainCamera</b> child of
    /// CameraPivot and be assigned to <c>_camera</c> in the Inspector.
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private InputReader _input;
        [SerializeField] private PlayerController _controller;
        [SerializeField] private DashAbility _dash;

        [Header("Camera Child (MainCamera)")]
        [SerializeField] private Camera _camera;

        [Header("Look")]
        [SerializeField] private float _mouseSensitivity = 0.15f;

        [Header("FOV")]
        [SerializeField] private float _baseFOV = 75f;
        [SerializeField] private float _sprintFOV = 100f;
        [SerializeField] private float _fovLerpSpeed = 10f;

        [Header("Head Bob")]
        [SerializeField] private float _bobFrequency = 8f;
        [SerializeField] private float _bobAmplitude = 0.012f;

        [Header("Landing Dip")]
        [SerializeField] private float _landingDipAngle = 3f;
        [SerializeField] private float _landingDipDuration = 0.18f;

        [Header("Dash Kickback")]
        [SerializeField] private float _kickbackAngle = -6f;
        [SerializeField] private float _kickInDuration = 0.08f;
        [SerializeField] private float _kickOutDuration = 0.2f;

        [Header("Wall-Slide Tilt")]
        [SerializeField] private float _wallSlideTiltAngle = 3f;
        [SerializeField] private float _tiltLerpSpeed = 5f;

        private float _yaw;
        private float _pitch;
        private float _bobTimer;
        private float _bobOffset;
        private float _dashKickback;
        private float _landingDip;
        private float _wallSlideTilt;
        private Vector2 _lookDelta;
        private Coroutine _kickbackCoroutine;
        private Coroutine _landingDipCoroutine;

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Initialise yaw from player's current facing so there's no snap on start.
            _yaw = _controller.transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            _input.LookInput      += HandleLook;
            _dash.OnDashPerformed += TriggerKickback;
            _controller.OnLanded  += TriggerLandingDip;
        }

        private void OnDisable()
        {
            _input.LookInput      -= HandleLook;
            _dash.OnDashPerformed -= TriggerKickback;
            _controller.OnLanded  -= TriggerLandingDip;
        }

        private void HandleLook(Vector2 delta) => _lookDelta = delta;

        private void TriggerKickback()
        {
            if (_kickbackCoroutine != null)
                StopCoroutine(_kickbackCoroutine);
            _kickbackCoroutine = StartCoroutine(DashKickbackRoutine());
        }

        // ──────────────────────────────────────────────────────────────────────
        // Per-frame updates (LateUpdate so all physics have settled)
        // ──────────────────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            ApplyLook();
            UpdateWallSlideTilt();
            UpdateHeadBob();
            UpdateFOV();

            // Compose all rotation contributions on the pivot.
            transform.localRotation = Quaternion.Euler(_pitch + _dashKickback + _landingDip, 0f, _wallSlideTilt);

            // Head bob is applied to the camera child's local position.
            _camera.transform.localPosition = new Vector3(0f, _bobOffset, 0f);

            _lookDelta = Vector2.zero;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Look
        // ──────────────────────────────────────────────────────────────────────

        private void ApplyLook()
        {
            _yaw   += _lookDelta.x * _mouseSensitivity;
            _pitch -= _lookDelta.y * _mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, -80f, 80f);

            // Yaw rotates the entire player root (so the collider faces the right way).
            _controller.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Wall-slide tilt
        // ──────────────────────────────────────────────────────────────────────

        private void UpdateWallSlideTilt()
        {
            float target = _controller.IsWallSliding ? _wallSlideTiltAngle : 0f;
            _wallSlideTilt = Mathf.Lerp(_wallSlideTilt, target, Time.deltaTime * _tiltLerpSpeed);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Head bob
        // ──────────────────────────────────────────────────────────────────────

        private void UpdateHeadBob()
        {
            if (!_controller.IsGrounded || _controller.CurrentSpeed < 0.1f)
            {
                // Smoothly return to zero when airborne or stationary.
                _bobOffset = Mathf.Lerp(_bobOffset, 0f, Time.deltaTime * 8f);
                return;
            }

            float speedFactor = Mathf.Clamp01(_controller.CurrentSpeed / _controller.MaxHorizontalSpeed);
            _bobTimer += Time.deltaTime * _bobFrequency;
            _bobOffset = Mathf.Sin(_bobTimer) * _bobAmplitude * speedFactor;
        }

        // ──────────────────────────────────────────────────────────────────────
        // FOV
        // ──────────────────────────────────────────────────────────────────────

        private void UpdateFOV()
        {
            // FOV scales continuously with speed: 0 → baseFOV, max speed → sprintFOV.
            float t = Mathf.Clamp01(_controller.CurrentSpeed / _controller.MaxHorizontalSpeed);
            float target = Mathf.Lerp(_baseFOV, _sprintFOV, t);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, target, Time.deltaTime * _fovLerpSpeed);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Dash kickback animation (uses unscaled time so it survives time-scale changes)
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator DashKickbackRoutine()
        {
            // Kick in: snap toward _kickbackAngle over kickInDuration.
            float elapsed = 0f;
            float startAngle = _dashKickback;

            while (elapsed < _kickInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _dashKickback = Mathf.Lerp(startAngle, _kickbackAngle, elapsed / _kickInDuration);
                yield return null;
            }
            _dashKickback = _kickbackAngle;

            // Spring back: lerp back to 0 over kickOutDuration.
            elapsed = 0f;
            while (elapsed < _kickOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _dashKickback = Mathf.Lerp(_kickbackAngle, 0f, elapsed / _kickOutDuration);
                yield return null;
            }
            _dashKickback = 0f;
            _kickbackCoroutine = null;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Landing dip — camera nods forward on impact
        // ──────────────────────────────────────────────────────────────────────

        private void TriggerLandingDip()
        {
            if (_landingDipCoroutine != null)
                StopCoroutine(_landingDipCoroutine);
            _landingDipCoroutine = StartCoroutine(LandingDipRoutine());
        }

        private IEnumerator LandingDipRoutine()
        {
            // Quick snap down (35% of duration), then spring back (65%).
            float dipIn  = _landingDipDuration * 0.35f;
            float dipOut = _landingDipDuration * 0.65f;
            float elapsed = 0f;

            while (elapsed < dipIn)
            {
                elapsed += Time.unscaledDeltaTime;
                _landingDip = Mathf.Lerp(0f, _landingDipAngle, elapsed / dipIn);
                yield return null;
            }
            _landingDip = _landingDipAngle;

            elapsed = 0f;
            while (elapsed < dipOut)
            {
                elapsed += Time.unscaledDeltaTime;
                _landingDip = Mathf.Lerp(_landingDipAngle, 0f, elapsed / dipOut);
                yield return null;
            }
            _landingDip = 0f;
            _landingDipCoroutine = null;
        }
    }
}
