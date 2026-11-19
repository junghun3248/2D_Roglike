using Roglike.Combat;
using Roglike.Data;
using Roglike.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class HudFeedbackUI : MonoBehaviour
    {
        [SerializeField] private float _lowHealthThreshold = 0.3f;
        [SerializeField] private float _hintDuration = 8f;
        [SerializeField] private float _vignetteStrength = 0.32f;
        private Health _health;
        private RunManager _run;
        private Camera _camera;
        private Canvas _canvas;
        private Image _vignette;
        private Text _hint;
        private float _startedAt;
        private bool _hintEnded;
        private Texture2D _texture;
        private Sprite _sprite;
        public float VignetteAlpha => _vignette != null ? _vignette.color.a : 0f;
        public void Bind(Health health) => _health = health;

        private void Awake()
        {
            _canvas = UiFactory.CreateCanvas("HudFeedback", 29, transform);
            _texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            _texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float edge = Mathf.Max(Mathf.Abs(x / 63f * 2f - 1f), Mathf.Abs(y / 63f * 2f - 1f));
                    pixels[y * 64 + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.InverseLerp(0.55f, 1f, edge), 2f));
                }
            _texture.SetPixels(pixels);
            _texture.Apply(false, true);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
            _vignette = UiFactory.CreateImage("LowHealthVignette", _canvas.transform, Color.clear);
            _vignette.sprite = _sprite;
            UiFactory.Stretch(_vignette.rectTransform);
            _hint = UiFactory.Label("EntranceHint", _canvas.transform, "Z 점프   X 공격   C 대시", 23, Vector2.zero, new Vector2(440f, 44f));
            _hint.gameObject.SetActive(false);
        }

        private void Start()
        {
            _run = FindFirstObjectByType<RunManager>();
            _camera = Camera.main;
            _startedAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            bool low = _health != null && !_health.IsDead && _health.Normalized < _lowHealthThreshold;
            Color color = UiTheme.Health;
            color.a = low ? _vignetteStrength * (0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f)) : 0f;
            _vignette.color = color;
            float elapsed = Time.unscaledTime - _startedAt;
            if (_run == null || _run.RoomIndex != 0 || _run.CurrentRoomType != RoomType.Entrance || elapsed >= _hintDuration) _hintEnded = true;
            bool show = !_hintEnded && _health != null && !_health.IsDead && _camera != null;
            _hint.gameObject.SetActive(show);
            if (!show) return;
            Vector3 screen = _camera.WorldToScreenPoint(_health.transform.position + Vector3.up * 2.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, screen, null, out Vector2 local);
            _hint.rectTransform.anchoredPosition = local;
            _hint.color = new Color(1f, 1f, 1f, Mathf.Clamp01((_hintDuration - elapsed) / 2f));
        }

        private void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
    }
}
