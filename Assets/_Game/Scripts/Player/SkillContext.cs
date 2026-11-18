using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Player
{
    public class SkillContext
    {
        public PlayerController Controller { get; }
        public PlayerHitResolver HitResolver { get; }
        public Transform Transform { get; }
        public Rigidbody2D Rigidbody { get; }
        public PlayerInputReader Input { get; }
        public BoxCollider2D Collider { get; }
        public ContactFilter2D EnemyFilter { get; }
        public List<Collider2D> HitBuffer { get; } = new List<Collider2D>(16);
        private readonly Dictionary<Collider2D, Health> _healthCache = new Dictionary<Collider2D, Health>();

        public float Facing => Controller.Facing;
        public float PlayerDeltaTime => Time.deltaTime * TimeController.PlayerTimeMultiplier;
        public Vector2 Position => Rigidbody.position;
        public Vector2 Feet => new Vector2(Collider.bounds.center.x, Collider.bounds.min.y);

        public SkillContext(PlayerController controller, PlayerHitResolver hitResolver)
        {
            Controller = controller;
            HitResolver = hitResolver;
            Transform = controller.transform;
            Rigidbody = controller.GetComponent<Rigidbody2D>();
            Input = controller.GetComponent<PlayerInputReader>();
            Collider = controller.GetComponent<BoxCollider2D>();
            var filter = new ContactFilter2D();
            filter.SetLayerMask(GameLayers.EnemyMask);
            filter.useTriggers = true;
            EnemyFilter = filter;
        }

        public int OverlapEnemies(Vector2 center, Vector2 size)
            => Physics2D.OverlapBox(center, size, 0f, EnemyFilter, HitBuffer);

        public Health Target(Collider2D collider)
        {
            if (!_healthCache.TryGetValue(collider, out Health health))
            {
                health = collider.GetComponentInParent<Health>();
                _healthCache.Add(collider, health);
            }
            return health;
        }

        public bool HitAll(Vector2 center, Vector2 size, in HitSpec spec,
            HashSet<Health> alreadyHit, ref bool feedbackPlayed)
        {
            bool applied = false;
            int count = OverlapEnemies(center, size);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = HitBuffer[i];
                Health target = Target(collider);
                if (target == null || alreadyHit.Contains(target)) continue;
                if (!HitResolver.TryHit(target, collider, center, spec, ref feedbackPlayed)) continue;
                alreadyHit.Add(target);
                applied = true;
            }
            return applied;
        }
    }
}
