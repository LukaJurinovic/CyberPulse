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

        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;
        private Vector2 _shakeOffset;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

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

        private void LateUpdate()
        {
            ApplyLook();
            UpdateWallSlideTilt();
            UpdateHeadBob();
            UpdateFOV();
            UpdateShake();

            transform.localRotation = Quaternion.Euler(_pitch + _dashKickback + _landingDip, 0f, _wallSlideTilt);

            _camera.transform.localPosition = new Vector3(_shakeOffset.x, _bobOffset + _shakeOffset.y, 0f);

            _lookDelta = Vector2.zero;
        }

        /// <summary>Trigger a screen shake. Larger values override smaller in-progress shakes.</summary>
        public void Shake(float intensity, float duration)
        {
            if (intensity > _shakeIntensity)
            {
                _shakeIntensity = intensity;
                _shakeDuration  = duration;
                _shakeTimer     = duration;
            }
        }

        private void UpdateShake()
        {
            if (_shakeTimer <= 0f)
            {
                _shakeOffset = Vector2.zero;
                return;
            }
            _shakeTimer -= Time.unscaledDeltaTime;
            float t         = _shakeDuration > 0f ? (_shakeTimer / _shakeDuration) : 0f;
            float magnitude = _shakeIntensity * t;
            _shakeOffset = new Vector2(
                Random.Range(-1f, 1f) * magnitude,
                Random.Range(-1f, 1f) * magnitude);
        }

        private void ApplyLook()
        {
            _yaw   += _lookDelta.x * _mouseSensitivity;
            _pitch -= _lookDelta.y * _mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, -80f, 80f);

            _controller.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void UpdateWallSlideTilt()
        {
            float target = _controller.IsWallSliding ? _wallSlideTiltAngle : 0f;
            _wallSlideTilt = Mathf.Lerp(_wallSlideTilt, target, Time.deltaTime * _tiltLerpSpeed);
        }

        private void UpdateHeadBob()
        {
            if (!_controller.IsGrounded || _controller.CurrentSpeed < 0.1f)
            {
                _bobOffset = Mathf.Lerp(_bobOffset, 0f, Time.deltaTime * 8f);
                return;
            }

            float speedFactor = Mathf.Clamp01(_controller.CurrentSpeed / _controller.MaxHorizontalSpeed);
            _bobTimer += Time.deltaTime * _bobFrequency;
            _bobOffset = Mathf.Sin(_bobTimer) * _bobAmplitude * speedFactor;
        }

        private void UpdateFOV()
        {
            float t = Mathf.Clamp01(_controller.CurrentSpeed / _controller.MaxHorizontalSpeed);
            float target = Mathf.Lerp(_baseFOV, _sprintFOV, t);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, target, Time.deltaTime * _fovLerpSpeed);
        }

        private IEnumerator DashKickbackRoutine()
        {
            float elapsed = 0f;
            float startAngle = _dashKickback;

            while (elapsed < _kickInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _dashKickback = Mathf.Lerp(startAngle, _kickbackAngle, elapsed / _kickInDuration);
                yield return null;
            }
            _dashKickback = _kickbackAngle;

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

        private void TriggerLandingDip()
        {
            if (_landingDipCoroutine != null)
                StopCoroutine(_landingDipCoroutine);
            _landingDipCoroutine = StartCoroutine(LandingDipRoutine());
        }

        private IEnumerator LandingDipRoutine()
        {
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
