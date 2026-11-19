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
        [SerializeField, Range(0f, 1f)] private float _flashPeak = 0.85f;
        [SerializeField, Range(0f, 0.03f)] private float _flashHold = 0.02f;
        [SerializeField, Range(0.001f, 0.07f)] private float _flashFade = 0.07f;
        [SerializeField] private float _squashAmount = 0.25f;
        [SerializeField] private float _squashDuration = 0.14f;

        private Health _health;
        private Color[] _baseColors;
        private Vector3 _baseScale;
        private float _flashStart;
        private float _squashStart = -10f;
        private bool _squashing;
        private bool _flashing;
        private MaterialPropertyBlock _block;
        private Color[] _originalColors;
        private Color[] _tintColors;
        private float[] _tintAmounts;
        private Color _statusTint;
        private bool _frozen;
        private Color _freezeTint;

        public void SetFreezeTint(bool frozen, Color color)
        {
            _frozen = frozen;
            _freezeTint = color;
            if (!_flashing) RestoreColors();
        }
        private float _statusStrength;

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
                _tintColors[i] = _renderers[i].color;
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
            _flashing = false;
            RestoreColors();
        }

        /// <summary>기본 색 변경 (예: 적 공격 예고 색). 번쩍임이 끝나면 이 색으로 돌아간다.</summary>
        public void SetBaseColor(int rendererIndex, Color color)
        {
            if (rendererIndex < 0 || rendererIndex >= _baseColors.Length)
            {
                return;
            }

            _tintColors[rendererIndex] = color;
            if (UsesFlashShader(rendererIndex))
            {
                // 그림 스프라이트에 색을 곱하면 탁해지므로(초록×빨강=갈색) 셰이더로 그 색을 얹는다.
                // 원래 색과 차이가 클수록 진하게 (공격 예고·분노·충전 빛).
                Color original = _originalColors[rendererIndex];
                float difference = Mathf.Max(Mathf.Abs(color.r - original.r), Mathf.Abs(color.g - original.g), Mathf.Abs(color.b - original.b));
                _tintAmounts[rendererIndex] = Mathf.Clamp01(difference) * _maxTint;
                if (!_flashing)
                {
                    ApplyTint(rendererIndex);
                }

                return;
            }

            _baseColors[rendererIndex] = color;
            if (!_flashing)
            {
                ApplyTint(rendererIndex);
            }
        }

        public void SetBaseColor(SpriteRenderer renderer, Color color)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] == renderer) SetBaseColor(i, color);
        }

        // Persistent status tint is visible only after the telegraph restores the original color.
        public void SetStatusTint(Color color, float strength)
        {
            _statusTint = color;
            _statusStrength = Mathf.Clamp01(strength);
            if (!_flashing)
                for (int i = 0; i < _renderers.Length; i++) ApplyTint(i);
        }

        public void ClearStatusTint() => SetStatusTint(Color.white, 0f);

        private void OnDamaged(DamageInfo info)
        {
            _flashStart = Time.unscaledTime;
            _squashStart = Time.unscaledTime;
            _flashing = true;
            ApplyFlash(_flashPeak);
        }

        private void ApplyFlash(float amount)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                ApplyTint(i);
                if (UsesFlashShader(i))
                {
                    _renderers[i].GetPropertyBlock(_block);
                    float tint = _block.GetFloat(FlashAmountId) * (1f - amount);
                    float total = tint + amount;
                    Color color = total > 0f
                        ? (_block.GetColor(FlashColorId) * tint + _flashColor * amount) / total
                        : _flashColor;
                    SetOverlay(i, color, Mathf.Min(_flashPeak, total));
                }
                else
                    _renderers[i].color = Color.Lerp(_renderers[i].color, _flashColor, amount);
            }
        }

        private void LateUpdate()
        {
            if (_flashing)
            {
                float elapsed = Time.unscaledTime - _flashStart;
                float amount = _flashPeak * (1f - Mathf.Clamp01((elapsed - _flashHold) / _flashFade));
                if (amount > 0f) ApplyFlash(amount);
                else
                {
                    _flashing = false;
                    RestoreColors();
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

        private void ApplyTint(int index)
        {
            if (_renderers[index] == null) return;
            if (_frozen)
            {
                if (UsesFlashShader(index)) SetOverlay(index, _freezeTint, _maxTint);
                else _renderers[index].color = Color.Lerp(_baseColors[index], _freezeTint, 0.7f);
                return;
            }
            bool telegraph = _tintColors[index] != _originalColors[index];
            if (UsesFlashShader(index))
                SetOverlay(index, telegraph ? _tintColors[index] : _statusTint,
                    telegraph ? _tintAmounts[index] : _statusStrength * _maxTint);
            else
                _renderers[index].color = telegraph ? _baseColors[index]
                    : Color.Lerp(_baseColors[index], _statusTint, _statusStrength);
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
                    ApplyTint(i);
                }
            }
        }
    }
}
