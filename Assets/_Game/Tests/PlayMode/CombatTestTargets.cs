using Roglike.Combat;
using Roglike.Core;
using Roglike.Enemies;
using UnityEngine;

namespace Roglike.Tests
{
    // 공격/스킬 검증은 훈련장 랜덤 적 풀과 독립된 동일 크기의 표적을 사용한다.
    internal static class CombatTestTargets
    {
        public static SlimeEnemy[] ReplaceSceneEnemies(int count)
        {
            foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team != Team.Enemy) continue;
                health.gameObject.SetActive(false);
                Object.Destroy(health.gameObject);
            }
            var targets = new SlimeEnemy[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("CombatTestTarget" + i) { layer = GameLayers.Enemy };
                go.transform.position = new Vector3(-25f, 2f, 0f);
                var body = go.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.freezeRotation = true;
                go.AddComponent<BoxCollider2D>().size = new Vector2(1f, 0.8f);
                go.AddComponent<Health>().Configure(Team.Enemy, 1000);
                targets[i] = go.AddComponent<SlimeEnemy>();
            }
            return targets;
        }
    }
}
