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
        public IEnumerator Run_StartsInRoomOne_WithEnemies()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            Assert.IsNotNull(run);
            Assert.AreEqual(0, run.RoomIndex);
            Assert.AreEqual(5, run.RoomCount, "일반 4구역 + 보스");
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

            yield return ClearUntil(() => choice.IsOpen, 30f);
            Assert.IsTrue(choice.IsOpen, "웨이브 정리 후 강화 선택이 뜨지 않았습니다");
            Assert.AreEqual(0f, Time.timeScale, "강화 선택 중 게임이 멈추지 않았습니다");

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Digit1);
            Assert.IsFalse(choice.IsOpen, "1키로 선택되지 않았습니다");
            Assert.Greater(stats.Modifiers.Count, 0, "선택한 강화가 적용되지 않았습니다");

            // 구역 끝까지 진행하면 다음 구역으로.
            yield return PickUntil(() => run.RoomIndex >= 1, choice, 40f);
            Assert.AreEqual(1, run.RoomIndex, "다음 구역으로 넘어가지 않았습니다");
            Assert.Greater(Time.timeScale, 0.1f);
        }

        [UnityTest]
        public IEnumerator Reroll_ConsumesOneCharge()
        {
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
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
            yield return Tap(Key.J);
            Assert.IsTrue(shop.IsOpen, "결과 화면 다음에 강화소가 열려야 합니다");

            yield return new WaitForSecondsRealtime(0.5f);
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

            BossSlime boss = null;
            float waited = 0f;
            while (boss == null && waited < 10f)
            {
                boss = Object.FindFirstObjectByType<BossSlime>();
                waited += 0.1f;
                yield return new WaitForSecondsRealtime(0.1f);
            }

            Assert.IsNotNull(boss, "보스가 나오지 않았습니다");
            var bossHealth = boss.GetComponent<Health>();
            Assert.Greater(bossHealth.Max, 2500, "보스 체력이 구역 성장 배율로 늘어나지 않았습니다");

            yield return new WaitForSecondsRealtime(1.5f);
            bossHealth.TryApplyDamage(new DamageInfo(bossHealth.Max * 0.6, false, boss.transform.position, Vector2.zero, 0f, null));
            Assert.IsTrue(boss.IsEnraged, "체력 50% 이하에서 분노하지 않았습니다");
            yield return new WaitForSecondsRealtime(1f);
            bossHealth.TryApplyDamage(new DamageInfo(bossHealth.Max, false, boss.transform.position, Vector2.zero, 0f, null));

            yield return new WaitForSecondsRealtime(3.5f);
            Assert.IsTrue(result.IsOpen, "승리 결과 화면이 뜨지 않았습니다");
            Assert.Greater(run.Kills, 20);
            Assert.Greater(run.CrystalsEarned, 40, "승리 보상 결정이 들어오지 않았습니다");
            Assert.AreEqual(1, MetaProgression.Data.victories);
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

                KillEnemies();
                elapsed += 0.2f;
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        private static void KillEnemies()
        {
            foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team == Team.Enemy && !health.IsDead && health.GetComponent<BossSlime>() == null)
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
