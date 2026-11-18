using Roglike.Core;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Player
{
    /// <summary>플레이어 상태(대기·달리기·공격·스킬)에 맞는 스프라이트 애니메이션을 고른다.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private SpriteAnimator _animator;
        [SerializeField] private SpriteAnimationData _idle;
        [SerializeField] private SpriteAnimationData _run;
        [SerializeField] private SpriteAnimationData _attack;
        [SerializeField] private float _runThreshold = 0.5f;
        [Tooltip("스킬은 길이가 제각각이라 공격 동작을 이 길이로 한 번 재생한다 (플레이어 시간 초)")]
        [SerializeField] private float _skillAnimationDuration = 0.35f;

        private PlayerController _controller;
        private PlayerCombat _combat;
        private Rigidbody2D _body;
        private bool _wasInSkill;
        private bool _playingOneShot;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _combat = GetComponent<PlayerCombat>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            if (_combat != null)
            {
                _combat.AttackStarted += OnAttackStarted;
            }
        }

        private void OnDisable()
        {
            if (_combat != null)
            {
                _combat.AttackStarted -= OnAttackStarted;
            }
        }

        private void OnAttackStarted(float duration)
        {
            PlayOneShot(duration);
        }

        private void PlayOneShot(float duration)
        {
            if (_animator == null || _attack == null)
            {
                return;
            }

            _animator.PlayOnce(_attack, duration);
            _playingOneShot = true;
        }

        private void LateUpdate()
        {
            if (_animator == null)
            {
                return;
            }

            bool inSkill = _controller.IsInSkill;
            if (inSkill && !_wasInSkill)
            {
                PlayOneShot(_skillAnimationDuration);
            }

            _wasInSkill = inSkill;

            if (_playingOneShot)
            {
                bool stillActing = _controller.IsAttacking || inSkill;
                if (!_animator.IsFinished || stillActing)
                {
                    return;
                }

                _playingOneShot = false;
            }

            // 속도는 플레이어 시간 기준으로 되돌려 비교한다 (오버드라이브 중 물리 속도가 배로 커지므로).
            float speed = _body != null ? Mathf.Abs(_body.linearVelocity.x) / TimeController.PlayerTimeMultiplier : 0f;
            bool running = speed > _runThreshold;
            _animator.Play(running && _run != null ? _run : _idle);
        }
    }
}
