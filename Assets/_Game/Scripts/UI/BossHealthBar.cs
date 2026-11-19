using Roglike.Combat;
using Roglike.Core;
using Roglike.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class BossHealthBar : MonoBehaviour
    {
        // 화면 위 가운데. 좌하단 플레이어 묶음과 넓은 하단 바가 겹치기 때문에 위로 올렸다 (장르에서 흔한 위치).
        [SerializeField] private Vector2 _bottomBarSize = new Vector2(900f, 22f);
        [SerializeField] private float _topOffset = 104f;
        [SerializeField] private float _trailDuration = 0.4f;
        [SerializeField] private float _trailDelay = 0.1f;
        [SerializeField] private float _deathHideDelay = 1f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _bar;
        private Image _plateImage, _fillImage;
        private StagKingBoss _stag;
        private float _shownAt;
        [SerializeField] private float _slideDuration = 0.3f;
        private RectTransform _fill;
        private RectTransform _trail;
        private Text _nameText;
        private Text _healthText;
        private Health _health;
        private float _target;
        private float _trailStart;
        private float _trailTime;
        private float _hideTimer = -1f;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (_canvas != null) return;
            _canvas = UiFactory.CreateCanvas("BossHealthCanvas", 35, transform);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            RectTransform bar = UiFactory.CreatePanel(_canvas.transform, _bottomBarSize, "BossBar");
            _bar = bar;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -_topOffset);
            var content = UiFactory.CreateRect("BarContent", bar, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, _bottomBarSize - Vector2.one * 4f);
            Image background = UiFactory.CreateImage("Background", content, UiTheme.HealthBack);
            UiFactory.Stretch(background.rectTransform);
            _trail = UiFactory.CreateImage("Trail", content, Color.white).rectTransform;
            UiFactory.Stretch(_trail);
            _fillImage = UiFactory.CreateImage("Health", content, UiTheme.Health);
            _fill = _fillImage.rectTransform;
            UiFactory.Stretch(_fill);
            var plate = UiFactory.CreatePanel(bar, new Vector2(340f, 42f), "BossNamePlate");
            plate.anchoredPosition = new Vector2(0f, 44f);
            _plateImage = plate.GetComponent<Image>();
            _nameText = UiFactory.CreateText("Name", plate, 26, UiTheme.Text, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_nameText.rectTransform);
            var tick = UiFactory.CreateImage("PhaseMarker", bar, UiTheme.Gold);
            tick.rectTransform.sizeDelta = new Vector2(2f, 30f);
            _healthText = UiFactory.CreateText("HealthText", bar, 22, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_healthText.rectTransform);
            _canvas.gameObject.SetActive(false);
        }

        public void Show(Health health, string displayName)
        {
            Hide();
            if (health == null) return;
            Build();
            _health = health;
            _stag = health.GetComponent<StagKingBoss>();
            _shownAt = Time.unscaledTime;
            _group.alpha = 0f;
            _nameText.text = displayName;
            _health.Changed += OnHealthChanged;
            _health.Died += OnDied;
            _canvas.gameObject.SetActive(true);
            OnHealthChanged();
            SetFill(_trail, _target);
            _trailStart = _target;
            if (_health.IsDead) _hideTimer = _deathHideDelay;
        }

        public void Hide()
        {
            // 파괴된 Unity 객체도 관리 이벤트 참조는 해제한다.
            if (!ReferenceEquals(_health, null))
            {
                _health.Changed -= OnHealthChanged;
                _health.Died -= OnDied;
            }
            _health = null;
            _hideTimer = -1f;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        private void OnHealthChanged()
        {
            _target = Mathf.Clamp01(_health.Normalized);
            SetFill(_fill, _target);
            _trailStart = Mathf.Max(_trail.anchorMax.x, _target);
            SetFill(_trail, _trailStart);
            _trailTime = -_trailDelay;
            _healthText.text = NumberFormat.ToKorean(_health.Current) + " / " + NumberFormat.ToKorean(_health.Max);
        }

        private void OnDied(DamageInfo info)
        {
            _hideTimer = _deathHideDelay;
        }

        private void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime;
            float entrance = Mathf.Clamp01((Time.unscaledTime - _shownAt) / Mathf.Max(0.01f, _slideDuration));
            // 위에서 미끄러져 내려온다.
            _bar.anchoredPosition = new Vector2(0f, Mathf.Lerp(60f, -_topOffset, Mathf.SmoothStep(0f, 1f, entrance)));
            _group.alpha = _hideTimer >= 0f ? Mathf.Clamp01(_hideTimer / Mathf.Max(0.01f, _deathHideDelay)) : entrance;
            bool enraged = _stag != null && _stag.IsEnraged;
            _fillImage.color = enraged ? UiTheme.Enraged : UiTheme.Health;
            _plateImage.color = enraged ? Color.Lerp(UiTheme.Enraged, Color.white, (1f + Mathf.Sin(Time.unscaledTime * 6f)) * 0.3f) : UiTheme.Gold;
            if (_hideTimer >= 0f)
            {
                _hideTimer -= dt;
                if (_hideTimer <= 0f)
                {
                    Hide();
                    return;
                }
            }
            else if (_health == null)
            {
                Hide();
                return;
            }
            _trailTime += dt;
            float t = _trailDuration > 0f ? Mathf.Clamp01(_trailTime / _trailDuration) : 1f;
            SetFill(_trail, Mathf.Lerp(_trailStart, _target, Mathf.SmoothStep(0f, 1f, t)));
        }

        private static void SetFill(RectTransform rect, float amount)
        {
            rect.anchorMax = new Vector2(amount, 1f);
        }

        private void OnDestroy()
        {
            Hide();
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
