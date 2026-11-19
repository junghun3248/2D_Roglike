using System.Globalization;
using Roglike.Data;
using Roglike.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class SkillBarUI : MonoBehaviour
    {
        private const int SlotCount = 4;
        [SerializeField] private PlayerSkills _skills;
        [SerializeField] private float _clusterSlotSize = 72f;
        [SerializeField] private float _spacing = 12f;
        [SerializeField] private float _clusterBottom = 0f;
        [SerializeField] private float _frameWidth = 4f;
        [SerializeField] private float _popDuration = 0.2f;
        [SerializeField] private float _popScale = 0.18f;
        [SerializeField] private Color _emptyColor = new Color(0.2f, 0.2f, 0.25f, 0.6f);
        [SerializeField] private Color _cooldownColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private int _shortNameFontSize = 18;
        [SerializeField] private int _keyFontSize = 18;
        [SerializeField] private int _timerFontSize = 24;

        private readonly RectTransform[] _frames = new RectTransform[SlotCount];
        private readonly Image[] _colors = new Image[SlotCount];
        private readonly Image[] _overlays = new Image[SlotCount];
        private RectTransform _root;
        private readonly Text[] _names = new Text[SlotCount];
        private readonly Text[] _timers = new Text[SlotCount];
        private readonly bool[] _wasReady = new bool[SlotCount];
        private readonly float[] _popRemaining = new float[SlotCount];
        private readonly string[] _tenths = new string[10];
        private string[] _seconds;

        private void Awake()
        {
            for (int i = 0; i < _tenths.Length; i++)
                _tenths[i] = (i / 10f).ToString("0.0", CultureInfo.InvariantCulture);
            Canvas canvas = UiFactory.CreateCanvas("SkillBar", 31, transform);
            _root = UiFactory.CreateRect("SkillSlots", canvas.transform, Vector2.zero, Vector2.zero, new Vector2(172f, 32f), new Vector2(360f, 90f));
            string[] keys = { "A", "S", "D", "F" };
            for (int i = 0; i < SlotCount; i++)
            {
                float x = i * (_clusterSlotSize + _spacing);
                RectTransform frame = UiFactory.CreateRect("SkillSlot", _root,
                    Vector2.zero, Vector2.zero, new Vector2(x, _clusterBottom), Vector2.one * _clusterSlotSize);
                _frames[i] = frame;
                _colors[i] = UiFactory.CreateRadialFill("Frame", frame, _emptyColor);
                UiFactory.Stretch(_colors[i].rectTransform);
                Image background = UiFactory.CreateRadialFill("Background", frame, UiTheme.Panel);
                UiFactory.Stretch(background.rectTransform);
                background.rectTransform.offsetMin = Vector2.one * _frameWidth;
                background.rectTransform.offsetMax = -Vector2.one * _frameWidth;
                _names[i] = UiFactory.CreateText("Name", frame, _shortNameFontSize, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(_names[i].rectTransform);
                _names[i].rectTransform.offsetMin = new Vector2(-5f, 72f);
                _names[i].rectTransform.offsetMax = new Vector2(5f, 24f);
                Image overlay = UiFactory.CreateRadialFill("Cooldown", frame, _cooldownColor);
                _overlays[i] = overlay;
                _timers[i] = UiFactory.CreateText("Remaining", frame, _timerFontSize, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(_timers[i].rectTransform);
                var badge = UiFactory.CreatePanel(frame, new Vector2(26f, 24f), "KeyBadge");
                badge.anchoredPosition = new Vector2(25f, -27f);
                Text key = UiFactory.CreateText("Key", badge, _keyFontSize, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(key.rectTransform);
                key.text = keys[i];
            }
        }

        private void OnEnable()
        {
            if (_skills != null) _skills.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_skills != null) _skills.Changed -= Refresh;
        }

        private void Start()
        {
            var hud = FindFirstObjectByType<HudController>();
            if (hud != null)
            {
                _root.SetParent(hud.Cluster, false);
                _root.anchoredPosition = new Vector2(140f, 0f);
            }
            Refresh();
        }

        private void Refresh()
        {
            float maxSeconds = 1f;
            for (int i = 0; i < SlotCount; i++)
            {
                SkillDefinition skill = _skills != null && _skills.Slots != null && i < _skills.Slots.Count ? _skills.Slots[i] : null;
                _colors[i].color = skill != null ? skill.Color : _emptyColor;
                _names[i].text = skill != null ? skill.ShortName : string.Empty;
                if (skill != null) maxSeconds = Mathf.Max(maxSeconds, skill.Cooldown, _skills.CooldownRemaining(i));
            }
            int count = Mathf.CeilToInt(maxSeconds) + 1;
            if (_seconds != null && _seconds.Length >= count) return;
            _seconds = new string[count];
            for (int i = 0; i < count; i++) _seconds[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                bool equipped = _skills != null && _skills.Slots != null && i < _skills.Slots.Count && _skills.Slots[i] != null;
                bool ready = equipped && _skills.IsReady(i);
                float remaining = equipped ? _skills.CooldownRemaining(i) : 0f;
                if (ready && !_wasReady[i]) _popRemaining[i] = _popDuration;
                _wasReady[i] = ready;
                _popRemaining[i] = Mathf.Max(0f, _popRemaining[i] - Time.unscaledDeltaTime);
                float pop = _popDuration > 0f ? _popRemaining[i] / _popDuration : 0f;
                _frames[i].localScale = Vector3.one * (1f + _popScale * Mathf.Sin(pop * Mathf.PI));
                _overlays[i].fillAmount = equipped ? _skills.CooldownNormalized(i) : 1f;
                _timers[i].text = remaining <= 0f ? string.Empty : remaining < 1f
                    ? _tenths[Mathf.Clamp(Mathf.FloorToInt(remaining * 10f), 0, 9)]
                    : _seconds[Mathf.Min(Mathf.CeilToInt(remaining), _seconds.Length - 1)];
            }
        }
    }
}
