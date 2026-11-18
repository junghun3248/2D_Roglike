using System.IO;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roglike.EditorTools
{
    // 새 씬을 만든 뒤 호출한다. 저장 반환값 대신 디스크에서 프리팹을 다시 읽는다.
    public static class EnemyPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/Enemies";
        private const string ArtFolder = "Assets/_Game/Art/Placeholder";
        private static readonly Color GearColor = new Color(0.85f, 0.73f, 0.38f);

        // 그림이 있으면 프리팹을 만든(불러온) 뒤 상태별 그림을 입힌다. feetY = 콜라이더 바닥 (가운데 원점 기준 -높이/2).
        public static GameObject EnsureBoar()
        {
            Ensure("ClockworkBoar", 160, new Vector2(1.8f, 1f));
            ArtSetup.ApplyToEnemyPrefab($"{PrefabFolder}/ClockworkBoar.prefab", -0.5f, "boar_idle",
                ("Move", "boar_run", 10f, true), ("Charge", "boar_run", 16f, true),
                ("Telegraph", "boar_telegraph", 8f, true), ("Stunned", "boar_stunned", 6f, true));
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/ClockworkBoar.prefab");
        }

        public static GameObject EnsureSquirrel()
        {
            Ensure("GearSquirrel", 90, new Vector2(0.75f, 0.9f));
            ArtSetup.ApplyToEnemyPrefab($"{PrefabFolder}/GearSquirrel.prefab", -0.45f, "squirrel_idle",
                ("Move", "squirrel_move", 8f, true), ("Windup", "squirrel_windup", 8f, true),
                ("Throw", "squirrel_throw", 8f, true), ("Stunned", "squirrel_idle", 4f, true));
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/GearSquirrel.prefab");
        }

        public static GameObject EnsureStagKing()
        {
            Ensure("StagKing", 6000, new Vector2(2.4f, 2.6f));
            ArtSetup.ApplyToEnemyPrefab($"{PrefabFolder}/StagKing.prefab", -1.3f, "stag_idle",
                ("Move", "stag_idle", 4f, true), ("Charge", "stag_charge", 8f, true), ("Sweep", "stag_sweep", 8f, true),
                ("Rear", "stag_rear", 8f, true), ("Stunned", "stag_enraged", 6f, true), ("Enraged", "stag_enraged", 6f, true));
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/StagKing.prefab");
        }

        private static GameObject Ensure(string name, double hp, Vector2 colliderSize)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            EnsureFolder(PrefabFolder);
            EnsureFolder(ArtFolder);
            Sprite square = EnsureSprite("Square", false);
            Sprite circle = EnsureSprite("Circle", true);
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Shaders/SpriteFlash.mat");
            if (material == null)
            {
                string materialPath = ArtFolder + "/EnemySpriteUnlit.mat";
                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) throw new System.InvalidOperationException("2D URP Sprite-Unlit-Default shader is missing.");
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
            }
            var root = new GameObject(name) { layer = GameLayers.Enemy };
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                root.AddComponent<BoxCollider2D>().size = colliderSize;
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                if (name == "ClockworkBoar")
                {
                    Shape("Body", visual, circle, material, new Color(0.5f, 0.28f, 0.12f), Vector2.zero, new Vector2(2f, 1.1f));
                    Shape("Snout", visual, square, material, new Color(0.65f, 0.38f, 0.2f), new Vector2(0.85f, 0f), new Vector2(0.4f, 0.45f));
                    Shape("Tusk", visual, square, material, GearColor, new Vector2(0.95f, 0.22f), new Vector2(0.16f, 0.5f), -25f);
                    Gear(visual, square, circle, material, new Vector2(-0.2f, 0.45f), 0.55f);
                    Shape("Eye", visual, circle, material, Color.black, new Vector2(0.6f, 0.25f), Vector2.one * 0.12f);
                }
                else if (name == "GearSquirrel")
                {
                    Shape("Body", visual, circle, material, new Color(0.95f, 0.4f, 0.12f), Vector2.zero, new Vector2(0.8f, 1f));
                    Shape("Tail", visual, circle, material, new Color(1f, 0.6f, 0.15f), new Vector2(-0.6f, 0.35f), new Vector2(0.65f, 1.35f), -25f);
                    Shape("Ear", visual, circle, material, new Color(0.8f, 0.25f, 0.08f), new Vector2(0.05f, 0.5f), new Vector2(0.2f, 0.4f));
                    Gear(visual, square, circle, material, new Vector2(-0.6f, 0.45f), 0.4f);
                    Shape("Eye", visual, circle, material, Color.black, new Vector2(0.23f, 0.22f), Vector2.one * 0.12f);
                }
                else
                {
                    Shape("Body", visual, circle, material, new Color(0.55f, 0.38f, 0.2f), new Vector2(0f, -0.1f), new Vector2(2.4f, 1.8f));
                    Shape("Neck", visual, square, material, new Color(0.65f, 0.46f, 0.25f), new Vector2(0.7f, 0.65f), new Vector2(0.6f, 1.5f), 15f);
                    Shape("Head", visual, circle, material, new Color(0.75f, 0.55f, 0.3f), new Vector2(0.95f, 1.25f), new Vector2(0.9f, 0.65f));
                    Shape("FrontLeg", visual, square, material, GearColor, new Vector2(0.65f, -0.9f), new Vector2(0.3f, 0.8f));
                    Shape("BackLeg", visual, square, material, GearColor, new Vector2(-0.7f, -0.9f), new Vector2(0.3f, 0.8f));
                    Gear(visual, square, circle, material, new Vector2(0.4f, 1.8f), 0.8f);
                    Gear(visual, square, circle, material, new Vector2(1.05f, 1.8f), 0.65f);
                    Shape("Eye", visual, circle, material, Color.black, new Vector2(1.2f, 1.32f), Vector2.one * 0.14f);
                }
                var health = root.AddComponent<Health>();
                var healthData = new SerializedObject(health);
                healthData.FindProperty("_team").enumValueIndex = (int)Team.Enemy;
                healthData.FindProperty("_maxHealth").doubleValue = hp;
                healthData.ApplyModifiedPropertiesWithoutUndo();
                var flash = root.AddComponent<HitFlash>();
                var flashData = new SerializedObject(flash);
                SpriteRenderer[] renderers = visual.GetComponentsInChildren<SpriteRenderer>();
                SerializedProperty array = flashData.FindProperty("_renderers");
                array.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                flashData.FindProperty("_squashTarget").objectReferenceValue = visual;
                flashData.ApplyModifiedPropertiesWithoutUndo();
                MonoBehaviour ai;
                if (name == "ClockworkBoar") ai = root.AddComponent<BoarEnemy>();
                else if (name == "GearSquirrel") ai = root.AddComponent<SquirrelEnemy>();
                else ai = root.AddComponent<StagKingBoss>();
                var aiData = new SerializedObject(ai);
                aiData.FindProperty("_visual").objectReferenceValue = visual;
                aiData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void Shape(string name, Transform parent, Sprite sprite, Material material, Color color,
            Vector2 position, Vector2 size, float angle = 0f)
        {
            var go = new GameObject(name) { layer = GameLayers.Enemy };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = 10 + parent.childCount;
        }

        private static void Gear(Transform parent, Sprite square, Sprite circle, Material material, Vector2 position, float size)
        {
            Shape("Gear", parent, circle, material, GearColor, position, Vector2.one * size);
            Shape("GearTeeth", parent, square, material, GearColor, position, Vector2.one * size * 0.85f, 45f);
            Shape("GearHub", parent, circle, material, new Color(0.25f, 0.2f, 0.12f), position, Vector2.one * size * 0.35f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static Sprite EnsureSprite(string name, bool circle)
        {
            string path = $"{ArtFolder}/{name}.png";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        byte alpha = 255;
                        if (circle)
                        {
                            float dx = x + 0.5f - size * 0.5f;
                            float dy = y + 0.5f - size * 0.5f;
                            float distance = Mathf.Sqrt(dx * dx + dy * dy);
                            alpha = (byte)(Mathf.Clamp01(size * 0.5f - distance) * 255f);
                        }

                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                File.WriteAllBytes(fullPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

    }
}
