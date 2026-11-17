using UnityEngine;

namespace Roglike.Core
{
    /// <summary>
    /// 2D 추적 카메라. 흔들림·타격 줌·오버드라이브 줌을 담당한다.
    /// 히트스톱·감속 중에도 연출이 움직이도록 모두 실시간(unscaled) 기준으로 동작한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GameCamera : MonoBehaviour
    {
        public static GameCamera Instance { get; private set; }

        [Header("Follow")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector2 _offset = new Vector2(0f, 1.5f);
        [SerializeField] private float _followSharpness = 7f;
        [SerializeField] private float _lookAhead = 1.5f;

        [Header("Zoom")]
        [SerializeField] private float _baseSize = 6.5f;
        [SerializeField] private float _zoomSharpness = 6f;
        [SerializeField] private float _punchZoomSharpness = 30f;
        [SerializeField, Range(0f, 1f)] private float _punchFocusWeight = 0.55f;

        [Header("Shake")]
        [SerializeField] private float _maxShakeOffset = 0.7f;
        [SerializeField] private float _maxShakeAngle = 2.5f;
        [SerializeField] private float _shakeDecayPerSecond = 2.2f;
        [SerializeField] private float _shakeFrequency = 28f;

        [Header("Bounds (x)")]
        [SerializeField] private bool _clampX = true;
        [SerializeField] private float _minX = -30f;
        [SerializeField] private float _maxX = 30f;

        private Camera _camera;
        private Vector2 _position;
        private float _trauma;
        private float _sizeMultiplier = 1f;
        private float _punchSize;
        private float _punchUntil;
        private Vector2 _punchFocus;
        private float _seed;
        private float _lastTargetX;
        private float _lookDirection = 1f;

        private void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _baseSize;
            _seed = Random.value * 100f;
            SnapToTarget();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            SnapToTarget();
        }

        /// <summary>흔들림 추가 (0~1). 여러 번 호출하면 누적된다.</summary>
        public void Shake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        /// <summary>타격 지점으로 순간 줌인. sizeFactor 0.7이면 화면이 30% 확대된다.</summary>
        public void PunchZoom(Vector2 worldPoint, float sizeFactor, float realSeconds)
        {
            _punchFocus = worldPoint;
            _punchSize = _baseSize * _sizeMultiplier * Mathf.Clamp(sizeFactor, 0.3f, 1f);
            _punchUntil = Time.unscaledTime + realSeconds;
        }

        /// <summary>지속 줌 배율 (오버드라이브 중 살짝 확대 등). 1이 기본.</summary>
        public void SetSizeMultiplier(float multiplier)
        {
            _sizeMultiplier = Mathf.Clamp(multiplier, 0.3f, 2f);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool punching = Time.unscaledTime < _punchUntil;

            Vector2 desired = DesiredFollowPosition();
            if (punching)
            {
                desired = Vector2.Lerp(desired, _punchFocus, _punchFocusWeight);
            }

            float followSharpness = punching ? _punchZoomSharpness : _followSharpness;
            _position = Vector2.Lerp(_position, desired, 1f - Mathf.Exp(-followSharpness * dt));
            if (_clampX)
            {
                _position.x = Mathf.Clamp(_position.x, _minX, _maxX);
            }

            float targetSize = punching ? _punchSize : _baseSize * _sizeMultiplier;
            float zoomSharpness = punching ? _punchZoomSharpness : _zoomSharpness;
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, targetSize, 1f - Mathf.Exp(-zoomSharpness * dt));

            float shake = _trauma * _trauma;
            float t = Time.unscaledTime * _shakeFrequency;
            float offsetX = (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * _maxShakeOffset * shake;
            float offsetY = (Mathf.PerlinNoise(_seed + 17f, t) * 2f - 1f) * _maxShakeOffset * shake;
            float angle = (Mathf.PerlinNoise(_seed + 41f, t) * 2f - 1f) * _maxShakeAngle * shake;
            _trauma = Mathf.Max(0f, _trauma - _shakeDecayPerSecond * dt);

            transform.SetPositionAndRotation(
                new Vector3(_position.x + offsetX, _position.y + offsetY, -10f),
                Quaternion.Euler(0f, 0f, angle));
        }

        private Vector2 DesiredFollowPosition()
        {
            if (_target == null)
            {
                return _position;
            }

            float x = _target.position.x;
            float deltaX = x - _lastTargetX;
            if (Mathf.Abs(deltaX) > 0.001f)
            {
                _lookDirection = Mathf.Sign(deltaX);
            }

            _lastTargetX = x;
            return (Vector2)_target.position + _offset + new Vector2(_lookDirection * _lookAhead, 0f);
        }

        private void SnapToTarget()
        {
            if (_target == null)
            {
                _position = transform.position;
                return;
            }

            _lastTargetX = _target.position.x;
            _position = (Vector2)_target.position + _offset;
        }
    }
}
