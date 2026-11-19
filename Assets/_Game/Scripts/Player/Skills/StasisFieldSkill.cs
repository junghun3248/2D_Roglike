using System.Collections;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Player.Skills
{
    [CreateAssetMenu(menuName = "Roglike/Skills/Stasis Field")]
    public class StasisFieldSkill : SkillDefinition
    {
        [SerializeField] private float _distance = 3f;
        [SerializeField] private float _delay = 0.15f;
        [SerializeField] private float _radius = 3.5f;
        [SerializeField] private float _freezeDuration = 1.5f;
        [SerializeField] private float _sigilDuration = 0.35f;

        public override IEnumerator Execute(SkillContext context)
        {
            Vector2 center = context.Position + Vector2.right * (context.Facing * _distance);
            StasisSigil.Spawn(center, _radius, Color, _delay + _sigilDuration);
            for (float elapsed = 0f; elapsed < _delay; elapsed += context.PlayerDeltaTime) yield return null;

            var seen = new HashSet<Health>();
            bool feedback = false;
            var spec = MakeHit(Vector2.zero, 0f);
            int count = Physics2D.OverlapCircle(center, _radius, context.EnemyFilter, context.HitBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = context.HitBuffer[i];
                Health target = context.Target(collider);
                if (target == null || target.IsDead || target.Team == Team.Player || !seen.Add(target)) continue;
                context.HitResolver.TryHit(target, collider, center, spec, ref feedback);
                StatusEffects status = target.GetComponent<StatusEffects>();
                if (status == null) status = target.gameObject.AddComponent<StatusEffects>();
                status.Freeze(_freezeDuration);
            }
        }
    }
}
