using System.Collections;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Player.Skills
{
    [CreateAssetMenu(menuName = "Roglike/Skills/Dash Slash")]
    public class DashSlashSkill : SkillDefinition
    {
        [SerializeField] private float _windup = 0.05f;
        [SerializeField] private float _speed = 22f;
        [SerializeField] private float _duration = 0.22f;
        [SerializeField] private Vector2 _hitbox = new Vector2(2f, 2f);
        [SerializeField] private Vector2 _knockback = new Vector2(10f, 5f);
        [SerializeField] private float _hitStun = 0.3f;
        [SerializeField] private Vector2 _slashSize = new Vector2(5f, 2f);
        [SerializeField] private float _visualInterval = 0.04f;
        [SerializeField] private float _visualDuration = 0.18f;

        public override IEnumerator Execute(SkillContext context)
        {
            var hit = new HashSet<Health>();
            bool feedback = false;
            context.Controller.BeginSkillMotion(Vector2.zero, _windup, true, false);
            for (float elapsed = 0f; elapsed < _windup; elapsed += context.PlayerDeltaTime) yield return null;

            float facing = context.Facing;
            var spec = MakeHit(new Vector2(_knockback.x * facing, _knockback.y), _hitStun, true);
            context.Controller.BeginSkillMotion(new Vector2(facing * _speed, 0f), _duration, true, true);
            Vector2 previous = context.Position;
            float visualTimer = 0f;
            float elapsedDash = 0f;
            do
            {
                Vector2 current = context.Position;
                Vector2 size = _hitbox + new Vector2(Mathf.Abs(current.x - previous.x), Mathf.Abs(current.y - previous.y));
                context.HitAll((previous + current) * 0.5f, size, spec, hit, ref feedback);
                previous = current;
                if (visualTimer <= 0f)
                {
                    SlashEffect.Spawn(current, _slashSize, 0f, Color, _visualDuration);
                    Afterimage.Spawn(context.Controller.BodyRenderer, Color, _visualDuration);
                    visualTimer = _visualInterval;
                }
                yield return null;
                elapsedDash += context.PlayerDeltaTime;
                visualTimer -= Time.unscaledDeltaTime;
            } while (elapsedDash < _duration);
            context.HitAll(context.Position, _hitbox, spec, hit, ref feedback);
        }
    }
}
