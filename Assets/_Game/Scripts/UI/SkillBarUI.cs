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
        [SerializeField] private float _slotSize = 96f;
        [SerializeField] private float _spacing = 12f;
        [SerializeField] private float _bottom = 110f;
        [SerializeField] private float _frameWidth = 4f;
        [SerializeField] private float _popDuration = 0.2f;
        [SerializeField] private float _popScale = 0.18f;
        [SerializeField] private Color _emptyColor = new Color(0.2f, 0.2f, 0.25f, 0.6f);
        [SerializeField] private Color _backgroundColor = new Color(0.08f, 0.1f, 0.16f, 0.95f);
        [SerializeField] private Color _cooldownColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private int _nameFontSize = 28;
        [SerializeField] private int _keyFontSize = 18;
        [SerializeField] private int _timerFontSize = 24;

        private readonly RectTransform[] _frames = new RectTransform[SlotCount];
        private readonly Image[] _colors = new Image[SlotCount];
        private readonly RectTransform[] _overlays = new RectTransform[SlotCount];
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
            string[] keys = { "Q", "W", "E", "R" };
            for (int i = 0; i < SlotCount; i++)
            {
                float x = (i - (SlotCount - 1) * 0.5f) * (_slotSize + _spacing);
                RectTransform frame = UiFactory.CreateRect("SkillSlot", canvas.transform,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, _bottom), Vector2.one * _slotSize);
                _frames[i] = frame;
                _colors[i] = UiFactory.CreateImage("Frame", frame, _emptyColor);
                UiFactory.Stretch(_colors[i].rectTransform);
                Image background = UiFactory.CreateImage("Background", frame, _backgroundColor);
                UiFactory.Stretch(background.rectTransform);
                background.rectTransform.offsetMin = Vector2.one * _frameWidth;
                background.rectTransform.offsetMax = -Vector2.one * _frameWidth;
                _names[i] = UiFactory.CreateText("Name", frame, _nameFontSize, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(_names[i].rectTransform);
                Image overlay = UiFactory.CreateImage("Cooldown", frame, _cooldownColor);
                _overlays[i] = overlay.rectTransform;
                UiFactory.Stretch(_overlays[i]);
                _timers[i] = UiFactory.CreateText("Remaining", frame, _timerFontSize, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Stretch(_timers[i].rectTransform);
                Text key = UiFactory.CreateText("Key", frame, _keyFontSize, Color.white, TextAnchor.LowerCenter);
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

        private void Start() => Refresh();

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
                bool equipped = _skills != null && i < _skills.Slots.Count && _skills.Slots[i] != null;
                bool ready = equipped && _skills.IsReady(i);
                float remaining = equipped ? _skills.CooldownRemaining(i) : 0f;
                if (ready && !_wasReady[i]) _popRemaining[i] = _popDuration;
                _wasReady[i] = ready;
                _popRemaining[i] = Mathf.Max(0f, _popRemaining[i] - Time.unscaledDeltaTime);
                float pop = _popDuration > 0f ? _popRemaining[i] / _popDuration : 0f;
                _frames[i].localScale = Vector3.one * (1f + _popScale * Mathf.Sin(pop * Mathf.PI));
                _overlays[i].anchorMax = new Vector2(1f, equipped ? _skills.CooldownNormalized(i) : 1f);
                _timers[i].text = remaining <= 0f ? string.Empty : remaining < 1f
                    ? _tenths[Mathf.Clamp(Mathf.FloorToInt(remaining * 10f), 0, 9)]
                    : _seconds[Mathf.Min(Mathf.CeilToInt(remaining), _seconds.Length - 1)];
            }
        }
    }
}
