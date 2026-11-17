using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    /// <summary>
    /// 데미지 숫자. 같은 대상을 연타하면 머리 위로 차곡차곡 쌓인다. 치명타는 크고 다른 색.
    /// 히트스톱 중에도 튀어나오도록 실시간 기준으로 움직인다.
    /// </summary>
    public class DamageNumberSpawner : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private int _fontSize = 46;
        [SerializeField] private int _critFontSize = 64;
        [SerializeField] private Color _normalColor = new Color(1f, 0.62f, 0.2f);
        [SerializeField] private Color _critColor = new Color(1f, 0.25f, 0.5f);
        [SerializeField] private Color _playerHurtColor = new Color(0.7f, 0.45f, 1f);

        [Header("Motion (실시간)")]
        [SerializeField] private float _headOffset = 1.1f;
        [SerializeField] private float _stackSpacing = 0.55f;
        [SerializeField] private float _stackResetTime = 0.7f;
        [SerializeField] private float _lifetime = 0.9f;
        [SerializeField] private float _riseSpeed = 1.2f;
        [SerializeField] private float _popScale = 1.8f;
        [SerializeField] private float _popDuration = 0.08f;

        private sealed class Entry
        {
            public Text Text;
            public RectTransform Rect;
            public Vector3 WorldPosition;
            public float Age;
            public Color Color;
        }

        private struct Stack
        {
            public int Count;
            public float LastTime;
        }

        private readonly List<Entry> _active = new List<Entry>(64);
        private readonly Stack<Entry> _pool = new Stack<Entry>(64);
        private readonly Dictionary<Health, Stack> _stacks = new Dictionary<Health, Stack>();
        private Canvas _canvas;
        private Camera _camera;

        private void Awake()
        {
            _canvas = UiFactory.CreateCanvas("DamageNumbers", 40, transform);
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
        }

        private void OnAnyDamaged(Health target, DamageInfo info)
        {
            float now = Time.unscaledTime;
            _stacks.TryGetValue(target, out Stack stack);
            if (now - stack.LastTime > _stackResetTime)
            {
                stack.Count = 0;
            }

            int index = stack.Count;
            stack.Count++;
            stack.LastTime = now;
            _stacks[target] = stack;

            bool isPlayer = target.Team == Team.Player;
            Entry entry = Rent();
            entry.Text.text = NumberFormat.ToKorean(info.Amount);
            entry.Text.fontSize = info.IsCritical ? _critFontSize : _fontSize;
            entry.Color = isPlayer ? _playerHurtColor : info.IsCritical ? _critColor : _normalColor;
            entry.Text.color = entry.Color;
            entry.Age = 0f;
            entry.WorldPosition = target.transform.position + new Vector3(Random.Range(-0.15f, 0.15f), _headOffset + index * _stackSpacing, 0f);
            entry.Rect.SetAsLastSibling();
            _active.Add(entry);
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Entry entry = _active[i];
                entry.Age += dt;
                if (entry.Age >= _lifetime)
                {
                    Return(entry);
                    _active.RemoveAt(i);
                    continue;
                }

                entry.WorldPosition.y += _riseSpeed * dt;
                Vector3 screen = _camera.WorldToScreenPoint(entry.WorldPosition);
                entry.Rect.position = screen;

                float scale = entry.Age < _popDuration
                    ? Mathf.Lerp(_popScale, 1f, entry.Age / _popDuration)
                    : 1f;
                entry.Rect.localScale = new Vector3(scale, scale, 1f);

                float fadeStart = _lifetime * 0.65f;
                float alpha = entry.Age < fadeStart ? 1f : 1f - (entry.Age - fadeStart) / (_lifetime - fadeStart);
                entry.Text.color = new Color(entry.Color.r, entry.Color.g, entry.Color.b, alpha);
            }
        }

        private Entry Rent()
        {
            if (_pool.Count > 0)
            {
                Entry pooled = _pool.Pop();
                pooled.Text.gameObject.SetActive(true);
                return pooled;
            }

            Text text = UiFactory.CreateText("DamageNumber", _canvas.transform, _fontSize, _normalColor, TextAnchor.MiddleCenter);
            var rect = (RectTransform)text.transform;
            rect.sizeDelta = new Vector2(400f, 80f);
            return new Entry { Text = text, Rect = rect };
        }

        private void Return(Entry entry)
        {
            entry.Text.gameObject.SetActive(false);
            _pool.Push(entry);
        }
    }
}
