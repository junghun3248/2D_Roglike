using UnityEngine;

namespace Roglike.Combat
{
    /// <summary>한 번의 타격 정보. 후반 억 단위 데미지를 위해 Amount는 double.</summary>
    public readonly struct DamageInfo
    {
        public readonly double Amount;
        public readonly bool IsCritical;
        public readonly Vector2 Point;
        /// <summary>피격 대상에게 줄 속도 (월드 단위/초). 방향 포함.</summary>
        public readonly Vector2 Knockback;
        /// <summary>피격 대상이 행동 불능이 되는 시간 (게임 시간 초).</summary>
        public readonly float HitStun;
        public readonly GameObject Source;

        public DamageInfo(double amount, bool isCritical, Vector2 point, Vector2 knockback, float hitStun, GameObject source)
        {
            Amount = amount;
            IsCritical = isCritical;
            Point = point;
            Knockback = knockback;
            HitStun = hitStun;
            Source = source;
        }
    }
}
