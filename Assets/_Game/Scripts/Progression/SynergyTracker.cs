using System;
using System.Collections.Generic;
using Roglike.Data;

namespace Roglike.Progression
{
    public class SynergyTracker
    {
        private readonly PlayerStats _stats;
        private readonly Dictionary<UpgradeTag, int> _counts = new Dictionary<UpgradeTag, int>();
        private readonly Dictionary<UpgradeTag, SynergyData> _byTag = new Dictionary<UpgradeTag, SynergyData>();
        private readonly Dictionary<UpgradeTag, int> _active = new Dictionary<UpgradeTag, int>();

        public IReadOnlyList<SynergyData> Synergies { get; }
        public event Action<SynergyData, int> TierReached;
        public event Action Changed;

        public SynergyTracker(PlayerStats stats, IReadOnlyList<SynergyData> synergies)
        {
            _stats = stats != null ? stats : throw new ArgumentNullException(nameof(stats));
            var entries = new List<SynergyData>();
            if (synergies != null)
            {
                foreach (SynergyData synergy in synergies)
                {
                    if (synergy == null || _byTag.ContainsKey(synergy.tag)) continue;
                    entries.Add(synergy);
                    _byTag.Add(synergy.tag, synergy);
                    _active.Add(synergy.tag, -1);
                }
            }
            Synergies = entries.AsReadOnly();
        }

        public void Add(UpgradeData upgrade)
        {
            if (upgrade == null) return;
            if (upgrade.tags != null)
                foreach (UpgradeTag tag in upgrade.tags) _counts[tag] = Count(tag) + 1;

            var reached = new List<SynergyData>();
            foreach (SynergyData synergy in Synergies)
            {
                int previous = ActiveTierIndex(synergy.tag);
                int next = -1;
                if (synergy.tiers != null)
                    for (int i = 0; i < synergy.tiers.Length; i++)
                        if (synergy.tiers[i] != null && Count(synergy.tag) >= synergy.tiers[i].threshold) next = i;
                if (previous == next) continue;
                _active[synergy.tag] = next;
                if (next >= 0) _stats.SetModifiers(synergy, synergy.tiers[next].modifiers);
                else _stats.RemoveModifiers(synergy);
                if (next > previous) reached.Add(synergy);
            }

            // 행을 먼저 갱신해야 새로 나타난 행에도 발동 연출을 적용할 수 있다.
            Changed?.Invoke();
            foreach (SynergyData synergy in reached) TierReached?.Invoke(synergy, ActiveTierIndex(synergy.tag));
        }

        public int Count(UpgradeTag tag) => _counts.TryGetValue(tag, out int count) ? count : 0;
        public int ActiveTierIndex(UpgradeTag tag) => _active.TryGetValue(tag, out int index) ? index : -1;
        public SynergyData Get(UpgradeTag tag) => _byTag.TryGetValue(tag, out SynergyData synergy) ? synergy : null;
        public SynergyTier ActiveTier(UpgradeTag tag)
        {
            int index = ActiveTierIndex(tag);
            return index >= 0 ? Get(tag).tiers[index] : null;
        }
    }
}
