using System;
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
    public class SkillTests
    {
        private Keyboard _keyboard;
        private InputSettings.BackgroundBehavior _previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorBehavior;
        private PlayerController _player;
        private PlayerSkills _skills;
        private SlimeEnemy[] _slimes;
        private Action<Health, DamageInfo> _damageListener;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousBackground = InputSystem.settings.backgroundBehavior;
            _previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("SkillTestKeyboard");
            SceneManager.LoadScene("Prototype");
            yield return new WaitForSecondsRealtime(0.3f);
            _player = Object.FindFirstObjectByType<PlayerController>();
            _skills = _player.GetComponent<PlayerSkills>();
            Assert.IsNotNull(_skills, "Rebuild Prototype with PrototypeSceneBuilder before running skill tests.");
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner != null) Object.Destroy(spawner.gameObject);
            _slimes = Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(_slimes.Length, 2);
            foreach (SlimeEnemy slime in _slimes)
            {
                slime.enabled = false;
                slime.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                slime.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                slime.gameObject.SetActive(false);
            }
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(_player.IsGrounded);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_damageListener != null) Health.AnyDamaged -= _damageListener;
            _damageListener = null;
            if (_keyboard != null)
            {
                Release();
                InputSystem.RemoveDevice(_keyboard);
            }
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
        public IEnumerator Prototype_HasFourSkills_AndRejectsDuplicateOrFullEquip()
        {
            Assert.AreEqual(4, _skills.Slots.Count);
            foreach (var skill in _skills.Slots)
            {
                Assert.IsNotNull(skill);
                Assert.IsTrue(_skills.HasSkill(skill));
                Assert.IsFalse(_skills.Equip(skill));
            }
            var extra = ScriptableObject.CreateInstance<Roglike.Player.Skills.DashSlashSkill>();
            try { Assert.IsFalse(_skills.Equip(extra)); }
            finally { Object.Destroy(extra); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DashSlash_MovesHits_AndCannotRecastOnCooldown()
        {
            Health target = PlaceSlime(0, 2f);
            double health = target.Current;
            float start = _player.transform.position.x;
            Press(Key.Q);
            yield return null;
            yield return null;
            Release();
            Assert.Greater(_skills.CooldownRemaining(0), 0f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.GreaterOrEqual((_player.transform.position.x - start) * _player.Facing, 3f);
            Assert.Less(target.Current, health);
            float cooldown = _skills.CooldownRemaining(0);
            float position = _player.transform.position.x;
            Press(Key.Q);
            yield return new WaitForSecondsRealtime(0.2f);
            Release();
            Assert.Less(_skills.CooldownRemaining(0), cooldown);
            Assert.That(_player.transform.position.x, Is.EqualTo(position).Within(0.3f));
        }

        [UnityTest]
        public IEnumerator Whirlwind_HitsNearbySlimeAtLeastThreeTimes()
        {
            Health target = PlaceSlime(0, 1.3f);
            int hits = 0;
            _damageListener = (health, info) => { if (health == target) hits++; };
            Health.AnyDamaged += _damageListener;
            Press(Key.W);
            yield return new WaitForSecondsRealtime(1f);
            Release();
            Assert.GreaterOrEqual(hits, 3);
        }

        [UnityTest]
        public IEnumerator SwordWave_PiercesTwoSlimes()
        {
            Health first = PlaceSlime(0, 3f);
            Health second = PlaceSlime(1, 5f);
            double firstHealth = first.Current;
            double secondHealth = second.Current;
            Press(Key.E);
            yield return new WaitForSecondsRealtime(0.8f);
            Release();
            Assert.Less(first.Current, firstHealth);
            Assert.Less(second.Current, secondHealth);
        }

        [UnityTest]
        public IEnumerator Slam_HopsLands_AndHitsNearbySlime()
        {
            Health target = PlaceSlime(0, 1.8f);
            double health = target.Current;
            float initialY = _player.transform.position.y;
            Press(Key.R);
            yield return new WaitForSecondsRealtime(0.08f);
            Release();
            Assert.Greater(_player.transform.position.y, initialY);
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(_player.IsGrounded);
            Assert.Less(target.Current, health);
        }

        [UnityTest]
        public IEnumerator Overdrive_CooldownUsesPlayerTime_NotSlowedWorldTime()
        {
            Press(Key.F);
            yield return new WaitForSecondsRealtime(0.3f);
            Release();
            Assert.IsTrue(_player.GetComponent<OverdriveController>().IsActive);
            yield return null;
            Press(Key.Q);
            yield return new WaitForSecondsRealtime(0.4f);
            Release();
            Assert.IsFalse(_skills.IsReady(0));
            float cooldown = _skills.CooldownRemaining(0);
            float gameTime = Time.time;
            float realTime = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(0.8f);
            float recovered = cooldown - _skills.CooldownRemaining(0);
            Assert.That(recovered / (Time.time - gameTime), Is.InRange(3.5f, 4.5f));
            Assert.That(recovered / (Time.unscaledTime - realTime), Is.InRange(0.8f, 1.2f));
            yield return new WaitForSecondsRealtime(_skills.CooldownRemaining(0) + 0.2f);
            Assert.IsTrue(_skills.IsReady(0));
        }

        [UnityTest]
        public IEnumerator Hurt_CancelsWhirlwind_AndPreservesHurtMotion()
        {
            Press(Key.W);
            yield return new WaitForSecondsRealtime(0.15f);
            Release();
            Assert.IsTrue(_player.IsInSkill);
            Health health = _player.GetComponent<Health>();
            health.ClearInvulnerable();
            Assert.IsTrue(health.TryApplyDamage(new DamageInfo(1, false, _player.transform.position,
                Vector2.left * 5f, 0.5f, null)));
            yield return null;
            Assert.IsTrue(_player.IsHurt);
            Assert.IsFalse(_player.IsInSkill);
            Assert.IsFalse(_player.CanUseSkill);
            Assert.Less(_player.GetComponent<Rigidbody2D>().linearVelocity.x, 0f);
        }

        private Health PlaceSlime(int index, float distance)
        {
            SlimeEnemy slime = _slimes[index];
            Vector2 position = (Vector2)_player.transform.position + new Vector2(distance * _player.Facing, -0.35f);
            slime.transform.position = position;
            slime.GetComponent<Rigidbody2D>().position = position;
            Health health = slime.GetComponent<Health>();
            health.Configure(Team.Enemy, 1000);
            slime.gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            return health;
        }

        private void Press(Key key) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
        private void Release() => InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
    }
}
