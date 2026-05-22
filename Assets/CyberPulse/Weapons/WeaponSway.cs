using UnityEngine;
using CyberPulse.Input;
using CyberPulse.Player;

namespace CyberPulse.Weapons
{
    /// <summary>
    /// Procedural weapon sway: look-lag, movement bob, and idle breathing.
    /// Attach to the weapon root object (child of the camera).
    /// </summary>
    public class WeaponSway : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private InputReader _input;
        [SerializeField] private PlayerController _controller;

        [Header("Look Sway")]
        [SerializeField] private float _swayAmount = 0.04f;
        [SerializeField] private float _swaySmoothing = 8f;
        [SerializeField] private float _maxSwayOffset = 0.08f;

        [Header("Tilt")]
        [SerializeField] private float _tiltAmount = 4f;
        [SerializeField] private float _tiltSmoothing = 8f;

        [Header("Movement Bob")]
        [SerializeField] private float _bobFrequency = 9f;
        [SerializeField] private float _bobAmplitudeX = 0.005f;
        [SerializeField] private float _bobAmplitudeY = 0.008f;

        [Header("Idle Breathing")]
        [SerializeField] private float _breathFrequency = 0.8f;
        [SerializeField] private float _breathAmplitude = 0.0015f;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector2 _lookDelta;
        private float _bobTimer;

        private void Awake()
        {
            _initialPosition = transform.localPosition;
            _initialRotation = transform.localRotation;
        }

        private void OnEnable()  => _input.LookInput += HandleLook;
        private void OnDisable() => _input.LookInput -= HandleLook;

        private void HandleLook(Vector2 delta) => _lookDelta = delta;

        private void Update()
        {
            UpdateSway();
            UpdateBob();
            _lookDelta = Vector2.zero;
        }

        private void UpdateSway()
        {
            float swayX = Mathf.Clamp(-_lookDelta.x * _swayAmount, -_maxSwayOffset, _maxSwayOffset);
            float swayY = Mathf.Clamp(-_lookDelta.y * _swayAmount, -_maxSwayOffset, _maxSwayOffset);

            Vector3 targetPos = _initialPosition + new Vector3(swayX, swayY, 0f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos,
                Time.deltaTime * _swaySmoothing);

            float tilt = Mathf.Clamp(_lookDelta.x * _tiltAmount, -_tiltAmount * 2f, _tiltAmount * 2f);
            Quaternion targetRot = _initialRotation * Quaternion.Euler(0f, 0f, -tilt);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot,
                Time.deltaTime * _tiltSmoothing);
        }

        private void UpdateBob()
        {
            bool isMoving = _controller.IsGrounded && _controller.CurrentSpeed > 0.1f;

            if (isMoving)
            {
                float speedFactor = Mathf.Clamp01(_controller.CurrentSpeed / _controller.MaxHorizontalSpeed);
                _bobTimer += Time.deltaTime * _bobFrequency * speedFactor;

                float bobX = Mathf.Cos(_bobTimer) * _bobAmplitudeX;
                float bobY = Mathf.Abs(Mathf.Sin(_bobTimer)) * _bobAmplitudeY;

                transform.localPosition += new Vector3(bobX, bobY, 0f);
            }
            else
            {
                // Idle breathing
                float breath = Mathf.Sin(Time.time * _breathFrequency * Mathf.PI * 2f) * _breathAmplitude;
                transform.localPosition += new Vector3(0f, breath, 0f);
            }
        }
    }
}
