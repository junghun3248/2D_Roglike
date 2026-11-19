using System.Collections.Generic;
using System.Text;
using Roglike.Data;
using Roglike.Progression;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Roglike.UI
{
    public class SynergyPanelUI : MonoBehaviour
    {
        [SerializeField] private Vector2 _badgePosition = new Vector2(32f, -32f);
        [SerializeField] private float _badgeHeight = 52f;
        [SerializeField, Min(0.01f)] private float _pulseDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float _toastDuration = 1.2f;
        [SerializeField] private float _pulseScale = 1.12f;

        private sealed class Row
        {
            public RectTransform rect;
            public CanvasGroup group;
            public float alpha;
            public float pulseUntil;
        }

        private readonly Dictionary<SynergyData, Row> _rows = new Dictionary<SynergyData, Row>();
        private readonly Queue<string> _toasts = new Queue<string>();
        private SynergyTracker _tracker;
        private Canvas _canvas;
        private RectTransform _list;
        private Text _toast;
        private RectTransform _toastPlate, _detailPanel;
        private Text _details;
        private float _toastUntil;

        private void Awake() => EnsureUI();

        public void Bind(SynergyTracker tracker)
        {
            Unsubscribe();
            EnsureUI();
            _tracker = tracker;
            _toasts.Clear();
            _toastPlate.gameObject.SetActive(false);
            if (_tracker != null)
            {
                _tracker.Changed += Rebuild;
                _tracker.TierReached += OnTierReached;
            }
            Rebuild();
        }

        private void EnsureUI()
        {
            if (_canvas != null) return;
            _canvas = UiFactory.CreateCanvas("Synergies", 32, transform);
            _list = UiFactory.CreateRect("Rows", _canvas.transform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), _badgePosition, new Vector2(360f, 400f));
            _detailPanel = UiFactory.CreatePanel(_canvas.transform, new Vector2(600f, 500f), "SynergyDetails");
            _detailPanel.anchorMin = _detailPanel.anchorMax = new Vector2(0f, 1f);
            _detailPanel.pivot = new Vector2(0f, 1f);
            _detailPanel.anchoredPosition = new Vector2(145f, -32f);
            _details = UiFactory.CreateText("Details", _detailPanel, 22, UiTheme.Text, TextAnchor.UpperLeft);
            UiFactory.Stretch(_details.rectTransform);
            _details.rectTransform.offsetMin = new Vector2(24f, 20f);
            _details.rectTransform.offsetMax = new Vector2(-24f, -20f);
            _details.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailPanel.gameObject.SetActive(false);
            _toastPlate = UiFactory.CreatePanel(_canvas.transform, new Vector2(700f, 90f), "SynergyToastPlate");
            _toast = UiFactory.CreateText("SynergyToast", _toastPlate, 36, UiTheme.Gold, TextAnchor.MiddleCenter);
            _toast.rectTransform.anchorMin = _toast.rectTransform.anchorMax = Vector2.one * 0.5f;
            _toast.rectTransform.sizeDelta = new Vector2(680f, 80f);
            _toastPlate.gameObject.SetActive(false);
        }

        private void Rebuild()
        {
            var visible = new List<SynergyData>();
            if (_tracker != null)
                foreach (SynergyData synergy in _tracker.Synergies)
                    if (_tracker.Count(synergy.tag) > 0) visible.Add(synergy);
            visible.Sort((a, b) =>
            {
                int tier = _tracker.ActiveTierIndex(b.tag).CompareTo(_tracker.ActiveTierIndex(a.tag));
                if (tier != 0) return tier;
                int count = _tracker.Count(b.tag).CompareTo(_tracker.Count(a.tag));
                return count != 0 ? count : a.tag.CompareTo(b.tag);
            });

            foreach (Row row in _rows.Values)
            {
                row.rect.gameObject.SetActive(false);
                Destroy(row.rect.gameObject);
            }
            _rows.Clear();
            var details = new StringBuilder();
            for (int i = 0; i < visible.Count; i++)
            {
                SynergyData synergy = visible[i];
                int active = _tracker.ActiveTierIndex(synergy.tag);
                int count = _tracker.Count(synergy.tag);
                RectTransform rect = UiFactory.CreateRect(synergy.name, _list, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(0f, -i * _badgeHeight), new Vector2(96f, _badgeHeight));
                CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.alpha = active >= 0 ? 1f : 0.5f;
                Image icon = UiFactory.CreateImage("Color", rect, synergy.color);
                icon.sprite = UiFactory.RoundedSquare;
                icon.type = Image.Type.Sliced;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(32f, 32f);
                Text label = UiFactory.CreateText("Progress", rect, 22, Color.white, TextAnchor.MiddleLeft);
                UiFactory.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(38f, 0f);
                var pips = new StringBuilder();
                int tierCount = synergy.tiers?.Length ?? 0;
                for (int t = 0; t < tierCount; t++) pips.Append(t <= active ? '●' : '○');
                string progress = active + 1 < tierCount && synergy.tiers[active + 1] != null
                    ? $"{count} / {synergy.tiers[active + 1].threshold}" : $"{count} / MAX";
                label.text = progress;
                details.Append(synergy.displayName).Append("  ").Append(progress).Append("  ").Append(pips).AppendLine();
                for (int t = 0; t < tierCount; t++)
                {
                    var tier = synergy.tiers[t];
                    if (tier == null) continue;
                    details.Append(t == active ? "[ON] " : "").Append(tier.threshold).Append(": ")
                        .Append(UpgradeText.Describe(tier.modifiers)).AppendLine();
                }
                details.AppendLine();
                _rows.Add(synergy, new Row { rect = rect, group = group, alpha = group.alpha });
            }
            _details.text = details.ToString();
            _detailPanel.sizeDelta = new Vector2(600f, Mathf.Min(950f, Mathf.Max(150f, _details.preferredHeight + 40f)));
        }

        private void OnTierReached(SynergyData synergy, int tierIndex)
        {
            if (_rows.TryGetValue(synergy, out Row row)) row.pulseUntil = Time.unscaledTime + _pulseDuration;
            _toasts.Enqueue($"{synergy.displayName} {tierIndex + 1}단계 발동!");
            if (!_toastPlate.gameObject.activeSelf) ShowNextToast();
        }

        private void ShowNextToast()
        {
            _toastPlate.gameObject.SetActive(_toasts.Count > 0);
            if (_toasts.Count == 0) return;
            _toast.text = _toasts.Dequeue();
            _toastUntil = Time.unscaledTime + _toastDuration;
        }

        private void Update()
        {
            _detailPanel.gameObject.SetActive(Keyboard.current != null && Keyboard.current.tabKey.isPressed && _rows.Count > 0);
            float now = Time.unscaledTime;
            foreach (Row row in _rows.Values)
            {
                float remaining = Mathf.Clamp01((row.pulseUntil - now) / _pulseDuration);
                float pulse = Mathf.Sin(remaining * Mathf.PI);
                row.rect.localScale = Vector3.one * (1f + (_pulseScale - 1f) * pulse);
                row.group.alpha = Mathf.Lerp(row.alpha, 1f, pulse);
            }
            if (_toast != null && _toastPlate.gameObject.activeSelf && now >= _toastUntil) ShowNextToast();
        }

        private void Unsubscribe()
        {
            if (_tracker == null) return;
            _tracker.Changed -= Rebuild;
            _tracker.TierReached -= OnTierReached;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
