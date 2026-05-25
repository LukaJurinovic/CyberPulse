using System;
using UnityEngine;
using CyberPulse.Input;

namespace CyberPulse.Player
{
    /// <summary>
    /// Core physics-based FPS movement controller.
    /// All forces are applied via <c>Rigidbody.AddForce</c> in FixedUpdate.
    /// Horizontal velocity is clamped rather than set directly.
    /// Supports coyote time, jump buffering, and wall-slide detection.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputReader _input;

        [Header("Camera Reference — assign the CameraPivot transform")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Movement")]
        [SerializeField] private float _moveForce = 140f;
        [SerializeField] private float _airStrafeForce = 45f;
        [SerializeField] private float _maxHorizontalSpeed = 18f;
        [SerializeField] private float _groundDrag = 12f;
        [SerializeField] private float _airDrag = 0.5f;

        [Header("Jump")]
        [SerializeField] private float _jumpForce = 6.26f; // sqrt(2 * 9.81 * 2m) — peak height formula (mass = 1)
        [SerializeField] private int   _maxJumps = 2;
        [SerializeField] private float _coyoteTime = 0.12f;
        [SerializeField] private float _jumpBufferTime = 0.15f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _groundCheckRadius = 0.25f;
        [SerializeField] private float _groundCheckDistance = 0.15f;

        [Header("Wall Slide")]
        [SerializeField] private float _wallCheckDistance = 0.65f;
        [SerializeField] private float _maxWallSlideSpeed = -2f;

        [Header("Feel")]
        [SerializeField] private float _fallGravityMultiplier = 1.4f;

        private Rigidbody _rb;
        private CapsuleCollider _col;
        private Vector2 _moveInput;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private int _jumpsRemaining;
        private float _lastLandTime;

        // Four cardinal directions pre-allocated to avoid per-frame allocation.
        private static readonly Vector3[] WallDirs =
            { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        // ──────────────────────────────────────────────────────────────────────
        // Public state (read-only properties)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>True when the player is standing on a Ground-layer surface.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>True when the player is sliding down a wall (clamped fall speed).</summary>
        public bool IsWallSliding { get; private set; }

        /// <summary>Set externally by <see cref="DashAbility"/> for the impulse duration.</summary>
        public bool IsDashing { get; set; }

        /// <summary>Horizontal speed magnitude (XZ plane, excludes vertical).</summary>
        public float CurrentSpeed => new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;

        /// <summary>Full world-space Rigidbody velocity.</summary>
        public Vector3 Velocity => _rb.linearVelocity;

        /// <summary>Configured max horizontal speed; exposed for camera head-bob scaling.</summary>
        public float MaxHorizontalSpeed => _maxHorizontalSpeed;

        /// <summary>Seconds remaining in the coyote window (0 when expired or grounded).</summary>
        public float CoyoteTimeRemaining => Mathf.Max(0f, _coyoteTimer);

        /// <summary>Seconds remaining in the jump-buffer window (0 when expired).</summary>
        public float JumpBufferRemaining => Mathf.Max(0f, _jumpBufferTimer);

        /// <summary>Maximum number of jumps allowed per grounded session.</summary>
        public int MaxJumps => _maxJumps;

        /// <summary>Aerial jumps still available this airborne session (resets on landing).</summary>
        public int JumpsRemaining => _jumpsRemaining;

        /// <summary>Fires on the frame the player's feet touch a Ground-layer surface.</summary>
        public event Action OnLanded;

        // ──────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<CapsuleCollider>();
            _jumpsRemaining = _maxJumps;
        }

        private void OnEnable()
        {
            _input.MoveInput += HandleMove;
            _input.JumpInput += HandleJumpRequest;
        }

        private void OnDisable()
        {
            _input.MoveInput -= HandleMove;
            _input.JumpInput -= HandleJumpRequest;
        }

        private void HandleMove(Vector2 input) => _moveInput = input;

        private void HandleJumpRequest()
        {
            // Store a request; TryJump() consumes it in FixedUpdate.
            _jumpBufferTimer = _jumpBufferTime;
        }

        private void Update()
        {
            // Timers tick in Update for sub-frame responsiveness.
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;
            if (!IsGrounded && _coyoteTimer > 0f) _coyoteTimer -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            UpdateGroundState();
            TryJump();
            ApplyMovement();
            ClampHorizontalSpeed();
            UpdateWallSlide();
            ApplyVariableGravity();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Ground detection
        // ──────────────────────────────────────────────────────────────────────

        private void UpdateGroundState()
        {
            // Origin = centre of the capsule's bottom hemisphere.
            Vector3 origin = transform.position
                + _col.center
                + Vector3.down * (_col.height * 0.5f - _col.radius);

            bool nowGrounded = Physics.SphereCast(
                origin,
                _groundCheckRadius,
                Vector3.down,
                out _,
                _groundCheckDistance,
                _groundLayer,
                QueryTriggerInteraction.Ignore);

            if (nowGrounded && !IsGrounded)
            {
                _rb.linearDamping = _groundDrag;
                _jumpsRemaining = _maxJumps;

                // Only fire OnLanded when actually falling — Rigidbody jitter on flat surfaces
                // produces tiny negative Y velocities that would spam the landing dip otherwise.
                if (_rb.linearVelocity.y < -1.5f)
                {
                    _lastLandTime = Time.time;
                    OnLanded?.Invoke();
                }
            }
            else if (!nowGrounded && IsGrounded)
            {
                _rb.linearDamping = _airDrag;
                _coyoteTimer = _coyoteTime;
            }

            IsGrounded = nowGrounded;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Jump
        // ──────────────────────────────────────────────────────────────────────

        private void TryJump()
        {
            if (_jumpBufferTimer <= 0f) return;

            bool coyote  = !IsGrounded && _coyoteTimer > 0f;
            bool canJump = IsGrounded || coyote || _jumpsRemaining > 0;
            if (!canJump) return;

            // For a mid-air (double) jump, zero out any downward velocity first so
            // the second jump always reaches the same height regardless of fall speed.
            if (!IsGrounded && !coyote)
            {
                var v = _rb.linearVelocity;
                if (v.y < 0f) v.y = 0f;
                _rb.linearVelocity = v;
            }

            _rb.linearDamping = _airDrag;
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

            _jumpBufferTimer = 0f;
            _coyoteTimer     = 0f;
            IsGrounded       = false;

            if (_jumpsRemaining > 0)
                _jumpsRemaining--;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Horizontal movement
        // ──────────────────────────────────────────────────────────────────────

        private void ApplyMovement()
        {
            Vector3 camForward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            Vector3 camRight   = Vector3.ProjectOnPlane(_cameraTransform.right,   Vector3.up).normalized;
            Vector3 moveDir    = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;

            float force = IsGrounded ? _moveForce : _airStrafeForce;
            _rb.AddForce(moveDir * force, ForceMode.Force);
        }

        private void ClampHorizontalSpeed()
        {
            Vector3 horizontal = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            if (horizontal.magnitude <= _maxHorizontalSpeed) return;

            Vector3 clamped = horizontal.normalized * _maxHorizontalSpeed;
            _rb.linearVelocity = new Vector3(clamped.x, _rb.linearVelocity.y, clamped.z);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Variable gravity — heavier fall for a punchy Doom/Ultrakill jump arc
        // ──────────────────────────────────────────────────────────────────────

        private void ApplyVariableGravity()
        {
            // Only kick in while airborne and falling.
            if (IsGrounded || _rb.linearVelocity.y >= 0f) return;

            float extra = Physics.gravity.magnitude * (_fallGravityMultiplier - 1f);
            _rb.AddForce(Vector3.down * extra, ForceMode.Acceleration);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Wall slide
        // ──────────────────────────────────────────────────────────────────────

        private void UpdateWallSlide()
        {
            if (IsGrounded || _rb.linearVelocity.y >= 0f)
            {
                IsWallSliding = false;
                return;
            }

            // Cast from hip height (capsule centre) in four cardinal directions.
            Vector3 hipPos = transform.position + _col.center;
            bool touchingWall = false;

            foreach (Vector3 dir in WallDirs)
            {
                if (Physics.SphereCast(hipPos, _groundCheckRadius, dir, out _,
                        _wallCheckDistance, _groundLayer, QueryTriggerInteraction.Ignore))
                {
                    touchingWall = true;
                    break;
                }
            }

            IsWallSliding = touchingWall;

            if (IsWallSliding)
            {
                Vector3 vel = _rb.linearVelocity;
                vel.y = Mathf.Max(vel.y, _maxWallSlideSpeed);
                _rb.linearVelocity = vel;
            }
        }
    }
}
