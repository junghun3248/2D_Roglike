using Roglike.Combat;
using Roglike.Core;
using Roglike.Player;
using Roglike.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        private InputActionMap _actions;
        private GameObject _root;
        private Text _options, _controls;
        private PlayerInputReader _input;
        private Health _health;
        private RunManager _run;
        private UpgradeChoiceUI _upgrade;
        private RestChoiceUI _rest;
        private RunResultScreen _result;
        private MetaShopUI _shop;
        private bool _inputWasEnabled;
        private int _selected;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            var canvas = UiFactory.CreateCanvas("PauseMenu", 80, transform);
            _root = canvas.gameObject;
            var dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            UiFactory.Stretch(dim.rectTransform);
            var panel = UiFactory.CreatePanel(canvas.transform, new Vector2(850f, 760f));
            UiFactory.Label("Title", panel, "잠시 쉬어가기", 44, new Vector2(0f, 300f), new Vector2(700f, 70f), UiTheme.Gold);
            _options = UiFactory.Label("Options", panel, "", 32, new Vector2(0f, 160f), new Vector2(700f, 160f));
            _controls = UiFactory.Label("Controls", panel,
                "이동 ←→     점프 Z     공격 X\n대시 C     강공격/차지 V\n스킬 A S D F     오버드라이브 Space\n↑+X 띄우기     공중 ↓+X 내려찍기",
                26, new Vector2(0f, -95f), new Vector2(760f, 220f), UiTheme.Muted);
            UiFactory.Label("Navigation", panel, "↑↓ 선택 · X / Enter 결정 · Esc / Start 계속", 22,
                new Vector2(0f, -310f), new Vector2(780f, 40f), UiTheme.Muted);
            _root.SetActive(false);
            _actions = new InputActionMap("PauseMenu");
            Add("Toggle", Toggle, "<Keyboard>/escape", "<Gamepad>/start");
            Add("Up", () => Move(-1), "<Keyboard>/upArrow", "<Keyboard>/leftArrow", "<Gamepad>/dpad/up");
            Add("Down", () => Move(1), "<Keyboard>/downArrow", "<Keyboard>/rightArrow", "<Gamepad>/dpad/down");
            Add("Confirm", Confirm, "<Keyboard>/x", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
        }

        private void Start()
        {
            _input = FindFirstObjectByType<PlayerInputReader>();
            _health = _input != null ? _input.GetComponent<Health>() : null;
            _run = FindFirstObjectByType<RunManager>();
            _upgrade = FindFirstObjectByType<UpgradeChoiceUI>();
            _rest = FindFirstObjectByType<RestChoiceUI>();
            _result = FindFirstObjectByType<RunResultScreen>();
            _shop = FindFirstObjectByType<MetaShopUI>();
        }

        private bool OtherMenuOpen => (_upgrade != null && _upgrade.IsOpen) || (_rest != null && _rest.IsOpen)
            || (_result != null && _result.IsOpen) || (_shop != null && _shop.IsOpen);

        public void Toggle()
        {
            if (IsOpen) { Resume(); return; }
            if (TimeController.Instance == null || TimeController.Instance.IsPaused || OtherMenuOpen
                || _input == null || !_input.enabled || (_health != null && _health.IsDead)) return;
            _inputWasEnabled = _input.enabled;
            _input.enabled = false;
            TimeController.Instance.SetPaused(true);
            IsOpen = true;
            _selected = 0;
            _controls.gameObject.SetActive(false);
            Refresh();
            _root.SetActive(true);
        }

        public void Resume()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _root.SetActive(false);
            // 다른 메뉴로 소유권이 넘어갔다면 그 메뉴의 정지를 풀지 않는다.
            if (!OtherMenuOpen)
            {
                TimeController.Instance?.SetPaused(false);
                if (_input != null && (_health == null || !_health.IsDead)) _input.enabled = _inputWasEnabled;
            }
        }

        private void Update() { if (IsOpen && OtherMenuOpen) Resume(); }
        private void Move(int direction) { if (!IsOpen) return; _selected = (_selected + direction + 3) % 3; Refresh(); }
        private void Refresh()
        {
            _options.text = (_selected == 0 ? "▶ " : "") + "계속하기\n"
                + (_selected == 1 ? "▶ " : "") + "조작법\n"
                + (_selected == 2 ? "▶ " : "") + "런 다시 시작";
        }
        private void Confirm()
        {
            if (!IsOpen) return;
            if (_selected == 0) Resume();
            else if (_selected == 1) _controls.gameObject.SetActive(!_controls.gameObject.activeSelf);
            else
            {
                Resume();
                if (_run != null) _run.Restart();
                else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
        private void Add(string name, System.Action callback, params string[] bindings)
        {
            var action = _actions.AddAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            action.performed += _ => callback();
        }
        private void OnEnable() => _actions?.Enable();
        private void OnDisable() { Resume(); _actions?.Disable(); }
        private void OnDestroy() { _actions?.Dispose(); }
    }
}
