using UnityEngine;
using Roglike.Core;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Roglike.Player
{
    /// <summary>
    /// 오버드라이브 중 화면 효과 (채도↓, 색수차, 비네트, 렌즈 왜곡). 런타임에 전역 Volume을 만든다.
    /// 카메라의 Post Processing이 켜져 있어야 보인다.
    /// </summary>
    public class OverdriveScreenEffect : MonoBehaviour
    {
        [SerializeField] private OverdriveController _overdrive;
        [SerializeField] private float _fadeInSpeed = 14f;
        [SerializeField] private float _fadeOutSpeed = 5f;

        private Volume _volume;
        private VolumeProfile _profile;
        private float _targetWeight;
        [SerializeField, Range(0f, 1f)] private float _focusWeight = 0.18f;

        private void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var color = _profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-45f);
            color.contrast.Override(25f);
            color.colorFilter.Override(new Color(0.85f, 0.92f, 1.15f));

            var chromatic = _profile.Add<ChromaticAberration>(true);
            chromatic.intensity.Override(0.8f);

            var vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.05f, 0.45f, 0.55f));

            var lens = _profile.Add<LensDistortion>(true);
            lens.intensity.Override(-0.22f);

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.weight = 0f;
            _volume.sharedProfile = _profile;
        }

        private void OnEnable()
        {
            if (_overdrive != null)
            {
                _overdrive.ActiveChanged += OnActiveChanged;
            }
        }

        private void OnDisable()
        {
            if (_overdrive != null)
            {
                _overdrive.ActiveChanged -= OnActiveChanged;
            }
        }

        private void OnDestroy()
        {
            if (_profile != null)
            {
                Destroy(_profile);
            }
        }

        private void OnActiveChanged(bool active)
        {
            _targetWeight = active ? 1f : 0f;
        }

        private void Update()
        {
            float target = Mathf.Max(_targetWeight, TimeController.Instance != null && TimeController.Instance.IsFocusing ? _focusWeight : 0f);
            float speed = target > _volume.weight ? _fadeInSpeed : _fadeOutSpeed;
            _volume.weight = Mathf.MoveTowards(_volume.weight, target, speed * Time.unscaledDeltaTime);
        }
    }
}
