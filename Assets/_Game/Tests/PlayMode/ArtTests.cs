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
            // 돌진하는 적에게 밀리면 달리기 판정이 흔들리므로 적을 치운다.
            Object.Destroy(Object.FindFirstObjectByType<EnemySpawner>().gameObject);
            foreach (Health enemy in Object.FindObjectsByType<Health>(FindObjectsSortMode.None).Where(h => h.Team == Team.Enemy))
            {
                Object.Destroy(enemy.gameObject);
            }

            foreach (GearProjectile gear in Object.FindObjectsByType<GearProjectile>(FindObjectsSortMode.None))
            {
                Object.Destroy(gear.gameObject);
            }

            var controller = Object.FindFirstObjectByType<PlayerController>();
            // 정리 전에 맞았을 수 있으니 경직·공중 상태가 풀릴 때까지 기다린다.
            float settle = 0f;
            while ((controller.IsHurt || !controller.IsGrounded) && settle < 2f)
            {
                settle += 0.05f;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            yield return new WaitForSecondsRealtime(0.1f);
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
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.X));
            yield return new WaitForSecondsRealtime(0.08f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            StringAssert.StartsWith("hero_attack", controller.BodyRenderer.sprite.name, "공격할 때 hero_attack 프레임이어야 합니다");
        }

        [UnityTest]
        public IEnumerator Enemy_Flashes_WhenDamaged()
        {
            var enemy = Object.FindObjectsByType<Health>(FindObjectsSortMode.None).First(h => h.Team == Team.Enemy);
            var renderer = enemy.GetComponentInChildren<SpriteRenderer>();
            Assert.IsNotNull(renderer.sprite);
            Assert.IsNotNull(enemy.GetComponent<HitFlash>());
            Color originalColor = renderer.color;
            enemy.TryApplyDamage(new DamageInfo(1, false, enemy.transform.position, Vector2.zero, 0.1f, null));
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.HasProperty("_FlashAmount"))
                Assert.That(block.GetFloat("_FlashAmount"), Is.EqualTo(0.85f).Within(0.001f));
            else
                Assert.AreEqual(Color.Lerp(originalColor, Color.white, 0.85f), renderer.color);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_ShowsHurtFace_WhenDamaged()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Enemies/Slime.prefab");
            Assert.IsNotNull(prefab);
            var slime = Object.Instantiate(prefab, new Vector3(0f, 100f, 0f), Quaternion.identity);
            try
            {
                yield return null;
                var renderer = slime.GetComponentInChildren<SpriteRenderer>();
                StringAssert.StartsWith("slime_idle", renderer.sprite.name);
                slime.GetComponent<Health>().TryApplyDamage(new DamageInfo(1, false,
                    slime.transform.position, Vector2.zero, 0.1f, null));
                yield return null;
                StringAssert.StartsWith("slime_hurt", renderer.sprite.name,
                    "Damage must show the slime hurt sprite");
            }
            finally
            {
                Object.Destroy(slime);
            }
#else
            Assert.Ignore("This test loads the slime prefab through the editor AssetDatabase.");
            yield break;
#endif
        }

        [Test]
        public void Background_HasThreeParallaxLayers()
        {
            Assert.AreEqual(3, Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None).Length);
        }
    }
}
