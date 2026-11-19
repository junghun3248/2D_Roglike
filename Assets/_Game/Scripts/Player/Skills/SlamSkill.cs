using System.Collections;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Player.Skills
{
    [CreateAssetMenu(menuName = "Roglike/Skills/Slam")]
    public class SlamSkill : SkillDefinition
    {
        [SerializeField] private float _hopDuration = 0.12f;
        [SerializeField] private float _hopSpeed = 12f;
        [SerializeField] private float _diveSpeed = 32f;
        [SerializeField] private float _maxDiveDuration = 1f;
        [SerializeField] private Vector2 _hitbox = new Vector2(5f, 2f);
        [SerializeField] private Vector2 _knockback = new Vector2(10f, 14f);
        [SerializeField] private float _hitStun = 0.45f;
        [SerializeField] private Vector2 _slashSize = new Vector2(6f, 3f);
        [SerializeField] private float _visualDuration = 0.3f;

        public override IEnumerator Execute(SkillContext context)
        {
            var hit = new HashSet<Health>();
            bool feedback = false;
            if (context.Controller.IsGrounded)
            {
                context.Controller.BeginSkillMotion(Vector2.up * _hopSpeed, _hopDuration, true, false);
                for (float elapsed = 0f; elapsed < _hopDuration; elapsed += context.PlayerDeltaTime) yield return null;
            }
            context.Controller.BeginSkillMotion(Vector2.down * _diveSpeed, _maxDiveDuration, true, true);
            float diveElapsed = 0f;
            yield return new WaitForFixedUpdate();
            while (!context.Controller.IsGrounded && diveElapsed < _maxDiveDuration)
            {
                yield return null;
                diveElapsed += context.PlayerDeltaTime;
            }
            if (!context.Controller.IsGrounded) yield break;

            Vector2 center = context.Feet;
            int count = context.OverlapEnemies(center, _hitbox);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = context.HitBuffer[i];
                Health target = context.Target(collider);
                if (target == null || hit.Contains(target)) continue;
                float direction = collider.bounds.center.x < center.x ? -1f : 1f;
                var spec = MakeHit(new Vector2(_knockback.x * direction, _knockback.y), _hitStun, true);
                if (context.HitResolver.TryHit(target, collider, center, spec, ref feedback)) hit.Add(target);
            }
            SlashEffect.SpawnAnimation(VfxSprites.SlamAnimation, center, _slashSize, 0f, Color, _visualDuration);
        }
    }
}
