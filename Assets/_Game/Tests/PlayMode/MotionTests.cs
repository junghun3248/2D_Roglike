using System.Collections;
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
using Object = UnityEngine.Object;

namespace Roglike.Tests
{
    public class MotionTests
    {
        private Keyboard _keyboard;
        private InputSettings.BackgroundBehavior _previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorBehavior;
        private PlayerController _player;
        private PlayerVisualFeel _feel;
        private SpriteRenderer _sprite;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousBackground = InputSystem.settings.backgroundBehavior;
            _previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("MotionTestKeyboard");
            Time.timeScale = 1f;
            SceneManager.LoadScene("Prototype");
            yield return null;
            foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
            {
                spawner.gameObject.SetActive(false);
                Object.Destroy(spawner.gameObject);
            }
            foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team != Team.Enemy) continue;
                health.gameObject.SetActive(false);
                Object.Destroy(health.gameObject);
            }
            _player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(_player);
            _feel = _player.GetComponentInChildren<PlayerVisualFeel>();
            Assert.IsNotNull(_feel, "Rebuild Prototype so ArtSetup.ApplyToPlayer installs VisualFeel.");
            _sprite = _feel.GetComponentInChildren<SpriteRenderer>();
            Assert.IsNotNull(_sprite);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(_player.IsGrounded);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.backgroundBehavior = _previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorBehavior;
            if (TimeController.Instance != null)
            {
                TimeController.Instance.SetPaused(false);
                TimeController.Instance.SetOverdriveScale(1f);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Idle_RenderedTopStaysStable()
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                float top = _sprite.bounds.max.y;
                min = Mathf.Min(min, top);
                max = Mathf.Max(max, top);
            }
            Assert.Less(max - min, 0.08f);
        }

        [UnityTest]
        public IEnumerator RunRight_LeansForward()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.RightArrow));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Greater(_player.GetComponent<Rigidbody2D>().linearVelocity.x, 0.5f);
            float angle = Mathf.DeltaAngle(0f, _feel.transform.localEulerAngles.z);
            Assert.Less(angle, -1f, "Rightward lean should rotate clockwise around the feet.");
        }

        [UnityTest]
        public IEnumerator JumpLanding_SquashesThenRecovers()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Z));
            float deadline = Time.realtimeSinceStartup + 1f;
            while (_player.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_player.IsGrounded, "Z should start a jump.");
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            deadline = Time.realtimeSinceStartup + 3f;
            while (!_player.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_player.IsGrounded, "Jump should land before timeout.");
            float landedAt = Time.realtimeSinceStartup;
            bool squashed = false;
            while (Time.realtimeSinceStartup - landedAt < 0.1f)
            {
                yield return null;
                squashed |= _feel.transform.localScale.y < 0.97f;
            }
            Assert.IsTrue(squashed, "Landing should squash within 0.1 seconds.");
            while (Time.realtimeSinceStartup - landedAt < 0.3f) yield return null;
            Assert.That(_feel.transform.localScale.y, Is.EqualTo(1f).Within(0.02f));
        }
    }
}
