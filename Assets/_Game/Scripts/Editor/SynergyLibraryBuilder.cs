#if UNITY_EDITOR
using Roglike.Data;
using Roglike.Progression;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class SynergyLibraryBuilder
    {
        private const string Folder = "Assets/_Game/Data/Synergies";

        public static SynergyData[] EnsureDefaultSynergies()
        {
            EnsureFolder(Folder);
            return new[]
            {
                Ensure(UpgradeTag.Blade, "검술", new Color(0.3f, 0.75f, 1f),
                    Tier(2, M(StatType.AttackPower, 0.2f, ModifierKind.PercentAdd)),
                    Tier(4, M(StatType.AttackPower, 0.5f, ModifierKind.PercentAdd)),
                    Tier(6, M(StatType.AttackPower, 1f, ModifierKind.PercentAdd))),
                Ensure(UpgradeTag.Critical, "치명", new Color(1f, 0.8f, 0.2f),
                    Tier(2, M(StatType.CritChance, 0.1f)),
                    Tier(4, M(StatType.CritChance, 0.15f), M(StatType.CritDamage, 0.5f)),
                    Tier(6, M(StatType.CritChance, 0.2f), M(StatType.CritDamage, 1f), M(StatType.FinalDamage, 1.2f, ModifierKind.Multiply))),
                Ensure(UpgradeTag.Tempo, "질풍", new Color(0.2f, 1f, 0.7f),
                    Tier(2, M(StatType.AttackSpeed, 0.1f, ModifierKind.PercentAdd), M(StatType.MoveSpeed, 0.05f, ModifierKind.PercentAdd)),
                    Tier(4, M(StatType.AttackSpeed, 0.25f, ModifierKind.PercentAdd), M(StatType.MoveSpeed, 0.1f, ModifierKind.PercentAdd)),
                    Tier(6, M(StatType.AttackSpeed, 0.45f, ModifierKind.PercentAdd), M(StatType.MoveSpeed, 0.15f, ModifierKind.PercentAdd))),
                Ensure(UpgradeTag.Fury, "파괴", new Color(1f, 0.35f, 0.2f),
                    Tier(2, M(StatType.DamageIncrease, 0.3f)),
                    Tier(4, M(StatType.DamageIncrease, 0.7f)),
                    Tier(6, M(StatType.DamageIncrease, 1f), M(StatType.FinalDamage, 1.3f, ModifierKind.Multiply))),
                Ensure(UpgradeTag.Overdrive, "과부하", new Color(0.8f, 0.4f, 1f),
                    Tier(2, M(StatType.OverdriveGain, 0.3f, ModifierKind.PercentAdd)),
                    Tier(4, M(StatType.OverdriveGain, 0.6f, ModifierKind.PercentAdd), M(StatType.OverdriveDuration, 1f)),
                    Tier(6, M(StatType.OverdriveGain, 1f, ModifierKind.PercentAdd), M(StatType.OverdriveDuration, 2f), M(StatType.FinalDamage, 1.15f, ModifierKind.Multiply))),
                Ensure(UpgradeTag.Vitality, "생존", new Color(1f, 0.45f, 0.7f),
                    Tier(2, M(StatType.MaxHealth, 20f)),
                    Tier(4, M(StatType.MaxHealth, 50f)),
                    Tier(6, M(StatType.MaxHealth, 100f), M(StatType.FinalDamage, 1.1f, ModifierKind.Multiply))),
            };
        }

        private static StatModifier M(StatType stat, float value, ModifierKind kind = ModifierKind.Flat)
            => new StatModifier(stat, kind, value);

        private static SynergyTier Tier(int threshold, params StatModifier[] modifiers)
            => new SynergyTier { threshold = threshold, modifiers = modifiers };

        private static SynergyData Ensure(UpgradeTag tag, string displayName, Color color, params SynergyTier[] tiers)
        {
            string path = $"{Folder}/{tag}.asset";
            SynergyData existing = AssetDatabase.LoadAssetAtPath<SynergyData>(path);
            if (existing != null) return existing;
            var synergy = ScriptableObject.CreateInstance<SynergyData>();
            synergy.tag = tag;
            synergy.displayName = displayName;
            synergy.color = color;
            synergy.tiers = tiers;
            AssetDatabase.CreateAsset(synergy, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<SynergyData>(path);
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
#endif
