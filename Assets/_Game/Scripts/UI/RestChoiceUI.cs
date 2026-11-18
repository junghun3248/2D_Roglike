using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class RestChoiceUI : MonoBehaviour
    {
        [SerializeField] private float _inputDelay = 0.35f;
        private GameObject _root;
        private Text _choices;
        private InputActionMap _actions;
        private Action<bool> _onChosen;
        private float _shownAt;
        private int _selected;
        private string _healLabel;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _root = UiFactory.CreateCanvas("RestChoice", 60, transform).gameObject;
            var dim = UiFactory.CreateImage("Dim", _root.transform, new Color(0.04f, 0.08f, 0.12f, 0.95f));
            UiFactory.Stretch(dim.rectTransform);
            _choices = UiFactory.CreateText("Choices", _root.transform, 40, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_choices.rectTransform);
            _actions = new InputActionMap("RestChoice");
            Add("Heal", () => Choose(true), "<Keyboard>/1");
            Add("Upgrade", () => Choose(false), "<Keyboard>/2");
            Add("Left", () => Select(0), "<Keyboard>/leftArrow", "<Gamepad>/dpad/left");
            Add("Right", () => Select(1), "<Keyboard>/rightArrow", "<Gamepad>/dpad/right");
            Add("Confirm", () => Choose(_selected == 0), "<Keyboard>/space", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            Hide();
        }

        public void Show(float healFraction, Action<bool> onChosen)
        {
            _onChosen = onChosen;
            _healLabel = $"회복: 최대 체력 {healFraction:P0}";
            _shownAt = Time.unscaledTime;
            IsOpen = true;
            Select(0);
            _root.SetActive(true);
            _actions.Enable();
        }

        private void Select(int index)
        {
            _selected = index;
            _choices.text = $"휴식\n\n{(index == 0 ? "▶ " : "")} [1] {_healLabel}\n\n{(index == 1 ? "▶ " : "")} [2] 강화 1개 (희귀 이상 우선)\n\n← → 선택 · Space / Enter 결정";
        }

        private void Choose(bool heal)
        {
            if (!IsOpen) return;
            var callback = _onChosen;
            Hide();
            callback?.Invoke(heal);
        }

        private void Add(string name, Action callback, params string[] bindings)
        {
            var action = _actions.AddAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            action.performed += _ =>
            {
                if (IsOpen && Time.unscaledTime - _shownAt >= _inputDelay) callback();
            };
        }

        public void Hide()
        {
            IsOpen = false;
            _onChosen = null;
            _actions?.Disable();
            if (_root != null) _root.SetActive(false);
        }
        private void OnDisable() => Hide();
        private void OnDestroy() { Hide(); _actions?.Dispose(); }
    }
}
