using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roglike.Progression
{
    public static class UpgradeText
    {
        public static string Describe(IReadOnlyList<StatModifier> modifiers)
        {
            if (modifiers == null) return string.Empty;

            var text = new StringBuilder();
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (i > 0) text.Append('\n');
                StatModifier modifier = modifiers[i];
                text.Append(StatName(modifier.stat)).Append(' ');
                if (modifier.kind == ModifierKind.Multiply)
                {
                    text.Append('×').Append(modifier.value.ToString("0.##", CultureInfo.InvariantCulture));
                    continue;
                }

                bool percent = modifier.kind == ModifierKind.PercentAdd || IsRatio(modifier.stat);
                double value = percent ? modifier.value * 100.0 : modifier.value;
                text.Append(value.ToString("+0.##;-0.##;+0", CultureInfo.InvariantCulture));
                if (percent) text.Append('%');
                else if (modifier.stat == StatType.OverdriveDuration) text.Append("초");
                else if (modifier.stat == StatType.Rerolls) text.Append("회");
            }

            return text.ToString();
        }

        public static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.AttackPower: return "공격력";
                case StatType.DamageIncrease: return "피해 증가";
                case StatType.FinalDamage: return "최종 피해";
                case StatType.CritChance: return "치명타 확률";
                case StatType.CritDamage: return "치명타 피해";
                case StatType.AttackSpeed: return "공격 속도";
                case StatType.MoveSpeed: return "이동 속도";
                case StatType.MaxHealth: return "최대 체력";
                case StatType.OverdriveGain: return "오버드라이브 충전";
                case StatType.OverdriveDuration: return "오버드라이브 지속";
                case StatType.Rerolls: return "강화 다시 뽑기";
                default: return stat.ToString();
            }
        }

        private static bool IsRatio(StatType stat)
        {
            return stat == StatType.DamageIncrease || stat == StatType.FinalDamage
                || stat == StatType.CritChance || stat == StatType.CritDamage
                || stat == StatType.AttackSpeed || stat == StatType.MoveSpeed
                || stat == StatType.OverdriveGain;
        }
    }
}
