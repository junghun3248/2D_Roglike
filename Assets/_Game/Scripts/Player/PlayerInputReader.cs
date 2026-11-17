using UnityEngine;
using UnityEngine.InputSystem;

namespace Roglike.Player
{
    /// <summary>
    /// 키보드·패드 입력. M1에서는 바인딩을 코드로 정의한다 (키 설정 화면이 생기면 .inputactions 에셋으로 이동).
    /// 키보드: A/D·방향키 이동, Space 점프, J·마우스 왼쪽 공격, K·Shift 대시, L·E 오버드라이브.
    /// 패드: 왼쪽 스틱·십자키 이동, A 점프, X 공격, RB 대시, LB 오버드라이브.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        private InputAction _move;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _dash;
        private InputAction _overdrive;

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
            _move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick/x");
            _move.AddBinding("<Gamepad>/dpad/x");

            _jump = CreateButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            _attack = CreateButton("Attack", "<Keyboard>/j", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            _dash = CreateButton("Dash", "<Keyboard>/k", "<Keyboard>/leftShift", "<Gamepad>/rightShoulder");
            _overdrive = CreateButton("Overdrive", "<Keyboard>/l", "<Keyboard>/e", "<Gamepad>/leftShoulder");
        }

        private void OnEnable()
        {
            _move.Enable();
            _jump.Enable();
            _attack.Enable();
            _dash.Enable();
            _overdrive.Enable();
        }

        private void OnDisable()
        {
            _move.Disable();
            _jump.Disable();
            _attack.Disable();
            _dash.Disable();
            _overdrive.Disable();
        }

        private void OnDestroy()
        {
            _move.Dispose();
            _jump.Dispose();
            _attack.Dispose();
            _dash.Dispose();
            _overdrive.Dispose();
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
