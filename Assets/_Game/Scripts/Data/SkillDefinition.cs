using System.Collections;
using Roglike.Player;
using UnityEngine;

namespace Roglike.Data
{
    public abstract class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private Sprite _icon;
        [SerializeField] private string _displayName;
        [SerializeField] private string _shortName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private Color _color = Color.cyan;
        [SerializeField, Min(0f)] private float _cooldown = 4f;
        [SerializeField] private float _damageMultiplier = 1f;
        [SerializeField] private float _hitStop = 0.06f;
        [SerializeField] private float _shake = 0.3f;
        [SerializeField] private float _overdriveGain = 6f;

        public string Id => _id;
        public Sprite Icon => _icon;
        public string DisplayName => _displayName;
        public string ShortName => _shortName;
        public string Description => _description;
        public Color Color => _color;
        public float Cooldown => _cooldown;
        public float DamageMultiplier => _damageMultiplier;
        public float HitStop => _hitStop;
        public float Shake => _shake;
        public float OverdriveGain => _overdriveGain;

        protected HitSpec MakeHit(Vector2 knockback, float hitStun, bool finisher = false)
            => new HitSpec(DamageMultiplier, knockback, hitStun, HitStop, Shake, OverdriveGain, finisher);

        public abstract IEnumerator Execute(SkillContext context);
    }
}
