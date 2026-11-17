using System.Collections.Generic;
using System.Text;
using Roglike.Data;
using Roglike.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class SynergyPanelUI : MonoBehaviour
    {
        [SerializeField] private Vector2 _position = new Vector2(28f, -130f);
        [SerializeField] private float _rowHeight = 56f;
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
        private float _toastUntil;

        private void Awake() => EnsureUI();

        public void Bind(SynergyTracker tracker)
        {
            Unsubscribe();
            EnsureUI();
            _tracker = tracker;
            _toasts.Clear();
            _toast.gameObject.SetActive(false);
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
                new Vector2(0f, 1f), _position, new Vector2(360f, 400f));
            _toast = UiFactory.CreateText("SynergyToast", _canvas.transform, 40, Color.white, TextAnchor.MiddleCenter);
            _toast.rectTransform.anchorMin = _toast.rectTransform.anchorMax = Vector2.one * 0.5f;
            _toast.rectTransform.sizeDelta = new Vector2(1200f, 100f);
            _toast.gameObject.SetActive(false);
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
            for (int i = 0; i < visible.Count; i++)
            {
                SynergyData synergy = visible[i];
                int active = _tracker.ActiveTierIndex(synergy.tag);
                int count = _tracker.Count(synergy.tag);
                RectTransform rect = UiFactory.CreateRect(synergy.name, _list, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(0f, -i * _rowHeight), new Vector2(360f, _rowHeight));
                CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.alpha = active >= 0 ? 1f : 0.5f;
                Image icon = UiFactory.CreateImage("Color", rect, synergy.color);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(14f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(22f, 22f);
                Text label = UiFactory.CreateText("Progress", rect, 22, Color.white, TextAnchor.MiddleLeft);
                UiFactory.Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(38f, 0f);
                var pips = new StringBuilder();
                int tierCount = synergy.tiers?.Length ?? 0;
                for (int t = 0; t < tierCount; t++) pips.Append(t <= active ? '●' : '○');
                string progress = active + 1 < tierCount && synergy.tiers[active + 1] != null
                    ? $"{count} / {synergy.tiers[active + 1].threshold}" : $"{count} / MAX";
                label.text = $"{synergy.displayName}  {progress}\n{pips}";
                _rows.Add(synergy, new Row { rect = rect, group = group, alpha = group.alpha });
            }
        }

        private void OnTierReached(SynergyData synergy, int tierIndex)
        {
            if (_rows.TryGetValue(synergy, out Row row)) row.pulseUntil = Time.unscaledTime + _pulseDuration;
            _toasts.Enqueue($"{synergy.displayName} {tierIndex + 1}단계 발동!");
            if (!_toast.gameObject.activeSelf) ShowNextToast();
        }

        private void ShowNextToast()
        {
            _toast.gameObject.SetActive(_toasts.Count > 0);
            if (_toasts.Count == 0) return;
            _toast.text = _toasts.Dequeue();
            _toastUntil = Time.unscaledTime + _toastDuration;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            foreach (Row row in _rows.Values)
            {
                float remaining = Mathf.Clamp01((row.pulseUntil - now) / _pulseDuration);
                float pulse = Mathf.Sin(remaining * Mathf.PI);
                row.rect.localScale = Vector3.one * (1f + (_pulseScale - 1f) * pulse);
                row.group.alpha = Mathf.Lerp(row.alpha, 1f, pulse);
            }
            if (_toast != null && _toast.gameObject.activeSelf && now >= _toastUntil) ShowNextToast();
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
