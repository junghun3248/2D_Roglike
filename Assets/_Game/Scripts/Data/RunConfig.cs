using System;
using UnityEngine;

namespace Roglike.Data
{
    [Serializable]
    public class WaveEntry
    {
        public GameObject enemyPrefab;
        public int count = 3;
    }

    [Serializable]
    public class RoomEntry
    {
        public string name = "구역";
        [Tooltip("순서대로 나온다. 한 웨이브를 다 잡으면 다음 웨이브")]
        public WaveEntry[] waves;
    }

    /// <summary>한 런의 구성: 일반 구역들 → 보스. 숫자 폭발 체감을 위해 적 체력은 구역마다 곱으로 늘어난다.</summary>
    [CreateAssetMenu(menuName = "Roglike/Run Config", fileName = "RunConfig")]
    public class RunConfig : ScriptableObject
    {
        public RoomEntry[] rooms;
        public GameObject bossPrefab;

        [Header("Scaling")]
        [Tooltip("구역 하나 지날 때마다 적 체력에 곱하는 값")]
        public float enemyHealthGrowth = 1.8f;
        [Tooltip("보스 체력에 추가로 곱하는 값 (구역 성장 배율 적용 후)")]
        public float bossHealthMultiplier = 1f;

        [Header("Rewards")]
        [Tooltip("구역 클리어 시 최대 체력 대비 회복 비율")]
        [Range(0f, 1f)] public float healOnRoomClear = 0.25f;
        public int upgradeChoices = 3;
        [Tooltip("켜면 구역 클리어뿐 아니라 웨이브를 정리할 때마다 강화를 고른다 (시너지를 모을 기회를 늘림)")]
        public bool upgradeAfterEveryWave = true;
    }
}
