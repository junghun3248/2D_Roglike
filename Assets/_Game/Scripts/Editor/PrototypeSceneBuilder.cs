using System.IO;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using Roglike.Enemies;
using Roglike.Player;
using Roglike.Progression;
using Roglike.Run;
using Roglike.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Roglike.EditorTools
{
    /// <summary>
    /// M1 손맛 테스트 씬을 자동으로 만든다: 메뉴 Roglike > Build Prototype Scene.
    /// 씬은 매번 새로 만들고, 공격 데이터·슬라임 프리팹은 없을 때만 만든다 (직접 조정한 수치 보존).
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string Root = "Assets/_Game";
        private const string ScenePath = Root + "/Scenes/Prototype.unity";
        private const string ArtFolder = Root + "/Art/Placeholder";
        private const string AttackFolder = Root + "/Data/Player";
        private const string PrefabFolder = Root + "/Prefabs/Enemies";
        private const string SlimePrefabPath = PrefabFolder + "/Slime.prefab";
        private const string BossPrefabPath = PrefabFolder + "/KingSlime.prefab";
        private const string RunScenePath = Root + "/Scenes/Run.unity";
        private const string RunConfigPath = Root + "/Data/Run/RunConfig.asset";
        private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const float LevelHalfWidth = 35f;

        private static Sprite _square;
        private static Sprite _circle;
        private static Material _material;

        [MenuItem("Roglike/Build Prototype Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[Roglike] Prototype 씬을 만들었습니다. Play를 눌러 테스트하세요.");
        }

        [MenuItem("Roglike/Build Run Scene")]
        public static void BuildRunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildRun();
            EditorSceneManager.OpenScene(RunScenePath);
            Debug.Log("[Roglike] Run 씬을 만들었습니다. Play를 눌러 한 판을 테스트하세요.");
        }

        /// <summary>배치 모드용: Unity -batchmode -executeMethod Roglike.EditorTools.PrototypeSceneBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            Build();
            BuildRun();
            Debug.Log("[Roglike] Scenes built: " + ScenePath + ", " + RunScenePath);
        }

        private static Scene BeginScene()
        {
            EnsureLayers();
            EnsureFolder(ArtFolder);
            EnsureFolder(AttackFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(Root + "/Scenes");

            // NewScene이 참조 없는 에셋을 언로드하므로, 에셋은 씬을 만든 뒤에 불러온다.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _square = EnsureSprite("Square", false);
            _circle = EnsureSprite("Circle", true);
            _material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            return scene;
        }

        private static void Build()
        {
            Scene scene = BeginScene();
            AttackData[] combo = EnsureCombo();
            GameObject slimePrefab = EnsureSlimePrefab();

            GameObject player = CreatePlayer(combo);
            CreateLevel();
            CreateSpawner(slimePrefab);
            CreateCamera(player.transform);
            CreateSystems(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath, false);
            AssetDatabase.SaveAssets();
        }

        /// <summary>실제 한 판: 구역 → 강화 3택1 → … → 보스 → 결과.</summary>
        private static void BuildRun()
        {
            Scene scene = BeginScene();
            AttackData[] combo = EnsureCombo();
            GameObject slimePrefab = EnsureSlimePrefab();
            GameObject bossPrefab = EnsureBossPrefab(slimePrefab);
            UpgradeData[] upgrades = UpgradeLibraryBuilder.EnsureDefaultUpgrades();
            RunConfig config = EnsureRunConfig(slimePrefab, bossPrefab);

            GameObject player = CreatePlayer(combo);
            SetBool(player.GetComponent<PlayerDeath>(), "_restartOnDeath", false);
            SetBool(player.GetComponent<OverdriveController>(), "_startFull", false);

            CreateLevel();
            CreateCamera(player.transform);
            CreateSystems(player);

            var runObject = new GameObject("Run");
            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.SetParent(runObject.transform, false);
            playerSpawn.position = new Vector3(0f, 1.2f, 0f);
            var bossSpawn = new GameObject("BossSpawn").transform;
            bossSpawn.SetParent(runObject.transform, false);
            bossSpawn.position = new Vector3(10f, 3f, 0f);

            Vector2[] points =
            {
                new Vector2(-24f, 1.5f), new Vector2(-14f, 1.5f), new Vector2(-6f, 1.5f), new Vector2(7f, 1.5f),
                new Vector2(15f, 1.5f), new Vector2(25f, 1.5f), new Vector2(-12f, 4.5f), new Vector2(6f, 5f),
                new Vector2(19f, 6.5f), new Vector2(-25f, 7f),
            };
            var spawnPoints = new Object[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("SpawnPoint" + i).transform;
                point.SetParent(runObject.transform, false);
                point.position = points[i];
                spawnPoints[i] = point;
            }

            var ui = new GameObject("RunUI");
            var upgradeChoice = ui.AddComponent<UpgradeChoiceUI>();
            var bossBar = ui.AddComponent<BossHealthBar>();
            var fader = ui.AddComponent<ScreenFader>();
            var result = ui.AddComponent<RunResultScreen>();

            var run = runObject.AddComponent<RunManager>();
            SetObject(run, "_config", config);
            SetObjectArray(run, "_upgrades", upgrades);
            SetObject(run, "_player", player.GetComponent<PlayerStats>());
            SetObject(run, "_playerSpawn", playerSpawn);
            SetObject(run, "_bossSpawn", bossSpawn);
            SetObjectArray(run, "_spawnPoints", spawnPoints);
            SetObject(run, "_upgradeChoice", upgradeChoice);
            SetObject(run, "_bossBar", bossBar);
            SetObject(run, "_fader", fader);
            SetObject(run, "_result", result);

            var hud = ui.AddComponent<RunHud>();
            SetObject(hud, "_run", run);

            EditorSceneManager.SaveScene(scene, RunScenePath);
            AddSceneToBuildSettings(RunScenePath, true);
            AssetDatabase.SaveAssets();
        }

        private static GameObject EnsureBossPrefab(GameObject minionPrefab)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var boss = new GameObject("KingSlime") { layer = GameLayers.Enemy };
            var body = boss.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.mass = 20f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = boss.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2.6f, 2f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(boss.transform, false);
            SpriteRenderer bodyRenderer = CreateSprite("Body", visual, _circle, new Color(0.35f, 0.8f, 0.95f), 12, new Vector2(0f, 0.1f), new Vector2(3.2f, 2.5f));
            CreateSprite("EyeL", visual, _circle, new Color(0.08f, 0.1f, 0.2f), 13, new Vector2(0.45f, 0.45f), new Vector2(0.28f, 0.42f));
            CreateSprite("EyeR", visual, _circle, new Color(0.08f, 0.1f, 0.2f), 13, new Vector2(0.95f, 0.45f), new Vector2(0.28f, 0.42f));
            CreateSprite("Crown", visual, _square, new Color(1f, 0.82f, 0.25f), 14, new Vector2(0f, 1.45f), new Vector2(1.2f, 0.45f));

            var health = boss.AddComponent<Health>();
            SetEnum(health, "_team", (int)Team.Enemy);
            SetDouble(health, "_maxHealth", 2500);

            var flash = boss.AddComponent<HitFlash>();
            SetObjectArray(flash, "_renderers", new Object[] { bodyRenderer });
            SetObject(flash, "_squashTarget", visual);
            SetFloat(flash, "_squashAmount", 0.08f);

            var bossSlime = boss.AddComponent<BossSlime>();
            SetObject(bossSlime, "_visual", visual);
            SetObject(bossSlime, "_minionPrefab", minionPrefab);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(boss, BossPrefabPath);
            Object.DestroyImmediate(boss);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) ?? prefab;
        }

        private static RunConfig EnsureRunConfig(GameObject slimePrefab, GameObject bossPrefab)
        {
            var existing = AssetDatabase.LoadAssetAtPath<RunConfig>(RunConfigPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Root + "/Data/Run");
            var config = ScriptableObject.CreateInstance<RunConfig>();
            config.bossPrefab = bossPrefab;
            config.rooms = new[]
            {
                Room("초원", slimePrefab, 3, 4),
                Room("언덕", slimePrefab, 4, 5),
                Room("숲 입구", slimePrefab, 4, 5, 6),
                Room("깊은 숲", slimePrefab, 5, 6, 7),
            };
            AssetDatabase.CreateAsset(config, RunConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RunConfigPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<RunConfig>(RunConfigPath);
        }

        private static RoomEntry Room(string name, GameObject prefab, params int[] waveCounts)
        {
            var waves = new WaveEntry[waveCounts.Length];
            for (int i = 0; i < waveCounts.Length; i++)
            {
                waves[i] = new WaveEntry { enemyPrefab = prefab, count = waveCounts[i] };
            }

            return new RoomEntry { name = name, waves = waves };
        }

        // ---------- 플레이어 ----------

        private static GameObject CreatePlayer(AttackData[] combo)
        {
            var player = new GameObject("Player") { layer = GameLayers.Player };
            player.transform.position = new Vector3(0f, 1.2f, 0f);

            var body = player.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = player.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 1.6f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            SpriteRenderer bodyRenderer = CreateSprite("Body", visual, _square, new Color(0.35f, 0.6f, 1f), 20, Vector2.zero, new Vector2(0.8f, 1.6f));
            SpriteRenderer eye = CreateSprite("Eye", visual, _square, new Color(0.1f, 0.12f, 0.25f), 21, new Vector2(0.22f, 0.4f), new Vector2(0.16f, 0.22f));
            SpriteRenderer scarf = CreateSprite("Scarf", visual, _square, new Color(1f, 0.4f, 0.55f), 22, new Vector2(-0.1f, 0.15f), new Vector2(0.95f, 0.18f));

            var health = player.AddComponent<Health>();
            SetEnum(health, "_team", (int)Team.Player);
            SetDouble(health, "_maxHealth", 100);

            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerInputReader>();
            var controller = player.AddComponent<PlayerController>();
            SetObject(controller, "_visual", visual);
            SetObject(controller, "_bodyRenderer", bodyRenderer);

            var combat = player.AddComponent<PlayerCombat>();
            SetObjectArray(combat, "_combo", combo);

            player.AddComponent<OverdriveController>();

            var flash = player.AddComponent<HitFlash>();
            SetObjectArray(flash, "_renderers", new Object[] { bodyRenderer, eye, scarf });
            SetObject(flash, "_squashTarget", visual);

            player.AddComponent<PlayerDeath>();
            return player;
        }

        // ---------- 적 ----------

        private static GameObject EnsureSlimePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var slime = new GameObject("Slime") { layer = GameLayers.Enemy };
            var body = slime.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = slime.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 0.8f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(slime.transform, false);
            SpriteRenderer bodyRenderer = CreateSprite("Body", visual, _circle, new Color(0.45f, 0.9f, 0.5f), 10, Vector2.zero, new Vector2(1.15f, 0.9f));
            CreateSprite("Eye", visual, _circle, new Color(0.1f, 0.2f, 0.1f), 11, new Vector2(0.25f, 0.1f), new Vector2(0.14f, 0.2f));

            var health = slime.AddComponent<Health>();
            SetEnum(health, "_team", (int)Team.Enemy);
            SetDouble(health, "_maxHealth", 120);

            var flash = slime.AddComponent<HitFlash>();
            SetObjectArray(flash, "_renderers", new Object[] { bodyRenderer });
            SetObject(flash, "_squashTarget", visual);

            var enemy = slime.AddComponent<SlimeEnemy>();
            SetObject(enemy, "_visual", visual);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(slime, SlimePrefabPath);
            Object.DestroyImmediate(slime);
            return prefab;
        }

        private static void CreateSpawner(GameObject slimePrefab)
        {
            var spawner = new GameObject("EnemySpawner");
            Vector2[] points =
            {
                new Vector2(-22f, 1.5f), new Vector2(-9f, 1.5f), new Vector2(9f, 1.5f), new Vector2(24f, 1.5f),
                new Vector2(-12f, 4.5f), new Vector2(6f, 5f), new Vector2(19f, 6.5f),
            };

            var transforms = new Object[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("SpawnPoint" + i).transform;
                point.SetParent(spawner.transform, false);
                point.position = points[i];
                transforms[i] = point;
            }

            var component = spawner.AddComponent<EnemySpawner>();
            SetObject(component, "_enemyPrefab", slimePrefab);
            SetObjectArray(component, "_spawnPoints", transforms);
            SetInt(component, "_maxAlive", 6);
        }

        // ---------- 맵 ----------

        private static void CreateLevel()
        {
            var level = new GameObject("Level").transform;

            // 배경 (밝은 하늘 + 언덕 + 구름). 충돌 없음.
            var background = new GameObject("Background").transform;
            background.SetParent(level, false);
            CreateSprite("HillFar", background, _circle, new Color(0.62f, 0.86f, 0.72f), -20, new Vector2(-18f, -6f), new Vector2(40f, 18f));
            CreateSprite("HillFar2", background, _circle, new Color(0.62f, 0.86f, 0.72f), -20, new Vector2(22f, -7f), new Vector2(44f, 20f));
            CreateSprite("HillNear", background, _circle, new Color(0.5f, 0.8f, 0.6f), -15, new Vector2(2f, -9f), new Vector2(50f, 16f));
            for (int i = 0; i < 6; i++)
            {
                float x = -30f + i * 12f;
                float y = 9f + (i % 2) * 1.5f;
                CreateSprite("Cloud" + i, background, _circle, new Color(1f, 1f, 1f, 0.85f), -18, new Vector2(x, y), new Vector2(4.5f, 1.6f));
            }

            Color groundColor = new Color(0.55f, 0.4f, 0.3f);
            Color grassColor = new Color(0.45f, 0.82f, 0.4f);

            CreateSolid("Ground", level, new Vector2(0f, -1f), new Vector2(LevelHalfWidth * 2f, 2f), groundColor, grassColor, false);
            CreateSolid("WallLeft", level, new Vector2(-LevelHalfWidth - 0.5f, 10f), new Vector2(1f, 24f), groundColor, null, false);
            CreateSolid("WallRight", level, new Vector2(LevelHalfWidth + 0.5f, 10f), new Vector2(1f, 24f), groundColor, null, false);

            // 아래에서 뛰어올라 통과할 수 있는 발판 (메이플식).
            Color platformColor = new Color(0.75f, 0.55f, 0.35f);
            CreateSolid("Platform0", level, new Vector2(-12f, 3f), new Vector2(8f, 0.5f), platformColor, grassColor, true);
            CreateSolid("Platform1", level, new Vector2(6f, 3.5f), new Vector2(10f, 0.5f), platformColor, grassColor, true);
            CreateSolid("Platform2", level, new Vector2(-3f, 6.5f), new Vector2(6f, 0.5f), platformColor, grassColor, true);
            CreateSolid("Platform3", level, new Vector2(19f, 5f), new Vector2(7f, 0.5f), platformColor, grassColor, true);
            CreateSolid("Platform4", level, new Vector2(-25f, 5.5f), new Vector2(6f, 0.5f), platformColor, grassColor, true);
        }

        private static void CreateSolid(string name, Transform parent, Vector2 center, Vector2 size, Color color, Color? topColor, bool oneWay)
        {
            var go = new GameObject(name) { layer = GameLayers.Ground };
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            if (oneWay)
            {
                collider.usedByEffector = true;
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 160f;
            }

            CreateSprite("Fill", go.transform, _square, color, 0, Vector2.zero, size);
            if (topColor.HasValue)
            {
                CreateSprite("Top", go.transform, _square, topColor.Value, 1, new Vector2(0f, size.y * 0.5f - 0.1f), new Vector2(size.x, 0.25f));
            }
        }

        // ---------- 카메라·시스템 ----------

        private static void CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 2f, -10f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.8f, 0.98f);
            go.AddComponent<AudioListener>();

            var cameraData = go.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;

            var gameCamera = go.AddComponent<GameCamera>();
            SetObject(gameCamera, "_target", target);
            SetFloat(gameCamera, "_minX", -LevelHalfWidth + 11f);
            SetFloat(gameCamera, "_maxX", LevelHalfWidth - 11f);

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static void CreateSystems(GameObject player)
        {
            var systems = new GameObject("Systems");
            systems.AddComponent<GameBootstrap>();
            systems.AddComponent<TimeController>();

            var vfx = systems.AddComponent<VfxSprites>();
            SetObject(vfx, "_square", _square);
            SetObject(vfx, "_circle", _circle);
            SetObject(vfx, "_material", _material);

            systems.AddComponent<DamageNumberSpawner>();

            var overdrive = player.GetComponent<OverdriveController>();
            var hud = systems.AddComponent<HudController>();
            SetObject(hud, "_playerHealth", player.GetComponent<Health>());
            SetObject(hud, "_overdrive", overdrive);

            var screenEffect = systems.AddComponent<OverdriveScreenEffect>();
            SetObject(screenEffect, "_overdrive", overdrive);
        }

        // ---------- 데이터 ----------

        private static AttackData[] EnsureCombo()
        {
            return new[]
            {
                EnsureAttack("Attack1", a =>
                {
                    a.damageMultiplier = 1f; a.startup = 0.05f; a.active = 0.08f; a.recovery = 0.2f; a.cancelTime = 0.13f;
                    a.lungeSpeed = 5f; a.knockback = new Vector2(4f, 1.5f); a.hitStun = 0.25f;
                    a.hitStop = 0.05f; a.shake = 0.15f; a.overdriveGain = 5f; a.slashAngle = -15f;
                    a.hitboxOffset = new Vector2(1f, 0.1f); a.hitboxSize = new Vector2(2f, 1.3f);
                }),
                EnsureAttack("Attack2", a =>
                {
                    a.damageMultiplier = 1.2f; a.startup = 0.05f; a.active = 0.08f; a.recovery = 0.22f; a.cancelTime = 0.14f;
                    a.lungeSpeed = 6f; a.knockback = new Vector2(5f, 2f); a.hitStun = 0.28f;
                    a.hitStop = 0.06f; a.shake = 0.2f; a.overdriveGain = 6f; a.slashAngle = 15f;
                    a.hitboxOffset = new Vector2(1f, 0.1f); a.hitboxSize = new Vector2(2.1f, 1.4f);
                }),
                EnsureAttack("Attack3_Finisher", a =>
                {
                    a.damageMultiplier = 2.2f; a.startup = 0.09f; a.active = 0.1f; a.recovery = 0.32f; a.cancelTime = 0.3f;
                    a.lungeSpeed = 9f; a.knockback = new Vector2(12f, 5f); a.hitStun = 0.45f;
                    a.hitStop = 0.12f; a.shake = 0.45f; a.overdriveGain = 10f; a.slashAngle = 0f; a.isFinisher = true;
                    a.hitboxOffset = new Vector2(1.3f, 0.1f); a.hitboxSize = new Vector2(2.6f, 1.6f);
                }),
            };
        }

        private static AttackData EnsureAttack(string name, System.Action<AttackData> configure)
        {
            string path = $"{AttackFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (existing != null)
            {
                return existing;
            }

            var attack = ScriptableObject.CreateInstance<AttackData>();
            configure(attack);
            AssetDatabase.CreateAsset(attack, path);
            // 새로 만든 에셋은 저장·재로드한 뒤 참조해야 씬에 참조가 남는다.
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AttackData>(path);
        }

        // ---------- 자리표시 스프라이트 ----------

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

        private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Color color, int order, Vector2 localPosition, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (_material != null)
            {
                renderer.sharedMaterial = _material;
            }

            return renderer;
        }

        // ---------- 프로젝트 설정 ----------

        private static void EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(GameLayers.Ground).stringValue = "Ground";
            layers.GetArrayElementAtIndex(GameLayers.Player).stringValue = "Player";
            layers.GetArrayElementAtIndex(GameLayers.Enemy).stringValue = "Enemy";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <param name="first">true면 빌드 첫 씬(게임 시작 씬)으로 둔다.</param>
        private static void AddSceneToBuildSettings(string path, bool first)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path != path)
                {
                    scenes.Add(scene);
                }
            }

            var entry = new EditorBuildSettingsScene(path, true);
            if (first)
            {
                scenes.Insert(0, entry);
            }
            else
            {
                scenes.Add(entry);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

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

        // ---------- SerializedObject 도우미 (private 직렬화 필드 설정) ----------

        private static void SetObject(Object target, string field, Object value)
        {
            Modify(target, field, p => p.objectReferenceValue = value);
        }

        private static void SetObjectArray(Object target, string field, Object[] values)
        {
            Modify(target, field, p =>
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            });
        }

        private static void SetEnum(Object target, string field, int index) => Modify(target, field, p => p.enumValueIndex = index);

        private static void SetDouble(Object target, string field, double value) => Modify(target, field, p => p.doubleValue = value);

        private static void SetFloat(Object target, string field, float value) => Modify(target, field, p => p.floatValue = value);

        private static void SetInt(Object target, string field, int value) => Modify(target, field, p => p.intValue = value);

        private static void SetBool(Object target, string field, bool value) => Modify(target, field, p => p.boolValue = value);

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
