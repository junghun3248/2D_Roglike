using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Enemies
{
    /// <summary>적 스프라이트 애니메이션: 평소 대기, 맞으면 잠깐 아픈 얼굴.</summary>
    [RequireComponent(typeof(Health))]
    public class EnemySpriteAnimations : MonoBehaviour
    {
        [SerializeField] private SpriteAnimator _animator;
        [SerializeField] private SpriteAnimationData _idle;
        [SerializeField] private SpriteAnimationData _hurt;
        [Tooltip("맞은 얼굴 유지 시간 (게임 시간 초)")]
        [SerializeField] private float _hurtDuration = 0.3f;

        private Health _health;
        private float _hurtTimer;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _animator?.Play(_idle, true);
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            _hurtTimer = _hurtDuration;
            _animator?.Play(_hurt != null ? _hurt : _idle, true);
        }

        private void Update()
        {
            if (_hurtTimer <= 0f || _health.IsDead)
            {
                return;
            }

            _hurtTimer -= Time.deltaTime;
            if (_hurtTimer <= 0f)
            {
                _animator?.Play(_idle, true);
            }
        }
    }
}
