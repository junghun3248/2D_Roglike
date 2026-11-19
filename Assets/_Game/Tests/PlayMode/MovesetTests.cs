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
        public IEnumerator GroundSwings_UseCrescentAndAlternateVerticalFlip()
        {
            Assert.IsNotNull(VfxSprites.SlashAnimation, "Rebuild scenes with the VFX builder first.");
            Press(Key.X);
            SlashEffect first = null;
            float deadline = Time.realtimeSinceStartup + 1f;
            while (first == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                first = Object.FindFirstObjectByType<SlashEffect>();
            }
            Assert.IsNotNull(first);
            var renderer = first.GetComponent<SpriteRenderer>();
            CollectionAssert.Contains(VfxSprites.SlashAnimation.frames, renderer.sprite);
            Assert.AreNotSame(VfxSprites.Square, renderer.sprite);
            Assert.IsFalse(renderer.flipY);
            Assert.AreEqual(_player.Facing < 0f, renderer.flipX);
            SlashEffect second = null;
            deadline = Time.realtimeSinceStartup + 1f;
            while (second == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                var candidate = Object.FindFirstObjectByType<SlashEffect>();
                if (candidate != null && candidate != first) second = candidate;
            }
            Release();
            Assert.IsNotNull(second);
            renderer = second.GetComponent<SpriteRenderer>();
            CollectionAssert.Contains(VfxSprites.SlashAnimation.frames, renderer.sprite);
            Assert.IsTrue(renderer.flipY, "The alternating upward swing must mirror the crescent vertically.");
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
            Assert.That(airborneSwings, Is.InRange(2, 3));
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

        [UnityTest]
        public IEnumerator AirWhiffs_LandWithinNormalJumpTime()
        {
            Press(Key.Z);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsFalse(_player.IsGrounded);
            Press(Key.Z, Key.X);
            float deadline = Time.unscaledTime + 0.8f;
            while (!_player.IsGrounded && Time.unscaledTime < deadline) yield return null;
            Assert.IsTrue(_player.IsGrounded, "Repeated whiffs must not extend a normal jump.");
        }

        [UnityTest]
        public IEnumerator AirSwings_AllowHorizontalSteering()
        {
            Press(Key.Z);
            yield return new WaitForSecondsRealtime(0.12f);
            float startX = _player.transform.position.x;
            Press(Key.Z, Key.RightArrow, Key.X);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(_player.IsGrounded);
            Assert.Greater(_player.transform.position.x - startX, 0.8f);
        }

        [UnityTest]
        public IEnumerator GroundHit_RequestsOneSoundAndHitStopForTargetGroup()
        {
            PlaceSlime(0, new Vector2(1.2f, -0.35f));
            PlaceSlime(1, new Vector2(1.4f, -0.35f));
            Assert.IsNotNull(SfxPlayer.Instance);
            int sounds = 0;
            Action<string> listener = id => { if (id == "hit") sounds++; };
            SfxPlayer.Instance.SoundRequested += listener;
            bool stopped = false;
            try
            {
                Press(Key.X);
                yield return null;
                yield return null;
                Release();
                float deadline = Time.unscaledTime + 0.4f;
                while (Time.unscaledTime < deadline)
                {
                    stopped |= TimeController.Instance.IsHitStopped;
                    yield return null;
                }
                Assert.IsTrue(stopped);
                Assert.AreEqual(1, sounds);
            }
            finally { SfxPlayer.Instance.SoundRequested -= listener; }
        }

        [UnityTest]
        public IEnumerator Whiff_OnlyRequestsSwingSoundWithoutHitStop()
        {
            Assert.IsNotNull(SfxPlayer.Instance);
            int swings = 0;
            int hits = 0;
            Action<string> listener = id =>
            {
                if (id == "swing") swings++;
                if (id == "hit" || id == "hit_crit" || id == "kill") hits++;
            };
            SfxPlayer.Instance.SoundRequested += listener;
            try
            {
                Press(Key.X);
                yield return null;
                yield return null;
                Release();
                float deadline = Time.unscaledTime + 0.4f;
                while (Time.unscaledTime < deadline)
                {
                    Assert.IsFalse(TimeController.Instance.IsHitStopped);
                    yield return null;
                }
                Assert.AreEqual(1, swings);
                Assert.AreEqual(0, hits);
            }
            finally { SfxPlayer.Instance.SoundRequested -= listener; }
        }

        [UnityTest]
        public IEnumerator HoldingX_CameraRemainsWithinPointSixOfAverage()
        {
            Health target = PlaceSlime(0, new Vector2(1.3f, -0.35f));
            Listen(target);
            yield return new WaitForSecondsRealtime(0.5f);
            var samples = new System.Collections.Generic.List<float>();
            Press(Key.X);
            float deadline = Time.unscaledTime + 1.2f;
            while (Time.unscaledTime < deadline)
            {
                yield return null;
                samples.Add(GameCamera.Instance.transform.position.x);
            }
            Release();
            Assert.GreaterOrEqual(_hitCount, 2, "Must exercise repeated recoil and hit impulses.");
            float average = 0f;
            foreach (float x in samples) average += x;
            average /= samples.Count;
            foreach (float x in samples)
                Assert.That(Mathf.Abs(x - average), Is.LessThanOrEqualTo(0.6f));
        }

        [UnityTest]
        public IEnumerator PunchZoom_ReturnsWithinPointFifteenSeconds()
        {
            var camera = GameCamera.Instance.GetComponent<Camera>();
            float original = camera.orthographicSize;
            GameCamera.Instance.PunchZoom(_player.transform.position, 0.94f, 0.08f);
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.Less(camera.orthographicSize, original);
            yield return new WaitForSecondsRealtime(0.18f);
            Assert.That(camera.orthographicSize, Is.EqualTo(original).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator HitFlash_FadesDuringHitStop_AndRestoresTintPriority()
        {
            var target = new GameObject("FlashCurveTest");
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.color = Color.gray;
            var health = target.AddComponent<Health>();
            var flash = target.AddComponent<HitFlash>();
            try
            {
                flash.SetStatusTint(Color.green, 0.5f);
                flash.SetBaseColor(renderer, Color.red);
                var damage = new DamageInfo(1, false, Vector2.zero, Vector2.zero, 0f, null);
                health.TryApplyDamage(damage);
                Assert.That(renderer.color.g, Is.EqualTo(0.85f).Within(0.001f));
                TimeController.Instance.HitStop(0.3f);
                yield return new WaitForSecondsRealtime(0.055f);
                Assert.That(renderer.color.g, Is.InRange(0.01f, 0.7f));
                health.TryApplyDamage(damage);
                Assert.That(renderer.color.g, Is.EqualTo(0.85f).Within(0.001f));
                yield return new WaitForSecondsRealtime(0.11f);
                Assert.IsTrue(TimeController.Instance.IsHitStopped);
                Assert.AreEqual(Color.red, renderer.color);
                flash.SetBaseColor(renderer, Color.gray);
                Assert.AreEqual(Color.Lerp(Color.gray, Color.green, 0.5f), renderer.color);
            }
            finally { Object.Destroy(target); }
        }

        [UnityTest]
        public IEnumerator HitFlash_ShaderFadesDuringHitStop_AndRestoresTelegraph()
        {
            var target = new GameObject("ShaderFlashCurveTest");
            var renderer = target.AddComponent<SpriteRenderer>();
            var material = new Material(Shader.Find("Roglike/SpriteFlash"));
            renderer.sharedMaterial = material;
            var health = target.AddComponent<Health>();
            var flash = target.AddComponent<HitFlash>();
            var block = new MaterialPropertyBlock();
            try
            {
                flash.SetStatusTint(Color.green, 0.5f);
                flash.SetBaseColor(renderer, Color.red);
                renderer.GetPropertyBlock(block);
                float telegraphAmount = block.GetFloat("_FlashAmount");
                health.TryApplyDamage(new DamageInfo(1, false, Vector2.zero, Vector2.zero, 0f, null));
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_FlashAmount"), Is.EqualTo(0.85f).Within(0.001f));
                TimeController.Instance.HitStop(0.3f);
                yield return new WaitForSecondsRealtime(0.055f);
                renderer.GetPropertyBlock(block);
                Assert.Less(block.GetFloat("_FlashAmount"), 0.85f);
                yield return new WaitForSecondsRealtime(0.06f);
                renderer.GetPropertyBlock(block);
                Assert.IsTrue(TimeController.Instance.IsHitStopped);
                Assert.AreEqual(Color.red, block.GetColor("_FlashColor"));
                Assert.That(block.GetFloat("_FlashAmount"), Is.EqualTo(telegraphAmount).Within(0.001f));
            }
            finally
            {
                Object.Destroy(target);
                Object.Destroy(material);
            }
        }

        [UnityTest]
        public IEnumerator Camera_SmallReverseStepsDoNotFlip_ButSustainedTravelDoes()
        {
            var target = new GameObject("CameraFollowTest");
            GameCamera camera = GameCamera.Instance;
            camera.SetTarget(target.transform);
            try
            {
                for (int step = 0; step < 6; step++)
                {
                    target.transform.position = new Vector3(step % 2 == 0 ? -0.4f : 0f, 0f, 0f);
                    yield return null;
                    yield return null;
                    Assert.AreEqual(1f, ReadField(camera, "_lookDirection"));
                }
                float deadline = Time.unscaledTime + 0.3f;
                while (Time.unscaledTime < deadline)
                {
                    target.transform.position += Vector3.left * (4f * Time.unscaledDeltaTime);
                    yield return null;
                }
                Assert.AreEqual(-1f, ReadField(camera, "_lookDirection"));
            }
            finally
            {
                camera.SetTarget(_player.transform);
                Object.Destroy(target);
            }
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
