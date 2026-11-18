using System.Collections;
using System.Linq;
using NUnit.Framework;
using Roglike.Combat;
using Roglike.Enemies;
using Roglike.Player;
using Roglike.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Roglike.Tests
{
    /// <summary>1지역 적·보스·문 그림이 동작(상태)에 맞게 바뀌는지.</summary>
    public class EnemyArtTests
    {
        [UnityTest]
        public IEnumerator Boar_ShowsTelegraphThenRunFrames()
        {
            yield return LoadPrototypeWithout();
            BoarEnemy boar = Spawn<BoarEnemy>("ClockworkBoar", 6f);
            SpriteRenderer art = ArtOf(boar);
            StringAssert.StartsWith("boar_", art.sprite.name, "멧돼지 그림이 연결되지 않았습니다");

            yield return Until(() => boar.AnimationState == "Telegraph", 4f);
            yield return null;
            StringAssert.StartsWith("boar_telegraph", art.sprite.name, "돌진 예고 그림이어야 합니다");

            yield return Until(() => boar.AnimationState == "Charge", 2f);
            yield return null;
            StringAssert.StartsWith("boar_run", art.sprite.name, "돌진 중엔 달리기 그림이어야 합니다");
        }

        [UnityTest]
        public IEnumerator Squirrel_ShowsWindupAndThrowFrames()
        {
            yield return LoadPrototypeWithout();
            SquirrelEnemy squirrel = Spawn<SquirrelEnemy>("GearSquirrel", 6.5f);
            SpriteRenderer art = ArtOf(squirrel);

            yield return Until(() => squirrel.AnimationState == "Windup", 5f);
            yield return null;
            StringAssert.StartsWith("squirrel_windup", art.sprite.name);

            yield return Until(() => squirrel.AnimationState == "Throw", 2f);
            yield return null;
            StringAssert.StartsWith("squirrel_throw", art.sprite.name);
            Assert.IsNotNull(Object.FindFirstObjectByType<GearProjectile>(), "톱니 투사체가 없습니다");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator StagKing_ShowsPatternPoses()
        {
            yield return LoadPrototypeWithout();
            StagKingBoss stag = Spawn<StagKingBoss>("StagKing", 7f);
            SpriteRenderer art = ArtOf(stag);
            StringAssert.StartsWith("stag_", art.sprite.name, "사슴왕 그림이 연결되지 않았습니다");

            // 패턴이 랜덤이라 낙엽 폭풍(앞발 들기)이 나올 때까지 기다린다.
            yield return Until(() => stag.AnimationState == "Rear", 30f);
            yield return null;
            StringAssert.StartsWith("stag_rear", art.sprite.name, "낙엽 폭풍 예고엔 앞발 든 그림이어야 합니다");
        }

        [UnityTest]
        public IEnumerator RoomGate_UsesClosedThenOpenArt()
        {
            SceneManager.LoadScene("Run");
            yield return new WaitForSecondsRealtime(1f);
            Room[] rooms = Object.FindObjectsByType<Room>(FindObjectsSortMode.None).OrderBy(r => r.MinX).ToArray();
            Assert.Greater(rooms.Length, 1);
            StringAssert.StartsWith("gate_open", rooms[0].RightGateSprite.name, "입구 방의 오른쪽 문은 열려 있어야 합니다");
            StringAssert.StartsWith("gate_closed", rooms[1].RightGateSprite.name, "전투 방의 오른쪽 문은 닫혀 있어야 합니다");

            rooms[1].SetRightOpen(true);
            StringAssert.StartsWith("gate_open", rooms[1].RightGateSprite.name, "문이 열리면 열린 문 그림이어야 합니다");
        }

        private static IEnumerator LoadPrototypeWithout()
        {
            SceneManager.LoadScene("Prototype");
            yield return new WaitForSecondsRealtime(0.5f);
            Object.Destroy(Object.FindFirstObjectByType<EnemySpawner>().gameObject);
            foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None).Where(h => h.Team == Team.Enemy))
            {
                Object.Destroy(health.gameObject);
            }

            Object.FindFirstObjectByType<PlayerController>().GetComponent<Health>().SetInvulnerable(10000f);
            yield return null;
        }

        private static T Spawn<T>(string prefabName, float distance) where T : Component
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Enemies/{prefabName}.prefab");
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 position = player.transform.position + new Vector3(distance, 0.8f, 0f);
            return Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<T>();
#else
            Assert.Ignore("에디터에서만 실행");
            return null;
#endif
        }

        private static SpriteRenderer ArtOf(Component enemy)
        {
            Transform art = enemy.transform.Find("Visual").GetComponentsInChildren<Transform>().First(t => t.name == "Art");
            return art.GetComponent<SpriteRenderer>();
        }

        private static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(condition(), "제한 시간 안에 상태에 도달하지 못했습니다");
        }
    }
}
