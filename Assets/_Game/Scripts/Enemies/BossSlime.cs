using System;
using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Enemies
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class BossSlime : MonoBehaviour, IBoss
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private string _displayName = "킹 슬라임";
        [SerializeField] private GameObject _minionPrefab;
        [SerializeField] private double _contactDamage = 15;
        [SerializeField] private Vector2 _contactBoxSize = new Vector2(2.6f, 2f);
        [SerializeField] private float _contactKnockback = 12f;
        [SerializeField] private float _moveSpeed = 2.5f;
        [SerializeField] private float _hopSpeed = 7f;
        [SerializeField] private float _hopUpSpeed = 9f;
        [SerializeField] private float _slamUpSpeed = 22f;
        [SerializeField] private double _shockwaveDamage = 20;
        [SerializeField] private float _shockwaveSpeed = 11f;
        [SerializeField] private float _patternCooldown = 1.4f;
        [SerializeField] private int _minionsPerSummon = 2;
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.4f, 0.3f);
        [SerializeField] private Color _enragedColor = new Color(1f, 0.55f, 0.75f);

        [SerializeField] private float _introDuration = 1f;
        [SerializeField] private float _hopTelegraphTime = 0.35f;
        [SerializeField] private float _slamTelegraphTime = 0.6f;
        [SerializeField] private float _summonTelegraphTime = 0.5f;
        [SerializeField] private float _minimumAirTime = 0.15f;
        [SerializeField] private float _probeDistance = 0.15f;
        [SerializeField] private float _ledgeProbeDepth = 0.3f;
        [SerializeField] private float _contactCooldown = 0.8f;
        [SerializeField] private float _contactHitStun = 0.3f;
        [SerializeField] private float _knockbackUpRatio = 0.3f;
        [SerializeField] private Vector2 _telegraphScale = new Vector2(1.25f, 0.7f);
        [SerializeField] private Vector2 _minionLaunchVelocity = new Vector2(3f, 5f);
        [SerializeField] private float _minionSpawnClearance = 0.6f;
        [SerializeField] private float _enrageSpeedMultiplier = 1.25f;
        [SerializeField] private float _enrageCooldownMultiplier = 0.6f;
        [SerializeField] private float _enrageHitStop = 0.08f;
        [SerializeField] private float _enrageShake = 0.4f;
        [SerializeField] private float _slamShake = 0.6f;
        [SerializeField] private float _slamHitStop = 0.06f;
        [SerializeField] private float _deathDuration = 0.8f;
        [SerializeField] private float _deathPopScale = 1.6f;
        [SerializeField] private float _deathShake = 0.8f;
        [SerializeField] private float _deathSlowScale = 0.2f;
        [SerializeField] private float _deathSlowDuration = 0.6f;
        [SerializeField] private int _deathSparkCount = 40;
        [SerializeField] private float _deathSparkSpeed = 16f;
        [SerializeField] private float _deathSparkSize = 0.45f;

        private enum State { Intro, Cooldown, Telegraph, Airborne, Dead }
        private enum Pattern { Hop, Slam, Summon }

        private readonly Collider2D[] _contactHits = new Collider2D[16];
        private readonly Health[] _minions = new Health[4];
        private Rigidbody2D _body;
        private Health _health;
        private Health _player;
        private HitFlash _hitFlash;
        private SpriteRenderer _renderer;
        private Collider2D[] _colliders;
        private ContactFilter2D _contactFilter;
        private Vector3 _baseScale;
        private Color _baseColor;
        private State _state;
        private Pattern _pattern;
        private int _lastPattern = -1;
        private int _hopsRemaining;
        private float _stateTime;
        private float _contactTimer;
        private float _facing = 1f;

        public string DisplayName => _displayName;
        public event Action<BossSlime> Defeated;
        public event Action<IBoss> BossDefeated;
        public bool IsEnraged { get; private set; }
        private float SpeedMultiplier => IsEnraged ? _enrageSpeedMultiplier : 1f;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _hitFlash = GetComponent<HitFlash>();
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _baseColor = _renderer != null ? _renderer.color : Color.white;
            _baseScale = _visual != null ? _visual.localScale : Vector3.one;
            _facing = _baseScale.x < 0f ? -1f : 1f;
            _contactFilter.SetLayerMask(GameLayers.PlayerMask);
            _contactFilter.useTriggers = true;
            _state = State.Intro;
            _stateTime = _introDuration;
        }

        private void OnEnable()
        {
            _health.Changed += CheckEnrage;
            _health.Died += OnDied;
        }

        private void Start()
        {
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team != Team.Player) continue;
                _player = health;
                break;
            }
            CheckEnrage();
        }

        private void OnDisable()
        {
            _health.Changed -= CheckEnrage;
            _health.Died -= OnDied;
            RestoreVisual();
        }

        private void FixedUpdate()
        {
            if (_state == State.Dead) return;
            _stateTime -= Time.fixedDeltaTime;
            _contactTimer -= Time.fixedDeltaTime;
            switch (_state)
            {
                case State.Intro:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f && IsGrounded()) BeginCooldown(0f);
                    return;
                case State.Cooldown:
                    FacePlayer();
                    SetHorizontalSpeed(_player != null && !_player.IsDead && CanWalk(_facing)
                        ? _facing * _moveSpeed * SpeedMultiplier : 0f);
                    if (_stateTime <= 0f && IsGrounded()) ChoosePattern();
                    break;
                case State.Telegraph:
                    SetHorizontalSpeed(0f);
                    if (_stateTime <= 0f && IsGrounded()) ExecutePattern();
                    break;
                case State.Airborne:
                    if (_stateTime <= 0f && _body.linearVelocity.y <= 0f && IsGrounded()) Land();
                    break;
            }
            ApplyContactDamage();
        }

        private void ChoosePattern()
        {
            int count = _minionPrefab != null && AliveMinions() < _minions.Length ? 3 : 2;
            int choice = UnityEngine.Random.Range(0, _lastPattern >= 0 && _lastPattern < count ? count - 1 : count);
            if (_lastPattern >= 0 && _lastPattern < count && choice >= _lastPattern) choice++;
            _lastPattern = choice;
            _pattern = (Pattern)choice;
            _hopsRemaining = UnityEngine.Random.Range(2, 4);
            BeginTelegraph();
        }

        private void BeginTelegraph()
        {
            FacePlayer();
            _state = State.Telegraph;
            _stateTime = _pattern == Pattern.Slam ? _slamTelegraphTime
                : _pattern == Pattern.Summon ? _summonTelegraphTime : _hopTelegraphTime;
            SetHorizontalSpeed(0f);
            SetColor(_telegraphColor);
        }

        private void ExecutePattern()
        {
            RestoreVisual();
            if (_pattern == Pattern.Summon)
            {
                Summon();
                BeginCooldown();
                return;
            }
            FacePlayer();
            _state = State.Airborne;
            _stateTime = _minimumAirTime;
            float horizontal = _pattern == Pattern.Hop ? _hopSpeed : _moveSpeed;
            float vertical = _pattern == Pattern.Hop ? _hopUpSpeed : _slamUpSpeed;
            _body.linearVelocity = new Vector2(_facing * horizontal, vertical) * SpeedMultiplier;
        }

        private void Land()
        {
            SetHorizontalSpeed(0f);
            if (_pattern == Pattern.Slam)
            {
                GameCamera.Instance?.Shake(_slamShake);
                TimeController.Instance?.HitStop(_slamHitStop);
                Bounds bounds = GetBodyBounds();
                Vector2 foot = new Vector2(bounds.center.x, bounds.min.y + _probeDistance);
                RaycastHit2D ground = Physics2D.Raycast(foot, Vector2.down, _probeDistance * 2f, GameLayers.GroundMask);
                float y = ground ? ground.point.y : bounds.min.y;
                BossShockwave.Spawn(new Vector2(bounds.min.x, y), -1f, _shockwaveSpeed * SpeedMultiplier, _shockwaveDamage, gameObject);
                BossShockwave.Spawn(new Vector2(bounds.max.x, y), 1f, _shockwaveSpeed * SpeedMultiplier, _shockwaveDamage, gameObject);
            }
            else if (--_hopsRemaining > 0)
            {
                BeginTelegraph();
                return;
            }
            BeginCooldown();
        }

        private void BeginCooldown(float duration = -1f)
        {
            _state = State.Cooldown;
            _stateTime = duration >= 0f ? duration
                : _patternCooldown * (IsEnraged ? _enrageCooldownMultiplier : 1f);
        }

        private int AliveMinions()
        {
            int count = 0;
            foreach (Health minion in _minions)
                if (minion != null && !minion.IsDead) count++;
            return count;
        }

        private void Summon()
        {
            if (_minionPrefab == null) return;
            Bounds bounds = GetBodyBounds();
            int spawned = 0;
            for (int i = 0; i < _minions.Length && spawned < _minionsPerSummon; i++)
            {
                if (_minions[i] != null && !_minions[i].IsDead) continue;
                float direction = spawned % 2 == 0 ? -1f : 1f;
                Vector2 position = new Vector2(bounds.center.x + direction * bounds.extents.x,
                    bounds.max.y + _minionSpawnClearance);
                GameObject minion = Instantiate(_minionPrefab, position, Quaternion.identity);
                _minions[i] = minion.GetComponent<Health>();
                Rigidbody2D body = minion.GetComponent<Rigidbody2D>();
                if (body != null) body.linearVelocity = new Vector2(direction * _minionLaunchVelocity.x, _minionLaunchVelocity.y);
                spawned++;
            }
        }

        private void CheckEnrage()
        {
            if (IsEnraged || _health.Current <= 0 || _health.Normalized > 0.5f) return;
            IsEnraged = true;
            if (_state == State.Cooldown) _stateTime *= _enrageCooldownMultiplier;
            SetColor(_state == State.Telegraph ? _telegraphColor : _enragedColor);
            TimeController.Instance?.HitStop(_enrageHitStop);
            GameCamera.Instance?.Shake(_enrageShake);
        }

        private void FacePlayer()
        {
            if (_player == null || _player.IsDead) return;
            float offset = _player.transform.position.x - transform.position.x;
            if (Mathf.Abs(offset) > Mathf.Epsilon) _facing = Mathf.Sign(offset);
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

        private bool IsGrounded()
        {
            Bounds bounds = GetBodyBounds();
            Vector2 foot = new Vector2(bounds.center.x, bounds.min.y + _probeDistance);
            return Physics2D.Raycast(foot, Vector2.down, _probeDistance * 2f, GameLayers.GroundMask);
        }

        private bool CanWalk(float direction)
        {
            Bounds bounds = GetBodyBounds();
            Vector2 edge = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.center.y);
            if (Physics2D.Raycast(edge, Vector2.right * direction, _probeDistance, GameLayers.GroundMask)) return false;
            Vector2 ahead = new Vector2(edge.x + direction * _probeDistance, bounds.min.y + _probeDistance);
            return Physics2D.Raycast(ahead, Vector2.down, _probeDistance + _ledgeProbeDepth, GameLayers.GroundMask);
        }

        private void SetHorizontalSpeed(float speed)
        {
            _body.linearVelocity = new Vector2(speed, _body.linearVelocity.y);
        }

        private void ApplyContactDamage()
        {
            if (_contactTimer > 0f || _player == null || _player.IsDead || _player.IsInvulnerable) return;
            int count = Physics2D.OverlapBox(_body.position, _contactBoxSize, 0f, _contactFilter, _contactHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _contactHits[i];
                if (hit == null || !hit.transform.IsChildOf(_player.transform)) continue;
                float offset = _player.transform.position.x - transform.position.x;
                float direction = Mathf.Abs(offset) > Mathf.Epsilon ? Mathf.Sign(offset) : _facing;
                Vector2 knockback = new Vector2(direction, _knockbackUpRatio).normalized * _contactKnockback;
                if (!_player.TryApplyDamage(new DamageInfo(_contactDamage, false, hit.ClosestPoint(_body.position),
                    knockback, _contactHitStun, gameObject))) continue;
                _contactTimer = _contactCooldown;
                break;
            }
        }

        private void OnDied(DamageInfo info)
        {
            if (_state == State.Dead) return;
            _state = State.Dead;
            _stateTime = 0f;
            RestoreVisual();
            foreach (Collider2D collider in _colliders)
                if (collider != null) collider.enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            HitSpark.Spawn(transform.position, IsEnraged ? _enragedColor : _baseColor,
                _deathSparkCount, _deathSparkSpeed, _deathSparkSize);
            GameCamera.Instance?.Shake(_deathShake);
            TimeController.Instance?.PulseSlow(_deathSlowScale, _deathSlowDuration);
            Destroy(gameObject, _deathDuration);
            Defeated?.Invoke(this);
            BossDefeated?.Invoke(this);
        }

        private void SetColor(Color color)
        {
            if (_hitFlash != null) _hitFlash.SetBaseColor(0, color);
            else if (_renderer != null) _renderer.color = color;
        }

        private void RestoreVisual()
        {
            SetColor(IsEnraged ? _enragedColor : _baseColor);
            if (_visual != null) _visual.localScale = new Vector3(Mathf.Abs(_baseScale.x) * _facing, _baseScale.y, _baseScale.z);
        }

        private void LateUpdate()
        {
            if (_visual == null) return;
            Vector3 scale = _visual.localScale;
            if (_state == State.Telegraph)
                scale = Vector3.Scale(_baseScale, new Vector3(_telegraphScale.x, _telegraphScale.y, 1f));
            else if (_state == State.Dead)
            {
                _stateTime += Time.unscaledDeltaTime;
                float t = _deathDuration > 0f ? Mathf.Clamp01(_stateTime / _deathDuration) : 1f;
                scale = _baseScale * Mathf.Lerp(1f, _deathPopScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            }
            scale.x = Mathf.Abs(scale.x) * _facing;
            _visual.localScale = scale;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, _contactBoxSize);
        }
    }
}
