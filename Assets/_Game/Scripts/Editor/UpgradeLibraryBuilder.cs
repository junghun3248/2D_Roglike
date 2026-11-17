using Roglike.Data;
using Roglike.Progression;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class UpgradeLibraryBuilder
    {
        private const string Folder = "Assets/_Game/Data/Upgrades";

        public static UpgradeData[] EnsureDefaultUpgrades()
        {
            EnsureFolder(Folder);
            return new[]
            {
                Ensure("SharpBlade", "날카로운 칼날", UpgradeRarity.Common,
                    new StatModifier(StatType.AttackPower, ModifierKind.Flat, 5f)),
                Ensure("FightingSpirit", "투지", UpgradeRarity.Common,
                    new StatModifier(StatType.AttackPower, ModifierKind.PercentAdd, 0.15f)),
                Ensure("HeavyBlow", "묵직한 일격", UpgradeRarity.Common,
                    new StatModifier(StatType.DamageIncrease, ModifierKind.Flat, 0.2f)),
                Ensure("KeenEye", "예리한 눈", UpgradeRarity.Common,
                    new StatModifier(StatType.CritChance, ModifierKind.Flat, 0.08f)),
                Ensure("LightStep", "가벼운 발걸음", UpgradeRarity.Common,
                    new StatModifier(StatType.MoveSpeed, ModifierKind.PercentAdd, 0.1f)),
                Ensure("Vitality", "생명력", UpgradeRarity.Common,
                    new StatModifier(StatType.MaxHealth, ModifierKind.Flat, 20f)),
                Ensure("QuickHands", "빠른 손놀림", UpgradeRarity.Common,
                    new StatModifier(StatType.AttackSpeed, ModifierKind.PercentAdd, 0.1f)),
                Ensure("Destruction", "파괴 본능", UpgradeRarity.Rare,
                    new StatModifier(StatType.DamageIncrease, ModifierKind.Flat, 0.4f)),
                Ensure("FatalStrike", "치명적인 일격", UpgradeRarity.Rare,
                    new StatModifier(StatType.CritDamage, ModifierKind.Flat, 0.4f)),
                Ensure("Flurry", "질풍 연격", UpgradeRarity.Rare,
                    new StatModifier(StatType.AttackSpeed, ModifierKind.PercentAdd, 0.2f)),
                Ensure("OverdriveCharge", "넘치는 에너지", UpgradeRarity.Rare,
                    new StatModifier(StatType.OverdriveGain, ModifierKind.PercentAdd, 0.4f)),
                Ensure("ExtendedOverdrive", "끝나지 않는 순간", UpgradeRarity.Rare,
                    new StatModifier(StatType.OverdriveDuration, ModifierKind.Flat, 1.5f)),
                Ensure("BattleFury", "전투의 격노", UpgradeRarity.Rare,
                    new StatModifier(StatType.AttackPower, ModifierKind.PercentAdd, 0.3f)),
                Ensure("Transcendence", "한계 돌파", UpgradeRarity.Epic,
                    new StatModifier(StatType.FinalDamage, ModifierKind.Multiply, 1.3f)),
                Ensure("Executioner", "처형자의 눈", UpgradeRarity.Epic,
                    new StatModifier(StatType.FinalDamage, ModifierKind.Multiply, 1.2f),
                    new StatModifier(StatType.CritChance, ModifierKind.Flat, 0.1f)),
                Ensure("GlassCannon", "유리 대포", UpgradeRarity.Epic,
                    new StatModifier(StatType.FinalDamage, ModifierKind.Multiply, 1.5f),
                    new StatModifier(StatType.MaxHealth, ModifierKind.Flat, -30f)),
            };
        }

        private static UpgradeData Ensure(string fileName, string displayName,
            UpgradeRarity rarity, params StatModifier[] modifiers)
        {
            string path = $"{Folder}/{fileName}.asset";
            UpgradeData existing = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
            if (existing != null) return existing;

            var upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            upgrade.displayName = displayName;
            upgrade.description = string.Empty;
            upgrade.rarity = rarity;
            upgrade.maxStacks = rarity == UpgradeRarity.Common ? 5 : rarity == UpgradeRarity.Rare ? 3 : 2;
            upgrade.modifiers = modifiers;
            AssetDatabase.CreateAsset(upgrade, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
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
