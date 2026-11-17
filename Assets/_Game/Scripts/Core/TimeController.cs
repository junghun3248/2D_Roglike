using UnityEngine;

namespace Roglike.Core
{
    /// <summary>
    /// 게임 전체 시간 흐름을 한곳에서 관리한다: 히트스톱, 오버드라이브 감속, 처치 연출 감속.
    /// 다른 코드는 Time.timeScale을 직접 바꾸지 말고 이 클래스를 통한다.
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        private const float BaseFixedDeltaTime = 0.02f;
        private const float MinScale = 0.05f;

        public static TimeController Instance { get; private set; }

        private float _overdriveScale = 1f;
        private float _pulseScale = 1f;
        private float _pulseUntil;
        private float _hitStopUntil;

        /// <summary>
        /// 오버드라이브 감속을 무시하고 정상 속도로 움직여야 하는 대상(플레이어)이 곱할 배율.
        /// 히트스톱·처치 연출 감속은 보정하지 않는다 (플레이어도 함께 멈칫해야 하므로).
        /// </summary>
        public static float PlayerTimeMultiplier => Instance == null ? 1f : 1f / Instance._overdriveScale;

        public bool IsHitStopped => Time.unscaledTime < _hitStopUntil;

        /// <summary>강화 선택·결과 화면 등에서 게임을 완전히 멈춘다. 다른 시간 효과보다 우선.</summary>
        public bool IsPaused { get; private set; }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Apply();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Instance = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = BaseFixedDeltaTime;
        }

        private void Update()
        {
            Apply();
        }

        /// <summary>모든 것을 잠깐 멈춘다. 이미 멈춰 있으면 더 긴 쪽을 유지한다.</summary>
        public void HitStop(float realSeconds)
        {
            if (realSeconds <= 0f)
            {
                return;
            }

            _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + realSeconds);
            Apply();
        }

        /// <summary>오버드라이브 감속 배율. 1이면 해제.</summary>
        public void SetOverdriveScale(float scale)
        {
            _overdriveScale = Mathf.Clamp(scale, MinScale, 1f);
            Apply();
        }

        /// <summary>처치 연출용 짧은 전체 감속 (플레이어 포함).</summary>
        public void PulseSlow(float scale, float realSeconds)
        {
            _pulseScale = Mathf.Clamp(scale, MinScale, 1f);
            _pulseUntil = Time.unscaledTime + realSeconds;
            Apply();
        }

        private void Apply()
        {
            if (IsPaused || IsHitStopped)
            {
                Time.timeScale = 0f;
                return;
            }

            float pulse = Time.unscaledTime < _pulseUntil ? _pulseScale : 1f;
            float scale = Mathf.Max(MinScale * MinScale, _overdriveScale * pulse);
            Time.timeScale = scale;
            // 감속 중에도 물리 스텝이 실시간 기준으로 촘촘하게 돌도록 함께 줄인다.
            Time.fixedDeltaTime = BaseFixedDeltaTime * scale;
        }
    }
}
