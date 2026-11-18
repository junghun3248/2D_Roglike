using System.Collections;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Player.Skills
{
    [CreateAssetMenu(menuName = "Roglike/Skills/Whirlwind")]
    public class WhirlwindSkill : SkillDefinition
    {
        [SerializeField] private float _duration = 0.7f;
        [SerializeField, Min(0.01f)] private float _tickInterval = 0.12f;
        [SerializeField] private float _radius = 2.5f;
        [SerializeField] private float _moveMultiplier = 0.4f;
        [SerializeField] private Vector2 _knockback = new Vector2(1f, 0.5f);
        [SerializeField] private float _hitStun = 0.15f;
        [SerializeField] private Vector2 _slashSize = new Vector2(4.5f, 2f);
        [SerializeField] private float _rotationSpeed = 900f;
        [SerializeField] private float _visualDuration = 0.16f;

        public override IEnumerator Execute(SkillContext context)
        {
            var hit = new HashSet<Health>();
            bool feedback = false;
            float elapsed = 0f;
            float nextTick = 0f;
            while (elapsed < _duration)
            {
                context.Controller.BeginSkillMotion(new Vector2(context.Input.MoveX * context.Controller.MoveSpeed * _moveMultiplier, 0f),
                    _duration - elapsed, false, false);
                if (elapsed >= nextTick)
                {
                    nextTick += Mathf.Max(_tickInterval, Mathf.Epsilon);
                    hit.Clear();
                    Vector2 center = context.Position;
                    int count = Physics2D.OverlapCircle(center, _radius, context.EnemyFilter, context.HitBuffer);
                    for (int i = 0; i < count; i++)
                    {
                        Collider2D collider = context.HitBuffer[i];
                        Health target = context.Target(collider);
                        if (target == null || hit.Contains(target)) continue;
                        float direction = collider.bounds.center.x < center.x ? -1f : 1f;
                        var spec = MakeHit(new Vector2(_knockback.x * direction, _knockback.y), _hitStun);
                        if (context.HitResolver.TryHit(target, collider, center, spec, ref feedback)) hit.Add(target);
                    }
                    SlashEffect.Spawn(center, _slashSize, elapsed * _rotationSpeed, Color, _visualDuration);
                }
                yield return null;
                elapsed += context.PlayerDeltaTime;
            }
        }
    }
}
