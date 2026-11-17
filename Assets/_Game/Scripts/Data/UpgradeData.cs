using Roglike.Progression;
using UnityEngine;

namespace Roglike.Data
{
    public enum UpgradeRarity
    {
        Common,
        Rare,
        Epic,
    }

    /// <summary>
    /// 강화 계열 (롤체 시너지처럼 같은 계열을 모으면 2/4/6단계 보너스).
    /// 새 계열을 추가하면 SynergyData 에셋도 함께 추가한다.
    /// </summary>
    public enum UpgradeTag
    {
        /// <summary>검술: 공격력 계열</summary>
        Blade,
        /// <summary>치명: 치명타 계열</summary>
        Critical,
        /// <summary>질풍: 공격·이동 속도 계열</summary>
        Tempo,
        /// <summary>파괴: 피해 증가·최종 피해 계열</summary>
        Fury,
        /// <summary>과부하: 오버드라이브 계열</summary>
        Overdrive,
        /// <summary>생존: 체력 계열</summary>
        Vitality,
    }

    /// <summary>방을 클리어할 때 3개 중 1개를 고르는 강화. 콘텐츠 추가 = 이 에셋 추가.</summary>
    [CreateAssetMenu(menuName = "Roglike/Upgrade Data", fileName = "Upgrade")]
    public class UpgradeData : ScriptableObject
    {
        public string displayName = "강화";
        [TextArea] public string description;
        public UpgradeRarity rarity = UpgradeRarity.Common;
        [Tooltip("한 런에서 최대 몇 번 고를 수 있는지")]
        public int maxStacks = 5;
        [Tooltip("시너지 계열. 고를 때마다 각 계열 카운트가 1씩 오른다")]
        public UpgradeTag[] tags;
        public StatModifier[] modifiers;
    }
}
