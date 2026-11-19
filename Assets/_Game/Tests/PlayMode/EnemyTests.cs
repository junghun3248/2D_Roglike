using System;
using System.Collections;
using NUnit.Framework;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Enemies;
using Roglike.Player;
using Roglike.Player.Skills;
using Roglike.Progression;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roglike.Tests
{
    public class EnemyTests
    {
        private GameObject _fixture;
        private Health _player;
        private const float FloorY = 100f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Prototype");
            yield return null;
            foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
            {
                spawner.enabled = false;
                Object.Destroy(spawner);
            }
            foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team == Team.Enemy) Object.Destroy(health.gameObject);
                else if (health.Team == Team.Player) _player = health;
            }
            yield return null;
            Assert.IsNotNull(_player);
            foreach (MonoBehaviour component in _player.GetComponents<MonoBehaviour>())
                if (component != _player) component.enabled = false;
            _player.SetInvulnerable(1000f);
            Rigidbody2D playerBody = _player.GetComponent<Rigidbody2D>();
            playerBody.linearVelocity = Vector2.zero;
            playerBody.bodyType = RigidbodyType2D.Kinematic;
            _fixture = new GameObject("EnemyTestFixture");
            Ground("Floor", new Vector2(0f, FloorY - 0.5f), new Vector2(80f, 1f));
            PlacePlayer(6f);
            Physics2D.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_fixture != null) Object.Destroy(_fixture);
            foreach (GearProjectile projectile in Object.FindObjectsByType<GearProjectile>(FindObjectsSortMode.None))
                Object.Destroy(projectile.gameObject);
            foreach (FallingLeafProjectile projectile in Object.FindObjectsByType<FallingLeafProjectile>(FindObjectsSortMode.None))
                Object.Destroy(projectile.gameObject);
            // 씬의 TimeController도 제거하여 사망 슬로모션이 다음 테스트에 남지 않게 한다.
            Scene empty = SceneManager.CreateScene("EnemyTestsCleanup");
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(previous);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Boar_ChargesAtPlayerWithinTwoSeconds()
        {
            BoarEnemy boar = Spawn<BoarEnemy>("ClockworkBoar", 0f);
            float start = boar.transform.position.x;
            yield return Until(() => boar.transform.position.x - start >= 3f, 2f);
            Assert.GreaterOrEqual(boar.transform.position.x - start, 3f);
        }

        [UnityTest]
        public IEnumerator Boar_WallImpactStunsAndStopsCharge()
        {
            Ground("Wall", new Vector2(3f, FloorY + 2f), new Vector2(0.5f, 4f));
            BoarEnemy boar = Spawn<BoarEnemy>("ClockworkBoar", 0f);
            yield return Until(() => boar.IsStunned, 2f);
            Assert.IsTrue(boar.IsStunned);
            Assert.That(boar.GetComponent<Rigidbody2D>().linearVelocity.x, Is.EqualTo(0f).Within(0.01f));
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(boar.IsStunned, "충돌 직후 취약 시간이 유지되어야 한다.");
        }

        [UnityTest]
        public IEnumerator Squirrel_RetreatsAndThrowsGear()
        {
            PlacePlayer(3f);
            SquirrelEnemy squirrel = Spawn<SquirrelEnemy>("GearSquirrel", 0f);
            bool sawProjectile = false;
            yield return Until(() =>
            {
                sawProjectile |= Object.FindFirstObjectByType<GearProjectile>() != null;
                return sawProjectile && squirrel.transform.position.x < -0.5f;
            }, 3f);
            Assert.Less(squirrel.transform.position.x, -0.5f);
            Assert.IsTrue(sawProjectile);
        }

        [UnityTest]
        public IEnumerator StagKing_StartsPatternEnragesAndRaisesDefeatOnce()
        {
            StagKingBoss boss = Spawn<StagKingBoss>("StagKing", 0f);
            Assert.IsInstanceOf<IBoss>(boss);
            Assert.AreEqual("태엽 사슴왕", boss.DisplayName);
            yield return Until(() => !string.IsNullOrEmpty(boss.CurrentPattern), 1.5f);
            Assert.IsNotEmpty(boss.CurrentPattern);
            Health health = boss.GetComponent<Health>();
            Assert.AreEqual(6000d, health.Max);
            health.TryApplyDamage(Damage(health.Max * 0.5));
            Assert.IsTrue(boss.IsEnraged);
            int defeats = 0;
            IBoss contract = boss;
            contract.BossDefeated += defeated => { Assert.AreSame(boss, defeated); defeats++; };
            Assert.IsTrue(health.TryApplyDamage(Damage(health.Max)));
            Assert.IsFalse(health.TryApplyDamage(Damage(health.Max)));
            Assert.AreEqual(1, defeats);
            yield return null;
            Assert.AreEqual(1, defeats);
        }

        [UnityTest]
        public IEnumerator Elite_MultipliesAlreadyScaledMaxOnlyOnce()
        {
            BoarEnemy boar = Spawn<BoarEnemy>("ClockworkBoar", 0f);
            Health health = boar.GetComponent<Health>();
            Assert.AreEqual(160d, health.Max);
            health.Configure(health.Team, health.Max * 3d);
            double before = health.Max;
            EliteModifier.Apply(boar.gameObject);
            Assert.AreEqual(before * 2.5d, health.Max);
            Assert.AreEqual(health.Max, health.Current);
            Assert.AreEqual(Team.Enemy, health.Team);
            Assert.IsTrue(boar.GetComponent<EliteModifier>().IsElite);
            EliteModifier.Apply(boar.gameObject);
            Assert.AreEqual(before * 2.5d, health.Max);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EliteBoar_TelegraphRemainsVisibleDuringWindUp()
        {
            BoarEnemy boar = Spawn<BoarEnemy>("ClockworkBoar", 0f);
            EliteModifier.Apply(boar.gameObject);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var state = typeof(BoarEnemy).GetField("_state", flags);
            Color telegraph = (Color)typeof(BoarEnemy).GetField("_telegraphColor", flags).GetValue(boar);
            var renderer = boar.GetComponentInChildren<SpriteRenderer>();
            yield return Until(() => state.GetValue(boar).ToString() == "Telegraph", 2f);
            Assert.AreEqual("Telegraph", state.GetValue(boar).ToString());
            // Sample the previous frame after EliteModifier.LateUpdate has also run.
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Assert.AreEqual("Telegraph", state.GetValue(boar).ToString());
                if (renderer.sharedMaterial.HasProperty("_FlashAmount"))
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    AssertColor(telegraph, block.GetColor("_FlashColor"));
                    Assert.Greater(block.GetFloat("_FlashAmount"), 0f);
                }
                else
                    AssertColor(telegraph, renderer.color);
            }
        }

        // 셰이더 프로퍼티를 거치면 부동소수 오차가 생기므로 허용 오차로 비교한다.
        [UnityTest]
        public IEnumerator StasisField_FreezesBoarForOneSecond_AndAddsThirtyPercentDamage()
        {
            PlacePlayer(0f);
            BoarEnemy boar = Spawn<BoarEnemy>("ClockworkBoar", 3f);
            Health target = boar.GetComponent<Health>();
            target.Configure(Team.Enemy, 1000);
            var resolver = _player.GetComponent<PlayerHitResolver>();
            _player.GetComponent<PlayerStats>().AddModifiers(new[]
            {
                new StatModifier(StatType.CritChance, ModifierKind.Multiply, 0f),
            });
            typeof(PlayerHitResolver).GetField("_damageVariance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resolver, 0f);
            var spec = new HitSpec(1f, Vector2.zero, 0f, 0f, 0f, 0f);
            bool feedback = false;
            double before = target.Current;
            Assert.IsTrue(resolver.TryHit(target, target.GetComponent<Collider2D>(), _player.transform.position, spec, ref feedback));
            double normalDamage = before - target.Current;
            StasisFieldSkill skill = ScriptableObject.CreateInstance<StasisFieldSkill>();
            try
            {
                yield return skill.Execute(new SkillContext(_player.GetComponent<PlayerController>(), resolver));
                StatusEffects status = target.GetComponent<StatusEffects>();
                Assert.IsTrue(status.IsFrozen);
                float frozenX = boar.transform.position.x;
                before = target.Current;
                Assert.IsTrue(resolver.TryHit(target, target.GetComponent<Collider2D>(), _player.transform.position, spec, ref feedback));
                Assert.That(before - target.Current, Is.EqualTo(normalDamage * 1.3).Within(0.001));
                float until = Time.time + 1f;
                while (Time.time < until)
                {
                    Assert.IsTrue(status.IsFrozen);
                    // 얼기 직전 물리 정착으로 0.01 정도 밀릴 수 있어 0.05까지 허용 (멈춘 적은 초당 수 칸씩 움직이지 않는다).
                    Assert.That(boar.transform.position.x, Is.EqualTo(frozenX).Within(0.05f));
                    yield return null;
                }
                yield return new WaitForSeconds(0.6f);
                Assert.IsFalse(status.IsFrozen);
                Assert.That(status.DamageTakenMultiplier, Is.EqualTo(1f));
            }
            finally { Object.Destroy(skill); }
        }

        [UnityTest]
        public IEnumerator Stasis_BossThawsAfterHalfSecond()
        {
            StagKingBoss boss = Spawn<StagKingBoss>("StagKing", 15f);
            StatusEffects status = boss.GetComponent<StatusEffects>();
            status.Freeze(1.5f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(status.IsFrozen);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(status.IsFrozen);
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f));
        }

        private static IEnumerator Until(Func<bool> predicate, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private T Spawn<T>(string name, float x) where T : Component
        {
#if UNITY_EDITOR
            string path = $"Assets/_Game/Prefabs/Enemies/{name}.prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"프리팹 빌드가 먼저 필요합니다: EnemyPrefabBuilder.Ensure* — {path}");
            GameObject enemy = Object.Instantiate(prefab, _fixture.transform);
            BoxCollider2D collider = enemy.GetComponent<BoxCollider2D>();
            enemy.transform.position = new Vector3(x, FloorY + collider.size.y * 0.5f + 0.02f, 0f);
            Physics2D.SyncTransforms();
            return enemy.GetComponent<T>();
#else
            Assert.Ignore("에디터 AssetDatabase로 생성 프리팹을 읽는 PlayMode 테스트입니다.");
            return null;
#endif
        }

        private void PlacePlayer(float x)
        {
            _player.transform.position = new Vector3(x, FloorY + 1f, 0f);
            Physics2D.SyncTransforms();
        }

        private void Ground(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name) { layer = GameLayers.Ground };
            go.transform.SetParent(_fixture.transform);
            go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        private DamageInfo Damage(double amount) => new DamageInfo(amount, false, Vector2.zero,
            Vector2.zero, 0f, _player.gameObject);
    }
}
