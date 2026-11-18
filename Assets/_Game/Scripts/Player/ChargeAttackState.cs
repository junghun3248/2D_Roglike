using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Player
{
    [RequireComponent(typeof(PlayerController), typeof(PlayerInputReader))]
    public class ChargeAttackState : MonoBehaviour
    {
        [SerializeField] private float _chargeDuration = 0.45f;
        // 그림 스프라이트의 기본 색이 흰색이라 흰색으로는 티가 안 난다 → 금빛.
        [SerializeField] private Color _chargeColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] private float _pulseSpeed = 15f;
        [SerializeField] private float _chargeZoom = 0.94f;
        [SerializeField] private float _zoomDuration = 0.2f;

        private PlayerController _controller;
        private PlayerInputReader _input;
        private HitFlash _flash;
        private SpriteRenderer _body;
        private Color _baseColor;
        private float _elapsed;
        private bool _active;
        private bool _ready;

        public bool IsActive => _active;

        private void Start()
        {
            _controller = GetComponent<PlayerController>();
            _input = GetComponent<PlayerInputReader>();
            _flash = GetComponent<HitFlash>();
            _body = _controller.BodyRenderer;
            if (_body != null) _baseColor = _body.color;
        }

        public bool Tick(float dt, bool canStart, out bool charged)
        {
            charged = false;
            if (_active && (!_controller.IsCharging || _controller.IsHurt || _controller.IsInSkill
                || _controller.IsDashing || !_controller.IsGrounded)) Cancel();

            if (!_active && canStart && _input.HeavyPressed && _controller.CanAttack && _controller.IsGrounded)
            {
                _active = true;
                _elapsed = 0f;
                _ready = false;
                _controller.FaceInputDirection();
                _controller.SetCharging(true);
            }
            if (!_active) return false;
            _elapsed += dt;
            if (!_ready && _elapsed >= _chargeDuration)
            {
                _ready = true;
                GameCamera.Instance?.PunchZoom(transform.position, _chargeZoom, _zoomDuration);
            }
            if (_input.HeavyReleased)
            {
                charged = _ready;
                Cancel();
                return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            if (!_active || _body == null) return;
            float pulse = _ready ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * _pulseSpeed) : 0.15f;
            SetTint(Color.Lerp(_baseColor, _chargeColor, pulse));
        }

        public void Cancel()
        {
            if (!_active) return;
            _active = false;
            _controller.SetCharging(false);
            SetTint(_baseColor);
        }

        private void SetTint(Color color)
        {
            if (_body == null) return;
            // HitFlash가 색 복원을 소유하게 하여 피격 플래시를 덮어쓰지 않는다.
            if (_flash != null) _flash.SetBaseColor(_body, color);
            else _body.color = color;
        }

        private void OnDisable() => Cancel();
    }
}
