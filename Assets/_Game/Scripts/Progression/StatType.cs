using System;

namespace Roglike.Progression
{
    /// <summary>
    /// 플레이어 능력치. 최종 값 = (기본 + Flat 합) × (1 + PercentAdd 합) × (Multiply 곱).
    /// 데미지 = 공격력 × 스킬 배율 × (1 + 피해 증가) × 최종 피해 × 치명타 피해 — 계층이 곱해져 후반 숫자가 폭발한다 (GDD 5절).
    /// </summary>
    public enum StatType
    {
        /// <summary>공격력. 기본 12.</summary>
        AttackPower,
        /// <summary>피해 증가(합연산). 기본 0, 0.2 = +20%.</summary>
        DamageIncrease,
        /// <summary>최종 피해(곱연산). 기본 1, Multiply 1.15 = ×1.15.</summary>
        FinalDamage,
        /// <summary>치명타 확률. 기본 0.2.</summary>
        CritChance,
        /// <summary>치명타 피해 배율. 기본 2.0.</summary>
        CritDamage,
        /// <summary>공격 속도 배율. 기본 1.</summary>
        AttackSpeed,
        /// <summary>이동 속도 배율. 기본 1.</summary>
        MoveSpeed,
        /// <summary>최대 체력. 기본 100.</summary>
        MaxHealth,
        /// <summary>오버드라이브 게이지 충전 배율. 기본 1.</summary>
        OverdriveGain,
        /// <summary>오버드라이브 지속 시간(초). 기본 5.</summary>
        OverdriveDuration,
    }

    public enum ModifierKind
    {
        /// <summary>기본값에 더한다.</summary>
        Flat,
        /// <summary>같은 종류끼리 합산한 뒤 (1 + 합)을 곱한다.</summary>
        PercentAdd,
        /// <summary>각각 곱한다.</summary>
        Multiply,
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public ModifierKind kind;
        public float value;

        public StatModifier(StatType stat, ModifierKind kind, float value)
        {
            this.stat = stat;
            this.kind = kind;
            this.value = value;
        }
    }
}
