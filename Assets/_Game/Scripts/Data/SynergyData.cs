using System;
using Roglike.Progression;
using UnityEngine;

namespace Roglike.Data
{
    [Serializable]
    public class SynergyTier
    {
        public int threshold;
        public StatModifier[] modifiers;
    }

    [CreateAssetMenu(menuName = "Roglike/Synergy Data", fileName = "Synergy")]
    public class SynergyData : ScriptableObject
    {
        public UpgradeTag tag;
        public string displayName;
        public Color color = Color.white;
        [Tooltip("필요 개수 오름차순으로 배치. 가장 높은 단계만 적용됩니다.")]
        public SynergyTier[] tiers;
    }
}
