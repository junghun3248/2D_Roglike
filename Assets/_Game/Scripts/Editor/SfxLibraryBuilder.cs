using System.IO;
using Roglike.Core;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class SfxLibraryBuilder
    {
        public static SfxLibrary Ensure()
        {
            const string folder = "Assets/_Game/Data/Audio";
            const string path = folder + "/SfxLibrary.asset";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(path);
            bool created = library == null;
            if (created) library = ScriptableObject.CreateInstance<SfxLibrary>();
            string[] ids = { "swing", "hit", "hit_crit", "kill", "dash", "jump", "land", "player_hurt" };
            float[] volumes = { 0.3f, 0.9f, 0.95f, 0.9f, 0.5f, 0.4f, 0.3f, 0.8f };
            var serialized = new SerializedObject(library);
            var entries = serialized.FindProperty("_entries");
            // Keep user-added entries and per-clip volume tuning on subsequent builds.
            for (int i = 0; i < ids.Length; i++)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Game/Audio/Sfx/{ids[i]}.wav");
                if (clip == null) throw new FileNotFoundException("Run Tools/audio/make_sfx.py first: " + ids[i]);
                bool found = false;
                for (int j = 0; j < entries.arraySize; j++)
                    if (entries.GetArrayElementAtIndex(j).FindPropertyRelative("_id").stringValue == ids[i])
                    {
                        entries.GetArrayElementAtIndex(j).FindPropertyRelative("_clip").objectReferenceValue = clip;
                        found = true;
                        break;
                    }
                if (found) continue;
                int index = entries.arraySize++;
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_id").stringValue = ids[i];
                entry.FindPropertyRelative("_clip").objectReferenceValue = clip;
                entry.FindPropertyRelative("_volume").floatValue = volumes[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (created) AssetDatabase.CreateAsset(library, path);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }
    }
}
