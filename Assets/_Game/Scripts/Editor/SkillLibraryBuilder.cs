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
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { Folder }))
                AssignIcon(AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            return new SkillDefinition[]
            {
                Ensure<StasisFieldSkill>("StasisField", "정지 구역", "정지", "전방의 시간을 멈춘다. 정지한 적이 받는 피해 +30%.",
                    new Color(0.55f, 0.9f, 1f), 6f, 1.5f, 0.04f, 0.2f, 8f),
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
            if (existing != null)
            {
                AssignIcon(existing);
                return existing;
            }
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
            AssignIcon(skill);
            AssetDatabase.CreateAsset(skill, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void AssignIcon(SkillDefinition skill)
        {
            var serialized = new SerializedObject(skill);
            serialized.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/_Game/Art/Generated/ui/skill_{skill.Id}.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
