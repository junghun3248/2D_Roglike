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
    /// <summary>생성 그림이 씬에 제대로 연결됐는지 (Prototype 씬).</summary>
    public class ArtTests
    {
        private Keyboard _keyboard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("ArtTestKeyboard");
            SceneManager.LoadScene("Prototype");
            yield return new WaitForSecondsRealtime(1.2f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.RemoveDevice(_keyboard);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_UsesHeroSprites_AndRunChangesFrame()
        {
            var controller = Object.FindFirstObjectByType<PlayerController>();
            SpriteRenderer body = controller.BodyRenderer;
            Assert.IsNotNull(body.sprite, "주인공 스프라이트가 비어 있습니다");
            StringAssert.StartsWith("hero_idle", body.sprite.name, "대기 중엔 hero_idle 프레임이어야 합니다");
            Assert.IsTrue(body.sharedMaterial.HasProperty("_FlashAmount"), "피격 번쩍임 셰이더가 연결되지 않았습니다");

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.RightArrow));
            yield return new WaitForSecondsRealtime(0.4f);
            StringAssert.StartsWith("hero_run", body.sprite.name, "달릴 때 hero_run 프레임이어야 합니다");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        }

        [UnityTest]
        public IEnumerator Attack_PlaysAttackFrames()
        {
            var controller = Object.FindFirstObjectByType<PlayerController>();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.A));
            yield return new WaitForSecondsRealtime(0.08f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            StringAssert.StartsWith("hero_attack", controller.BodyRenderer.sprite.name, "공격할 때 hero_attack 프레임이어야 합니다");
        }

        [UnityTest]
        public IEnumerator Slime_ShowsHurtFace_WhenDamaged()
        {
            var slime = Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None).First();
            var renderer = slime.GetComponentInChildren<SpriteRenderer>();
            StringAssert.StartsWith("slime_idle", renderer.sprite.name);

            slime.GetComponent<Health>().TryApplyDamage(new DamageInfo(1, false, slime.transform.position, Vector2.zero, 0.1f, null));
            yield return null;
            StringAssert.StartsWith("slime_hurt", renderer.sprite.name, "맞은 직후 아픈 얼굴이어야 합니다");
        }

        [Test]
        public void Background_HasThreeParallaxLayers()
        {
            Assert.AreEqual(3, Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None).Length);
        }
    }
}
