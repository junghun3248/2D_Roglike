using System.Collections;
using System.IO;
using NUnit.Framework;
using Roglike.Combat;
using Roglike.Data;
using Roglike.Enemies;
using Roglike.Progression;
using Roglike.Run;
using Roglike.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Roglike.Tests
{
    /// <summary>Run 씬 한 판 흐름: 구역 → 강화 선택(시너지·리롤) → 보스 → 결과 → 강화소.</summary>
    public class RunSceneTests
    {
        private Keyboard _keyboard;
        private InputSettings.BackgroundBehavior _previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorBehavior;
        private string _savePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 실제 플레이어 저장 파일을 건드리지 않도록 임시 파일 사용.
            _savePath = Path.Combine(Application.temporaryCachePath, "meta_test.json");
            File.Delete(_savePath);
            MetaProgression.SavePathOverride = _savePath;
            MetaProgression.ResetData();

            _previousBackground = InputSystem.settings.backgroundBehavior;
            _previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("RunTestKeyboard");

            SceneManager.LoadScene("Run");
            yield return new WaitForSecondsRealtime(0.2f);
            // 긴 테스트 중 접촉 피해로 죽지 않게.
            PlayerHealth().SetInvulnerable(10000f);
            yield return new WaitForSecondsRealtime(1.8f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.backgroundBehavior = _previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorBehavior;
            Time.timeScale = 1f;
            MetaProgression.SavePathOverride = null;
            MetaProgression.ResetData();
            File.Delete(_savePath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Run_StartsInEntrance_ThenWalkingLocksCombatRoom()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            Assert.IsNotNull(run);
            Assert.AreEqual(0, run.RoomIndex);
            Assert.AreEqual(6, run.RoomCount);
            Assert.AreEqual(RoomType.Entrance, run.CurrentRoomType);
            Assert.AreEqual(0, run.EnemiesAlive);
            Assert.IsInstanceOf<Roglike.Player.Skills.StasisFieldSkill>(
                PlayerHealth().GetComponent<Roglike.Player.PlayerSkills>().Slots[0]);
            Assert.IsTrue(run.CurrentRoom.RightDoorOpen);
            yield return EnterNextRoom(run);
            Assert.AreEqual(1, run.RoomIndex);
            Assert.IsTrue(run.CurrentRoom.LeftDoorClosed);
            Assert.IsFalse(run.CurrentRoom.RightDoorOpen);
            Assert.Greater(run.EnemiesAlive, 0, "적이 나오지 않았습니다");
            Assert.AreEqual(1, run.RerollsLeft, "기본 리롤 1회");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearingWaves_OpensUpgradeChoice_AndPickingAppliesIt()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            var stats = Object.FindFirstObjectByType<PlayerStats>();

            yield return EnterNextRoom(Object.FindFirstObjectByType<RunManager>());
            yield return ClearUntil(() => choice.IsOpen, 30f);
            Assert.IsTrue(choice.IsOpen, "웨이브 정리 후 강화 선택이 뜨지 않았습니다");
            Assert.AreEqual(0f, Time.timeScale, "강화 선택 중 게임이 멈추지 않았습니다");

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Z);
            Assert.IsTrue(choice.IsOpen, "점프는 강화를 확정하지 않는다");
            yield return Tap(Key.X);
            Assert.IsFalse(choice.IsOpen, "X키로 선택되지 않았습니다");
            Assert.Greater(stats.Modifiers.Count, 0, "선택한 강화가 적용되지 않았습니다");

            yield return PickUntil(() => run.CurrentRoom.RightDoorOpen && choice.IsOpen, choice, 40f);
            Assert.AreEqual(1, run.RoomIndex, "걸어가지 않으면 현재 방에 머물러야 합니다");
            Assert.IsTrue(run.CurrentRoom.RightDoorOpen);
            Assert.IsTrue(choice.IsOpen, "클리어 보상이 필요합니다");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Digit1);
            yield return EnterNextRoom(run);
            Assert.AreEqual(2, run.RoomIndex);
            Assert.Greater(Time.timeScale, 0.1f);
        }

        [UnityTest]
        public IEnumerator Reroll_ConsumesOneCharge()
        {
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            yield return EnterNextRoom(Object.FindFirstObjectByType<RunManager>());
            yield return ClearUntil(() => choice.IsOpen, 30f);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreEqual(1, choice.RerollsLeft);
            yield return Tap(Key.R);
            Assert.IsTrue(choice.IsOpen, "리롤 후에도 선택 화면이 열려 있어야 합니다");
            Assert.AreEqual(0, choice.RerollsLeft, "리롤 횟수가 줄지 않았습니다");

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.R);
            Assert.AreEqual(0, choice.RerollsLeft, "남은 리롤이 없는데 또 리롤됐습니다");
        }

        [UnityTest]
        public IEnumerator Synergy_ReachingThreshold_BoostsStats()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var stats = Object.FindFirstObjectByType<PlayerStats>();
            float before = stats.Get(StatType.AttackPower);

            var blade = ScriptableObject.CreateInstance<UpgradeData>();
            blade.tags = new[] { UpgradeTag.Blade };
            int tierReached = -1;
            run.Synergy.TierReached += (_, tier) => tierReached = tier;

            run.Synergy.Add(blade);
            Assert.AreEqual(-1, run.Synergy.ActiveTierIndex(UpgradeTag.Blade), "1개로는 발동하면 안 됩니다");
            run.Synergy.Add(blade);
            Assert.AreEqual(0, run.Synergy.ActiveTierIndex(UpgradeTag.Blade), "2개에서 1단계가 발동해야 합니다");
            Assert.AreEqual(0, tierReached);
            float tier1 = stats.Get(StatType.AttackPower);
            Assert.Greater(tier1, before, "검술 시너지가 공격력을 올리지 않았습니다");

            run.Synergy.Add(blade);
            run.Synergy.Add(blade);
            Assert.AreEqual(1, run.Synergy.ActiveTierIndex(UpgradeTag.Blade));
            Assert.Greater(stats.Get(StatType.AttackPower), tier1, "2단계가 1단계를 대체하며 더 강해야 합니다");

            Object.Destroy(blade);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MetaUpgrade_PurchasePersists_AndAppliesToStats()
        {
            MetaProgression.Data.crystals = 100;
            var attack = ScriptableObject.CreateInstance<MetaUpgradeData>();
            attack.id = "test_attack";
            attack.maxLevel = 5;
            attack.baseCost = 20;
            attack.costGrowth = 1.5f;
            attack.modifiersPerLevel = new[] { new StatModifier(StatType.AttackPower, ModifierKind.Flat, 3f) };

            Assert.IsTrue(MetaProgression.TryBuy(attack));
            Assert.IsTrue(MetaProgression.TryBuy(attack));
            Assert.AreEqual(2, MetaProgression.Level("test_attack"));
            Assert.AreEqual(100 - 20 - 30, MetaProgression.Crystals);
            Assert.IsTrue(File.Exists(_savePath), "저장 파일이 만들어지지 않았습니다");

            MetaProgression.Load();
            Assert.AreEqual(2, MetaProgression.Level("test_attack"), "다시 불러온 저장에 레벨이 남아 있어야 합니다");

            var stats = Object.FindFirstObjectByType<PlayerStats>();
            float before = stats.Get(StatType.AttackPower);
            MetaProgression.ApplyTo(stats, new[] { attack });
            Assert.That(stats.Get(StatType.AttackPower), Is.EqualTo(before + 6f).Within(0.01f));

            Object.Destroy(attack);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerDeath_AwardsCrystals_ThenShop_ThenNewRun()
        {
            Health player = PlayerHealth();
            var result = Object.FindFirstObjectByType<RunResultScreen>();
            var shop = Object.FindFirstObjectByType<MetaShopUI>();
            var firstRun = Object.FindFirstObjectByType<RunManager>();

            player.ClearInvulnerable();
            player.TryApplyDamage(new DamageInfo(99999, false, player.transform.position, Vector2.zero, 0f, null));
            Assert.IsTrue(player.IsDead);

            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(result.IsOpen, "패배 결과 화면이 뜨지 않았습니다");
            Assert.AreEqual(1, MetaProgression.Data.totalRuns, "런 기록이 저장되지 않았습니다");

            yield return new WaitForSecondsRealtime(0.8f);
            yield return Tap(Key.Z);
            Assert.IsTrue(result.IsOpen, "Jump must not confirm a result screen");
            yield return Tap(Key.X);
            Assert.IsTrue(shop.IsOpen, "결과 화면 다음에 강화소가 열려야 합니다");

            MetaProgression.Data.crystals = 10000;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Z);
            Assert.AreEqual(10000, MetaProgression.Crystals);
            Assert.IsTrue(shop.IsOpen);
            yield return Tap(Key.X);
            Assert.Less(MetaProgression.Crystals, 10000, "X must buy the selected upgrade");
            yield return Tap(Key.Space);
            yield return new WaitForSecondsRealtime(1f);
            var newRun = Object.FindFirstObjectByType<RunManager>();
            Assert.IsNotNull(newRun);
            Assert.AreNotEqual(firstRun, newRun, "새 런이 시작되지 않았습니다");
            Assert.Greater(Time.timeScale, 0.1f);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator FullRun_ReachesBoss_AndVictory()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            var result = Object.FindFirstObjectByType<RunResultScreen>();

            yield return PickUntil(() => run.IsBossRoom, choice, 120f);
            Assert.IsTrue(run.IsBossRoom, "보스 구역에 도달하지 못했습니다");

            StagKingBoss boss = null;
            float waited = 0f;
            while (boss == null && waited < 10f)
            {
                boss = Object.FindFirstObjectByType<StagKingBoss>();
                waited += 0.1f;
                yield return new WaitForSecondsRealtime(0.1f);
            }

            Assert.IsNotNull(boss, "보스가 나오지 않았습니다");
            var barUI = Object.FindFirstObjectByType<BossHealthBar>();
            var bar = (RectTransform)barUI.transform.Find("BossHealthCanvas/BossBar");
            Assert.IsNotNull(bar);
            var canvas = bar.GetComponentInParent<Canvas>();
            Canvas.ForceUpdateCanvases();
            // 보스 바는 화면 위 가운데: 캔버스 위쪽 절반, 좌하단 플레이어 묶음과 세로로 겹치지 않음.
            Assert.Greater(canvas.transform.InverseTransformPoint(bar.TransformPoint(bar.rect.center)).y, 0f);
            var cluster = Object.FindFirstObjectByType<HudController>().Cluster;
            var barCorners = new Vector3[4];
            var clusterCorners = new Vector3[4];
            bar.GetWorldCorners(barCorners);
            cluster.GetWorldCorners(clusterCorners);
            Assert.Greater(barCorners[0].y, clusterCorners[1].y, "보스 바가 플레이어 묶음과 겹칩니다");
            var bossHealth = boss.GetComponent<Health>();
            var config = (RunConfig)typeof(RunManager).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(run);
            double expectedHealth = config.areas[0].boss.GetComponent<Health>().Max
                * Mathf.Pow(config.enemyHealthGrowth, run.RoomIndex) * config.bossHealthMultiplier;
            Assert.That(bossHealth.Max, Is.EqualTo(expectedHealth).Within(0.1));

            yield return new WaitForSecondsRealtime(1.5f);
            bossHealth.TryApplyDamage(new DamageInfo(bossHealth.Max * 0.6, false, boss.transform.position, Vector2.zero, 0f, null));
            Assert.IsTrue(boss.IsEnraged, "체력 50% 이하에서 분노하지 않았습니다");
            yield return new WaitForSecondsRealtime(1f);
            bossHealth.TryApplyDamage(new DamageInfo(bossHealth.Max, false, boss.transform.position, Vector2.zero, 0f, null));

            yield return new WaitForSecondsRealtime(3.5f);
            Assert.IsTrue(result.IsOpen, "승리 결과 화면이 뜨지 않았습니다");
            // 몹 수를 줄인 뒤(웨이브 약 60%) 한 판 처치 수는 15 안팎.
            Assert.Greater(run.Kills, 10);
            Assert.Greater(run.CrystalsEarned, 40, "승리 보상 결정이 들어오지 않았습니다");
            Assert.AreEqual(1, MetaProgression.Data.victories);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Rest_OffersTwoChoices_AndHealsFortyPercent()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            var rest = Object.FindFirstObjectByType<RestChoiceUI>();
            yield return PickUntil(() => rest.IsOpen, choice, 90f);
            Assert.AreEqual(RoomType.Rest, run.CurrentRoomType);
            Assert.IsFalse(run.CurrentRoom.RightDoorOpen);
            Assert.AreEqual(0, run.EnemiesAlive);
            Health health = PlayerHealth();
            health.ClearInvulnerable();
            health.TryApplyDamage(new DamageInfo(health.Max * 0.7, false, health.transform.position, Vector2.zero, 0f, null));
            double before = health.Current;
            health.SetInvulnerable(10000f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Z);
            Assert.IsTrue(rest.IsOpen);
            yield return Tap(Key.X);
            Assert.IsFalse(rest.IsOpen);
            Assert.That(health.Current, Is.EqualTo(before + health.Max * 0.4).Within(0.01));
            Assert.IsTrue(run.CurrentRoom.RightDoorOpen);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Rest_UpgradeChoice_PrefersRareRewards()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            var rest = Object.FindFirstObjectByType<RestChoiceUI>();
            yield return PickUntil(() => rest.IsOpen, choice, 90f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Digit2);
            Assert.IsTrue(choice.IsOpen);
            Assert.IsFalse(run.CurrentRoom.RightDoorOpen);
            foreach (UpgradeData option in choice.Options) Assert.AreNotEqual(UpgradeRarity.Common, option.rarity);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Digit1);
            Assert.IsTrue(run.CurrentRoom.RightDoorOpen);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Elite_SpawnsElites_AndOffersRareRewardsIncludingReroll()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            yield return PickUntil(() => run.CurrentRoomType == RoomType.Elite, choice, 90f);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(2, run.EnemiesAlive);
            var elites = Object.FindObjectsByType<EliteModifier>(FindObjectsSortMode.None);
            Assert.AreEqual(1, elites.Length);
            foreach (EliteModifier elite in elites) Assert.IsTrue(elite.IsElite);
            yield return ClearUntil(() => choice.IsOpen, 20f);
            Assert.IsTrue(choice.IsOpen);
            foreach (UpgradeData option in choice.Options) Assert.AreNotEqual(UpgradeRarity.Common, option.rarity);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.R);
            foreach (UpgradeData option in choice.Options) Assert.AreNotEqual(UpgradeRarity.Common, option.rarity);
        }

        [UnityTest]
        public IEnumerator CombatRoom_CapsAliveAtFour_AndRefillsAfterDeath()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var config = (RunConfig)typeof(RunManager).GetField("_config",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(run);
            WaveEntry wave = config.areas[0].rooms[1].waves[0];
            int originalCount = wave.count;
            wave.count = 6;
            try
            {
                yield return EnterNextRoom(run);
                Assert.AreEqual(4, run.EnemiesAlive);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.AreEqual(4, run.EnemiesAlive);
                foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                {
                    if (health.Team != Team.Enemy || health.IsDead) continue;
                    health.ClearInvulnerable();
                    health.TryApplyDamage(new DamageInfo(health.Current + 1, false, health.transform.position, Vector2.zero, 0f, null));
                    break;
                }
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(4, run.EnemiesAlive);
            }
            finally { wave.count = originalCount; }
        }

        private static Health PlayerHealth()
        {
            return Object.FindFirstObjectByType<PlayerStats>().GetComponent<Health>();
        }

        /// <summary>조건이 될 때까지 살아 있는 적을 계속 처치한다.</summary>
        private static IEnumerator ClearUntil(System.Func<bool> done, float timeout)
        {
            float elapsed = 0f;
            while (!done() && elapsed < timeout)
            {
                KillEnemies();
                elapsed += 0.2f;
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        /// <summary>적을 처치하고 강화 선택이 뜰 때마다 2번을 고르며 조건까지 진행한다.</summary>
        private IEnumerator PickUntil(System.Func<bool> done, UpgradeChoiceUI choice, float timeout)
        {
            float elapsed = 0f;
            while (!done() && elapsed < timeout)
            {
                if (choice.IsOpen)
                {
                    yield return new WaitForSecondsRealtime(0.45f);
                    yield return Tap(Key.Digit2);
                    elapsed += 0.65f;
                    continue;
                }

                var rest = Object.FindFirstObjectByType<RestChoiceUI>();
                if (rest.IsOpen)
                {
                    yield return new WaitForSecondsRealtime(0.45f);
                    yield return Tap(Key.Digit1);
                    elapsed += 0.65f;
                    continue;
                }
                var run = Object.FindFirstObjectByType<RunManager>();
                if (run.CurrentRoom.RightDoorOpen && run.RoomIndex + 1 < run.RoomCount)
                {
                    yield return EnterNextRoom(run);
                    elapsed += 1.2f;
                    continue;
                }
                KillEnemies();
                elapsed += 0.2f;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            Assert.IsTrue(done(), "제한 시간 안에 목표 흐름에 도달하지 못했습니다");
        }

        private static IEnumerator EnterNextRoom(RunManager run)
        {
            Assert.IsTrue(run.CurrentRoom.RightDoorOpen);
            float boundary = run.CurrentRoom.MaxX;
            var player = PlayerHealth();
            var body = player.GetComponent<Rigidbody2D>();
            Vector3 position = new Vector3(boundary + 2.5f, 1.2f, 0f);
            body.position = position;
            body.linearVelocity = Vector2.zero;
            player.transform.position = position;
            Physics2D.SyncTransforms();
            yield return new WaitForSecondsRealtime(1.2f);
        }

        private static void KillEnemies()
        {
            foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team == Team.Enemy && !health.IsDead && health.GetComponent<IBoss>() == null)
                {
                    health.TryApplyDamage(new DamageInfo(health.Current + 1, false, health.transform.position, Vector2.zero, 0f, null));
                }
            }
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}
