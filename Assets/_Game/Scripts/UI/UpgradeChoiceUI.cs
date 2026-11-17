using System;
using System.Collections.Generic;
using System.Text;
using Roglike.Data;
using Roglike.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class UpgradeChoiceUI : MonoBehaviour
    {
        [SerializeField, Min(0.35f)] private float _inputDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float _popDuration = 0.22f;
        [SerializeField, Min(0f)] private float _popStagger = 0.07f;
        [SerializeField] private float _selectedScale = 1.08f;

        private readonly List<UpgradeData> _options = new List<UpgradeData>(3);
        private readonly RectTransform[] _cards = new RectTransform[3];
        private readonly Image[] _frames = new Image[3];
        private readonly Text[] _rarities = new Text[3];
        private readonly Text[] _names = new Text[3];
        private readonly Text[] _tags = new Text[3];
        private readonly Text[] _descriptions = new Text[3];
        private readonly Text[] _stackTexts = new Text[3];
        private readonly Color[] _colors = new Color[3];
        private GameObject _root;
        private InputActionMap _actions;
        private Action<UpgradeData> _onChosen;
        private Func<UpgradeData, int> _stacks;
        private Func<IReadOnlyList<UpgradeData>> _reroll;
        private Func<UpgradeTag, (string name, Color color)> _tagStyle;
        private Text _hint;
        private int _selected;
        private float _shownAt;

        public bool IsOpen { get; private set; }
        public int RerollsLeft { get; private set; }

        private void Awake()
        {
            Canvas canvas = UiFactory.CreateCanvas("UpgradeChoice", 60, transform);
            _root = canvas.gameObject;
            Image dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
            UiFactory.Stretch(dim.rectTransform);
            CreateLabel("Title", canvas.transform, "강화 선택", 56, Color.white,
                new Vector2(0f, 370f), new Vector2(1000f, 90f));
            _hint = CreateLabel("Hint", canvas.transform, "",
                28, Color.white, new Vector2(0f, -370f), new Vector2(1600f, 100f));
            for (int i = 0; i < _cards.Length; i++) BuildCard(i, canvas.transform);

            _actions = new InputActionMap("UpgradeChoice");
            AddButton("Reroll", Reroll, "<Keyboard>/r", "<Gamepad>/buttonNorth");
            AddButton("Left", () => Move(-1), "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left");
            AddButton("Right", () => Move(1), "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right");
            AddButton("Confirm", () => Choose(_selected), "<Keyboard>/j", "<Keyboard>/space",
                "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                AddButton($"Pick{i + 1}", () => Choose(index), $"<Keyboard>/{i + 1}", $"<Keyboard>/numpad{i + 1}");
            }

            _root.SetActive(false);
        }

        /// <summary>호출 측에서 게임 일시정지·재개 및 선택한 강화 적용을 담당한다.</summary>
        public void Show(IReadOnlyList<UpgradeData> options, Func<UpgradeData, int> stacks,
            Action<UpgradeData> onChosen, Func<IReadOnlyList<UpgradeData>> reroll = null, int rerollsLeft = 0)
        {
            Hide();
            RerollsLeft = Mathf.Max(0, rerollsLeft);
            if (!isActiveAndEnabled || options == null) return;
            ReplaceOptions(options);
            if (_options.Count == 0) return;

            _selected = 0;
            _onChosen = onChosen;
            _stacks = stacks;
            _reroll = reroll;
            RefreshCards();
            IsOpen = true;
            _root.SetActive(true);
            _actions.Enable();
        }

        public void SetTagStyle(Func<UpgradeTag, (string name, Color color)> style)
        {
            _tagStyle = style;
            if (!IsOpen) return;
            for (int i = 0; i < _options.Count; i++) _tags[i].text = DescribeTags(_options[i]);
        }

        private void ReplaceOptions(IReadOnlyList<UpgradeData> options)
        {
            _options.Clear();
            if (options == null) return;
            for (int i = 0; i < options.Count && _options.Count < _cards.Length; i++)
                if (options[i] != null) _options.Add(options[i]);
        }

        private void Reroll()
        {
            if (_reroll == null || RerollsLeft <= 0) return;
            IReadOnlyList<UpgradeData> options = _reroll();
            RerollsLeft--;
            bool hasOption = false;
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                    if (options[i] != null) { hasOption = true; break; }
            }
            // 잘못된 빈 결과로 선택 창에 갇히지 않도록 기존 선택지를 유지한다.
            if (hasOption) ReplaceOptions(options);
            _selected = Mathf.Clamp(_selected, 0, _options.Count - 1);
            RefreshCards();
        }

        private void RefreshCards()
        {
            _shownAt = Time.unscaledTime;
            for (int i = 0; i < _cards.Length; i++)
            {
                _cards[i].gameObject.SetActive(i < _options.Count);
                if (i >= _options.Count) continue;
                UpgradeData upgrade = _options[i];
                _cards[i].anchoredPosition = new Vector2((i - (_options.Count - 1) * 0.5f) * 480f, 0f);
                _cards[i].localScale = Vector3.zero;
                _colors[i] = RarityColor(upgrade.rarity);
                _rarities[i].color = _colors[i];
                _rarities[i].text = RarityName(upgrade.rarity);
                _names[i].text = upgrade.displayName;
                _tags[i].text = DescribeTags(upgrade);
                _descriptions[i].text = string.IsNullOrWhiteSpace(upgrade.description)
                    ? UpgradeText.Describe(upgrade.modifiers) : upgrade.description;
                _stackTexts[i].text = $"(현재 {_stacks?.Invoke(upgrade) ?? 0} / 최대 {upgrade.maxStacks})";
            }

            _hint.text = "← → 선택 · J / Space / Enter 결정 · 1 2 3 바로 선택";
            if (_reroll != null && RerollsLeft > 0)
                _hint.text += $"\nR 다시 뽑기 (남은 {RerollsLeft}회) · 패드 Y / △";
            RefreshFrames();
        }

        private string DescribeTags(UpgradeData upgrade)
        {
            if (upgrade.tags == null) return string.Empty;
            var text = new StringBuilder();
            foreach (UpgradeTag tag in upgrade.tags)
            {
                if (text.Length > 0) text.Append(" · ");
                (string name, Color color) style = _tagStyle != null ? _tagStyle(tag) : (tag.ToString(), Color.white);
                text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(style.color)).Append('>')
                    .Append(style.name).Append("</color>");
            }
            return text.ToString();
        }

        private void Update()
        {
            if (!IsOpen) return;
            float elapsed = Time.unscaledTime - _shownAt;
            for (int i = 0; i < _options.Count; i++)
            {
                float progress = Mathf.Clamp01((elapsed - i * _popStagger) / Mathf.Max(0.01f, _popDuration));
                float pop = 1f - Mathf.Pow(1f - progress, 3f);
                _cards[i].localScale = Vector3.one * (pop * (i == _selected ? _selectedScale : 1f));
            }
        }

        private void BuildCard(int index, Transform parent)
        {
            RectTransform card = UiFactory.CreateRect($"Card{index + 1}", parent,
                Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(420f, 520f));
            _cards[index] = card;
            _frames[index] = UiFactory.CreateImage("Frame", card, Color.white);
            UiFactory.Stretch(_frames[index].rectTransform);
            Image body = UiFactory.CreateImage("Body", card, new Color(0.08f, 0.1f, 0.16f, 1f));
            UiFactory.Stretch(body.rectTransform);
            body.rectTransform.offsetMin = Vector2.one * 5f;
            body.rectTransform.offsetMax = Vector2.one * -5f;
            _rarities[index] = CreateLabel("Rarity", card, "", 26, Color.white,
                new Vector2(0f, 207f), new Vector2(360f, 42f));
            _names[index] = CreateLabel("Name", card, "", 38, Color.white,
                new Vector2(0f, 130f), new Vector2(364f, 100f));
            _tags[index] = CreateLabel("Tags", card, "", 22, Color.white,
                new Vector2(0f, 65f), new Vector2(364f, 36f));
            _tags[index].supportRichText = true;
            _descriptions[index] = CreateLabel("Description", card, "", 27, Color.white,
                new Vector2(0f, -40f), new Vector2(364f, 160f));
            _stackTexts[index] = CreateLabel("Stacks", card, "", 24, new Color(0.8f, 0.83f, 0.9f),
                new Vector2(0f, -160f), new Vector2(364f, 42f));
            CreateLabel("Key", card, $"[{index + 1}]", 30, Color.white,
                new Vector2(0f, -218f), new Vector2(364f, 42f));
        }

        private static Text CreateLabel(string name, Transform parent, string value, int size,
            Color color, Vector2 position, Vector2 dimensions)
        {
            Text text = UiFactory.CreateText(name, parent, size, color, TextAnchor.MiddleCenter);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = size;
            text.text = value;
            return text;
        }

        private void AddButton(string name, Action callback, params string[] bindings)
        {
            InputAction action = _actions.AddAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            action.performed += context =>
            {
                if (IsOpen && Time.unscaledTime - _shownAt >= Mathf.Max(0.35f, _inputDelay)) callback();
            };
        }

        private void Move(int direction)
        {
            _selected = (_selected + direction + _options.Count) % _options.Count;
            RefreshFrames();
        }

        private void RefreshFrames()
        {
            for (int i = 0; i < _options.Count; i++)
                _frames[i].color = i == _selected ? Color.Lerp(_colors[i], Color.white, 0.4f) : _colors[i] * 0.7f;
        }

        private void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= _options.Count) return;
            UpgradeData chosen = _options[index];
            Action<UpgradeData> callback = _onChosen;
            Hide();
            callback?.Invoke(chosen);
        }

        private void Hide()
        {
            IsOpen = false;
            _onChosen = null;
            _stacks = null;
            _reroll = null;
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

        private static Color RarityColor(UpgradeRarity rarity)
        {
            switch (rarity)
            {
                case UpgradeRarity.Rare: return new Color(0.35f, 0.75f, 1f);
                case UpgradeRarity.Epic: return new Color(1f, 0.78f, 0.22f);
                default: return new Color(0.82f, 0.85f, 0.9f);
            }
        }

        private static string RarityName(UpgradeRarity rarity)
        {
            switch (rarity)
            {
                case UpgradeRarity.Rare: return "희귀";
                case UpgradeRarity.Epic: return "영웅";
                default: return "일반";
            }
        }
    }
}
