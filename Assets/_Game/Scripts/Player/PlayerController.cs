using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Player
{
    /// <summary>
    /// 플레이어 이동: 달리기, 2단 점프(코요테·선입력), 대시(무적), 공격 중 전진, 피격 넉백.
    /// 중력은 직접 계산한다 — 오버드라이브 감속 중에도 플레이어만 정상 속도로 움직이게 하기 위해
    /// 모든 속도에 TimeController.PlayerTimeMultiplier를 곱해 물리 엔진에 넘긴다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private float _moveSpeed = 9f;
        [SerializeField] private float _groundAcceleration = 110f;
        [SerializeField] private float _airAcceleration = 70f;

        [Header("Jump")]
        [SerializeField] private float _jumpVelocity = 17f;
        [SerializeField] private float _gravity = 48f;
        [SerializeField] private float _fallGravityMultiplier = 1.6f;
        [SerializeField] private float _jumpCutMultiplier = 0.45f;
        [SerializeField] private float _maxFallSpeed = 24f;
        [SerializeField] private int _extraJumps = 1;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpBufferTime = 0.12f;

        [Header("Dash")]
        [SerializeField] private float _dashSpeed = 24f;
        [SerializeField] private float _dashDuration = 0.16f;
        [SerializeField] private float _dashCooldown = 0.4f;
        [SerializeField] private float _dashAfterimageInterval = 0.025f;

        [Header("Attack Motion")]
        [SerializeField] private float _attackLungeDecay = 14f;
        [SerializeField] private float _airAttackMaxFallSpeed = 2.5f;

        [Header("Hurt")]
        [SerializeField] private float _hurtInvulnerability = 1f;

        [Header("References")]
        [SerializeField] private Transform _visual;
        [SerializeField] private SpriteRenderer _bodyRenderer;

        private Rigidbody2D _rigidbody;
        private BoxCollider2D _collider;
        private PlayerInputReader _input;
        private Health _health;
        private readonly Collider2D[] _groundHits = new Collider2D[4];
        private ContactFilter2D _groundFilter;

        private Vector2 _velocity;
        private float _lastMultiplier = 1f;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private int _jumpsLeft;
        private bool _jumpCutPending;
        private bool _jumpRising;
        private bool _attackInterrupted;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private float _afterimageTimer;
        private bool _airDashUsed;
        private float _attackTimer;
        private float _lungeVelocity;
        private float _hurtTimer;

        public bool IsGrounded { get; private set; }
        public bool IsDashing => _dashTimer > 0f;
        public bool IsAttacking => _attackTimer > 0f;
        public bool IsHurt => _hurtTimer > 0f;
        public float Facing { get; private set; } = 1f;
        public SpriteRenderer BodyRenderer => _bodyRenderer;

        /// <summary>공격을 시작할 수 있는 상태인지 (대시·피격 중 불가).</summary>
        public bool CanAttack => !IsDashing && !IsHurt;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();
            _input = GetComponent<PlayerInputReader>();
            _health = GetComponent<Health>();

            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _groundFilter = new ContactFilter2D();
            _groundFilter.SetLayerMask(GameLayers.GroundMask);
            _groundFilter.useTriggers = false;
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime * TimeController.PlayerTimeMultiplier;

            if (_input.JumpPressed)
            {
                _jumpBufferTimer = _jumpBufferTime;
            }

            // 점프 버튼을 일찍 떼면 한 번만 상승을 끊는다 (짧은 점프).
            if (_jumpRising && !_input.JumpHeld)
            {
                _jumpRising = false;
                _jumpCutPending = true;
            }

            if (_input.DashPressed && _dashCooldownTimer <= 0f && !IsHurt && !(_airDashUsed && !IsGrounded))
            {
                StartDash();
            }

            _jumpBufferTimer -= dt;
            _dashCooldownTimer -= dt;
            _attackTimer -= dt;
            _hurtTimer -= dt;

            if (IsDashing)
            {
                _afterimageTimer -= Time.unscaledDeltaTime;
                if (_afterimageTimer <= 0f && _bodyRenderer != null)
                {
                    _afterimageTimer = _dashAfterimageInterval;
                    Afterimage.Spawn(_bodyRenderer, new Color(0.5f, 0.9f, 1f, 0.6f), 0.18f);
                }
            }

            UpdateFacingVisual();
        }

        private void FixedUpdate()
        {
            float multiplier = TimeController.PlayerTimeMultiplier;
            float dt = Time.fixedDeltaTime * multiplier;

            // 물리 엔진이 충돌로 바꾼 속도를 "플레이어 시간" 단위로 되돌려 읽는다.
            _velocity = _rigidbody.linearVelocity / _lastMultiplier;

            UpdateGrounded();

            if (IsDashing)
            {
                _dashTimer -= dt;
                _velocity = new Vector2(Facing * _dashSpeed, 0f);
                if (!IsDashing)
                {
                    _velocity.x = Facing * _moveSpeed;
                }
            }
            else
            {
                UpdateHorizontal(dt);
                UpdateJump();
                UpdateGravity(dt);
            }

            _rigidbody.linearVelocity = _velocity * multiplier;
            _lastMultiplier = multiplier;
        }

        /// <summary>공격 시작 시 호출. 전진 속도와 공격 모션 시간(이동 제한)을 준다.</summary>
        public void BeginAttack(float lungeSpeed, float duration)
        {
            _attackTimer = duration;
            _attackInterrupted = false;
            _lungeVelocity = lungeSpeed * Facing;
            if (!IsGrounded && _velocity.y < 0f)
            {
                _velocity.y = 0f;
            }
        }

        /// <summary>공격 중 입력 방향으로 돌아보기 (콤보 사이 방향 전환 허용).</summary>
        public void FaceInputDirection()
        {
            float x = _input.MoveX;
            if (Mathf.Abs(x) > 0.3f)
            {
                Facing = Mathf.Sign(x);
            }
        }

        public void CancelAttack()
        {
            if (IsAttacking)
            {
                _attackInterrupted = true;
            }

            _attackTimer = 0f;
            _lungeVelocity = 0f;
        }

        /// <summary>점프·대시·피격으로 공격이 끊겼으면 true를 한 번 반환한다.</summary>
        public bool ConsumeAttackInterrupted()
        {
            bool interrupted = _attackInterrupted;
            _attackInterrupted = false;
            return interrupted;
        }

        private void StartDash()
        {
            FaceInputDirection();
            CancelAttack();
            _dashTimer = _dashDuration;
            _dashCooldownTimer = _dashCooldown;
            _afterimageTimer = 0f;
            if (!IsGrounded)
            {
                _airDashUsed = true;
            }

            // 대시 중 무적 (게임 시간 기준이므로 감속 보정).
            _health?.SetInvulnerable(_dashDuration / TimeController.PlayerTimeMultiplier);
        }

        private void UpdateGrounded()
        {
            Bounds bounds = _collider.bounds;
            Vector2 center = new Vector2(bounds.center.x, bounds.min.y - 0.04f);
            Vector2 size = new Vector2(bounds.size.x * 0.9f, 0.08f);
            bool grounded = _velocity.y <= 0.01f && Physics2D.OverlapBox(center, size, 0f, _groundFilter, _groundHits) > 0;

            IsGrounded = grounded;
            if (grounded)
            {
                _coyoteTimer = _coyoteTime;
                _jumpsLeft = _extraJumps;
                _airDashUsed = false;
            }
            else
            {
                _coyoteTimer -= Time.fixedDeltaTime * _lastMultiplier;
            }
        }

        private void UpdateHorizontal(float dt)
        {
            if (IsHurt)
            {
                _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, _airAcceleration * 0.3f * dt);
                return;
            }

            if (IsAttacking)
            {
                _lungeVelocity *= Mathf.Exp(-_attackLungeDecay * dt);
                _velocity.x = _lungeVelocity;
                return;
            }

            float input = _input.MoveX;
            if (Mathf.Abs(input) > 0.2f)
            {
                Facing = Mathf.Sign(input);
            }

            float target = input * _moveSpeed;
            float acceleration = IsGrounded ? _groundAcceleration : _airAcceleration;
            _velocity.x = Mathf.MoveTowards(_velocity.x, target, acceleration * dt);
        }

        private void UpdateJump()
        {
            if (IsHurt || _jumpBufferTimer <= 0f)
            {
                return;
            }

            bool canGroundJump = _coyoteTimer > 0f;
            if (!canGroundJump && _jumpsLeft <= 0)
            {
                return;
            }

            if (!canGroundJump)
            {
                _jumpsLeft--;
            }

            CancelAttack();
            _velocity.y = _jumpVelocity;
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _jumpCutPending = false;
            _jumpRising = true;
        }

        private void UpdateGravity(float dt)
        {
            if (_velocity.y <= 0f)
            {
                _jumpRising = false;
            }

            if (_jumpCutPending)
            {
                if (_velocity.y > 0f)
                {
                    _velocity.y *= _jumpCutMultiplier;
                }

                _jumpCutPending = false;
            }

            if (IsGrounded && _velocity.y <= 0f)
            {
                _velocity.y = -1f;
                return;
            }

            float gravity = _velocity.y < 0f ? _gravity * _fallGravityMultiplier : _gravity;
            _velocity.y -= gravity * dt;

            float maxFall = IsAttacking ? _airAttackMaxFallSpeed : _maxFallSpeed;
            _velocity.y = Mathf.Max(_velocity.y, -maxFall);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (info.Source == gameObject)
            {
                return;
            }

            CancelAttack();
            _dashTimer = 0f;
            _hurtTimer = Mathf.Max(0.25f, info.HitStun);
            _velocity = info.Knockback;
            _rigidbody.linearVelocity = _velocity * TimeController.PlayerTimeMultiplier;
            _lastMultiplier = TimeController.PlayerTimeMultiplier;
            _health.SetInvulnerable(_hurtInvulnerability);
            GameCamera.Instance?.Shake(0.45f);
            TimeController.Instance?.HitStop(0.08f);
        }

        private void UpdateFacingVisual()
        {
            if (_visual == null)
            {
                return;
            }

            Vector3 scale = _visual.localScale;
            scale.x = Mathf.Abs(scale.x) * Facing;
            _visual.localScale = scale;

            // 피격 무적 중 깜빡임.
            if (_bodyRenderer != null)
            {
                bool blink = _health != null && _health.IsInvulnerable && !IsDashing && Mathf.Repeat(Time.unscaledTime, 0.12f) < 0.06f;
                _bodyRenderer.enabled = !blink;
            }
        }

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null)
            {
                return;
            }

            Bounds bounds = box.bounds;
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(new Vector3(bounds.center.x, bounds.min.y - 0.04f), new Vector3(bounds.size.x * 0.9f, 0.08f));
        }
    }
}
