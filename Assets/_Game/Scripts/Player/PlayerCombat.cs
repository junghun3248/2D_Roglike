using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using Roglike.Progression;
using UnityEngine;

namespace Roglike.Player
{
    /// <summary>
    /// 기본 단일 베기와 타격 판정, 타격감(히트스톱·흔들림·줌·파편) 발생.
    /// 공격 키를 누르고 있으면 같은 베기를 일정한 간격으로 반복한다.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader), typeof(PlayerHitResolver))]
    [RequireComponent(typeof(ChargeAttackState))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Attacks")]
        [SerializeField] private AttackData _slash;
        [SerializeField] private AttackData _airSlash;
        [SerializeField] private float _alternateWindow = 0.45f;
        [SerializeField] private float _slashVfxScale = 1.25f;
        [SerializeField] private float _specialSlashVfxScale = 1.1f;
        private float _sinceNormalSwing = float.PositiveInfinity;
        private bool _upwardSwing;
        private bool _normalSwing;
        [SerializeField] private AttackData _heavy;
        [SerializeField] private float _chargedLungeDistance = 4f;
        [SerializeField] private float _chargedLungeDuration = 0.12f;
        private bool _chargedLunging;
        [SerializeField] private AttackData _dashAttack;
        [SerializeField] private AttackData _launcher;
        [SerializeField] private AttackData _plunge;
        [SerializeField] private AttackData _chargedStrike;
        [SerializeField] private float _dashAttackWindow = 0.15f;
        [SerializeField] private float _directionThreshold = 0.5f;
        [SerializeField] private float _attackBufferTime = 0.25f;

        [SerializeField] private Color _slashColor = new Color(0.85f, 0.95f, 1f, 0.9f);

        private PlayerController _controller;
        private PlayerInputReader _input;
        private PlayerHitResolver _hitResolver;
        private PlayerStats _stats;
        private ChargeAttackState _charge;
        private bool _plunging;
        private Vector2 _previousHitCenter;
        private readonly List<Collider2D> _hits = new List<Collider2D>(16);
        private readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
        private ContactFilter2D _filter;

        private AttackData _current;
        private float _attackTime;
        private float _bufferTimer;
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
            if (_slash == null)
            {
                return;
            }

            float dt = Time.deltaTime * TimeController.PlayerTimeMultiplier;
            _sinceNormalSwing += dt;

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
            }

            if (_chargedLunging)
            {
                if (_controller.IsHurt || _controller.IsDashing)
                {
                    _chargedLunging = false;
                    _controller.EndSkillMotion();
                    return;
                }
                if (_controller.IsInSkill) return;
                _chargedLunging = false;
                _controller.EndSkillMotion();
                StartAttack(_chargedStrike);
                return;
            }

            bool released = _charge.Tick(dt, _current == null, out bool charged);
            if (_charge.IsActive) return;
            if (released && _controller.CanAttack)
            {
                _bufferTimer = 0f;
                if (charged)
                {
                    _chargedLunging = true;
                    _controller.BeginSkillMotion(new Vector2(_controller.Facing * _chargedLungeDistance / _chargedLungeDuration, 0f),
                        _chargedLungeDuration, true, true);
                }
                else StartAttack(_heavy);
                return;
            }
            if (_current != null)
            {
                if (_plunging) UpdatePlunge(dt);
                else UpdateAttack(dt);
                return;
            }

            if (_controller.IsHurt || _controller.IsInSkill) return;
            if (_bufferTimer <= 0f) return;
            if (_controller.TimeSinceDash <= _dashAttackWindow && _dashAttack != null)
                StartAttack(_dashAttack);
            else if (_controller.CanAttack)
            {
                if (!_controller.IsGrounded && _input.MoveY < -_directionThreshold) StartPlunge();
                else if (_controller.IsGrounded && _input.MoveY > _directionThreshold) StartAttack(_launcher);
                else StartAttack(_controller.IsGrounded ? _slash : _airSlash);
            }
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
                SlashEffect.SpawnAnimation(VfxSprites.SlamAnimation, center, _current.LandingHitboxSize,
                    0f, _slashColor, _current.active);
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

            if (_attackTime >= _current.TotalDuration) _current = null;
        }

        private void StartAttack(AttackData attack)
        {
            if (attack == null) return;
            _controller.FaceInputDirection();
            _current = attack;
            _normalSwing = attack == _slash || attack == _airSlash;
            if (_normalSwing)
            {
                _upwardSwing = _sinceNormalSwing <= _alternateWindow && !_upwardSwing;
                _sinceNormalSwing = 0f;
            }
            _attackTime = 0f;
            _bufferTimer = 0f;
            _slashSpawned = false;
            _checkedOnce = false;
            _feedbackPlayed = false;
            _hitThisSwing.Clear();
            _previousHitCenter = HitboxCenter(_current);
            _controller.BeginAttack(_current.lungeSpeed, _current.TotalDuration / AttackSpeed,
                _current.SelfVelocity, _current.KeepMomentum, _normalSwing);
            SfxPlayer.Instance?.Play("swing");
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
            float angle = SwingAngle;
            float duration = Mathf.Max(0.08f, _current.active + 0.06f);
            float scale = _normalSwing ? _slashVfxScale : _specialSlashVfxScale;
            SlashEffect.Spawn(HitboxCenter(_current), _current.hitboxSize * scale, angle, _slashColor, duration,
                _controller.Facing < 0f, _normalSwing && _upwardSwing);
        }

        private float SwingAngle => _current.slashAngle * _controller.Facing * (_normalSwing && _upwardSwing ? -1f : 1f);

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
                bool firstHit = !_feedbackPlayed;
                float angle = SwingAngle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _controller.Facing;
                if (_hitResolver.TryHit(target, hit, center, spec, ref _feedbackPlayed, direction) && firstHit && _normalSwing)
                    _controller.ApplyNormalHitMotion();
            }
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            if (_chargedLunging || (_plunging && _controller.IsPlunging)) _controller.EndSkillMotion();
            _controller.CancelAttack();
            _charge.Cancel();
            _chargedLunging = false;
            _current = null;

            _plunging = false;
            _bufferTimer = 0f;

        }

        private void OnDrawGizmosSelected()
        {
            if (_slash == null) return;
            float facing = Application.isPlaying && _controller != null ? _controller.Facing : 1f;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Vector2 center = (Vector2)transform.position + new Vector2(_slash.hitboxOffset.x * facing, _slash.hitboxOffset.y);
            Gizmos.DrawWireCube(center, _slash.hitboxSize);
        }
    }
}
