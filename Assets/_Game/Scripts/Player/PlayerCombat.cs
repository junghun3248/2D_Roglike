using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using Roglike.Progression;
using UnityEngine;

namespace Roglike.Player
{
    /// <summary>
    /// 기본 공격 콤보와 타격 판정, 타격감(히트스톱·흔들림·줌·파편) 발생.
    /// 공격 키를 누르고 있으면 콤보가 계속 이어진다 (메이플식 꾹 누르기).
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader), typeof(PlayerHitResolver))]
    [RequireComponent(typeof(ChargeAttackState))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Combo")]
        [SerializeField] private AttackData[] _combo;
        [SerializeField] private AttackData[] _airCombo;
        [SerializeField] private AttackData[] _heavyCombo;
        [SerializeField] private AttackData _dashAttack;
        [SerializeField] private AttackData _launcher;
        [SerializeField] private AttackData _plunge;
        [SerializeField] private AttackData _chargedStrike;
        [SerializeField] private float _dashAttackWindow = 0.15f;
        [SerializeField] private float _directionThreshold = 0.5f;
        [SerializeField] private float _attackBufferTime = 0.25f;
        [SerializeField] private float _comboResetTime = 0.4f;

        [SerializeField] private Color _slashColor = new Color(0.85f, 0.95f, 1f, 0.9f);

        private PlayerController _controller;
        private PlayerInputReader _input;
        private PlayerHitResolver _hitResolver;
        private PlayerStats _stats;
        private ChargeAttackState _charge;
        private AttackData[] _sequence;
        private bool _plunging;
        private Vector2 _previousHitCenter;
        private readonly List<Collider2D> _hits = new List<Collider2D>(16);
        private readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
        private ContactFilter2D _filter;

        private AttackData _current;
        private int _comboIndex;
        private int _nextIndex;
        private float _attackTime;
        private float _bufferTimer;
        private float _comboResetTimer;
        private bool _slashSpawned;
        private bool _checkedOnce;
        private bool _feedbackPlayed;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _input = GetComponent<PlayerInputReader>();
            _hitResolver = GetComponent<PlayerHitResolver>();
            _stats = GetComponent<PlayerStats>();
            _charge = GetComponent<ChargeAttackState>();

            _filter = new ContactFilter2D();
            _filter.SetLayerMask(GameLayers.EnemyMask);
            _filter.useTriggers = true;
        }

        private void Update()
        {
            if (_combo == null || _combo.Length == 0)
            {
                return;
            }

            float dt = Time.deltaTime * TimeController.PlayerTimeMultiplier;

            if (_input.AttackPressed)
            {
                _bufferTimer = _attackBufferTime;
            }
            else if (_input.AttackHeld)
            {
                _bufferTimer = Mathf.Max(_bufferTimer, 0.05f);
            }
            else
            {
                _bufferTimer -= dt;
            }

            bool interrupted = _controller.ConsumeAttackInterrupted();
            bool blocked = _controller.IsHurt || _controller.IsDashing || (_controller.IsInSkill && !_plunging);
            if (_current != null && (interrupted || blocked || (_plunging && !_controller.IsPlunging)))
            {
                _current = null;
                _plunging = false;
                _sequence = null;
                _nextIndex = 0;
            }

            bool released = _charge.Tick(dt, _current == null, out bool charged);
            if (_charge.IsActive) return;
            if (released && _controller.CanAttack)
            {
                _bufferTimer = 0f;
                if (charged) StartAttack(_chargedStrike);
                else StartSequence(_heavyCombo);
                return;
            }
            if (_current != null)
            {
                if (_plunging) UpdatePlunge(dt);
                else UpdateAttack(dt);
                return;
            }

            _comboResetTimer -= dt;
            if (_comboResetTimer <= 0f) _nextIndex = 0;
            if (_controller.IsHurt || (_controller.IsInSkill && !_controller.IsPlunging)) return;
            if (!_controller.IsGrounded && _input.HeavyPressed && _controller.CanAttack)
            {
                StartPlunge();
                return;
            }
            if (_bufferTimer <= 0f) return;
            if (_controller.TimeSinceDash <= _dashAttackWindow && _dashAttack != null)
                StartAttack(_dashAttack);
            else if (_controller.CanAttack)
            {
                if (!_controller.IsGrounded && _input.MoveY < -_directionThreshold) StartPlunge();
                else if (_controller.IsGrounded && _input.MoveY > _directionThreshold) StartAttack(_launcher);
                else StartSequence(_controller.IsGrounded ? _combo : _airCombo);
            }
        }

        private void StartSequence(AttackData[] sequence)
        {
            if (sequence == null || sequence.Length == 0) return;
            int index = _sequence == sequence ? _nextIndex : 0;
            StartAttack(sequence[Mathf.Clamp(index, 0, sequence.Length - 1)], sequence, index);
        }

        private void StartPlunge()
        {
            if (_plunge == null) return;
            StartAttack(_plunge);
            _controller.BeginPlunge(_plunge.SelfVelocity, _plunge.MotionTimeout);
            _controller.ConsumeAttackInterrupted();
            _plunging = true;
        }

        private void UpdatePlunge(float dt)
        {
            _attackTime += dt;
            if (_controller.IsGrounded)
            {
                _controller.EndSkillMotion();
                _plunging = false;
                _hitThisSwing.Clear();
                _feedbackPlayed = false;
                Vector2 center = (Vector2)transform.position + _current.LandingHitboxOffset;
                DetectHits(center, _current.LandingHitboxSize);
                SlashEffect.Spawn(center, _current.LandingHitboxSize, 0f, _slashColor, _current.active);
                _attackTime = _current.startup + _current.active;
                _checkedOnce = _slashSpawned = true;
                _controller.BeginAttack(0f, _current.recovery / AttackSpeed);
            }
            else if (_attackTime >= _current.MotionTimeout)
            {
                _controller.EndSkillMotion();
                _plunging = false;
                _current = null;
            }
            else if (_attackTime >= _current.startup) DetectHits();
        }

        private void UpdateAttack(float dt)
        {
            _attackTime += dt * AttackSpeed;
            float activeStart = _current.startup;
            float activeEnd = _current.startup + _current.active;

            if (_attackTime >= activeStart)
            {
                if (!_slashSpawned)
                {
                    SpawnSlash();
                }

                // 프레임이 길어 활성 구간을 건너뛰어도 최소 한 번은 판정한다.
                if (_attackTime < activeEnd || !_checkedOnce)
                {
                    DetectHits();
                    _checkedOnce = true;
                }
            }

            bool isLast = _sequence == null || _comboIndex >= _sequence.Length - 1;
            float chainTime = Mathf.Max(_current.cancelTime, activeEnd);
            if (!isLast && _sequence != _heavyCombo && _bufferTimer > 0f && _attackTime >= chainTime)
            {
                AttackData[] contextCombo = _controller.IsGrounded ? _combo : _airCombo;
                if (_sequence != contextCombo) StartSequence(contextCombo);
                else StartAttack(_sequence[_comboIndex + 1], _sequence, _comboIndex + 1);
                return;
            }

            if (_attackTime >= _current.TotalDuration)
            {
                _current = null;
                _nextIndex = isLast ? 0 : _comboIndex + 1;
                _comboResetTimer = _comboResetTime;
            }
        }

        private void StartAttack(AttackData attack, AttackData[] sequence = null, int index = 0)
        {
            if (attack == null) return;
            _controller.FaceInputDirection();
            _sequence = sequence;
            _comboIndex = index;
            _current = attack;
            _nextIndex = 0;
            _attackTime = 0f;
            _bufferTimer = 0f;
            _slashSpawned = false;
            _checkedOnce = false;
            _feedbackPlayed = false;
            _hitThisSwing.Clear();
            _previousHitCenter = HitboxCenter(_current);
            _controller.BeginAttack(_current.lungeSpeed, _current.TotalDuration / AttackSpeed,
                _current.SelfVelocity, _current.KeepMomentum);
            AttackStarted?.Invoke(_current.TotalDuration / AttackSpeed);
        }

        /// <summary>공격 하나가 시작될 때 (인자: 플레이어 시간 기준 공격 길이). 애니메이션용.</summary>
        public event System.Action<float> AttackStarted;

        private float AttackSpeed => _stats != null ? Mathf.Max(0.2f, _stats.Get(StatType.AttackSpeed)) : 1f;

        private Vector2 HitboxCenter(AttackData attack)
        {
            Vector2 offset = attack.hitboxOffset;
            return (Vector2)transform.position + new Vector2(offset.x * _controller.Facing, offset.y);
        }

        private void SpawnSlash()
        {
            _slashSpawned = true;
            float angle = _controller.Facing > 0f ? _current.slashAngle : -_current.slashAngle;
            float duration = Mathf.Max(0.08f, _current.active + 0.06f);
            SlashEffect.Spawn(HitboxCenter(_current), _current.hitboxSize * 1.1f, angle, _slashColor, duration);
        }

        private void DetectHits()
        {
            Vector2 center = HitboxCenter(_current);
            Vector2 size = _current.hitboxSize;
            if (_current.HitsWhileMoving)
            {
                Vector2 travel = center - _previousHitCenter;
                size += new Vector2(Mathf.Abs(travel.x), Mathf.Abs(travel.y));
                Vector2 midpoint = (center + _previousHitCenter) * 0.5f;
                _previousHitCenter = center;
                center = midpoint;
            }
            DetectHits(center, size);
        }

        private void DetectHits(Vector2 center, Vector2 size)
        {
            int count = Physics2D.OverlapBox(center, size, 0f, _filter, _hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                Health target = hit.GetComponentInParent<Health>();
                if (target == null || target.IsDead || target.Team == Team.Player || !_hitThisSwing.Add(target))
                {
                    continue;
                }

                var spec = new HitSpec(_current.damageMultiplier,
                    new Vector2(_current.knockback.x * _controller.Facing, _current.knockback.y),
                    _current.hitStun, _current.hitStop, _current.shake, _current.overdriveGain, _current.isFinisher);
                _hitResolver.TryHit(target, hit, center, spec, ref _feedbackPlayed);
            }
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            if (_plunging && _controller.IsPlunging) _controller.EndSkillMotion();
            _controller.CancelAttack();
            _charge.Cancel();
            _current = null;
            _sequence = null;
            _plunging = false;
            _bufferTimer = 0f;
            _nextIndex = 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (_combo == null)
            {
                return;
            }

            float facing = Application.isPlaying && _controller != null ? _controller.Facing : 1f;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            foreach (AttackData attack in _combo)
            {
                if (attack == null)
                {
                    continue;
                }

                Vector2 center = (Vector2)transform.position + new Vector2(attack.hitboxOffset.x * facing, attack.hitboxOffset.y);
                Gizmos.DrawWireCube(center, attack.hitboxSize);
            }
        }
    }
}
