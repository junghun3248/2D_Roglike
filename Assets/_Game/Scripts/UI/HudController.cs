using Roglike.Combat;
using Roglike.Core;
using Roglike.Player;
using Roglike.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class HudController : MonoBehaviour
    {
        [SerializeField] private Health _playerHealth;
        [SerializeField] private OverdriveController _overdrive;
        [SerializeField] private Sprite _circleSprite;
        [SerializeField] private float _trailSpeed = 1.2f;
        [SerializeField] private float _trailDelay = 0.2f;
        [SerializeField] private float _hitFlashDuration = 0.25f;
        [SerializeField] private float _bigHitFraction = 0.15f;
        [SerializeField] private float _shakeDistance = 5f;
        [SerializeField] private Vector2 _portraitZoom = new Vector2(90f, 150f);
        [SerializeField] private Vector2 _portraitOffset = new Vector2(0f, -50f);
        private RectTransform _healthFill, _trail, _overdriveFill, _healthBar;
        private Image _portrait, _overdriveImage;
        private Text _healthText, _ready;
        private float _lastHealth = 1f, _hitUntil, _shakeUntil;
        private double _lastCurrent = -1, _lastMax = -1;
        private SpriteRenderer _body;
        private PlayerController _player;
        private Rigidbody2D _playerBody;
        public RectTransform Cluster { get; private set; }
        public float HealthFill => _healthFill.anchorMax.x;

        private void Awake()
        {
            UiFactory.SetCircle(_circleSprite);
            Canvas canvas = UiFactory.CreateCanvas("Hud", 30, transform);
            Cluster = UiFactory.CreateRect("PlayerCluster", canvas.transform, Vector2.zero,
                Vector2.zero, Vector2.one * 32f, UiTheme.ClusterSize);
            RectTransform portrait = UiFactory.CreateRect("Portrait", Cluster, Vector2.zero, Vector2.zero,
                new Vector2(0f, 60f), Vector2.one * 120f);
            UiFactory.CreateRadialFill("GoldRing", portrait, UiTheme.Gold);
            Image mask = UiFactory.CreateRadialFill("PortraitMask", portrait, UiTheme.Panel);
            mask.rectTransform.offsetMin = Vector2.one * 4f;
            mask.rectTransform.offsetMax = -Vector2.one * 4f;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _portrait = UiFactory.CreateImage("Hero", mask.transform, Color.white);
            UiFactory.Stretch(_portrait.rectTransform);
            _portrait.rectTransform.sizeDelta = _portraitZoom;
            _portrait.rectTransform.anchoredPosition = _portraitOffset;
            _portrait.preserveAspect = true;
            _healthBar = Bar("Health", new Vector2(140f, 142f), new Vector2(360f, 28f), UiTheme.HealthBack);
            _trail = UiFactory.CreateImage("Trail", _healthBar, Color.white).rectTransform;
            UiFactory.Stretch(_trail);
            _healthFill = UiFactory.CreateImage("Fill", _healthBar, UiTheme.Health).rectTransform;
            UiFactory.Stretch(_healthFill);
            _healthText = UiFactory.CreateText("HealthText", _healthBar, 20, UiTheme.Text, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_healthText.rectTransform);
            var gauge = Bar("Overdrive", new Vector2(140f, 114f), new Vector2(300f, 12f), UiTheme.Panel);
            _overdriveImage = UiFactory.CreateImage("Fill", gauge, UiTheme.Cyan);
            _overdriveFill = _overdriveImage.rectTransform;
            UiFactory.Stretch(_overdriveFill);
            _ready = UiFactory.Label("ReadyKey", Cluster, "[Space]", 18, new Vector2(198f, 30f), new Vector2(85f, 28f));
            _ready.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (_playerHealth != null)
            {
                _player = _playerHealth.GetComponent<PlayerController>();
                _playerBody = _playerHealth.GetComponent<Rigidbody2D>();
                _body = _player != null ? _player.BodyRenderer : null;
                if (_body != null) _portrait.sprite = _body.sprite;
            }
            if (GetComponent<PauseMenuUI>() == null) gameObject.AddComponent<PauseMenuUI>();
            if (GetComponent<HudFeedbackUI>() == null) gameObject.AddComponent<HudFeedbackUI>().Bind(_playerHealth);
            if (FindFirstObjectByType<RunHud>() == null) gameObject.AddComponent<RunHud>();
        }

        private RectTransform Bar(string name, Vector2 position, Vector2 size, Color back)
        {
            var bar = UiFactory.CreatePanel(Cluster, size + Vector2.one * 4f, name);
            bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
            bar.anchoredPosition = position - Vector2.one * 2f;
            var content = UiFactory.CreateRect("Bar", bar, Vector2.zero, Vector2.zero, Vector2.one * 2f, size);
            var image = content.gameObject.AddComponent<Image>();
            image.color = back;
            image.raycastTarget = false;
            return content;
        }

        private void LateUpdate()
        {
            // 공격·달리기 프레임이 초상화에서 튀지 않도록 마지막 대기 프레임을 유지한다.
            if (_body != null && _body.sprite != null && _player.IsGrounded && !_player.IsAttacking
                && !_player.IsInSkill && !_player.IsDashing && !_player.IsHurt && !_player.IsCharging
                && (_playerBody == null || Mathf.Abs(_playerBody.linearVelocity.x) < 0.05f))
                _portrait.sprite = _body.sprite;
            if (_playerHealth != null)
            {
                float hp = _playerHealth.Normalized;
                if (hp < _lastHealth)
                {
                    _hitUntil = Time.unscaledTime + _hitFlashDuration;
                    if (_lastHealth - hp >= _bigHitFraction) _shakeUntil = _hitUntil;
                }
                if (hp >= _lastHealth) SetFill(_trail, Mathf.Max(hp, _trail.anchorMax.x));
                _lastHealth = hp;
                SetFill(_healthFill, hp);
                if (Time.unscaledTime > _hitUntil + _trailDelay)
                    SetFill(_trail, Mathf.MoveTowards(_trail.anchorMax.x, hp, _trailSpeed * Time.unscaledDeltaTime));
                if (_lastCurrent != _playerHealth.Current || _lastMax != _playerHealth.Max)
                {
                    _lastCurrent = _playerHealth.Current;
                    _lastMax = _playerHealth.Max;
                    _healthText.text = $"{NumberFormat.ToKorean(_lastCurrent)} / {NumberFormat.ToKorean(_lastMax)}";
                }
                _portrait.color = Time.unscaledTime < _hitUntil ? UiTheme.Health : Color.white;
                _healthBar.anchoredPosition = Vector2.one * 2f + (Time.unscaledTime < _shakeUntil
                    ? Vector2.right * (Mathf.Sin(Time.unscaledTime * 90f) * _shakeDistance) : Vector2.zero);
            }
            if (_overdrive == null) return;
            SetFill(_overdriveFill, _overdrive.Gauge01);
            Color color = Color.Lerp(UiTheme.Cyan, UiTheme.Magenta, _overdrive.Gauge01);
            _overdriveImage.color = _overdrive.IsReady ? Color.Lerp(color, Color.white, (1f + Mathf.Sin(Time.unscaledTime * 7f)) * 0.3f) : color;
            _ready.gameObject.SetActive(_overdrive.IsReady);
        }

        private static void SetFill(RectTransform fill, float value) => fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }
}
