using Roglike.Combat;
using Roglike.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class BossHealthBar : MonoBehaviour
    {
        // 왼쪽 위 HUD(체력·오버드라이브 라벨, x≈40~780)와 겹치지 않는 폭.
        [SerializeField] private Vector2 _barSize = new Vector2(760f, 34f);
        [SerializeField] private float _topOffset = 90f;
        [SerializeField] private float _trailDuration = 0.4f;
        [SerializeField] private float _trailDelay = 0.1f;
        [SerializeField] private float _deathHideDelay = 1f;

        private Canvas _canvas;
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
            RectTransform bar = UiFactory.CreateRect("BossBar", _canvas.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -_topOffset), _barSize);
            Image background = UiFactory.CreateImage("Background", bar, new Color(0.12f, 0.04f, 0.05f, 0.95f));
            UiFactory.Stretch(background.rectTransform);
            _trail = UiFactory.CreateImage("Trail", bar, Color.white).rectTransform;
            UiFactory.Stretch(_trail);
            _fill = UiFactory.CreateImage("Health", bar, new Color(0.85f, 0.12f, 0.18f)).rectTransform;
            UiFactory.Stretch(_fill);
            _nameText = UiFactory.CreateText("Name", bar, 30, Color.white, TextAnchor.MiddleCenter);
            RectTransform nameRect = _nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = Vector2.one;
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.sizeDelta = new Vector2(0f, 44f);
            nameRect.anchoredPosition = new Vector2(0f, 8f);
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
