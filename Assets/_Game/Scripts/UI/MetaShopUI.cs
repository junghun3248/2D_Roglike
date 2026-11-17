using System;
using System.Collections.Generic;
using System.Globalization;
using Roglike.Data;
using Roglike.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class MetaShopUI : MonoBehaviour
    {
        [SerializeField, Min(0.4f)] private float _inputDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float _feedbackDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float _earnedFadeDuration = 0.6f;
        [SerializeField, Min(0f)] private float _shakeDistance = 9f;
        [SerializeField] private Color _selectedColor = new Color(0.35f, 0.8f, 1f);
        [SerializeField] private Color _unaffordableColor = new Color(1f, 0.35f, 0.35f);

        private sealed class Row
        {
            public MetaUpgradeData Upgrade;
            public RectTransform Rect;
            public Image Frame;
            public Text Level;
            public Text Cost;
            public Vector2 Position;
            public float FlashUntil;
            public float ShakeUntil;
        }

        private readonly List<Row> _rows = new List<Row>();
        private GameObject _root;
        private Text _crystals;
        private Text _earned;
        private InputActionMap _actions;
        private Action _onStartRun;
        private int _selected;
        private float _shownAt;
        private float _tickStartedAt;
        private int _tickFrom;
        private int _tickTarget;
        private int _displayedCrystals;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Canvas canvas = UiFactory.CreateCanvas("MetaShop", 75, transform);
            _root = canvas.gameObject;
            Image dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0.025f, 0.04f, 0.08f, 0.97f));
            UiFactory.Stretch(dim.rectTransform);
            CreateLabel("Title", canvas.transform, "강화소", 58, new Vector2(-600f, 440f),
                new Vector2(400f, 90f), TextAnchor.MiddleLeft);
            _crystals = CreateLabel("Crystals", canvas.transform, "", 40, new Vector2(550f, 450f),
                new Vector2(500f, 60f), TextAnchor.MiddleRight);
            _earned = CreateLabel("Earned", canvas.transform, "", 28, new Vector2(550f, 395f),
                new Vector2(500f, 50f), TextAnchor.MiddleRight);
            CreateLabel("Hint", canvas.transform, "↑↓ 선택 · J 구매 · Space 다음 런 출발", 30,
                new Vector2(0f, -455f), new Vector2(1600f, 70f), TextAnchor.MiddleCenter);

            _actions = new InputActionMap("MetaShop");
            AddButton("Up", () => Move(-1), "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
            AddButton("Down", () => Move(1), "<Keyboard>/s", "<Keyboard>/downArrow", "<Gamepad>/dpad/down");
            AddButton("Buy", BuySelected, "<Keyboard>/j", "<Keyboard>/enter",
                "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            AddButton("StartRun", StartRun, "<Keyboard>/space", "<Gamepad>/start");
            _root.SetActive(false);
        }

        /// <summary>호출 측에서 일시정지·재개를 담당한다. 모든 연출은 비감속 시간을 사용한다.</summary>
        public void Show(IReadOnlyList<MetaUpgradeData> upgrades, int earnedThisRun, Action onStartRun)
        {
            Hide();
            if (!isActiveAndEnabled) return;
            foreach (Row row in _rows)
            {
                row.Rect.gameObject.SetActive(false);
                Destroy(row.Rect.gameObject);
            }
            _rows.Clear();
            var options = new List<MetaUpgradeData>();
            var ids = new HashSet<string>();
            if (upgrades != null)
            {
                foreach (MetaUpgradeData upgrade in upgrades)
                {
                    if (upgrade != null && !string.IsNullOrWhiteSpace(upgrade.id) && ids.Add(upgrade.id))
                        options.Add(upgrade);
                }
            }
            float spacing = Mathf.Min(104f, 740f / Mathf.Max(1, options.Count));
            for (int i = 0; i < options.Count; i++)
                BuildRow(options[i], i, spacing);

            _selected = 0;
            _shownAt = Time.unscaledTime;
            _onStartRun = onStartRun;
            _tickFrom = _tickTarget = _displayedCrystals = MetaProgression.Crystals;
            _crystals.text = "결정 " + FormatCrystals(_displayedCrystals);
            _crystals.rectTransform.localScale = Vector3.one;
            _earned.text = "이번 런 +" + FormatCrystals(Mathf.Max(0, earnedThisRun));
            _earned.color = new Color(0.55f, 1f, 0.8f, 0f);
            RefreshRows();
            IsOpen = true;
            _root.SetActive(true);
            _actions.Enable();
            AnimateRows();
        }

        private void BuildRow(MetaUpgradeData upgrade, int index, float spacing)
        {
            Vector2 position = new Vector2(0f, 340f - spacing * (index + 0.5f));
            RectTransform rect = UiFactory.CreateRect("Meta_" + upgrade.id, _root.transform,
                Vector2.one * 0.5f, Vector2.one * 0.5f, position, new Vector2(1600f, spacing - 10f));
            Image frame = UiFactory.CreateImage("Frame", rect, Color.white);
            UiFactory.Stretch(frame.rectTransform);
            Image body = UiFactory.CreateImage("Body", rect, new Color(0.08f, 0.1f, 0.16f));
            UiFactory.Stretch(body.rectTransform);
            body.rectTransform.offsetMin = Vector2.one * 3f;
            body.rectTransform.offsetMax = Vector2.one * -3f;
            float height = spacing - 18f;
            CreateLabel("Name", rect, upgrade.displayName, 30, new Vector2(-610f, 0f),
                new Vector2(320f, height), TextAnchor.MiddleLeft);
            Text level = CreateLabel("Level", rect, "", 25, new Vector2(-350f, 0f),
                new Vector2(180f, height), TextAnchor.MiddleCenter);
            CreateLabel("Effect", rect, UpgradeText.Describe(upgrade.modifiersPerLevel), 26,
                new Vector2(120f, 0f), new Vector2(700f, height), TextAnchor.MiddleLeft);
            Text cost = CreateLabel("Cost", rect, "", 30, new Vector2(650f, 0f),
                new Vector2(240f, height), TextAnchor.MiddleRight);
            _rows.Add(new Row { Upgrade = upgrade, Rect = rect, Frame = frame, Level = level,
                Cost = cost, Position = position });
        }

        private void Update()
        {
            if (!IsOpen) return;
            float elapsed = Time.unscaledTime - _shownAt;
            _earned.color = new Color(0.55f, 1f, 0.8f,
                Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _earnedFadeDuration)));
            float tick = Mathf.Clamp01((Time.unscaledTime - _tickStartedAt) / Mathf.Max(0.01f, _feedbackDuration));
            int displayed = tick >= 1f ? _tickTarget
                : (int)Math.Round(_tickFrom + ((double)_tickTarget - _tickFrom) * tick);
            if (displayed != _displayedCrystals)
            {
                _displayedCrystals = displayed;
                _crystals.text = "결정 " + FormatCrystals(displayed);
            }
            float pulse = _tickFrom == _tickTarget ? 0f : Mathf.Sin(tick * Mathf.PI) * 0.1f;
            _crystals.rectTransform.localScale = Vector3.one * (1f + pulse);
            AnimateRows();
        }

        private void AnimateRows()
        {
            float duration = Mathf.Max(0.01f, _feedbackDuration);
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                Color color = i == _selected ? _selectedColor : new Color(0.22f, 0.28f, 0.38f);
                float flash = Mathf.Clamp01((row.FlashUntil - Time.unscaledTime) / duration);
                row.Frame.color = Color.Lerp(color, Color.white, flash);
                float shake = Mathf.Clamp01((row.ShakeUntil - Time.unscaledTime) / duration);
                float offset = Mathf.Sin((1f - shake) * Mathf.PI * 8f) * shake * _shakeDistance;
                row.Rect.anchoredPosition = row.Position + new Vector2(offset, 0f);
            }
        }

        private void RefreshRows()
        {
            foreach (Row row in _rows)
            {
                int level = MetaProgression.Level(row.Upgrade.id);
                bool maxed = level >= row.Upgrade.maxLevel;
                row.Level.text = $"Lv {level}/{row.Upgrade.maxLevel}";
                row.Cost.text = maxed ? "MAX" : "결정 " + FormatCrystals(row.Upgrade.CostForLevel(level));
                row.Cost.color = maxed || MetaProgression.CanBuy(row.Upgrade) ? Color.white : _unaffordableColor;
            }
        }

        private void Move(int direction)
        {
            if (_rows.Count == 0) return;
            _selected = (_selected + direction + _rows.Count) % _rows.Count;
            AnimateRows();
        }

        private void BuySelected()
        {
            if (_rows.Count == 0) return;
            Row row = _rows[_selected];
            if (MetaProgression.TryBuy(row.Upgrade))
            {
                row.FlashUntil = Time.unscaledTime + _feedbackDuration;
                _tickFrom = _displayedCrystals;
                _tickTarget = MetaProgression.Crystals;
                _tickStartedAt = Time.unscaledTime;
                RefreshRows();
            }
            else row.ShakeUntil = Time.unscaledTime + _feedbackDuration;
        }

        private void StartRun()
        {
            Action callback = _onStartRun;
            Hide();
            callback?.Invoke();
        }

        private void AddButton(string name, Action callback, params string[] bindings)
        {
            InputAction action = _actions.AddAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            action.performed += context =>
            {
                if (IsOpen && Time.unscaledTime - _shownAt >= Mathf.Max(0.4f, _inputDelay)) callback();
            };
        }

        private void Hide()
        {
            IsOpen = false;
            _onStartRun = null;
            _actions?.Disable();
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            Hide();
            _actions?.Dispose();
            if (_root != null) Destroy(_root);
        }

        private static string FormatCrystals(int amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

        private static Text CreateLabel(string name, Transform parent, string value, int size,
            Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            Text text = UiFactory.CreateText(name, parent, size, Color.white, alignment);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            text.text = value;
            return text;
        }
    }
}
