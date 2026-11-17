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
            Assert.AreEqual(6, Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None).Length, "슬라임 수");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Run_Jump_Dash_MoveThePlayer()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            float startX = player.transform.position.x;

            Press(Key.D);
            yield return new WaitForSecondsRealtime(0.4f);
            Release();
            Assert.Greater(player.transform.position.x, startX + 1.5f, "오른쪽으로 달리지 않았습니다");

            float groundY = player.transform.position.y;
            Press(Key.Space);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Greater(player.transform.position.y, groundY + 1f, "점프하지 않았습니다");
            Release();
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(player.IsGrounded, "점프 후 착지하지 않았습니다");

            float dashStartX = player.transform.position.x;
            Press(Key.K);
            yield return new WaitForSecondsRealtime(0.25f);
            Release();
            Assert.Greater(player.transform.position.x, dashStartX + 2.5f, "대시 거리가 부족합니다");
        }

        [UnityTest]
        public IEnumerator HoldingAttack_DamagesEnemyInFront_AndChargesOverdriveBack()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            SlimeEnemy slime = PlaceSlimeInFrontOfPlayer(player);
            var health = slime.GetComponent<Health>();
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
            Press(Key.J);
            yield return new WaitForSecondsRealtime(0.9f);
            Release();
            Health.AnyDamaged -= Count;

            Assert.GreaterOrEqual(damageEvents, 2, "공격 키를 누르고 있어도 콤보가 이어지지 않았습니다");
            Assert.IsTrue(health == null || health.Current < startHealth, "적이 피해를 입지 않았습니다");
        }

        [UnityTest]
        public IEnumerator Overdrive_SlowsWorld_PlayerKeepsNormalSpeed()
        {
            // 적 접촉 피격(히트스톱)이 시간 판정을 흔들지 않도록 적을 치운다.
            Object.Destroy(Object.FindFirstObjectByType<EnemySpawner>().gameObject);
            foreach (SlimeEnemy enemy in Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None))
            {
                Object.Destroy(enemy.gameObject);
            }

            yield return null;

            var overdrive = Object.FindFirstObjectByType<OverdriveController>();
            Assert.IsTrue(overdrive.IsReady, "M1 설정상 오버드라이브 게이지가 가득 차 있어야 합니다");

            Press(Key.L);
            yield return new WaitForSecondsRealtime(0.3f);
            Release();

            Assert.IsTrue(overdrive.IsActive, "오버드라이브가 발동하지 않았습니다");
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.01f), "세상이 느려지지 않았습니다");
            Assert.That(TimeController.PlayerTimeMultiplier, Is.EqualTo(4f).Within(0.01f));

            var player = Object.FindFirstObjectByType<PlayerController>();
            float startX = player.transform.position.x;
            Press(Key.D);
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
            TimeController.Instance.HitStop(0.1f);
            Assert.AreEqual(0f, Time.timeScale);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator KilledEnemy_IsRespawned()
        {
            var slime = Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None).First();
            var health = slime.GetComponent<Health>();
            health.TryApplyDamage(new DamageInfo(1e9, true, slime.transform.position, Vector2.zero, 0f, null));
            Assert.IsTrue(health.IsDead);

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(slime == null, "죽은 적이 사라지지 않았습니다");
            Assert.AreEqual(5, Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None).Length);

            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(6, Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None).Length, "적이 다시 생성되지 않았습니다");
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

        private static SlimeEnemy PlaceSlimeInFrontOfPlayer(PlayerController player)
        {
            SlimeEnemy slime = Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None).First();
            var body = slime.GetComponent<Rigidbody2D>();
            Vector2 position = (Vector2)player.transform.position + new Vector2(player.Facing * 1.2f, -0.35f);
            body.position = position;
            slime.transform.position = position;
            body.linearVelocity = Vector2.zero;
            return slime;
        }

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
