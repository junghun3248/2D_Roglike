using Roglike.Combat;
using Roglike.Core;
using Roglike.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    /// <summary>M1 HUD: 체력, 오버드라이브 게이지, 조작법. 실제 UI 디자인 전까지 코드로 생성.</summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private Health _playerHealth;
        [SerializeField] private OverdriveController _overdrive;
        [SerializeField] private Color _healthColor = new Color(1f, 0.35f, 0.4f);
        [SerializeField] private Color _overdriveChargeColor = new Color(0.45f, 0.85f, 1f);
        [SerializeField] private Color _overdriveReadyColor = new Color(1f, 0.45f, 0.9f);

        private RectTransform _healthFill;
        private Text _healthText;
        private RectTransform _overdriveFill;
        private Image _overdriveFillImage;
        private Text _overdriveLabel;

        private void Awake()
        {
            Canvas canvas = UiFactory.CreateCanvas("Hud", 30, transform);
            Transform root = canvas.transform;

            Vector2 topLeft = new Vector2(0f, 1f);
            _healthFill = CreateBar(root, "Health", new Vector2(40f, -40f), new Vector2(420f, 30f), _healthColor, out _);
            _healthText = UiFactory.CreateText("HealthText", _healthFill.parent, 22, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch((RectTransform)_healthText.transform);

            _overdriveFill = CreateBar(root, "Overdrive", new Vector2(40f, -82f), new Vector2(320f, 18f), _overdriveChargeColor, out _overdriveFillImage);
            _overdriveLabel = UiFactory.CreateText("OverdriveLabel", root, 22, Color.white, TextAnchor.MiddleLeft);
            var labelRect = (RectTransform)_overdriveLabel.transform;
            labelRect.anchorMin = labelRect.anchorMax = topLeft;
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(372f, -91f);
            labelRect.sizeDelta = new Vector2(400f, 30f);

            Text help = UiFactory.CreateText("Controls", root, 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerLeft);
            var helpRect = (RectTransform)help.transform;
            helpRect.anchorMin = helpRect.anchorMax = Vector2.zero;
            helpRect.pivot = Vector2.zero;
            helpRect.anchoredPosition = new Vector2(40f, 30f);
            helpRect.sizeDelta = new Vector2(1840f, 60f);
            help.text = "이동 ←→ · 점프 Space · 공격 A (공중·↑A 띄우기·↓A 내려찍기·대시 중 A) ·\n강공격 S (꾹 모으기) · 대시 Shift · 오버드라이브 F · 스킬 Q W E R";
        }

        private void LateUpdate()
        {
            if (_playerHealth != null)
            {
                SetFill(_healthFill, _playerHealth.Normalized);
                _healthText.text = $"{NumberFormat.ToKorean(_playerHealth.Current)} / {NumberFormat.ToKorean(_playerHealth.Max)}";
            }

            if (_overdrive == null)
            {
                return;
            }

            SetFill(_overdriveFill, _overdrive.Gauge01);
            if (_overdrive.IsActive)
            {
                _overdriveFillImage.color = _overdriveReadyColor;
                _overdriveLabel.text = "OVERDRIVE!";
                _overdriveLabel.color = _overdriveReadyColor;
            }
            else if (_overdrive.IsReady)
            {
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.6f) < 0.3f;
                _overdriveFillImage.color = blink ? _overdriveReadyColor : Color.white;
                _overdriveLabel.text = "READY  [F]";
                _overdriveLabel.color = _overdriveReadyColor;
            }
            else
            {
                _overdriveFillImage.color = _overdriveChargeColor;
                _overdriveLabel.text = "OVERDRIVE";
                _overdriveLabel.color = new Color(1f, 1f, 1f, 0.7f);
            }
        }

        private static RectTransform CreateBar(Transform root, string name, Vector2 position, Vector2 size, Color fillColor, out Image fillImage)
        {
            RectTransform frame = UiFactory.CreateRect(name, root, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            Image background = UiFactory.CreateImage("Background", frame, new Color(0f, 0f, 0f, 0.55f));
            UiFactory.Stretch((RectTransform)background.transform);

            fillImage = UiFactory.CreateImage("Fill", frame, fillColor);
            var fill = (RectTransform)fillImage.transform;
            UiFactory.Stretch(fill);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            return fill;
        }

        private static void SetFill(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }
    }
}
