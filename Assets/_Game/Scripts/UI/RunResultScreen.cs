using System;
using Roglike.Core;
using Roglike.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Roglike.UI
{
    /// <summary>런 종료 화면 (승리/패배, 기록, 다시 시작). 일시정지 중에 뜨므로 실시간 기준.</summary>
    public class RunResultScreen : MonoBehaviour
    {
        [SerializeField] private float _inputDelay = 0.8f;

        private GameObject _root;
        private Text _title;
        private Text _body;
        private Text _hint;
        private InputAction _confirm;
        private Action _onRestart;
        private float _shownAt;

        public bool IsOpen => _root != null && _root.activeSelf;

        private void Awake()
        {
            Canvas canvas = UiFactory.CreateCanvas("RunResult", 70, transform);
            _root = canvas.gameObject;

            Image dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0.03f, 0.02f, 0.06f, 0.82f));
            UiFactory.Stretch((RectTransform)dim.transform);

            _title = UiFactory.CreateText("Title", canvas.transform, 96, Color.white, TextAnchor.MiddleCenter);
            Place(_title, new Vector2(0f, 250f), new Vector2(1200f, 140f));

            _body = UiFactory.CreateText("Body", canvas.transform, 38, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter);
            _body.lineSpacing = 1.3f;
            Place(_body, new Vector2(0f, -10f), new Vector2(1200f, 360f));

            _hint = UiFactory.CreateText("Hint", canvas.transform, 30, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            Place(_hint, new Vector2(0f, -300f), new Vector2(1200f, 60f));
            _hint.text = "J / Space / Enter  다시 시작";

            _confirm = new InputAction("Restart", InputActionType.Button);
            _confirm.AddBinding("<Keyboard>/j");
            _confirm.AddBinding("<Keyboard>/space");
            _confirm.AddBinding("<Keyboard>/enter");
            _confirm.AddBinding("<Keyboard>/r");
            _confirm.AddBinding("<Gamepad>/buttonSouth");

            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            _confirm?.Dispose();
        }

        public void Show(RunSummary summary, Action onRestart)
        {
            _onRestart = onRestart;
            _shownAt = Time.unscaledTime;
            _root.SetActive(true);
            _confirm.Enable();

            _title.text = summary.Victory ? "승리!" : "쓰러졌다…";
            _title.color = summary.Victory ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.45f, 0.5f);

            int minutes = Mathf.FloorToInt(summary.Seconds / 60f);
            int seconds = Mathf.FloorToInt(summary.Seconds % 60f);
            string reached = summary.Victory
                ? "보스 처치"
                : summary.RoomReached >= summary.RoomCount ? "보스전" : $"{summary.RoomReached}구역";
            _body.text =
                $"도달  {reached}\n" +
                $"시간  {minutes:00}:{seconds:00}\n" +
                $"처치  {summary.Kills}\n" +
                $"최고 데미지  {NumberFormat.ToKorean(summary.HighestHit)}\n" +
                $"선택한 강화  {summary.Upgrades}개";
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime - _shownAt < _inputDelay)
            {
                return;
            }

            float pulse = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
            _hint.color = new Color(1f, 1f, 1f, pulse);

            if (_confirm.WasPressedThisFrame())
            {
                _confirm.Disable();
                _onRestart?.Invoke();
            }
        }

        private static void Place(Text text, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
