using UnityEngine;

namespace Roglike.Combat
{
    /// <summary>
    /// 피격 시 스프라이트를 하얗게 번쩍이고 살짝 찌그러뜨린다. 히트스톱 중에도 보이도록 실시간 기준.
    /// 머티리얼에 _FlashAmount가 있으면(Roglike/SpriteFlash 셰이더) 셰이더로, 없으면(흰 자리표시) 색으로 번쩍인다.
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
        private bool _flashing;
        private MaterialPropertyBlock _block;
        private Color[] _originalColors;
        private Color[] _tintColors;
        private float[] _tintAmounts;

        [Tooltip("그림 스프라이트에 SetBaseColor 색을 얹는 최대 세기 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _maxTint = 0.55f;

        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>();
            }

            _baseColors = new Color[_renderers.Length];
            _originalColors = new Color[_renderers.Length];
            _tintColors = new Color[_renderers.Length];
            _tintAmounts = new float[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i].color;
                _originalColors[i] = _renderers[i].color;
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

            if (UsesFlashShader(rendererIndex))
            {
                // 그림 스프라이트에 색을 곱하면 탁해지므로(초록×빨강=갈색) 셰이더로 그 색을 얹는다.
                // 원래 색과 차이가 클수록 진하게 (공격 예고·분노·충전 빛).
                Color original = _originalColors[rendererIndex];
                float difference = Mathf.Max(Mathf.Abs(color.r - original.r), Mathf.Abs(color.g - original.g), Mathf.Abs(color.b - original.b));
                _tintColors[rendererIndex] = color;
                _tintAmounts[rendererIndex] = Mathf.Clamp01(difference) * _maxTint;
                if (!_flashing)
                {
                    ApplyTint(rendererIndex);
                }

                return;
            }

            _baseColors[rendererIndex] = color;
            if (Time.unscaledTime >= _flashUntil)
            {
                _renderers[rendererIndex].color = color;
            }
        }

        public void SetBaseColor(SpriteRenderer renderer, Color color)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] == renderer) SetBaseColor(i, color);
        }

        private void OnDamaged(DamageInfo info)
        {
            _flashUntil = Time.unscaledTime + _flashDuration;
            _squashStart = Time.unscaledTime;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (UsesFlashShader(i))
                {
                    // 텍스처가 있는 스프라이트는 색 곱셈으로 하얘지지 않으므로 셰이더로 덮는다.
                    SetFlashAmount(i, 1f);
                }
                else
                {
                    _renderers[i].color = _flashColor;
                }
            }

            _flashing = true;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _flashUntil)
            {
                RestoreColors();
                if (_flashing)
                {
                    _flashing = false;
                    for (int i = 0; i < _renderers.Length; i++)
                    {
                        if (UsesFlashShader(i))
                        {
                            ApplyTint(i);
                        }
                    }
                }
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

        private bool UsesFlashShader(int index)
        {
            SpriteRenderer renderer = _renderers[index];
            return renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(FlashAmountId);
        }

        private void SetFlashAmount(int index, float amount)
        {
            SetOverlay(index, _flashColor, amount);
        }

        private void ApplyTint(int index)
        {
            SetOverlay(index, _tintColors[index], _tintAmounts[index]);
        }

        private void SetOverlay(int index, Color color, float amount)
        {
            _block ??= new MaterialPropertyBlock();
            _renderers[index].GetPropertyBlock(_block);
            _block.SetFloat(FlashAmountId, amount);
            _block.SetColor(FlashColorId, color);
            _renderers[index].SetPropertyBlock(_block);
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
