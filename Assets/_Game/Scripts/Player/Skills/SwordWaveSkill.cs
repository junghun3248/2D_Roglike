using System.Collections;
using Roglike.Combat;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Player.Skills
{
    [CreateAssetMenu(menuName = "Roglike/Skills/Sword Wave")]
    public class SwordWaveSkill : SkillDefinition
    {
        [SerializeField] private float _speed = 16f;
        [SerializeField] private float _lifetime = 0.9f;
        [SerializeField] private float _recovery = 0.15f;
        [SerializeField] private Vector2 _spawnOffset = new Vector2(0.6f, 0f);
        [SerializeField] private Vector2 _size = new Vector2(0.7f, 2f);
        [SerializeField] private Vector2 _knockback = new Vector2(5f, 2f);
        [SerializeField] private float _hitStun = 0.2f;
        [SerializeField] private int _sortingOrder = 40;

        public override IEnumerator Execute(SkillContext context)
        {
            float facing = context.Facing;
            context.Controller.BeginSkillMotion(Vector2.zero, _recovery, true, false);
            var renderer = VfxSprites.CreateRenderer("SwordWave", VfxSprites.Circle, Color, _sortingOrder);
            renderer.transform.position = context.Position + new Vector2(_spawnOffset.x * facing, _spawnOffset.y);
            renderer.transform.localScale = new Vector3(_size.x, _size.y, 1f);
            var projectile = renderer.gameObject.AddComponent<SwordWaveProjectile>();
            projectile.Initialize(context, MakeHit(new Vector2(_knockback.x * facing, _knockback.y), _hitStun),
                facing * _speed, _lifetime, _size);
            for (float elapsed = 0f; elapsed < _recovery; elapsed += context.PlayerDeltaTime) yield return null;
        }
    }
}
