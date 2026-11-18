using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Enemies
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class BoarEnemy : MonoBehaviour, IEnemyAnimationState, ISpeedScalable
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private float _probeDistance = 0.15f;
        [SerializeField] private float _ledgeProbeDepth = 0.35f;
        [SerializeField] private float _contactCooldown = 0.8f;
        [SerializeField] private float _contactHitStun = 0.3f;
        [SerializeField] private float _contactKnockback = 9f;
        [SerializeField] private float _knockbackUpRatio = 0.3f;
        [SerializeField] private float _deathDuration = 0.25f;
        [SerializeField] private float _deathPopScale = 1.5f;
        [SerializeField] private Color _deathSparkColor = new Color(1f, 0.7f, 0.25f);
        [SerializeField] private int _deathSparkCount = 16;
        [SerializeField] private float _deathSparkSpeed = 12f;
        [SerializeField] private float _deathSparkSize = 0.3f;
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.35f, 0.1f);
        [SerializeField] private float _telegraphShake = 0.04f;
        [SerializeField] private float _telegraphFrequency = 45f;
        private readonly List<Collider2D> _contactHits = new List<Collider2D>(8);
        private Rigidbody2D _body;
        private Health _health;
        private Health _player;
        private HitFlash _hitFlash;
        private Collider2D[] _colliders;
        private ContactFilter2D _contactFilter;
        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private Color _baseColor;
        private State _state;
        private float _facing = 1f;
        private float _stateTime;
        private float _attackTimer;
        private float _contactTimer;
        private float _speedMultiplier = 1f;

        public void ScaleSpeed(float multiplier)
        {
            if (multiplier > 0f && !float.IsInfinity(multiplier)) _speedMultiplier *= multiplier;
        }
        [SerializeField] private float _patrolSpeed = 1.5f;
        [SerializeField] private float _detectRange = 9f;
        [SerializeField] private float _verticalDetectRange = 2f;
        [SerializeField] private float _telegraphTime = 0.5f;
        [SerializeField] private float _chargeSpeed = 13f;
        [SerializeField] private float _chargeDuration = 1.2f;
        [SerializeField] private float _crashStun = 0.8f;
        [SerializeField] private float _crashShake = 0.18f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private double _contactDamage = 14;
        [SerializeField] private Vector2 _contactBoxSize = new Vector2(1.8f, 1f);
        private enum State { Patrol, Telegraph, Charge, Stunned, Dead }
        public bool IsStunned => _state == State.Stunned;

        /// <summary>애니메이션용 상태 이름 (IEnemyAnimationState).</summary>
        public string AnimationState
        {
            get
            {
                switch (_state)
                {
                    case State.Telegraph: return "Telegraph";
                    case State.Charge: return "Charge";
                    case State.Stunned: return "Stunned";
                    case State.Dead: return "Dead";
                    default: return _body != null && Mathf.Abs(_body.linearVelocity.x) > 0.3f ? "Move" : "Idle";
                }
            }
        }

        private void FixedUpdate()
        {
            if (_state == State.Dead) return;
            float dt = Time.fixedDeltaTime;
            _stateTime -= dt;
            _attackTimer -= dt * _speedMultiplier;
            _contactTimer -= dt;
            switch (_state)
            {
                case State.Stunned:
                    if (_stateTime <= 0f) _state = State.Patrol;
                    return;
                case State.Telegraph:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f)
                    {
                        RestoreVisual();
                        _state = State.Charge;
                        _stateTime = _chargeDuration;
                    }
                    break;
                case State.Charge:
                    if (!CanWalk(_facing))
                    {
                        SetHorizontalSpeed(0f);
                        _state = State.Stunned;
                        _stateTime = _crashStun;
                        _attackTimer = _attackCooldown;
                        HitSpark.Spawn(GetBodyBounds().center, _deathSparkColor, _deathSparkCount, _deathSparkSpeed, _deathSparkSize);
                        GameCamera.Instance?.Shake(_crashShake);
                    }
                    else if (_stateTime <= 0f)
                    {
                        _state = State.Patrol;
                        _attackTimer = _attackCooldown;
                        SetHorizontalSpeed(0f);
                    }
                    else SetHorizontalSpeed(_facing * _chargeSpeed * _speedMultiplier);
                    break;
                default:
                    Vector2 offset = _player != null ? (Vector2)(_player.transform.position - transform.position) : Vector2.zero;
                    if (_player != null && !_player.IsDead && Mathf.Abs(offset.x) <= _detectRange
                        && Mathf.Abs(offset.y) <= _verticalDetectRange && _attackTimer <= 0f && IsGrounded())
                    {
                        if (Mathf.Abs(offset.x) > Mathf.Epsilon) _facing = Mathf.Sign(offset.x);
                        _state = State.Telegraph;
                        _stateTime = _telegraphTime / _speedMultiplier;
                        SetHorizontalSpeed(0f);
                    }
                    else
                    {
                        if (!CanWalk(_facing)) _facing = -_facing;
                        SetHorizontalSpeed(CanWalk(_facing) ? _facing * _patrolSpeed * _speedMultiplier : 0f);
                    }
                    break;
            }
            if (_state != State.Stunned) ApplyContactDamage();
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_state == State.Dead) return;
            RestoreVisual();
            _body.linearVelocity = info.Knockback;
            _stateTime = Mathf.Max(_state == State.Stunned ? _stateTime : 0f, info.HitStun);
            _state = State.Stunned;
            _attackTimer = _attackCooldown;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(_detectRange * 2f, _verticalDetectRange * 2f, 0f));
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, _contactBoxSize);
        }
        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _hitFlash = GetComponent<HitFlash>();
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _contactFilter.SetLayerMask(GameLayers.PlayerMask);
            _contactFilter.useTriggers = true;
            if (_visual == null) _visual = transform.Find("Visual");
            _baseScale = _visual != null ? _visual.localScale : Vector3.one;
            _basePosition = _visual != null ? _visual.localPosition : Vector3.zero;
            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            _baseColor = renderer != null ? renderer.color : Color.white;
        }

        private void Start()
        {
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
                if (health.Team == Team.Player) { _player = health; break; }
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            RestoreVisual();
        }

        private void RestoreVisual()
        {
            if (_hitFlash != null) _hitFlash.SetBaseColor(0, _baseColor);
            if (_visual == null) return;
            _visual.localPosition = _basePosition;
            _visual.localScale = new Vector3(Mathf.Abs(_baseScale.x) * _facing, _baseScale.y, _baseScale.z);
        }

        private void LateUpdate()
        {
            if (_visual == null) return;
            Vector3 scale = _visual.localScale;
            _visual.localPosition = _basePosition;
            if (_state == State.Telegraph)
            {
                _visual.localPosition += Vector3.right * (Mathf.Sin(Time.time * _telegraphFrequency) * _telegraphShake);
                _hitFlash?.SetBaseColor(0, _telegraphColor);
            }
            else if (_state == State.Dead)
            {
                _stateTime += Time.deltaTime;
                scale = _baseScale * Mathf.Lerp(1f, _deathPopScale, Mathf.Clamp01(_stateTime / _deathDuration));
            }
            scale.x = Mathf.Abs(scale.x) * _facing;
            _visual.localScale = scale;
        }
        private Bounds GetBodyBounds()
        {
            Bounds bounds = new Bounds(transform.position, _contactBoxSize);
            bool found = false;
            foreach (Collider2D collider in _colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found) bounds = collider.bounds;
                else bounds.Encapsulate(collider.bounds);
                found = true;
            }
            return bounds;
        }

        private bool CanWalk(float direction)
        {
            Bounds bounds = GetBodyBounds();
            Vector2 edge = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.center.y);
            if (Physics2D.Raycast(edge, Vector2.right * direction, _probeDistance, GameLayers.GroundMask)) return false;
            Vector2 ahead = new Vector2(edge.x + direction * _probeDistance, bounds.min.y + _probeDistance);
            return Physics2D.Raycast(ahead, Vector2.down, _probeDistance + _ledgeProbeDepth, GameLayers.GroundMask);
        }

        private bool IsGrounded()
        {
            Bounds bounds = GetBodyBounds();
            Vector2 foot = new Vector2(bounds.center.x, bounds.min.y + _probeDistance);
            return Physics2D.Raycast(foot, Vector2.down, _probeDistance * 2f, GameLayers.GroundMask);
        }

        private void SetHorizontalSpeed(float speed)
        {
            _body.linearVelocity = new Vector2(speed, _body.linearVelocity.y);
        }

        private void ApplyContactDamage()
        {
            if (_contactTimer > 0f || _player == null || _player.IsDead || _player.IsInvulnerable) return;
            _contactHits.Clear();
            Physics2D.OverlapBox(_body.position, _contactBoxSize, 0f, _contactFilter, _contactHits);
            foreach (Collider2D hit in _contactHits)
            {
                if (hit == null || !hit.transform.IsChildOf(_player.transform)) continue;
                float offset = _player.transform.position.x - transform.position.x;
                float direction = Mathf.Abs(offset) > Mathf.Epsilon ? Mathf.Sign(offset) : _facing;
                Vector2 knockback = new Vector2(direction, _knockbackUpRatio).normalized * _contactKnockback;
                Vector2 point = hit.ClosestPoint(_body.position);
                if (_player.TryApplyDamage(new DamageInfo(_contactDamage, false, point, knockback, _contactHitStun, gameObject)))
                {
                    _contactTimer = _contactCooldown;
                    break;
                }
            }
        }

        private void OnDied(DamageInfo info)
        {
            if (_state == State.Dead) return;
            RestoreVisual();
            _state = State.Dead;
            _stateTime = 0f;
            foreach (Collider2D collider in _colliders)
            {
                if (collider != null) collider.enabled = false;
            }
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            HitSpark.Spawn(transform.position, _deathSparkColor, _deathSparkCount, _deathSparkSpeed, _deathSparkSize);
            Destroy(gameObject, _deathDuration);
        }

    }
}
