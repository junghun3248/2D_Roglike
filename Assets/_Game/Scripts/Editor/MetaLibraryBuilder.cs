using Roglike.Data;
using Roglike.Progression;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class MetaLibraryBuilder
    {
        private const string Folder = "Assets/_Game/Data/Meta";

        public static MetaUpgradeData[] EnsureDefaultMetaUpgrades()
        {
            EnsureFolder(Folder);
            return new[]
            {
                Ensure("attack", "기초 단련", 10, 15, 1.45f, StatType.AttackPower, ModifierKind.Flat, 2f),
                Ensure("health", "강인함", 10, 15, 1.45f, StatType.MaxHealth, ModifierKind.Flat, 10f),
                Ensure("crit", "급소 파악", 5, 25, 1.6f, StatType.CritChance, ModifierKind.Flat, 0.03f),
                Ensure("damage", "파괴의 기억", 5, 30, 1.6f, StatType.DamageIncrease, ModifierKind.Flat, 0.1f),
                Ensure("speed", "가속", 5, 20, 1.5f, StatType.MoveSpeed, ModifierKind.PercentAdd, 0.04f),
                Ensure("overdrive", "넘치는 동력", 5, 25, 1.6f, StatType.OverdriveGain, ModifierKind.PercentAdd, 0.1f),
                Ensure("reroll", "행운의 손", 3, 40, 2f, StatType.Rerolls, ModifierKind.Flat, 1f),
            };
        }

        private static MetaUpgradeData Ensure(string id, string displayName, int maxLevel,
            int baseCost, float costGrowth, StatType stat, ModifierKind kind, float value)
        {
            string path = $"{Folder}/{id}.asset";
            MetaUpgradeData existing = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path);
            if (existing != null) return existing;

            var upgrade = ScriptableObject.CreateInstance<MetaUpgradeData>();
            upgrade.id = id;
            upgrade.displayName = displayName;
            upgrade.maxLevel = maxLevel;
            upgrade.baseCost = baseCost;
            upgrade.costGrowth = costGrowth;
            upgrade.modifiersPerLevel = new[] { new StatModifier(stat, kind, value) };
            upgrade.description = UpgradeText.Describe(upgrade.modifiersPerLevel);
            AssetDatabase.CreateAsset(upgrade, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
