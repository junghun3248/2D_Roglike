using Roglike.Combat;
using Roglike.Core;
using Roglike.Progression;
using UnityEngine;

namespace Roglike.Player
{
    public class PlayerHitResolver : MonoBehaviour
    {
        [Header("Damage (PlayerStats가 없을 때의 기본값)")]
        [SerializeField] private double _attackPower = 12;
        [SerializeField, Range(0f, 1f)] private float _critChance = 0.2f;
        [SerializeField] private float _critMultiplier = 2f;
        [SerializeField, Range(0f, 0.5f)] private float _damageVariance = 0.1f;

        [Header("Feel")]
        [SerializeField] private Color _sparkColor = Color.white;
        [SerializeField] private Color _critSparkColor = new Color(1f, 0.8f, 0.25f);
        [SerializeField] private float _critHitStopMultiplier = 1.4f;
        [SerializeField] private float _critPunchZoom = 0.94f;
        [SerializeField] private float _directionalKick = 0.12f;
        [SerializeField] private float _sparkSpread = 0.65f;
        [SerializeField] private float _sparkSize = 0.22f;
        [SerializeField] private Vector2 _impactSize = new Vector2(1.3f, 1.3f);
        [SerializeField] private float _impactDuration = 0.1f;
        [SerializeField] private float _critImpactScale = 1.25f;
        private PlayerController _controller;
        [SerializeField] private float _overdrivePunchZoom = 0.72f;
        [SerializeField] private float _finisherPunchZoom = 0.85f;
        [SerializeField] private float _killPunchZoom = 0.62f;
        [SerializeField] private float _punchDuration = 0.16f;
        [SerializeField] private float _killSlowScale = 0.2f;
        [SerializeField] private float _killSlowDuration = 0.35f;

        [SerializeField] private float _hitPointScatter = 0.2f;
        [SerializeField] private int _sparkCount = 7;
        [SerializeField] private int _critSparkCount = 10;
        [SerializeField] private float _sparkSpeed = 9f;
        [SerializeField] private float _critSparkSpeed = 12f;
        [SerializeField] private float _critShakeMultiplier = 1.3f;
        [SerializeField] private float _finisherPunchDurationMultiplier = 0.7f;

        private PlayerStats _stats;
        private OverdriveController _overdrive;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _controller = GetComponent<PlayerController>();
            _overdrive = GetComponent<OverdriveController>();
        }

        public bool TryHit(Health target, Collider2D hitCollider, Vector2 origin,
            in HitSpec spec, ref bool feedbackPlayed, Vector2 swingDirection = default)
        {
            if (target == null || hitCollider == null || target.IsDead
                || target.Team == Team.Player || target.IsInvulnerable) return false;

            float critChance = _stats != null ? _stats.Get(StatType.CritChance) : _critChance;
            float critMultiplier = _stats != null ? _stats.Get(StatType.CritDamage) : _critMultiplier;
            bool isCritical = Random.value < critChance;
            double damage = (_stats != null ? _stats.BaseHitDamage(spec.DamageMultiplier) : _attackPower * spec.DamageMultiplier)
                * (1f + Random.Range(-_damageVariance, _damageVariance))
                * (isCritical ? critMultiplier : 1f)
                * (_overdrive != null ? _overdrive.DamageMultiplier : 1f);
            if (target.TryGetComponent<StatusEffects>(out var status)) damage *= status.DamageTakenMultiplier;
            Vector2 point = (Vector2)hitCollider.bounds.center + Random.insideUnitCircle * _hitPointScatter;
            var info = new DamageInfo(damage, isCritical, point, spec.Knockback, spec.HitStun, gameObject);
            if (!target.TryApplyDamage(info)) return false;

            HitSpark.Spawn(point, isCritical ? _critSparkColor : _sparkColor,
                isCritical ? _critSparkCount : _sparkCount, isCritical ? _critSparkSpeed : _sparkSpeed,
                _sparkSize, Vector2.right * (_controller != null ? _controller.Facing : 1f), _sparkSpread);
            Vector2 direction = swingDirection == Vector2.zero ? spec.Knockback.normalized : swingDirection.normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            SlashEffect.SpawnAnimation(VfxSprites.ImpactAnimation, point,
                _impactSize * (isCritical ? _critImpactScale : 1f), angle, Color.white, _impactDuration);
            OnHitLanded(info, target, spec, ref feedbackPlayed, swingDirection);
            return true;
        }

        private void OnHitLanded(DamageInfo info, Health target, in HitSpec spec, ref bool feedbackPlayed, Vector2 swingDirection)
        {
            bool overdriveActive = _overdrive != null && _overdrive.IsActive;
            _overdrive?.AddGauge(spec.OverdriveGain);

            // 한 번의 휘두르기에 여러 마리를 맞혀도 연출은 한 번만.
            if (!feedbackPlayed)
            {
                feedbackPlayed = true;
                float hitStop = spec.HitStop * (info.IsCritical ? _critHitStopMultiplier : 1f);
                TimeController.Instance?.HitStop(hitStop);
                SfxPlayer.Instance?.Play(target.IsDead ? "kill" : info.IsCritical ? "hit_crit" : "hit");
                Vector2 direction = swingDirection == Vector2.zero ? spec.Knockback.normalized : swingDirection.normalized;
                GameCamera.Instance?.Impulse(direction * _directionalKick);
                GameCamera.Instance?.Shake(spec.Shake * (info.IsCritical ? _critShakeMultiplier : 1f));

                if (overdriveActive)
                {
                    GameCamera.Instance?.PunchZoom(info.Point, _overdrivePunchZoom, _punchDuration);
                }
                else if (spec.IsFinisher)
                {
                    GameCamera.Instance?.PunchZoom(info.Point, _finisherPunchZoom, _punchDuration * _finisherPunchDurationMultiplier);
                }
                else if (info.IsCritical)
                    GameCamera.Instance?.PunchZoom(info.Point, _critPunchZoom, _punchDuration);
            }

            if (target.IsDead && (spec.IsFinisher || overdriveActive))
            {
                TimeController.Instance?.PulseSlow(_killSlowScale, _killSlowDuration);
                GameCamera.Instance?.PunchZoom(info.Point, _killPunchZoom, _killSlowDuration);
            }
        }

    }
}
