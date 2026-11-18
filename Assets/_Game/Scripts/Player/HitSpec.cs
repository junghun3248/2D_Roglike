using UnityEngine;

namespace Roglike.Player
{
    public readonly struct HitSpec
    {
        public readonly float DamageMultiplier;
        public readonly Vector2 Knockback;
        public readonly float HitStun;
        public readonly float HitStop;
        public readonly float Shake;
        public readonly float OverdriveGain;
        public readonly bool IsFinisher;

        public HitSpec(float damageMultiplier, Vector2 knockback, float hitStun,
            float hitStop, float shake, float overdriveGain, bool isFinisher = false)
        {
            DamageMultiplier = damageMultiplier;
            Knockback = knockback;
            HitStun = hitStun;
            HitStop = hitStop;
            Shake = shake;
            OverdriveGain = overdriveGain;
            IsFinisher = isFinisher;
        }
    }
}
