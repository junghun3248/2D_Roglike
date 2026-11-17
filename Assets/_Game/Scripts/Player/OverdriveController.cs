using System;
using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Player
{
    /// <summary>
    /// 특수기 오버드라이브(가칭): 게이지가 차면 발동, 세상은 느려지고 플레이어만 정상 속도.
    /// 발동 중 타격하면 PlayerCombat이 타격 지점으로 줌인한다.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerController))]
    public class OverdriveController : MonoBehaviour
    {
        [SerializeField] private float _maxGauge = 100f;
        [Tooltip("지속 시간 (실시간 초)")]
        [SerializeField] private float _duration = 5f;
        [SerializeField, Range(0.05f, 1f)] private float _worldTimeScale = 0.25f;
        [SerializeField] private float _damageMultiplier = 1.5f;
        [SerializeField] private float _cameraSizeMultiplier = 0.88f;
        [SerializeField] private float _afterimageInterval = 0.05f;
        [SerializeField] private Color _afterimageColor = new Color(1f, 0.4f, 0.85f, 0.55f);
        [Tooltip("M1 테스트용: 시작할 때 게이지를 가득 채운다")]
        [SerializeField] private bool _startFull = true;

        private PlayerInputReader _input;
        private PlayerController _controller;
        private float _gauge;
        private float _remaining;
        private float _afterimageTimer;

        public event Action<bool> ActiveChanged;

        public bool IsActive { get; private set; }
        public bool IsReady => !IsActive && _gauge >= _maxGauge;
        /// <summary>발동 중이면 남은 시간 비율, 아니면 충전 비율.</summary>
        public float Gauge01 => IsActive ? _remaining / _duration : _gauge / _maxGauge;
        public float DamageMultiplier => IsActive ? _damageMultiplier : 1f;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _controller = GetComponent<PlayerController>();
            _gauge = _startFull ? _maxGauge : 0f;
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                Deactivate();
            }
        }

        public void AddGauge(float amount)
        {
            if (IsActive || amount <= 0f)
            {
                return;
            }

            _gauge = Mathf.Min(_maxGauge, _gauge + amount);
        }

        private void Update()
        {
            if (!IsActive)
            {
                if (_input.OverdrivePressed && IsReady)
                {
                    Activate();
                }

                return;
            }

            bool frozen = TimeController.Instance != null && TimeController.Instance.IsHitStopped;
            if (!frozen)
            {
                _remaining -= Time.unscaledDeltaTime;
            }

            _afterimageTimer -= Time.unscaledDeltaTime;
            if (_afterimageTimer <= 0f)
            {
                _afterimageTimer = _afterimageInterval;
                Afterimage.Spawn(_controller.BodyRenderer, _afterimageColor, 0.3f);
            }

            if (_remaining <= 0f)
            {
                Deactivate();
            }
        }

        private void Activate()
        {
            IsActive = true;
            _gauge = 0f;
            _remaining = _duration;
            _afterimageTimer = 0f;
            TimeController.Instance?.SetOverdriveScale(_worldTimeScale);
            TimeController.Instance?.HitStop(0.12f);
            GameCamera.Instance?.SetSizeMultiplier(_cameraSizeMultiplier);
            GameCamera.Instance?.PunchZoom(transform.position, 0.75f, 0.25f);
            GameCamera.Instance?.Shake(0.35f);
            ActiveChanged?.Invoke(true);
        }

        private void Deactivate()
        {
            IsActive = false;
            _remaining = 0f;
            TimeController.Instance?.SetOverdriveScale(1f);
            GameCamera.Instance?.SetSizeMultiplier(1f);
            ActiveChanged?.Invoke(false);
        }
    }
}
