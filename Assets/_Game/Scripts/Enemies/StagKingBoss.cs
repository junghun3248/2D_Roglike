using System;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Enemies
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class StagKingBoss : MonoBehaviour, IBoss, IEnemyAnimationState, ISpeedScalable
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private float _probeDistance = 0.15f;
        [SerializeField] private float _ledgeProbeDepth = 0.35f;
        [SerializeField] private float _contactCooldown = 0.8f;
        [SerializeField] private float _contactHitStun = 0.3f;
        [SerializeField] private float _contactKnockback = 12f;
        [SerializeField] private float _knockbackUpRatio = 0.3f;
        [SerializeField] private float _deathDuration = 0.8f;
        [SerializeField] private float _deathPopScale = 1.5f;
        [SerializeField] private Color _deathSparkColor = new Color(1f, 0.7f, 0.25f);
        [SerializeField] private int _deathSparkCount = 40;
        [SerializeField] private float _deathSparkSpeed = 16f;
        [SerializeField] private float _deathSparkSize = 0.45f;
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
        private float _contactTimer;
        private float _speedMultiplier = 1f;

        public void ScaleSpeed(float multiplier)
        {
            if (multiplier > 0f && !float.IsInfinity(multiplier)) _speedMultiplier *= multiplier;
        }
        [SerializeField] private string _displayName = "태엽 사슴왕";
        [SerializeField] private float _introDuration = 1f;
        [SerializeField] private float _patternCooldown = 1.4f;
        [SerializeField] private float _chargeTelegraph = 0.7f;
        [SerializeField] private float _chargeSpeed = 14f;
        [SerializeField] private float _chargeTimeout = 5f;
        [SerializeField] private float _crashStun = 0.6f;
        [SerializeField] private float _crashShake = 0.65f;
        [SerializeField] private float _stepDuration = 0.3f;
        [SerializeField] private float _stepSpeed = 4f;
        [SerializeField] private float _sweepTelegraph = 0.45f;
        [SerializeField] private float _sweepDuration = 0.2f;
        [SerializeField] private Vector2 _sweepSize = new Vector2(4.5f, 2f);
        [SerializeField] private Vector2 _sweepOffset = new Vector2(2.5f, 0f);
        [SerializeField] private double _sweepDamage = 25;
        [SerializeField] private Vector2 _sweepKnockback = new Vector2(17f, 6f);
        [SerializeField] private float _sweepHitStun = 0.4f;
        [SerializeField] private float _leafTelegraph = 0.6f;
        [SerializeField] private int _leafMinCount = 6;
        [SerializeField] private int _leafMaxCount = 8;
        [SerializeField] private float _leafSpread = 6f;
        [SerializeField] private float _leafFallbackHeight = 10f;
        [SerializeField] private float _leafTopMargin = 0.5f;
        [SerializeField] private float _enrageThreshold = 0.5f;
        [SerializeField] private float _enrageCooldownMultiplier = 0.6f;
        [SerializeField] private float _enrageHitStop = 0.08f;
        [SerializeField] private float _enrageShake = 0.4f;
        [SerializeField] private Color _enragedColor = new Color(1f, 0.35f, 0.1f);
        [SerializeField] private float _deathShake = 0.8f;
        [SerializeField] private float _deathSlowScale = 0.2f;
        [SerializeField] private float _deathSlowDuration = 0.6f;
        [SerializeField] private double _contactDamage = 22;
        [SerializeField] private Vector2 _contactBoxSize = new Vector2(2.4f, 2.6f);
        [SerializeField] private Vector2 _chargePose = new Vector2(1.15f, 0.8f);
        [SerializeField] private Vector2 _rearPose = new Vector2(0.85f, 1.15f);
        [SerializeField] private float _sweepAngle = 30f;
        private enum State { Intro, Cooldown, Step, Telegraph, Charge, Sweep, Stunned, Dead }
        private enum Pattern { Charge, AntlerSweep, LeafStorm }
        private Pattern _pattern;
        private int _lastPattern = -1;
        private int _chargesRemaining;
        private bool _sweepHit;
        private Camera _camera;
        public string DisplayName => _displayName;
        public bool IsEnraged { get; private set; }
        public string CurrentPattern => _lastPattern < 0 || _state == State.Dead ? string.Empty : _pattern.ToString();

        /// <summary>애니메이션용 상태 이름 (IEnemyAnimationState). 예고 중엔 그 패턴의 자세를 미리 보여 준다.</summary>
        public string AnimationState
        {
            get
            {
                switch (_state)
                {
                    case State.Telegraph:
                        return _pattern == Pattern.Charge ? "Charge" : _pattern == Pattern.AntlerSweep ? "Sweep" : "Rear";
                    case State.Charge: return "Charge";
                    case State.Sweep: return "Sweep";
                    case State.Stunned: return "Stunned";
                    case State.Dead: return "Dead";
                    case State.Step: return "Move";
                    default: return IsEnraged ? "Enraged" : "Idle";
                }
            }
        }
        public event Action<IBoss> BossDefeated;

        private void FixedUpdate()
        {
            if (_state == State.Dead) return;
            float dt = Time.fixedDeltaTime;
            _stateTime -= dt;
            _contactTimer -= dt;
            switch (_state)
            {
                case State.Intro:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f && IsGrounded()) ChoosePattern();
                    return;
                case State.Cooldown:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f && IsGrounded()) ChoosePattern();
                    break;
                case State.Step:
                    SetHorizontalSpeed(CanWalk(_facing) ? _facing * _stepSpeed * _speedMultiplier : 0f);
                    if (_stateTime <= 0f) BeginTelegraph(_sweepTelegraph);
                    break;
                case State.Telegraph:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f) ExecutePattern();
                    break;
                case State.Charge:
                    if (!CanWalk(_facing))
                    {
                        SetHorizontalSpeed(0f);
                        _state = State.Stunned;
                        _stateTime = _crashStun;
                        HitSpark.Spawn(GetBodyBounds().center, _deathSparkColor, _deathSparkCount, _deathSparkSpeed, _deathSparkSize);
                        GameCamera.Instance?.Shake(_crashShake);
                    }
                    else if (_stateTime <= 0f) EndCharge();
                    else SetHorizontalSpeed(_facing * _chargeSpeed * _speedMultiplier);
                    break;
                case State.Stunned:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f) EndCharge();
                    return;
                case State.Sweep:
                    ApplySweep();
                    if (_stateTime <= 0f) BeginCooldown();
                    break;
            }
            if (_state != State.Stunned) ApplyContactDamage();
        }

        private void FacePlayer()
        {
            if (_player == null || _player.IsDead) return;
            float offset = _player.transform.position.x - transform.position.x;
            if (Mathf.Abs(offset) > Mathf.Epsilon) _facing = Mathf.Sign(offset);
        }

        private void ChoosePattern()
        {
            if (_player == null || _player.IsDead) { BeginCooldown(); return; }
            int next = _lastPattern < 0 ? UnityEngine.Random.Range(0, 3)
                : (_lastPattern + UnityEngine.Random.Range(1, 3)) % 3;
            _lastPattern = next;
            _pattern = (Pattern)next;
            FacePlayer();
            if (_pattern == Pattern.Charge)
            {
                _chargesRemaining = IsEnraged ? 2 : 1;
                BeginTelegraph(_chargeTelegraph);
            }
            else if (_pattern == Pattern.AntlerSweep)
            {
                _state = State.Step;
                _stateTime = _stepDuration / _speedMultiplier;
            }
            else BeginTelegraph(_leafTelegraph);
        }

        private void BeginTelegraph(float duration)
        {
            RestoreVisual();
            _state = State.Telegraph;
            _stateTime = duration / _speedMultiplier;
            SetHorizontalSpeed(0f);
        }

        private void ExecutePattern()
        {
            RestoreVisual();
            switch (_pattern)
            {
                case Pattern.Charge:
                    _chargesRemaining--;
                    _state = State.Charge;
                    _stateTime = _chargeTimeout;
                    break;
                case Pattern.AntlerSweep:
                    _state = State.Sweep;
                    _stateTime = _sweepDuration / _speedMultiplier;
                    _sweepHit = false;
                    ApplySweep();
                    break;
                default:
                    SpawnLeaves();
                    BeginCooldown();
                    break;
            }
        }

        private void EndCharge()
        {
            SetHorizontalSpeed(0f);
            if (_chargesRemaining > 0)
            {
                FacePlayer();
                BeginTelegraph(_chargeTelegraph);
            }
            else BeginCooldown();
        }

        private void BeginCooldown()
        {
            RestoreVisual();
            SetHorizontalSpeed(0f);
            _state = State.Cooldown;
            _stateTime = _patternCooldown * (IsEnraged ? _enrageCooldownMultiplier : 1f) / _speedMultiplier;
        }

        private Vector2 SweepCenter => (Vector2)transform.position + new Vector2(_sweepOffset.x * _facing, _sweepOffset.y);

        private void ApplySweep()
        {
            if (_sweepHit || _player == null || _player.IsDead) return;
            _contactHits.Clear();
            Physics2D.OverlapBox(SweepCenter, _sweepSize, 0f, _contactFilter, _contactHits);
            foreach (Collider2D hit in _contactHits)
            {
                if (hit == null || !hit.transform.IsChildOf(_player.transform)) continue;
                if (!_player.TryApplyDamage(new DamageInfo(_sweepDamage, false, hit.ClosestPoint(SweepCenter),
                    new Vector2(_sweepKnockback.x * _facing, _sweepKnockback.y), _sweepHitStun, gameObject))) continue;
                _sweepHit = true;
                _contactTimer = _contactCooldown;
                break;
            }
        }

        private void SpawnLeaves()
        {
            if (_player == null || _player.IsDead) return;
            Vector2 target = _player.transform.position;
            float top = target.y + _leafFallbackHeight;
            if (_camera != null)
                top = Mathf.Max(target.y + _leafTopMargin,
                    _camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, Mathf.Abs(_camera.transform.position.z - transform.position.z))).y + _leafTopMargin);
            int count = UnityEngine.Random.Range(Mathf.Max(1, _leafMinCount), Mathf.Max(_leafMinCount, _leafMaxCount) + 1);
            for (int i = 0; i < count; i++)
            {
                float fraction = count == 1 ? 0.5f : (float)i / (count - 1);
                FallingLeafProjectile.Spawn(new Vector2(target.x + Mathf.Lerp(-_leafSpread, _leafSpread, fraction), top), gameObject);
            }
        }

        private void CheckEnrage()
        {
            if (IsEnraged || _health.Current <= 0 || _health.Normalized > _enrageThreshold) return;
            IsEnraged = true;
            if (_state == State.Cooldown) _stateTime *= _enrageCooldownMultiplier;
            if (_pattern == Pattern.Charge)
            {
                if (_state == State.Telegraph) _chargesRemaining = 2;
                else if (_state == State.Charge || _state == State.Stunned) _chargesRemaining = 1;
            }
            _hitFlash?.SetBaseColor(0, _enragedColor);
            TimeController.Instance?.HitStop(_enrageHitStop);
            GameCamera.Instance?.Shake(_enrageShake);
        }

        private void OnDied(DamageInfo info)
        {
            if (_state == State.Dead) return;
            RestoreVisual();
            _state = State.Dead;
            _stateTime = 0f;
            foreach (Collider2D collider in _colliders)
                if (collider != null) collider.enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            HitSpark.Spawn(transform.position, IsEnraged ? _enragedColor : _deathSparkColor,
                _deathSparkCount, _deathSparkSpeed, _deathSparkSize);
            GameCamera.Instance?.Shake(_deathShake);
            TimeController.Instance?.PulseSlow(_deathSlowScale, _deathSlowDuration);
            Destroy(gameObject, _deathDuration);
            BossDefeated?.Invoke(this);
        }

        private void RestoreVisual()
        {
            _hitFlash?.SetBaseColor(0, IsEnraged ? _enragedColor : _baseColor);
            if (_visual == null) return;
            _visual.localPosition = _basePosition;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = new Vector3(Mathf.Abs(_baseScale.x) * _facing, _baseScale.y, _baseScale.z);
        }

        private void LateUpdate()
        {
            if (_visual == null) return;
            Vector3 scale = _visual.localScale;
            if (_state == State.Telegraph)
            {
                Vector2 pose = _pattern == Pattern.LeafStorm ? _rearPose : _chargePose;
                scale = Vector3.Scale(_baseScale, new Vector3(pose.x, pose.y, 1f));
                _visual.localPosition = _basePosition + Vector3.right * (Mathf.Sin(Time.time * _telegraphFrequency) * _telegraphShake);
                _hitFlash?.SetBaseColor(0, _telegraphColor);
            }
            else if (_state == State.Sweep)
                _visual.localRotation = Quaternion.Euler(0f, 0f, -_facing * _sweepAngle);
            else if (_state == State.Dead)
            {
                _stateTime += Time.unscaledDeltaTime;
                scale = _baseScale * Mathf.Lerp(1f, _deathPopScale, Mathf.Clamp01(_stateTime / _deathDuration));
            }
            scale.x = Mathf.Abs(scale.x) * _facing;
            _visual.localScale = scale;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, _contactBoxSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(SweepCenter, _sweepSize);
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
            _state = State.Intro;
            _stateTime = _introDuration;
            _camera = Camera.main;
        }

        private void Start()
        {
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
                if (health.Team == Team.Player) { _player = health; break; }
        }

        private void OnEnable()
        {
            _health.Changed += CheckEnrage;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Changed -= CheckEnrage;
            _health.Died -= OnDied;
            RestoreVisual();
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

    }
}
