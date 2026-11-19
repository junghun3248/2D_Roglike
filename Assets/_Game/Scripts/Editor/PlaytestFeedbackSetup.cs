using Roglike.Player;
using Roglike.Player.Skills;
using Roglike.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roglike.EditorTools
{
    /// <summary>Unity가 새 스크립트의 meta를 생성한 뒤 실행하는 기존 씬 데이터 이관.</summary>
    public static class PlaytestFeedbackSetup
    {
        [MenuItem("Roglike/Apply Playtest Feedback")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                SkillDefinition[] skills = SkillLibraryBuilder.EnsureDefaultSkills();
                foreach (string name in new[] { "Prototype", "Run" })
                {
                    Scene scene = EditorSceneManager.OpenScene($"Assets/_Game/Scenes/{name}.unity", OpenSceneMode.Single);
                    foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (PlayerSkills player in root.GetComponentsInChildren<PlayerSkills>(true))
                    {
                        var serialized = new SerializedObject(player);
                        SerializedProperty starting = serialized.FindProperty("_startingSkills");
                        for (int i = 0; i < starting.arraySize; i++)
                        {
                            SerializedProperty slot = starting.GetArrayElementAtIndex(i);
                            if (slot.objectReferenceValue is DashSlashSkill) slot.objectReferenceValue = skills[0];
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
