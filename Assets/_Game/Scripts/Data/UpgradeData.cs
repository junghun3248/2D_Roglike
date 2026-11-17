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

    /// <summary>방을 클리어할 때 3개 중 1개를 고르는 강화. 콘텐츠 추가 = 이 에셋 추가.</summary>
    [CreateAssetMenu(menuName = "Roglike/Upgrade Data", fileName = "Upgrade")]
    public class UpgradeData : ScriptableObject
    {
        public string displayName = "강화";
        [TextArea] public string description;
        public UpgradeRarity rarity = UpgradeRarity.Common;
        [Tooltip("한 런에서 최대 몇 번 고를 수 있는지")]
        public int maxStacks = 5;
        public StatModifier[] modifiers;
    }
}
