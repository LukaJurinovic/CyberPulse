using System;
using System.Collections;
using UnityEngine;
using CyberPulse.Input;

namespace CyberPulse.Player
{
    /// <summary>
    /// Adds a directional dash impulse to the Rigidbody with a timed cooldown.
    /// Dash direction is camera forward projected onto the XZ plane.
    /// Fires <see cref="OnDashPerformed"/> immediately after the impulse so camera
    /// and UI can react.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DashAbility : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputReader _input;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private ParticleSystem _dashParticles;

        [Header("Dash")]
        [SerializeField] private float _dashForce = 40f;
        [SerializeField] private float _cooldownDuration = 1.2f;
        [SerializeField] private float _dashStateDuration = 0.2f;

        private Rigidbody _rb;
        private PlayerController _controller;
        private float _cooldownTimer;

        // ──────────────────────────────────────────────────────────────────────
        // Public state
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Fires immediately when a dash executes (after force is applied).</summary>
        public event Action OnDashPerformed;

        /// <summary>0 when the dash is ready; 1 immediately after use. Suitable for a UI cooldown bar.</summary>
        public float CooldownProgress => _cooldownDuration > 0f
            ? Mathf.Clamp01(_cooldownTimer / _cooldownDuration)
            : 0f;

        /// <summary>Seconds remaining on the cooldown. 0 means ready.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

        /// <summary>True when the cooldown has expired and a dash can be performed.</summary>
        public bool CanDash => _cooldownTimer <= 0f;

        /// <summary>Immediately clears the cooldown. Called by BeatReactor on an on-beat dash (P1).</summary>
        public void ResetCooldown() => _cooldownTimer = 0f;

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _controller = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            _input.DashInput += HandleDash;
        }

        private void OnDisable()
        {
            _input.DashInput -= HandleDash;
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Dash logic
        // ──────────────────────────────────────────────────────────────────────

        private void HandleDash()
        {
            if (!CanDash) return;

            Vector3 camForward = _cameraTransform.forward;
            Vector3 dashDir = new Vector3(camForward.x, 0f, camForward.z);

            // Fallback to transform forward when looking straight up/down.
            if (dashDir.sqrMagnitude < 0.001f)
                dashDir = transform.forward;

            dashDir.Normalize();

            _rb.AddForce(dashDir * _dashForce, ForceMode.Impulse);
            _cooldownTimer = _cooldownDuration;

            if (_dashParticles != null)
            {
                _dashParticles.transform.position = transform.position;
                _dashParticles.transform.rotation = Quaternion.LookRotation(-dashDir);
                _dashParticles.Play();
            }

            OnDashPerformed?.Invoke();
            StartCoroutine(DashStateRoutine());
        }

        /// <summary>
        /// Keeps <see cref="PlayerController.IsDashing"/> true for the visual duration
        /// of the dash, then resets it.
        /// </summary>
        private IEnumerator DashStateRoutine()
        {
            _controller.IsDashing = true;
            yield return new WaitForSeconds(_dashStateDuration);
            _controller.IsDashing = false;
        }
    }
}
