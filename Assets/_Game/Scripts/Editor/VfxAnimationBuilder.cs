using System.IO;
using Roglike.Combat;
using Roglike.Data;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class VfxAnimationBuilder
    {
        public static void Configure(VfxSprites target)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty("_slashAnimation").objectReferenceValue = Ensure("slash_arc", 0.028f);
            serialized.FindProperty("_impactAnimation").objectReferenceValue = Ensure("impact_burst", 0.025f);
            serialized.FindProperty("_slamAnimation").objectReferenceValue = Ensure("slam_wave", 0.04f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SpriteAnimationData Ensure(string name, float frameTime)
        {
            const string folder = "Assets/_Game/Data/Vfx";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string path = $"{folder}/{name}.asset";
            var animation = AssetDatabase.LoadAssetAtPath<SpriteAnimationData>(path);
            bool created = animation == null;
            if (created)
            {
                animation = ScriptableObject.CreateInstance<SpriteAnimationData>();
                animation.fps = 1f / frameTime;
            }
            animation.loop = false;
            animation.frames = new Sprite[4];
            for (int i = 0; i < animation.frames.Length; i++)
            {
                string spritePath = $"Assets/_Game/Art/Generated/vfx/{name}_{i}.png";
                animation.frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (animation.frames[i] == null)
                    throw new FileNotFoundException("Run Tools/art/process_gpt_vfx.py first: " + spritePath);
            }
            if (created) AssetDatabase.CreateAsset(animation, path);
            EditorUtility.SetDirty(animation);
            AssetDatabase.SaveAssets();
            return animation;
        }
    }
}
