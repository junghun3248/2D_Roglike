using System.Collections;
using NUnit.Framework;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Player;
using Roglike.Run;
using Roglike.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Roglike.Tests
{
    public class HudLayoutTests
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("HudTestKeyboard");
            _gamepad = InputSystem.AddDevice<Gamepad>("HudTestGamepad");
            yield return Load("Prototype");
        }

        private static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return new WaitForSecondsRealtime(0.4f);
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            player.GetComponent<Health>().SetInvulnerable(10000f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.FindFirstObjectByType<PauseMenuUI>()?.Resume();
            TimeController.Instance?.SetPaused(false);
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_gamepad);
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothScenes_GroupPlayerInfoInBottomLeft_WithoutPermanentControls()
        {
            AssertCluster();
            yield return Load("Run");
            AssertCluster();
        }

        private static void AssertCluster()
        {
            Canvas.ForceUpdateCanvases();
            var hud = Object.FindFirstObjectByType<HudController>();
            Assert.IsNotNull(hud);
            var canvas = hud.Cluster.GetComponentInParent<Canvas>();
            Vector3 center = canvas.transform.InverseTransformPoint(hud.Cluster.TransformPoint(hud.Cluster.rect.center));
            Assert.Less(center.x, 0f);
            Assert.Less(center.y, 0f);
            var slots = hud.Cluster.Find("SkillSlots");
            Assert.IsNotNull(slots);
            Assert.AreEqual(4, slots.childCount);
            foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // 숨겨진 UI도 검사하므로 비활성 부모까지 포함해 캔버스를 찾는다.
                Canvas owner = text.GetComponentInParent<Canvas>(true);
                if (owner != null && owner.name == "PauseMenu") continue;
                Assert.IsFalse(text.text.Contains("강공격 V") || text.text.Contains("강공격/차지 V")
                    || text.text.Contains("오버드라이브 Space"), text.name);
            }
        }

        [UnityTest]
        public IEnumerator DamageUpdatesFillAndVignette_HealingClearsVignette()
        {
            var health = Object.FindFirstObjectByType<PlayerController>().GetComponent<Health>();
            health.ClearInvulnerable();
            Assert.IsTrue(health.TryApplyDamage(new DamageInfo(health.Current - health.Max * 0.2, false,
                health.transform.position, Vector2.zero, 0f, null)));
            health.SetInvulnerable(10000f);
            yield return null;
            yield return null;
            Assert.That(Object.FindFirstObjectByType<HudController>().HealthFill, Is.EqualTo(0.2f).Within(0.001f));
            Assert.Greater(Object.FindFirstObjectByType<HudFeedbackUI>().VignetteAlpha, 0f);
            health.Heal(health.Max);
            yield return null;
            yield return null;
            Assert.AreEqual(1f, Object.FindFirstObjectByType<HudController>().HealthFill);
            Assert.AreEqual(0f, Object.FindFirstObjectByType<HudFeedbackUI>().VignetteAlpha);
        }

        [UnityTest]
        public IEnumerator SkillCooldownUsesRadialSweep()
        {
            yield return Tap(Key.A);
            yield return new WaitForSecondsRealtime(0.2f);
            var slots = Object.FindFirstObjectByType<HudController>().Cluster.Find("SkillSlots");
            Image cooldown = slots.GetChild(0).Find("Cooldown").GetComponent<Image>();
            Assert.AreEqual(Image.Type.Filled, cooldown.type);
            Assert.AreEqual(Image.FillMethod.Radial360, cooldown.fillMethod);
            Assert.That(cooldown.fillAmount, Is.InRange(0.001f, 0.999f));
        }

        [UnityTest]
        public IEnumerator EscapeAndStart_TogglePauseAndRestoreInput()
        {
            var pause = Object.FindFirstObjectByType<PauseMenuUI>();
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            yield return Tap(Key.Escape);
            Assert.IsTrue(pause.IsOpen);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(input.enabled);
            yield return Tap(Key.Escape);
            Assert.IsFalse(pause.IsOpen);
            Assert.Greater(Time.timeScale, 0f);
            Assert.IsTrue(input.enabled);
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(GamepadButton.Start));
            yield return null;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            yield return null;
            Assert.IsTrue(pause.IsOpen);
            pause.Resume();
        }

        [UnityTest]
        public IEnumerator PauseCannotOpenOverRestMenu()
        {
            yield return Load("Run");
            var rest = Object.FindFirstObjectByType<RestChoiceUI>();
            rest.Show(0.4f, _ => { });
            yield return Tap(Key.Escape);
            Assert.IsFalse(Object.FindFirstObjectByType<PauseMenuUI>().IsOpen);
            Assert.IsTrue(rest.IsOpen);
            rest.Hide();
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }
    }
}
