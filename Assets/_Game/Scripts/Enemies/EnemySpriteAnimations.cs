using System;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Enemies
{
    /// <summary>
    /// 적 스프라이트 애니메이션. AI가 IEnemyAnimationState를 구현하면 상태 이름(Idle·Charge·Stunned…)에 맞는 그림을,
    /// 아니면 대기 그림을 보여 준다. 아픈 얼굴(_hurt)이 있으면 맞은 직후 잠깐 그것을 우선한다 (슬라임).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemySpriteAnimations : MonoBehaviour
    {
        [Serializable]
        public struct StateClip
        {
            public string state;
            public SpriteAnimationData clip;
        }

        [SerializeField] private SpriteAnimator _animator;
        [SerializeField] private SpriteAnimationData _idle;
        [SerializeField] private SpriteAnimationData _hurt;
        [SerializeField] private StateClip[] _stateClips;
        [Tooltip("맞은 얼굴 유지 시간 (게임 시간 초)")]
        [SerializeField] private float _hurtDuration = 0.3f;

        private Health _health;
        private IEnemyAnimationState _source;
        private float _hurtTimer;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _source = GetComponent<IEnemyAnimationState>();
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
            if (_hurt == null)
            {
                return;
            }

            _hurtTimer = _hurtDuration;
            _animator?.Play(_hurt, true);
        }

        private void LateUpdate()
        {
            if (_animator == null || _health.IsDead)
            {
                return;
            }

            if (_hurtTimer > 0f)
            {
                _hurtTimer -= Time.deltaTime;
                return;
            }

            SpriteAnimationData clip = _source != null ? ClipFor(_source.AnimationState) : null;
            _animator.Play(clip != null ? clip : _idle);
        }

        private SpriteAnimationData ClipFor(string state)
        {
            if (_stateClips == null || string.IsNullOrEmpty(state))
            {
                return null;
            }

            for (int i = 0; i < _stateClips.Length; i++)
            {
                if (_stateClips[i].clip != null && _stateClips[i].state == state)
                {
                    return _stateClips[i].clip;
                }
            }

            return null;
        }
    }
}
