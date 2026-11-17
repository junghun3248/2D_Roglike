using UnityEngine;

namespace Roglike.Combat
{
    /// <summary>
    /// 피격 시 스프라이트를 하얗게 번쩍이고 살짝 찌그러뜨린다. 히트스톱 중에도 보이도록 실시간 기준.
    /// 지금은 흰색 자리표시 스프라이트 기준(색을 흰색으로 바꾸면 번쩍임). 실제 아트가 들어오면 플래시 셰이더로 교체.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] _renderers;
        [SerializeField] private Transform _squashTarget;
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _flashDuration = 0.08f;
        [SerializeField] private float _squashAmount = 0.25f;
        [SerializeField] private float _squashDuration = 0.14f;

        private Health _health;
        private Color[] _baseColors;
        private Vector3 _baseScale;
        private float _flashUntil;
        private float _squashStart = -10f;
        private bool _squashing;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>();
            }

            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i].color;
            }

            if (_squashTarget != null)
            {
                _baseScale = _squashTarget.localScale;
            }
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            RestoreColors();
        }

        /// <summary>기본 색 변경 (예: 적 공격 예고 색). 번쩍임이 끝나면 이 색으로 돌아간다.</summary>
        public void SetBaseColor(int rendererIndex, Color color)
        {
            if (rendererIndex < 0 || rendererIndex >= _baseColors.Length)
            {
                return;
            }

            _baseColors[rendererIndex] = color;
            if (Time.unscaledTime >= _flashUntil)
            {
                _renderers[rendererIndex].color = color;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            _flashUntil = Time.unscaledTime + _flashDuration;
            _squashStart = Time.unscaledTime;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].color = _flashColor;
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _flashUntil)
            {
                RestoreColors();
            }

            if (_squashTarget == null)
            {
                return;
            }

            float t = (Time.unscaledTime - _squashStart) / _squashDuration;
            if (t >= 1f && !_squashing)
            {
                return;
            }

            _squashing = t < 1f;

            // 좌우 반전(scale.x 부호)은 이동 스크립트가 관리하므로 부호는 유지한다.
            float sign = _squashTarget.localScale.x < 0f ? -1f : 1f;
            float s = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI) * _squashAmount;
            float x = Mathf.Abs(_baseScale.x) * (1f + s) * sign;
            _squashTarget.localScale = new Vector3(x, _baseScale.y * (1f - s), _baseScale.z);
        }

        private void RestoreColors()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].color = _baseColors[i];
                }
            }
        }
    }
}
