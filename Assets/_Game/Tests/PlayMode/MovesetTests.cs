using System;
using System.Collections;
using System.Reflection;
using Roglike.Progression;
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
    public class MovesetTests
    {
        private Keyboard _keyboard;
        private InputSettings.BackgroundBehavior _previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorBehavior;
        private PlayerController _player;
        private double _lastDamage;
        private int _hitCount;
        private SlimeEnemy[] _slimes;
        private Action<Health, DamageInfo> _damageListener;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousBackground = InputSystem.settings.backgroundBehavior;
            _previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("MovesetTestKeyboard");
            SceneManager.LoadScene("Prototype");
            yield return new WaitForSecondsRealtime(0.3f);
            _player = Object.FindFirstObjectByType<PlayerController>();
            var combat = _player.GetComponent<PlayerCombat>();
            Assert.IsNotNull(ReadField(combat, "_chargedStrike"), "Rebuild both scenes with PrototypeSceneBuilder.BuildFromCommandLine first.");
            _player.GetComponent<PlayerStats>().AddModifiers(new[]
            {
                new StatModifier(StatType.CritChance, ModifierKind.Multiply, 0f),
            });
            SetField(_player.GetComponent<PlayerHitResolver>(), "_damageVariance", 0f);
            _lastDamage = 0;
            _hitCount = 0;
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner != null) Object.Destroy(spawner.gameObject);
            _slimes = CombatTestTargets.ReplaceSceneEnemies(2);
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
                TimeController.Instance.SetFocusScale(1f);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldingX_RepeatsSameSlashWhileAirborne()
        {
            int airborneSwings = 0;
            _player.GetComponent<PlayerCombat>().AttackStarted += duration =>
            {
                if (_player.IsGrounded) return;
                Assert.That(duration, Is.EqualTo(0.27f).Within(0.01f));
                airborneSwings++;
            };
            Press(Key.Z);
            yield return new WaitForSecondsRealtime(0.12f);
            Press(Key.Z, Key.X);
            yield return new WaitForSecondsRealtime(0.6f);
            Release();
            Assert.GreaterOrEqual(airborneSwings, 2);
        }

        [UnityTest]
        public IEnumerator DashAttack_PiercesAndRetainsForwardMomentum()
        {
            Health target = PlaceSlime(0, new Vector2(3f, -0.35f));
            Health second = PlaceSlime(1, new Vector2(4.5f, -0.35f));
            double hp = target.Current;
            double secondHp = second.Current;
            float start = _player.transform.position.x;
            Press(Key.C);
            yield return new WaitForSecondsRealtime(0.05f);
            Press(Key.X);
            yield return null;
            yield return null;
            Release();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.Less(target.Current, hp);
            Assert.Less(second.Current, secondHp);
            Assert.GreaterOrEqual((_player.transform.position.x - start) * _player.Facing, 3f);
        }

        [UnityTest]
        public IEnumerator Launcher_LiftsSlimeAtLeastOneAndAHalfUnits()
        {
            Health target = PlaceSlime(0, new Vector2(1.2f, -0.35f));
            SlimeEnemy slime = target.GetComponent<SlimeEnemy>();
            SetField(slime, "_detectRange", 0f);
            SetField(slime, "_patrolSpeed", 0f);
            slime.enabled = true;
            target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            float startY = target.transform.position.y;
            float highest = startY;
            Press(Key.UpArrow, Key.X);
            yield return null;
            yield return null;
            Release();
            float until = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < until)
            {
                highest = Mathf.Max(highest, target.transform.position.y);
                yield return null;
            }
            Assert.GreaterOrEqual(highest - startY, 1.5f);
        }

        [UnityTest]
        public IEnumerator Plunge_LandsAndHitsNearbyTargetWithinOneSecond()
        {
            Health target = PlaceSlime(0, new Vector2(1.1f, -0.35f));
            double hp = target.Current;
            Press(Key.Z);
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.IsFalse(_player.IsGrounded);
            Press(Key.DownArrow, Key.X);
            yield return null;
            yield return null;
            Release();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsTrue(_player.IsGrounded);
            Assert.Less(target.Current, hp);
        }

        [UnityTest]
        public IEnumerator HeavyTap_DealsMoreDamageThanOneLightHit()
        {
            Health lightTarget = PlaceSlime(0, new Vector2(1.2f, -0.35f));
            Listen(lightTarget);
            Press(Key.X);
            yield return null;
            yield return null;
            Release();
            yield return new WaitForSecondsRealtime(0.55f);
            Assert.AreEqual(1, _hitCount);
            double light = _lastDamage;
            lightTarget.gameObject.SetActive(false);
            Health heavyTarget = PlaceSlime(1, new Vector2(1.2f, -0.35f));
            Listen(heavyTarget);
            yield return TapHeavy();
            Assert.AreEqual(1, _hitCount);
            Assert.Greater(_lastDamage, light);
        }

        [UnityTest]
        public IEnumerator ChargedStrike_OutdamagesHeavyAndKillsLowHealthTarget()
        {
            Health heavyTarget = PlaceSlime(0, new Vector2(1.2f, -0.35f));
            Listen(heavyTarget);
            yield return TapHeavy();
            double heavy = _lastDamage;
            Assert.Greater(heavy, 0);
            heavyTarget.gameObject.SetActive(false);
            Health target = PlaceSlime(1, new Vector2(5.2f, -0.35f));
            target.Configure(Team.Enemy, 1);
            Listen(target);
            bool killed = false;
            target.Died += info => killed = true;
            Press(Key.V);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(_player.IsCharging);
            Release();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.Greater(_lastDamage, heavy);
            Assert.IsTrue(killed);
            Assert.IsFalse(_player.IsCharging);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Heavy_HasNoCooldown_ThreeTapsProduceThreeAttacks()
        {
            Health target = PlaceSlime(0, new Vector2(1.2f, -0.35f));
            Listen(target);
            for (int i = 0; i < 3; i++) yield return TapHeavy();
            Assert.AreEqual(3, _hitCount);
        }

        [UnityTest]
        public IEnumerator Hurt_CancelsChargeAndReleaseDoesNotAttack()
        {
            Health target = PlaceSlime(0, new Vector2(1.2f, -0.35f));
            Listen(target);
            Press(Key.V, Key.RightArrow);
            float x = _player.transform.position.x;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(_player.transform.position.x, Is.EqualTo(x).Within(0.2f));
            Health health = _player.GetComponent<Health>();
            health.ClearInvulnerable();
            Assert.IsTrue(health.TryApplyDamage(new DamageInfo(1, false, _player.transform.position, Vector2.zero, 0.3f, null)));
            yield return null;
            Release();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(_player.IsCharging);
            Assert.AreEqual(0, _hitCount);
        }

        [UnityTest]
        public IEnumerator HoldingX_RepeatsOneSlashAtSteadyRhythm()
        {
            var combat = _player.GetComponent<PlayerCombat>();
            var starts = new System.Collections.Generic.List<float>();
            var durations = new System.Collections.Generic.List<float>();
            combat.AttackStarted += duration => { starts.Add(Time.time); durations.Add(duration); };
            Press(Key.X);
            yield return new WaitForSecondsRealtime(1.05f);
            Release();
            Assert.GreaterOrEqual(starts.Count, 3);
            foreach (float duration in durations) Assert.That(duration, Is.EqualTo(0.27f).Within(0.01f));
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i] - starts[i - 1], Is.InRange(0.26f, 0.36f));
        }

        [UnityTest]
        public IEnumerator GroundDash_FourTimesWithinOneSecond_HasVulnerableTail()
        {
            float started = Time.unscaledTime;
            for (int i = 0; i < 4; i++)
            {
                Press(Key.C);
                yield return null;
                yield return null;
                Assert.IsTrue(_player.IsDashing, $"Dash {i + 1}");
                Release();
                yield return new WaitForSecondsRealtime(0.105f);
                Assert.IsFalse(_player.GetComponent<Health>().IsInvulnerable);
                while (_player.IsDashing) yield return null;
                if (i < 3) yield return new WaitForSecondsRealtime(0.065f);
            }
            Assert.Less(Time.unscaledTime - started, 1f);
        }

        [UnityTest]
        public IEnumerator AirCharge_SlowsWorld_ThenLungesAtLeastThreeUnits()
        {
            Press(Key.Z);
            yield return new WaitForSecondsRealtime(0.15f);
            Press(Key.V);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(_player.IsCharging);
            Assert.IsFalse(_player.IsGrounded);
            Assert.That(Time.timeScale, Is.GreaterThan(0f).And.LessThan(0.5f));
            float x = _player.transform.position.x;
            Release();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.GreaterOrEqual((_player.transform.position.x - x) * _player.Facing, 3f);
            Assert.IsFalse(_player.IsCharging);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Focus_OverdriveAndHitStopComposeWithoutDoubleSlow()
        {
            TimeController time = TimeController.Instance;
            time.SetFocusScale(0.4f);
            Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(0.001f));
            time.SetOverdriveScale(0.25f);
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(TimeController.PlayerTimeMultiplier, Is.EqualTo(4f));
            time.HitStop(0.05f);
            Assert.AreEqual(0f, Time.timeScale);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.001f));
            time.SetOverdriveScale(1f);
            Assert.That(Time.timeScale, Is.EqualTo(0.4f).Within(0.001f));
            time.SetFocusScale(1f);
        }

        [UnityTest]
        public IEnumerator GroundCamera_PrototypeAndRun_FrameFloorNearBottom()
        {
            foreach (string scene in new[] { "Prototype", "Run" })
            {
                SceneManager.LoadScene(scene);
                yield return new WaitForSecondsRealtime(0.1f);
                foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
                    Object.Destroy(spawner.gameObject);
                foreach (Health enemy in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                    if (enemy.Team == Team.Enemy) Object.Destroy(enemy.gameObject);
                _player = Object.FindFirstObjectByType<PlayerController>();
                yield return new WaitForSecondsRealtime(1f);
                Assert.IsTrue(_player.IsGrounded, scene);
                Bounds bounds = _player.GetComponent<BoxCollider2D>().bounds;
                RaycastHit2D floor = Physics2D.Raycast(new Vector2(bounds.center.x, bounds.min.y + 0.1f),
                    Vector2.down, 0.3f, GameLayers.GroundMask);
                Assert.IsNotNull(floor.collider, scene);
                Camera camera = GameCamera.Instance.GetComponent<Camera>();
                float y = camera.WorldToViewportPoint(floor.point).y;
                Assert.That(y, Is.InRange(0.18f, 0.28f), scene);
                if (scene == "Run")
                {
                    var room = Object.FindFirstObjectByType<Roglike.Run.RunManager>().CurrentRoom;
                    float halfWidth = camera.orthographicSize * camera.aspect;
                    Assert.GreaterOrEqual(camera.transform.position.x - halfWidth, room.MinX - 0.1f);
                    Assert.LessOrEqual(camera.transform.position.x + halfWidth, room.MaxX + 0.1f);
                }
            }
        }

        private IEnumerator TapHeavy()
        {
            Press(Key.V);
            yield return new WaitForSecondsRealtime(0.05f);
            Release();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private void Listen(Health target, bool airborneOnly = false)
        {
            if (_damageListener != null) Health.AnyDamaged -= _damageListener;
            _hitCount = 0;
            _lastDamage = 0;
            _damageListener = (health, info) =>
            {
                if (health != target || (airborneOnly && _player.IsGrounded)) return;
                _hitCount++;
                _lastDamage = info.Amount;
            };
            Health.AnyDamaged += _damageListener;
        }

        private Health PlaceSlime(int index, Vector2 offset)
        {
            SlimeEnemy slime = _slimes[index];
            offset.x *= _player.Facing;
            Vector2 position = (Vector2)_player.transform.position + offset;
            slime.transform.position = position;
            slime.GetComponent<Rigidbody2D>().position = position;
            Health health = slime.GetComponent<Health>();
            health.Configure(Team.Enemy, 1000);
            slime.gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            return health;
        }

        private static object ReadField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private void Press(params Key[] keys) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        private void Release() => InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
    }
}
