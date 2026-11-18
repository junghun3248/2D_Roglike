using System.Collections.Generic;
using Roglike.Combat;
using UnityEngine;

namespace Roglike.Player.Skills
{
    public class SwordWaveProjectile : MonoBehaviour
    {
        private SkillContext _context;
        private HitSpec _spec;
        private float _speed;
        private float _remaining;
        private Vector2 _size;
        private readonly HashSet<Health> _hit = new HashSet<Health>();
        private bool _feedbackPlayed;

        public void Initialize(SkillContext context, in HitSpec spec, float speed, float lifetime, Vector2 size)
        {
            _context = new SkillContext(context.Controller, context.HitResolver);
            _spec = spec;
            _speed = speed;
            _remaining = lifetime;
            _size = size;
        }

        private void Update()
        {
            if (_context == null || _context.HitResolver == null)
            {
                Destroy(gameObject);
                return;
            }
            if (Time.deltaTime <= 0f) return;
            float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0f, _remaining));
            Vector2 previous = transform.position;
            Vector2 current = previous + Vector2.right * (_speed * dt);
            _context.HitAll((previous + current) * 0.5f,
                _size + new Vector2(Mathf.Abs(current.x - previous.x), 0f), _spec, _hit, ref _feedbackPlayed);
            transform.position = current;
            _remaining -= dt;
            if (_remaining <= 0f) Destroy(gameObject);
        }
    }
}
