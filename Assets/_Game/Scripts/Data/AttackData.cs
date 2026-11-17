using UnityEngine;

namespace Roglike.Data
{
    /// <summary>공격 한 동작의 수치. 콤보는 이 에셋의 배열로 구성된다.</summary>
    [CreateAssetMenu(menuName = "Roglike/Attack Data", fileName = "AttackData")]
    public class AttackData : ScriptableObject
    {
        [Header("Damage")]
        [Tooltip("기본 공격력에 곱하는 배율")]
        public float damageMultiplier = 1f;
        public float hitStun = 0.25f;
        public Vector2 knockback = new Vector2(5f, 2f);

        [Header("Hitbox (오른쪽을 볼 때 기준)")]
        public Vector2 hitboxOffset = new Vector2(1f, 0.1f);
        public Vector2 hitboxSize = new Vector2(2f, 1.4f);

        [Header("Timing (초, 플레이어 시간)")]
        public float startup = 0.05f;
        public float active = 0.08f;
        public float recovery = 0.2f;
        [Tooltip("이 시점(공격 시작 후)부터 다음 콤보 입력을 받아 이어간다")]
        public float cancelTime = 0.14f;

        [Header("Motion")]
        public float lungeSpeed = 5f;

        [Header("Feel")]
        [Tooltip("명중 시 정지 시간 (실시간 초)")]
        public float hitStop = 0.06f;
        [Range(0f, 1f)] public float shake = 0.2f;
        [Tooltip("마무리 일격: 처치 시 줌인 + 슬로모션 연출")]
        public bool isFinisher;
        public float slashAngle = -10f;

        [Header("Overdrive")]
        public float overdriveGain = 6f;

        public float TotalDuration => startup + active + recovery;
    }
}
