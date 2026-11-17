using System.Collections;
using NUnit.Framework;
using Roglike.Combat;
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
    /// <summary>Run 씬 한 판 흐름: 구역 → 강화 선택 → 보스 → 결과.</summary>
    public class RunSceneTests
    {
        private Keyboard _keyboard;
        private InputSettings.BackgroundBehavior _previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearingRoom_OpensUpgradeChoice_AndPickingAppliesIt()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            var stats = Object.FindFirstObjectByType<PlayerStats>();

            yield return ClearUntil(() => choice.IsOpen, 30f);
            Assert.IsTrue(choice.IsOpen, "구역 클리어 후 강화 선택이 뜨지 않았습니다");
            Assert.AreEqual(0f, Time.timeScale, "강화 선택 중 게임이 멈추지 않았습니다");

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Tap(Key.Digit1);

            Assert.IsFalse(choice.IsOpen, "1키로 선택되지 않았습니다");
            Assert.Greater(stats.Modifiers.Count, 0, "선택한 강화가 적용되지 않았습니다");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(1, run.RoomIndex, "다음 구역으로 넘어가지 않았습니다");
            Assert.Greater(Time.timeScale, 0.1f);
        }

        [UnityTest]
        public IEnumerator PlayerDeath_ShowsDefeatResult()
        {
            Health player = PlayerHealth();
            var result = Object.FindFirstObjectByType<RunResultScreen>();

            // 무적 해제 후 즉사.
            player.ClearInvulnerable();
            player.TryApplyDamage(new DamageInfo(999, false, player.transform.position, Vector2.zero, 0f, null));
            Assert.IsTrue(player.IsDead);

            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(result.IsOpen, "패배 결과 화면이 뜨지 않았습니다");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FullRun_ReachesBoss_AndVictory()
        {
            var run = Object.FindFirstObjectByType<RunManager>();
            var choice = Object.FindFirstObjectByType<UpgradeChoiceUI>();
            var result = Object.FindFirstObjectByType<RunResultScreen>();

            for (int room = 0; room < run.RoomCount - 1; room++)
            {
                yield return ClearUntil(() => choice.IsOpen, 40f);
                Assert.IsTrue(choice.IsOpen, $"{room + 1}구역 강화 선택이 뜨지 않았습니다");
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Tap(Key.Digit2);
            }

            BossSlime boss = null;
            float waited = 0f;
            while (boss == null && waited < 10f)
            {
                boss = Object.FindFirstObjectByType<BossSlime>();
                waited += 0.1f;
                yield return new WaitForSecondsRealtime(0.1f);
            }

            Assert.IsNotNull(boss, "보스가 나오지 않았습니다");
            Assert.IsTrue(run.IsBossRoom);
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
                foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                {
                    if (health.Team == Team.Enemy && !health.IsDead && health.GetComponent<BossSlime>() == null)
                    {
                        health.TryApplyDamage(new DamageInfo(health.Current + 1, false, health.transform.position, Vector2.zero, 0f, null));
                    }
                }

                elapsed += 0.2f;
                yield return new WaitForSecondsRealtime(0.2f);
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
