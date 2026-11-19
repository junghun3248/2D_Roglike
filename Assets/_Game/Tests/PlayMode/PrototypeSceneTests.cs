using System.Collections;
using System.Linq;
using NUnit.Framework;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Enemies;
using Roglike.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Roglike.Tests
{
    /// <summary>
    /// Prototype 씬을 실제로 돌려 보는 스모크 테스트. 가상 키보드로 키를 눌러 조작한다.
    /// 실행: Window > General > Test Runner > PlayMode, 또는 배치 모드 -runTests -testPlatform PlayMode.
    /// </summary>
    public class PrototypeSceneTests
    {
        private Keyboard _keyboard;
        private InputSettings.BackgroundBehavior _previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 배치 모드·Test Runner에서는 게임 창에 포커스가 없어 입력이 막히므로 테스트 동안만 해제.
            _previousBackground = InputSystem.settings.backgroundBehavior;
            _previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            _keyboard = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            SceneManager.LoadScene("Prototype");
            // 착지·적 생성 대기.
            yield return new WaitForSecondsRealtime(1.5f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Release();
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.backgroundBehavior = _previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorBehavior;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_LandsOnGround_AndEnemiesSpawn()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Player가 없습니다");
            Assert.IsTrue(player.IsGrounded, "Player가 땅에 서 있지 않습니다");
            Assert.That(player.transform.position.y, Is.InRange(0.5f, 1.2f));
            Assert.AreEqual(MaxAlive, SceneEnemies().Length, "적 수 (연습장 동시 최대)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Run_Jump_Dash_MoveThePlayer()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            yield return ClearEnemiesAndSettle(player);
            float startX = player.transform.position.x;

            Press(Key.RightArrow);
            yield return new WaitForSecondsRealtime(0.4f);
            Release();
            Assert.Greater(player.transform.position.x, startX + 1.5f, "오른쪽으로 달리지 않았습니다");

            float groundY = player.transform.position.y;
            Press(Key.Z);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Greater(player.transform.position.y, groundY + 1f, "점프하지 않았습니다");
            Release();
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(player.IsGrounded, "점프 후 착지하지 않았습니다");

            float dashStartX = player.transform.position.x;
            Press(Key.C);
            yield return new WaitForSecondsRealtime(0.25f);
            Release();
            Assert.Greater(player.transform.position.x, dashStartX + 2.5f, "대시 거리가 부족합니다");
        }

        [UnityTest]
        public IEnumerator HoldingAttack_DamagesEnemyInFront_AndChargesOverdriveBack()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Health health = PlaceEnemyInFrontOfPlayer(player);
            double startHealth = health.Current;

            int damageEvents = 0;
            void Count(Health target, DamageInfo info)
            {
                if (target == health)
                {
                    damageEvents++;
                }
            }

            Health.AnyDamaged += Count;
            Press(Key.X);
            yield return new WaitForSecondsRealtime(0.9f);
            Release();
            Health.AnyDamaged -= Count;

            Assert.GreaterOrEqual(damageEvents, 2, "X를 누르고 있어도 단일 베기가 반복되지 않았습니다");
            Assert.IsTrue(health == null || health.Current < startHealth, "적이 피해를 입지 않았습니다");
        }

        [UnityTest]
        public IEnumerator Overdrive_SlowsWorld_PlayerKeepsNormalSpeed()
        {
            // 적 접촉 피격(히트스톱·경직)이 시간·이동 판정을 흔들지 않도록 적을 치우고 경직이 풀릴 때까지 기다린다.
            yield return ClearEnemiesAndSettle(Object.FindFirstObjectByType<PlayerController>());

            var overdrive = Object.FindFirstObjectByType<OverdriveController>();
            Assert.IsTrue(overdrive.IsReady, "M1 설정상 오버드라이브 게이지가 가득 차 있어야 합니다");

            Press(Key.Space);
            yield return new WaitForSecondsRealtime(0.3f);
            Release();

            Assert.IsTrue(overdrive.IsActive, "오버드라이브가 발동하지 않았습니다");
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.01f), "세상이 느려지지 않았습니다");
            Assert.That(TimeController.PlayerTimeMultiplier, Is.EqualTo(4f).Within(0.01f));

            var player = Object.FindFirstObjectByType<PlayerController>();
            float startX = player.transform.position.x;
            Press(Key.RightArrow);
            yield return new WaitForSecondsRealtime(0.4f);
            Release();
            Assert.Greater(player.transform.position.x, startX + 1.5f, "오버드라이브 중 플레이어가 정상 속도로 움직이지 않습니다");

            yield return new WaitForSecondsRealtime(5f);
            Assert.IsFalse(overdrive.IsActive, "오버드라이브가 끝나지 않았습니다");
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator HitStop_FreezesThenRecovers()
        {
            // 돌진하는 멧돼지에게 맞으면 새 히트스톱이 걸려 판정이 흔들리므로 적을 치운다.
            Object.Destroy(Object.FindFirstObjectByType<EnemySpawner>().gameObject);
            foreach (Health enemy in SceneEnemies())
            {
                Object.Destroy(enemy.gameObject);
            }

            yield return new WaitForSecondsRealtime(0.3f);
            TimeController.Instance.HitStop(0.1f);
            Assert.AreEqual(0f, Time.timeScale);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator KilledEnemy_IsRespawned()
        {
            Health health = SceneEnemies().First();
            health.TryApplyDamage(new DamageInfo(1e9, true, health.transform.position, Vector2.zero, 0f, null));
            Assert.IsTrue(health.IsDead);

            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(health == null, "죽은 적이 사라지지 않았습니다");
            Assert.AreEqual(MaxAlive - 1, SceneEnemies().Length);

            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(MaxAlive, SceneEnemies().Length, "적이 다시 생성되지 않았습니다");
        }

        [UnityTest]
        public IEnumerator DestroyedEnemy_IsRespawnedOnce()
        {
            Object.Destroy(SceneEnemies().First().gameObject);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(MaxAlive - 1, SceneEnemies().Length);
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(MaxAlive, SceneEnemies().Length);
        }

        [Test]
        public void NumberFormat_UsesKoreanUnits()
        {
            Assert.AreEqual("999", NumberFormat.ToKorean(999));
            Assert.AreEqual("9,999", NumberFormat.ToKorean(9999));
            Assert.AreEqual("1.2만", NumberFormat.ToKorean(12345));
            Assert.AreEqual("123만", NumberFormat.ToKorean(1234567));
            Assert.AreEqual("1.2억", NumberFormat.ToKorean(123456789));
            Assert.AreEqual("1.5조", NumberFormat.ToKorean(1.5e12));
        }

        private static Health PlaceEnemyInFrontOfPlayer(PlayerController player)
        {
            Health enemy = SceneEnemies().First(h => !h.IsDead && h.GetComponent<Rigidbody2D>() != null);
            foreach (MonoBehaviour behaviour in enemy.GetComponents<MonoBehaviour>())
                if (behaviour is BoarEnemy || behaviour is SquirrelEnemy || behaviour is SlimeEnemy) behaviour.enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            Vector2 position = (Vector2)player.transform.position + new Vector2(player.Facing * 1.2f, -0.35f);
            body.position = position;
            enemy.transform.position = position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            return enemy;
        }

        // 돌진하는 멧돼지·날아오는 톱니에 밀리면 판정이 흔들리므로 적과 투사체를 치우고,
        // 시작 전에 맞았을 수 있으니 경직이 풀리고 착지할 때까지 기다린다.
        private static IEnumerator ClearEnemiesAndSettle(PlayerController player)
        {
            Object.Destroy(Object.FindFirstObjectByType<EnemySpawner>().gameObject);
            foreach (Health enemy in SceneEnemies())
            {
                Object.Destroy(enemy.gameObject);
            }

            foreach (GearProjectile gear in Object.FindObjectsByType<GearProjectile>(FindObjectsSortMode.None))
            {
                Object.Destroy(gear.gameObject);
            }

            float settle = 0f;
            while ((player.IsHurt || !player.IsGrounded) && settle < 2f)
            {
                settle += 0.05f;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            yield return new WaitForSecondsRealtime(0.1f);
        }

        // 연습장 스포너의 동시 최대 적 수 (플레이 피드백 "몹이 너무 많다"로 6 → 3).
        private const int MaxAlive = 3;

        private static Health[] SceneEnemies() => Object.FindObjectsByType<Health>(FindObjectsSortMode.None)
            .Where(health => health.Team == Team.Enemy).ToArray();

        private void Press(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
        }

        private void Release()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        }
    }
}
