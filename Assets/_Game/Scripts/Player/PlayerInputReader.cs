using UnityEngine;
using UnityEngine.InputSystem;

namespace Roglike.Player
{
    /// <summary>
    /// 키보드·패드 입력. M1에서는 바인딩을 코드로 정의한다 (키 설정 화면이 생기면 .inputactions 에셋으로 이동).
    /// 키보드: ←/→ 이동, ↑/↓ 공격 방향, Space 점프, A·마우스 왼쪽 공격, S·마우스 오른쪽 강공격/차지, Left Shift·D 대시, F 오버드라이브, Q/W/E/R 스킬.
    /// 패드: 왼쪽 스틱·십자키 이동, A 점프, X 공격, RB 대시, LB 오버드라이브.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        private InputAction _move;
        private InputAction _moveY;
        private InputAction _heavy;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _dash;
        private InputAction _overdrive;
        private InputAction[] _skills;

        public bool SkillPressed(int slot) => isActiveAndEnabled && slot >= 0 && slot < _skills.Length && _skills[slot].WasPressedThisFrame();

        public float MoveY => _moveY.ReadValue<float>();
        public bool HeavyPressed => _heavy.WasPressedThisFrame();
        public bool HeavyHeld => _heavy.IsPressed();
        public bool HeavyReleased => _heavy.WasReleasedThisFrame();

        public float MoveX => _move.ReadValue<float>();
        public bool JumpPressed => _jump.WasPressedThisFrame();
        public bool JumpHeld => _jump.IsPressed();
        public bool AttackPressed => _attack.WasPressedThisFrame();
        public bool AttackHeld => _attack.IsPressed();
        public bool DashPressed => _dash.WasPressedThisFrame();
        public bool OverdrivePressed => _overdrive.WasPressedThisFrame();

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Axis");
            _move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick/x");
            _move.AddBinding("<Gamepad>/dpad/x");

            _moveY = new InputAction("MoveY", InputActionType.Value, expectedControlType: "Axis");
            _moveY.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            _moveY.AddBinding("<Gamepad>/leftStick/y");
            _moveY.AddBinding("<Gamepad>/dpad/y");
            _heavy = CreateButton("Heavy", "<Keyboard>/s", "<Mouse>/rightButton", "<Gamepad>/buttonNorth");
            _jump = CreateButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            _attack = CreateButton("Attack", "<Keyboard>/a", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            _dash = CreateButton("Dash", "<Keyboard>/d", "<Keyboard>/leftShift", "<Gamepad>/rightShoulder");
            _overdrive = CreateButton("Overdrive", "<Keyboard>/f", "<Gamepad>/leftShoulder");
            _skills = new[]
            {
                CreateButton("Skill1", "<Keyboard>/q", "<Gamepad>/buttonEast"),
                CreateButton("Skill2", "<Keyboard>/w", "<Gamepad>/rightTrigger"),
                CreateButton("Skill3", "<Keyboard>/e", "<Gamepad>/leftTrigger"),
                CreateButton("Skill4", "<Keyboard>/r", "<Gamepad>/rightStickPress"),
            };
        }

        private void OnEnable()
        {
            _move.Enable();
            _moveY.Enable();
            _heavy.Enable();
            _jump.Enable();
            _attack.Enable();
            _dash.Enable();
            _overdrive.Enable();
            foreach (InputAction skill in _skills) skill.Enable();
        }

        private void OnDisable()
        {
            _move.Disable();
            _moveY.Disable();
            _heavy.Disable();
            _jump.Disable();
            _attack.Disable();
            _dash.Disable();
            _overdrive.Disable();
            foreach (InputAction skill in _skills) skill.Disable();
        }

        private void OnDestroy()
        {
            _move.Dispose();
            _moveY.Dispose();
            _heavy.Dispose();
            _jump.Dispose();
            _attack.Dispose();
            _dash.Dispose();
            _overdrive.Dispose();
            foreach (InputAction skill in _skills) skill.Dispose();
        }

        private static InputAction CreateButton(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings)
            {
                action.AddBinding(binding);
            }

            return action;
        }
    }
}
