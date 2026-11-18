using NUnit.Framework;
using Roglike.Data;
using Roglike.Progression;
using UnityEngine;

namespace Roglike.Tests
{
    public class UpgradePoolTests
    {
        private UpgradeData[] _upgrades;
        private UpgradePool _pool;

        [SetUp]
        public void SetUp()
        {
            _upgrades = new UpgradeData[4];
            for (int i = 0; i < _upgrades.Length; i++)
            {
                _upgrades[i] = ScriptableObject.CreateInstance<UpgradeData>();
                _upgrades[i].rarity = i == 0 ? UpgradeRarity.Common : i == 3 ? UpgradeRarity.Epic : UpgradeRarity.Rare;
                _upgrades[i].maxStacks = 1;
            }
            _pool = new UpgradePool(_upgrades);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UpgradeData upgrade in _upgrades) Object.DestroyImmediate(upgrade);
        }

        [Test]
        public void RareRoll_ExcludesCommon_WhenEnoughEligibleOptionsExist()
        {
            var options = _pool.Roll(3, UpgradeRarity.Rare);
            Assert.AreEqual(3, options.Count);
            CollectionAssert.AllItemsAreUnique(options);
            foreach (UpgradeData option in options) Assert.AreNotEqual(UpgradeRarity.Common, option.rarity);
        }

        [Test]
        public void RareRoll_FillsOnlyMissingSlots_AndExcludesMaxedUpgrades()
        {
            _pool.Take(_upgrades[1]);
            var options = _pool.Roll(3, UpgradeRarity.Rare);
            CollectionAssert.AreEquivalent(new[] { _upgrades[0], _upgrades[2], _upgrades[3] }, options);
            Assert.AreNotEqual(UpgradeRarity.Common, options[0].rarity);
            Assert.AreNotEqual(UpgradeRarity.Common, options[1].rarity);
        }

        [Test]
        public void ExhaustedPool_ReturnsEmpty_WithoutRecursingForever()
        {
            foreach (UpgradeData upgrade in _upgrades) _pool.Take(upgrade);
            Assert.IsEmpty(_pool.Roll(3, UpgradeRarity.Rare));
        }
    }
}
