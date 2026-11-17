using System.Collections.Generic;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Progression
{
    public class UpgradePool
    {
        private readonly List<UpgradeData> _all = new List<UpgradeData>();
        private readonly Dictionary<UpgradeData, int> _stacks = new Dictionary<UpgradeData, int>();

        public UpgradePool(IReadOnlyList<UpgradeData> all)
        {
            if (all == null) return;
            var seen = new HashSet<UpgradeData>();
            for (int i = 0; i < all.Count; i++)
            {
                UpgradeData upgrade = all[i];
                if (upgrade != null && seen.Add(upgrade)) _all.Add(upgrade);
            }
        }

        public List<UpgradeData> Roll(int count)
        {
            var result = new List<UpgradeData>();
            if (count <= 0) return result;

            var candidates = new List<UpgradeData>();
            int totalWeight = 0;
            foreach (UpgradeData upgrade in _all)
            {
                if (upgrade == null || Stacks(upgrade) >= upgrade.maxStacks) continue;
                candidates.Add(upgrade);
                totalWeight += Weight(upgrade.rarity);
            }

            while (result.Count < count && candidates.Count > 0)
            {
                int roll = Random.Range(0, totalWeight);
                for (int i = 0; i < candidates.Count; i++)
                {
                    UpgradeData upgrade = candidates[i];
                    int weight = Weight(upgrade.rarity);
                    if (roll >= weight)
                    {
                        roll -= weight;
                        continue;
                    }

                    result.Add(upgrade);
                    candidates.RemoveAt(i);
                    totalWeight -= weight;
                    break;
                }
            }

            return result;
        }

        public void Take(UpgradeData upgrade)
        {
            if (upgrade != null) _stacks[upgrade] = Stacks(upgrade) + 1;
        }

        public int Stacks(UpgradeData upgrade)
        {
            return upgrade != null && _stacks.TryGetValue(upgrade, out int count) ? count : 0;
        }

        private static int Weight(UpgradeRarity rarity)
        {
            switch (rarity)
            {
                case UpgradeRarity.Rare: return 30;
                case UpgradeRarity.Epic: return 10;
                default: return 60;
            }
        }
    }
}
