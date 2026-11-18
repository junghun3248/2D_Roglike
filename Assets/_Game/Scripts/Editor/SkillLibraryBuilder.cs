using Roglike.Data;
using Roglike.Player.Skills;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class SkillLibraryBuilder
    {
        private const string Folder = "Assets/_Game/Data/Skills";

        public static SkillDefinition[] EnsureDefaultSkills()
        {
            EnsureFolder(Folder);
            return new SkillDefinition[]
            {
                Ensure<DashSlashSkill>("DashSlash", "돌진 베기", "돌진", "무적 돌진으로 적을 벤다.",
                    new Color(0.3f, 0.85f, 1f), 4f, 2.5f, 0.09f, 0.4f, 8f),
                Ensure<WhirlwindSkill>("Whirlwind", "회전 베기", "회전", "천천히 이동하며 주위를 연속 공격한다.",
                    new Color(0.45f, 1f, 0.5f), 6f, 0.6f, 0.04f, 0.2f, 2f),
                Ensure<SwordWaveSkill>("SwordWave", "검기", "검기", "적을 관통하는 검기를 날린다.",
                    new Color(0.9f, 0.45f, 1f), 3f, 1.8f, 0.07f, 0.3f, 5f),
                Ensure<SlamSkill>("Slam", "내려찍기", "강타", "뛰어올라 내려찍고 주변 적을 띄운다.",
                    new Color(1f, 0.65f, 0.2f), 7f, 3.2f, 0.12f, 0.65f, 10f),
            };
        }

        private static T Ensure<T>(string id, string displayName, string shortName, string description,
            Color color, float cooldown, float damage, float hitStop, float shake, float gain) where T : SkillDefinition
        {
            string path = $"{Folder}/{id}.asset";
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T skill = ScriptableObject.CreateInstance<T>();
            var serialized = new SerializedObject(skill);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_shortName").stringValue = shortName;
            serialized.FindProperty("_description").stringValue = description;
            serialized.FindProperty("_color").colorValue = color;
            serialized.FindProperty("_cooldown").floatValue = cooldown;
            serialized.FindProperty("_damageMultiplier").floatValue = damage;
            serialized.FindProperty("_hitStop").floatValue = hitStop;
            serialized.FindProperty("_shake").floatValue = shake;
            serialized.FindProperty("_overdriveGain").floatValue = gain;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(skill, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<T>(path);
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
