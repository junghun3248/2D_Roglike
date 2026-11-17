using Roglike.Progression;
using UnityEngine;

namespace Roglike.Data
{
    [CreateAssetMenu(menuName = "Roglike/Meta Upgrade Data")]
    public class MetaUpgradeData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public int maxLevel = 5;
        public int baseCost = 20;
        public float costGrowth = 1.6f;
        public StatModifier[] modifiersPerLevel;

        public int CostForLevel(int currentLevel)
        {
            return Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, currentLevel));
        }
    }
}
