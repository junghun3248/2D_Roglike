using System.IO;
using System.Linq;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using Roglike.Enemies;
using Roglike.Player;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    /// <summary>
    /// 생성 그림(Assets/_Game/Art/Generated)을 씬·프리팹에 입힌다. 그림 폴더가 없으면 아무것도 하지 않아
    /// 자리표시 도형이 그대로 쓰인다. PrototypeSceneBuilder에서 NewScene 이후에 호출해야 한다.
    /// </summary>
    public static class ArtSetup
    {
        private const string Generated = "Assets/_Game/Art/Generated";
        private const string AnimationFolder = "Assets/_Game/Data/Animations";
        private const string FlashShaderPath = "Assets/_Game/Art/Shaders/SpriteFlash.shader";
        private const string FlashMaterialPath = "Assets/_Game/Art/Shaders/SpriteFlash.mat";

        // 배경은 960x540px(PPU 48 → 20x11.25)라 카메라(세로 13)를 덮도록 키운다.
        private const float BackgroundScale = 1.35f;

        private static bool _reimported;

        /// <summary>
        /// 그림이 있으면 true. 처음 부를 때 그림 폴더를 강제로 다시 가져온다 — 그림 파일이 가져오기 설정
        /// 스크립트(GeneratedArtImporter)보다 먼저 들어오면 설정이 빠지기 때문.
        /// </summary>
        public static bool Available
        {
            get
            {
                if (!AssetDatabase.IsValidFolder(Generated + "/hero_idle"))
                {
                    return false;
                }

                if (!_reimported)
                {
                    _reimported = true;
                    AssetDatabase.ImportAsset(Generated, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }

                return true;
            }
        }

        // ---------- 공용 ----------

        public static Material FlashMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FlashShaderPath);
            var material = new Material(shader) { name = "SpriteFlash" };
            AssetDatabase.CreateAsset(material, FlashMaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FlashMaterialPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        }

        /// <summary>Generated/&lt;name&gt;/&lt;name&gt;_N.png 프레임으로 애니메이션 에셋을 만든다 (프레임은 매번 다시 채워 그림 갱신을 반영).</summary>
        public static SpriteAnimationData Animation(string name, float fps, bool loop)
        {
            string folder = $"{Generated}/{name}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return null;
            }

            Sprite[] frames = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(FrameIndex)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .ToArray();

            EnsureFolder(AnimationFolder);
            string path = $"{AnimationFolder}/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<SpriteAnimationData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<SpriteAnimationData>();
                data.fps = fps;
                data.loop = loop;
                AssetDatabase.CreateAsset(data, path);
            }

            data.frames = frames;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<SpriteAnimationData>(path);
        }

        private static int FrameIndex(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int underscore = name.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int index) ? index : 0;
        }

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Generated}/{name}.png");

        // ---------- 플레이어 ----------

        /// <summary>자리표시 몸통을 주인공 스프라이트로 바꾼다. 몸통 렌더러는 그대로 재사용(잔상·충전 색·HitFlash가 참조).</summary>
        public static void ApplyToPlayer(GameObject player, SpriteRenderer body, params SpriteRenderer[] placeholdersToRemove)
        {
            if (!Available)
            {
                return;
            }

            SpriteAnimationData idle = Animation("hero_idle", 8f, true);
            SpriteAnimationData run = Animation("hero_run", 14f, true);
            SpriteAnimationData attack = Animation("hero_attack", 12f, false);

            foreach (SpriteRenderer placeholder in placeholdersToRemove)
            {
                if (placeholder != null)
                {
                    Object.DestroyImmediate(placeholder.gameObject);
                }
            }

            // 플레이어 원점은 콜라이더(0.8x1.6) 가운데 → 발밑은 y -0.8.
            body.transform.localScale = Vector3.one;
            body.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            body.sprite = idle.frames[0];
            body.color = Color.white;
            body.sharedMaterial = FlashMaterial();

            var animator = body.gameObject.AddComponent<SpriteAnimator>();
            SetBool(animator, "_usePlayerTime", true);

            var driver = player.AddComponent<PlayerAnimationDriver>();
            SetObject(driver, "_animator", animator);
            SetObject(driver, "_idle", idle);
            SetObject(driver, "_run", run);
            SetObject(driver, "_attack", attack);

            var flash = player.GetComponent<HitFlash>();
            SetObjectArray(flash, "_renderers", new Object[] { body });
        }

        // ---------- 적 (프리팹) ----------

        public static void ApplyToSlimePrefab(string prefabPath, float scale, Color tint, float feetY, string removeChildPrefix, Vector2? crownPosition = null)
        {
            if (!Available)
            {
                return;
            }

            SpriteAnimationData idle = Animation("slime_idle", 6f, true);
            SpriteAnimationData hurt = Animation("slime_hurt", 8f, true);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform visual = root.transform.Find("Visual");
                Transform bodyTransform = visual.Find("Body");
                var body = bodyTransform.GetComponent<SpriteRenderer>();

                foreach (Transform child in visual.Cast<Transform>().ToArray())
                {
                    if (child.name.StartsWith(removeChildPrefix))
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                bodyTransform.localScale = new Vector3(scale, scale, 1f);
                bodyTransform.localPosition = new Vector3(0f, feetY, 0f);
                body.sprite = idle.frames[0];
                body.color = tint;
                body.sharedMaterial = FlashMaterial();

                if (crownPosition.HasValue)
                {
                    Transform crown = visual.Find("Crown");
                    if (crown != null)
                    {
                        crown.localPosition = crownPosition.Value;
                    }
                }

                // Unity 오브젝트는 ?? 연산자를 쓰면 "가짜 null"을 놓치므로 TryGetComponent로 확인한다.
                if (!bodyTransform.TryGetComponent(out SpriteAnimator animator))
                {
                    animator = bodyTransform.gameObject.AddComponent<SpriteAnimator>();
                }

                if (!root.TryGetComponent(out EnemySpriteAnimations animations))
                {
                    animations = root.AddComponent<EnemySpriteAnimations>();
                }
                SetObject(animations, "_animator", animator);
                SetObject(animations, "_idle", idle);
                SetObject(animations, "_hurt", hurt);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- 맵 ----------

        /// <summary>시차 배경 3겹 (하늘·언덕·숲). 각 겹은 가로로 3장 (가운데 장 좌우 반전으로 이음매 숨김).</summary>
        public static bool CreateBackground(Transform parent)
        {
            if (!Available)
            {
                return false;
            }

            CreateParallax(parent, "bg_sky", 0.95f, 0.95f, -40, new Vector2(0f, -6.5f));
            CreateParallax(parent, "bg_hills", 0.75f, 0.85f, -35, new Vector2(0f, -6.8f));
            CreateParallax(parent, "bg_forest", 0.5f, 0.7f, -30, new Vector2(0f, -6.0f));
            return true;
        }

        private static void CreateParallax(Transform parent, string spriteName, float factor, float verticalFactor, int order, Vector2 offset)
        {
            Sprite sprite = Load(spriteName);
            if (sprite == null)
            {
                return;
            }

            var layer = new GameObject("Parallax_" + spriteName);
            layer.transform.SetParent(parent, false);
            layer.transform.localScale = new Vector3(BackgroundScale, BackgroundScale, 1f);
            float width = sprite.bounds.size.x * BackgroundScale;

            for (int i = -1; i <= 1; i++)
            {
                var tile = new GameObject($"{spriteName}_{i + 1}");
                tile.transform.SetParent(layer.transform, false);
                tile.transform.localPosition = new Vector3(i * sprite.bounds.size.x, 0f, 0f);
                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = order;
                renderer.flipX = i == 0;
            }

            var parallax = layer.AddComponent<ParallaxLayer>();
            SetFloat(parallax, "_factor", factor);
            SetFloat(parallax, "_verticalFactor", verticalFactor);
            SetVector2(parallax, "_offset", offset);
            SetFloat(parallax, "_tileWidth", width);
        }

        /// <summary>땅·벽·발판을 타일 그림으로 칠한다. 콜라이더는 건드리지 않는다.</summary>
        public static bool DecorateSolid(GameObject solid, Vector2 size, bool oneWay, bool hasTop)
        {
            if (!Available)
            {
                return false;
            }

            if (oneWay)
            {
                Sprite plank = Load("platform");
                Tiled(solid.transform, "Plank", plank, new Vector2(size.x, plank.bounds.size.y), Vector2.zero, 2);
                return true;
            }

            Sprite fill = Load("ground_fill");
            Sprite top = Load("ground_top");
            Tiled(solid.transform, "Fill", fill, size, Vector2.zero, 0);
            if (hasTop)
            {
                float topHeight = top.bounds.size.y;
                // 풀끝이 땅 위로 살짝 올라오도록 윗면보다 조금 위에 둔다.
                float y = size.y * 0.5f - topHeight * 0.5f + 0.2f;
                Tiled(solid.transform, "Top", top, new Vector2(size.x, topHeight), new Vector2(0f, y), 1);
            }

            return true;
        }

        private static void Tiled(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
            renderer.sortingOrder = order;
        }

        // ---------- 도우미 ----------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void SetObject(Object target, string field, Object value) => Modify(target, field, p => p.objectReferenceValue = value);

        private static void SetObjectArray(Object target, string field, Object[] values) => Modify(target, field, p =>
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        });

        private static void SetBool(Object target, string field, bool value) => Modify(target, field, p => p.boolValue = value);

        private static void SetFloat(Object target, string field, float value) => Modify(target, field, p => p.floatValue = value);

        private static void SetVector2(Object target, string field, Vector2 value) => Modify(target, field, p => p.vector2Value = value);

        private static void Modify(Object target, string field, System.Action<SerializedProperty> set)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Roglike] {target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
                return;
            }

            set(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
