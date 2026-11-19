using Roglike.Core;
using UnityEngine;

namespace Roglike.Player
{
    // Visual의 방향 반전·HitFlash 변형 아래에서 발바닥을 축으로 움직인다.
    public class PlayerVisualFeel : MonoBehaviour
    {
        [SerializeField] private float _runLean = 5f;
        [SerializeField] private float _runThreshold = 0.5f;
        [SerializeField] private float _poseResponse = 18f;
        [SerializeField] private Vector2 _jumpStretch = new Vector2(0.9f, 1.1f);
        [SerializeField] private float _jumpDuration = 0.14f;
        [SerializeField] private float _fallSpeed = 6f;
        [SerializeField] private Vector2 _fallSquash = new Vector2(1.04f, 0.96f);
        [SerializeField] private Vector2 _landingSquash = new Vector2(1.15f, 0.85f);
        [SerializeField] private float _landingRecovery = 0.12f;
        [SerializeField] private float _attackNudgePixels = 2.5f;
        [SerializeField] private float _attackRecovery = 0.1f;
        [SerializeField] private float _pixelsPerUnit = 48f;

        private PlayerController _controller;
        private PlayerCombat _combat;
        private Rigidbody2D _body;
        private Vector3 _restPosition;
        private Vector3 _restScale;
        private Quaternion _restRotation;
        private bool _wasGrounded;
        private float _previousVerticalSpeed;
        private float _jumpRemaining;
        private float _landingRemaining;
        private float _recoilRemaining;
        private float _lean;

        private void Awake()
        {
            _controller = GetComponentInParent<PlayerController>();
            _combat = GetComponentInParent<PlayerCombat>();
            _body = _controller != null ? _controller.GetComponent<Rigidbody2D>() : null;
            _restPosition = transform.localPosition;
            _restScale = transform.localScale;
            _restRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            _wasGrounded = _controller != null && _controller.IsGrounded;
            if (_combat != null) _combat.AttackStarted += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_combat != null) _combat.AttackStarted -= OnAttackStarted;
            transform.localPosition = _restPosition;
            transform.localScale = _restScale;
            transform.localRotation = _restRotation;
            _jumpRemaining = _landingRemaining = _recoilRemaining = _lean = 0f;
            _previousVerticalSpeed = 0f;
        }

        private void OnAttackStarted(float duration) => _recoilRemaining = _attackRecovery;

        private void LateUpdate()
        {
            if (_controller == null || _body == null) return;
            if (TimeController.Instance != null && TimeController.Instance.IsPaused) return;

            float dt = Time.unscaledDeltaTime;
            float playerDt = Time.deltaTime * TimeController.PlayerTimeMultiplier;
            Vector2 velocity = _body.linearVelocity / TimeController.PlayerTimeMultiplier;
            bool grounded = _controller.IsGrounded;
            if (!grounded && velocity.y > 0f && (_wasGrounded || _previousVerticalSpeed <= 0f))
                _jumpRemaining = _jumpDuration;
            if (grounded && !_wasGrounded)
            {
                _landingRemaining = _landingRecovery;
                _jumpRemaining = 0f;
            }

            bool running = grounded && !_controller.IsDashing && !_controller.IsInSkill
                && Mathf.Abs(velocity.x) > _runThreshold;
            // Facing은 부모 Visual에 이미 적용되어 있으므로 여기서는 로컬 전방으로 기울인다.
            float targetLean = running ? -_runLean * Mathf.Sign(velocity.x) * _controller.Facing : 0f;
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-_poseResponse * playerDt));
            Vector2 pose = Vector2.one;
            if (!grounded && !_controller.IsDashing && !_controller.IsInSkill)
            {
                if (_jumpRemaining > 0f)
                    pose = Vector2.Lerp(Vector2.one, _jumpStretch, _jumpRemaining / Mathf.Max(_jumpDuration, 0.001f));
                else if (velocity.y < -_fallSpeed) pose = _fallSquash;
            }
            if (_landingRemaining > 0f)
                pose = Vector2.Lerp(Vector2.one, _landingSquash, _landingRemaining / Mathf.Max(_landingRecovery, 0.001f));

            transform.localScale = Vector3.Scale(_restScale, new Vector3(pose.x, pose.y, 1f));
            transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, _lean);
            float recoil = _recoilRemaining / Mathf.Max(_attackRecovery, 0.001f);
            transform.localPosition = _restPosition + Vector3.right * (recoil * _attackNudgePixels / Mathf.Max(_pixelsPerUnit, 1f));
            _jumpRemaining = Mathf.Max(0f, _jumpRemaining - playerDt);
            _landingRemaining = Mathf.Max(0f, _landingRemaining - dt);
            _recoilRemaining = Mathf.Max(0f, _recoilRemaining - dt);
            _wasGrounded = grounded;
            _previousVerticalSpeed = velocity.y;
        }
    }
}
