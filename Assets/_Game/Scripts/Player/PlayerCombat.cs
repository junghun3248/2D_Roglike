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
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Combo")]
        [SerializeField] private AttackData[] _combo;
        [SerializeField] private float _attackBufferTime = 0.25f;
        [SerializeField] private float _comboResetTime = 0.4f;

        [Header("Damage (M3에서 스탯 시스템으로 이동 예정)")]
        [SerializeField] private double _attackPower = 12;
        [SerializeField, Range(0f, 1f)] private float _critChance = 0.2f;
        [SerializeField] private float _critMultiplier = 2f;
        [SerializeField, Range(0f, 0.5f)] private float _damageVariance = 0.1f;

        [Header("Feel")]
        [SerializeField] private Color _slashColor = new Color(0.85f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color _sparkColor = Color.white;
        [SerializeField] private Color _critSparkColor = new Color(1f, 0.8f, 0.25f);
        [SerializeField] private float _critHitStopMultiplier = 1.4f;
        [SerializeField] private float _overdrivePunchZoom = 0.72f;
        [SerializeField] private float _finisherPunchZoom = 0.85f;
        [SerializeField] private float _killPunchZoom = 0.62f;
        [SerializeField] private float _punchDuration = 0.16f;
        [SerializeField] private float _killSlowScale = 0.2f;
        [SerializeField] private float _killSlowDuration = 0.35f;

        private PlayerController _controller;
        private PlayerInputReader _input;
        private OverdriveController _overdrive;
        private PlayerStats _stats;
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
            _overdrive = GetComponent<OverdriveController>();
            _stats = GetComponent<PlayerStats>();

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
            if (_current != null && (interrupted || _controller.IsDashing || _controller.IsHurt))
            {
                // 대시·피격·점프로 공격이 끊겼다.
                _current = null;
                _nextIndex = 0;
            }

            if (_current != null)
            {
                UpdateAttack(dt);
                return;
            }

            _comboResetTimer -= dt;
            if (_comboResetTimer <= 0f)
            {
                _nextIndex = 0;
            }

            if (_bufferTimer > 0f && _controller.CanAttack)
            {
                StartAttack(_nextIndex);
            }
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

            bool isLast = _comboIndex >= _combo.Length - 1;
            float chainTime = Mathf.Max(_current.cancelTime, activeEnd);
            if (!isLast && _bufferTimer > 0f && _attackTime >= chainTime)
            {
                StartAttack(_comboIndex + 1);
                return;
            }

            if (_attackTime >= _current.TotalDuration)
            {
                _current = null;
                _nextIndex = isLast ? 0 : _comboIndex + 1;
                _comboResetTimer = _comboResetTime;
            }
        }

        private void StartAttack(int index)
        {
            _controller.FaceInputDirection();
            _comboIndex = Mathf.Clamp(index, 0, _combo.Length - 1);
            _current = _combo[_comboIndex];
            _attackTime = 0f;
            _bufferTimer = 0f;
            _slashSpawned = false;
            _checkedOnce = false;
            _feedbackPlayed = false;
            _hitThisSwing.Clear();
            _controller.BeginAttack(_current.lungeSpeed, _current.TotalDuration / AttackSpeed);
        }

        // PlayerStats가 있으면 런 강화가 반영된 값을, 없으면(테스트 씬 등) 인스펙터 기본값을 쓴다.
        private float CritChance => _stats != null ? _stats.Get(StatType.CritChance) : _critChance;
        private float CritMultiplier => _stats != null ? _stats.Get(StatType.CritDamage) : _critMultiplier;
        private float AttackSpeed => _stats != null ? Mathf.Max(0.2f, _stats.Get(StatType.AttackSpeed)) : 1f;

        private double BaseHitDamage(float skillMultiplier)
        {
            return _stats != null ? _stats.BaseHitDamage(skillMultiplier) : _attackPower * skillMultiplier;
        }

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
            int count = Physics2D.OverlapBox(center, _current.hitboxSize, 0f, _filter, _hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                Health target = hit.GetComponentInParent<Health>();
                if (target == null || target.IsDead || target.Team == Team.Player || !_hitThisSwing.Add(target))
                {
                    continue;
                }

                bool isCritical = Random.value < CritChance;
                double damage = BaseHitDamage(_current.damageMultiplier)
                    * (1f + Random.Range(-_damageVariance, _damageVariance))
                    * (isCritical ? CritMultiplier : 1f)
                    * (_overdrive != null ? _overdrive.DamageMultiplier : 1f);

                Vector2 point = (Vector2)hit.bounds.center + Random.insideUnitCircle * 0.2f;
                Vector2 knockback = new Vector2(_current.knockback.x * _controller.Facing, _current.knockback.y);
                var info = new DamageInfo(damage, isCritical, point, knockback, _current.hitStun, gameObject);

                if (!target.TryApplyDamage(info))
                {
                    continue;
                }

                HitSpark.Spawn(point, isCritical ? _critSparkColor : _sparkColor, isCritical ? 10 : 7, isCritical ? 12f : 9f);
                OnHitLanded(info, target);
            }
        }

        private void OnHitLanded(DamageInfo info, Health target)
        {
            bool overdriveActive = _overdrive != null && _overdrive.IsActive;
            _overdrive?.AddGauge(_current.overdriveGain);

            // 한 번의 휘두르기에 여러 마리를 맞혀도 연출은 한 번만.
            if (!_feedbackPlayed)
            {
                _feedbackPlayed = true;
                float hitStop = _current.hitStop * (info.IsCritical ? _critHitStopMultiplier : 1f);
                TimeController.Instance?.HitStop(hitStop);
                GameCamera.Instance?.Shake(_current.shake * (info.IsCritical ? 1.3f : 1f));

                if (overdriveActive)
                {
                    GameCamera.Instance?.PunchZoom(info.Point, _overdrivePunchZoom, _punchDuration);
                }
                else if (_current.isFinisher)
                {
                    GameCamera.Instance?.PunchZoom(info.Point, _finisherPunchZoom, _punchDuration * 0.7f);
                }
            }

            if (target.IsDead && (_current.isFinisher || overdriveActive))
            {
                TimeController.Instance?.PulseSlow(_killSlowScale, _killSlowDuration);
                GameCamera.Instance?.PunchZoom(info.Point, _killPunchZoom, _killSlowDuration);
            }
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
