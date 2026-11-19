using System;
using System.Collections.Generic;
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
        private readonly Image[] _icons = new Image[3];
        private readonly Text[] _rarities = new Text[3];
        private readonly Text[] _names = new Text[3];
        private readonly Text[] _tags = new Text[3];
        private readonly Text[] _descriptions = new Text[3];
        private readonly Text[] _stackTexts = new Text[3];
        private readonly Outline[] _glows = new Outline[3];
        private readonly Color[] _colors = new Color[3];
        private GameObject _root;
        private InputActionMap _actions;
        private Action<UpgradeData> _onChosen;
        private Func<UpgradeData, int> _stacks;
        private Func<IReadOnlyList<UpgradeData>> _reroll;
        private Func<UpgradeTag, (string name, Color color)> _tagStyle;
        private Func<UpgradeTag, Sprite> _tagIcon;
        private Text _hint;
        private int _selected;
        private float _shownAt;

        public IReadOnlyList<UpgradeData> Options => _options;
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
                "<Keyboard>/enter", "<Keyboard>/x", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
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

        public void SetTagStyle(Func<UpgradeTag, (string name, Color color)> style, Func<UpgradeTag, Sprite> icon = null)
        {
            _tagStyle = style;
            _tagIcon = icon;
            if (!IsOpen) return;
            for (int i = 0; i < _options.Count; i++) RefreshTags(i, _options[i]);
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
                _glows[i].effectColor = new Color(_colors[i].r, _colors[i].g, _colors[i].b, 0.22f);
                _rarities[i].color = _colors[i];
                _rarities[i].text = RarityName(upgrade.rarity);
                _names[i].text = upgrade.displayName;
                RefreshTags(i, upgrade);
                _descriptions[i].text = string.IsNullOrWhiteSpace(upgrade.description)
                    ? UpgradeText.Describe(upgrade.modifiers) : upgrade.description;
                _stackTexts[i].text = $"(현재 {_stacks?.Invoke(upgrade) ?? 0} / 최대 {upgrade.maxStacks})";
            }

            _hint.text = "← → 선택 · X / J / Space / Enter 결정 · 1 2 3 바로 선택";
            if (_reroll != null && RerollsLeft > 0)
                _hint.text += $"\nR 다시 뽑기 (남은 {RerollsLeft}회) · 패드 Y / △";
            RefreshFrames();
        }

        private void RefreshTags(int index, UpgradeData upgrade)
        {
            _icons[index].sprite = upgrade.tags != null && upgrade.tags.Length > 0 ? _tagIcon?.Invoke(upgrade.tags[0]) : null;
            _icons[index].enabled = _icons[index].sprite != null;
            Transform root = _tags[index].transform;
            foreach (Transform child in root)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            if (upgrade.tags == null || upgrade.tags.Length == 0) return;
            float width = Mathf.Min(110f, 350f / upgrade.tags.Length);
            for (int i = 0; i < upgrade.tags.Length; i++)
            {
                UpgradeTag tag = upgrade.tags[i];
                (string name, Color color) style = _tagStyle != null ? _tagStyle(tag) : (tag.ToString(), Color.white);
                var chip = UiFactory.CreatePanel(root, new Vector2(width - 6f, 30f), "TagChip", style.color);
                chip.anchoredPosition = new Vector2((i - (upgrade.tags.Length - 1) * 0.5f) * width, 0f);
                var label = UiFactory.Label("Tag", chip, style.name, 18, Vector2.zero, new Vector2(width - 10f, 26f), style.color);
                Sprite sprite = _tagIcon?.Invoke(tag);
                if (sprite != null)
                {
                    Image icon = UiFactory.CreateIcon("Icon", chip, sprite, style.color);
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    icon.rectTransform.anchoredPosition = new Vector2(UiTheme.TagIconInset + UiTheme.TagIconSize * 0.5f, 0f);
                    icon.rectTransform.sizeDelta = Vector2.one * UiTheme.TagIconSize;
                    label.rectTransform.anchoredPosition = Vector2.right * (UiTheme.TagIconSize * 0.5f);
                    label.rectTransform.sizeDelta -= Vector2.right * UiTheme.TagIconSize;
                }
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = 18;
            }
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
            RectTransform card = UiFactory.CreatePanel(parent, new Vector2(420f, 520f), $"Card{index + 1}", background: UiTheme.CardBackground);
            _cards[index] = card;
            _frames[index] = card.GetComponent<Image>();
            var glow = card.gameObject.AddComponent<Outline>();
            _glows[index] = glow;
            glow.effectDistance = new Vector2(4f, -4f);
            glow.effectColor = new Color(1f, 0.8f, 0.3f, 0.2f);
            _rarities[index] = CreateLabel("Rarity", card, "", 26, Color.white,
                new Vector2(0f, UiTheme.CardRarityY), new Vector2(360f, 42f));
            _icons[index] = UiFactory.CreateIcon("UpgradeIcon", card, null, Color.white);
            _icons[index].rectTransform.anchoredPosition = Vector2.up * UiTheme.CardIconY;
            _icons[index].rectTransform.sizeDelta = Vector2.one * UiTheme.CardIconSize;
            _names[index] = CreateLabel("Name", card, "", 38, Color.white,
                new Vector2(0f, UiTheme.CardNameY), new Vector2(364f, UiTheme.CardNameHeight));
            _tags[index] = CreateLabel("Tags", card, "", 22, Color.white,
                new Vector2(0f, UiTheme.CardTagsY), new Vector2(364f, 36f));
            _tags[index].supportRichText = true;
            _descriptions[index] = CreateLabel("Description", card, "", 27, Color.white,
                new Vector2(0f, UiTheme.CardBodyY), new Vector2(364f, 160f));
            _stackTexts[index] = CreateLabel("Stacks", card, "", 24, new Color(0.8f, 0.83f, 0.9f),
                new Vector2(0f, -160f), new Vector2(364f, 42f));
            var badge = UiFactory.CreatePanel(card, new Vector2(60f, 42f), "KeyBadge");
            badge.anchoredPosition = new Vector2(0f, -218f);
            CreateLabel("Key", badge, $"[{index + 1}]", 30, Color.white,
                Vector2.zero, new Vector2(56f, 38f));
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

        public void Hide()
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

        private static Color RarityColor(UpgradeRarity rarity) => UiTheme.Rarity(rarity);

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
