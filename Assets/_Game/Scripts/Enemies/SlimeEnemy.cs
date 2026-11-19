using System;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Enemies
{
    // HitFlash의 스케일 복원 이후 방향과 공격 예고를 적용한다.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class SlimeEnemy : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private float _patrolSpeed = 1.5f;
        [SerializeField] private float _chaseSpeed = 3.5f;
        [SerializeField] private float _detectRange = 9f;
        [SerializeField] private float _attackRange = 2.2f;
        [SerializeField] private float _telegraphTime = 0.45f;
        [SerializeField] private float _lungeSpeed = 9f;
        [SerializeField] private float _lungeUpSpeed = 6f;
        [SerializeField] private float _attackCooldown = 1.2f;
        [SerializeField] private double _contactDamage = 8;
        [SerializeField] private Vector2 _contactBoxSize = new Vector2(0.9f, 0.7f);
        [SerializeField] private float _contactKnockback = 9f;
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.45f, 0.35f);

        [SerializeField] private float _verticalDetectRange = 3f;
        [SerializeField] private float _probeDistance = 0.15f;
        [SerializeField] private float _ledgeProbeDepth = 0.3f;
        [SerializeField] private float _minimumLungeTime = 0.15f;
        [SerializeField] private float _contactCooldown = 0.8f;
        [SerializeField] private float _contactHitStun = 0.3f;
        [SerializeField] private float _knockbackUpRatio = 0.3f;
        [SerializeField] private Vector2 _telegraphScale = new Vector2(1.25f, 0.7f);
        [SerializeField] private float _deathDuration = 0.25f;
        [SerializeField] private float _deathPopScale = 1.5f;
        [SerializeField] private Color _deathSparkColor = new Color(0.6f, 1f, 0.6f);
        [SerializeField] private int _deathSparkCount = 16;
        [SerializeField] private float _deathSparkSpeed = 12f;
        [SerializeField] private float _deathSparkSize = 0.3f;

        private enum State { Patrol, Chase, Telegraph, Lunge, Stunned, Dead }

        private readonly List<Collider2D> _contactHits = new List<Collider2D>(8);
        private Rigidbody2D _body;
        private Health _health;
        private StatusEffects _status;
        private Health _player;
        private HitFlash _hitFlash;
        private Collider2D[] _colliders;
        private ContactFilter2D _contactFilter;
        private Vector3 _baseScale;
        private Color _baseColor;
        private State _state;
        private float _facing = 1f;
        private float _stateTime;
        private float _attackTimer;
        private float _contactTimer;
        private bool _isTinted;

        public event Action<SlimeEnemy> Despawned;

        private void Awake()
        {
            _status = GetComponent<StatusEffects>();
            if (_status == null) _status = gameObject.AddComponent<StatusEffects>();
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _hitFlash = GetComponent<HitFlash>();
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _contactFilter = new ContactFilter2D();
            _contactFilter.SetLayerMask(GameLayers.PlayerMask);
            _contactFilter.useTriggers = true;
            if (_visual != null)
            {
                _baseScale = _visual.localScale;
                _facing = _baseScale.x < 0f ? -1f : 1f;
            }

            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            _baseColor = renderer != null ? renderer.color : Color.white;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void Start()
        {
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team == Team.Player)
                {
                    _player = health;
                    break;
                }
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreVisual();
            if (_state == State.Telegraph)
            {
                _state = State.Chase;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            Despawned?.Invoke(this);
        }

        private void Unsubscribe()
        {
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
        }

        private void FixedUpdate()
        {
            if (_status.IsFrozen) return;
            if (_state == State.Dead) return;

            float dt = Time.fixedDeltaTime;
            _attackTimer = Mathf.Max(0f, _attackTimer - dt);
            _contactTimer = Mathf.Max(0f, _contactTimer - dt);
            _stateTime -= dt;

            switch (_state)
            {
                case State.Stunned:
                    if (_stateTime <= 0f) _state = State.Chase;
                    return;
                case State.Telegraph:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f)
                    {
                        RestoreVisual();
                        _state = State.Lunge;
                        _stateTime = _minimumLungeTime;
                        _body.linearVelocity = new Vector2(_facing * _lungeSpeed, _lungeUpSpeed);
                    }
                    break;
                case State.Lunge:
                    if (_stateTime <= 0f && _body.linearVelocity.y <= 0f && IsGrounded())
                    {
                        _state = State.Chase;
                    }
                    break;
                default:
                    UpdateMovement();
                    break;
            }

            ApplyContactDamage();
        }

        private void UpdateMovement()
        {
            Vector2 offset = _player != null ? (Vector2)(_player.transform.position - transform.position) : Vector2.zero;
            bool detected = _player != null && !_player.IsDead
                && Mathf.Abs(offset.x) <= _detectRange && Mathf.Abs(offset.y) <= _verticalDetectRange;
            _state = detected ? State.Chase : State.Patrol;
            if (detected)
            {
                if (Mathf.Abs(offset.x) > Mathf.Epsilon) _facing = Mathf.Sign(offset.x);
                if (Mathf.Abs(offset.x) <= _attackRange && _attackTimer <= 0f && IsGrounded())
                {
                    _state = State.Telegraph;
                    _stateTime = _telegraphTime;
                    _attackTimer = _attackCooldown;
                    SetHorizontalSpeed(0f);
                    if (_hitFlash != null)
                    {
                        _hitFlash.SetBaseColor(0, _telegraphColor);
                        _isTinted = true;
                    }
                    return;
                }
            }

            bool canWalk = CanWalk(_facing);
            if (!canWalk && _state == State.Patrol)
            {
                _facing = -_facing;
                canWalk = CanWalk(_facing);
            }
            SetHorizontalSpeed(canWalk ? _facing * (detected ? _chaseSpeed : _patrolSpeed) : 0f);
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

        private void OnDamaged(DamageInfo info)
        {
            if (_state == State.Dead) return;
            RestoreVisual();
            _body.linearVelocity = info.Knockback;
            _state = State.Stunned;
            _stateTime = Mathf.Max(0f, info.HitStun);
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

        private void LateUpdate()
        {
            if (_status.IsFrozen) return;
            if (_visual == null) return;
            Vector3 scale = _visual.localScale;
            if (_state == State.Telegraph)
            {
                scale = Vector3.Scale(_baseScale, new Vector3(_telegraphScale.x, _telegraphScale.y, 1f));
            }
            else if (_state == State.Dead)
            {
                _stateTime += Time.deltaTime;
                float t = _deathDuration > 0f ? Mathf.Clamp01(_stateTime / _deathDuration) : 1f;
                scale = _baseScale * Mathf.Lerp(1f, _deathPopScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            }
            scale.x = Mathf.Abs(scale.x) * _facing;
            _visual.localScale = scale;
        }

        private void RestoreVisual()
        {
            if (_isTinted && _hitFlash != null) _hitFlash.SetBaseColor(0, _baseColor);
            _isTinted = false;
            if (_visual != null)
            {
                _visual.localScale = new Vector3(Mathf.Abs(_baseScale.x) * _facing, _baseScale.y, _baseScale.z);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(_detectRange * 2f, _verticalDetectRange * 2f, 0f));
            Gizmos.color = _telegraphColor;
            Gizmos.DrawWireCube(transform.position, new Vector3(_attackRange * 2f, _verticalDetectRange * 2f, 0f));
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, _contactBoxSize);
        }
    }
}
