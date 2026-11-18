using System;
using Roglike.Player;
using Roglike.Progression;
using UnityEngine;

namespace Roglike.Run
{
    public class Room : MonoBehaviour
    {
        [SerializeField] private float _minX;
        [SerializeField] private float _maxX;
        [SerializeField] private BoxCollider2D _entryTrigger;
        [SerializeField] private BoxCollider2D _leftDoor;
        [SerializeField] private BoxCollider2D _rightDoor;
        [SerializeField] private Transform _leftVisual;
        [SerializeField] private Transform _rightVisual;
        [SerializeField] private Transform _exitSign;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private Transform _playerEntry;
        [SerializeField] private float _gateSlideHeight = 14f;
        [SerializeField] private float _gateSpeed = 7f;
        [SerializeField] private float _signPulseSpeed = 4f;
        [SerializeField] private float _signPulseAmount = 0.12f;
        [Header("문 그림 (없으면 자리표시 문이 위로 미끄러진다)")]
        [SerializeField] private Sprite _gateClosed;
        [SerializeField] private Sprite _gateOpen;
        private SpriteRenderer _leftRenderer;
        private SpriteRenderer _rightRenderer;
        private bool HasGateArt => _gateClosed != null && _gateOpen != null;
        private Vector3 _leftClosed;
        private Vector3 _rightClosed;
        private bool _leftOpen = true;

        public event Action<Room> Entered;
        public float MinX => _minX;
        public float MaxX => _maxX;
        public Transform PlayerEntry => _playerEntry;
        public Transform[] SpawnPoints => _spawnPoints;
        public bool RightDoorOpen { get; private set; }
        public bool LeftDoorClosed => _leftDoor != null && _leftDoor.enabled;

        private void Awake()
        {
            _leftClosed = _leftVisual.localPosition;
            _rightClosed = _rightVisual.localPosition;
            _leftRenderer = _leftVisual.GetComponent<SpriteRenderer>();
            _rightRenderer = _rightVisual.GetComponent<SpriteRenderer>();
            SetLeftOpen(true);
            SetRightOpen(false);
        }

        public void LockBehind() => SetLeftOpen(false);

        public void HideExitSign() => _exitSign.gameObject.SetActive(false);
        public void SetRightOpen(bool open)
        {
            RightDoorOpen = open;
            _rightDoor.enabled = !open;
            _exitSign.gameObject.SetActive(open);
            ShowGate(_rightRenderer, open);
        }

        private void SetLeftOpen(bool open)
        {
            _leftOpen = open;
            _leftDoor.enabled = !open;
            ShowGate(_leftRenderer, open);
        }

        /// <summary>
        /// 문 그림을 닫힘/열림으로 바꾼다. 앞 방의 오른쪽 문과 이 방의 왼쪽 문이 같은 자리라,
        /// 닫힌 문이 열린 문보다 앞에 그려지도록 정렬 순서를 올린다.
        /// </summary>
        private void ShowGate(SpriteRenderer renderer, bool open)
        {
            if (!HasGateArt || renderer == null)
            {
                return;
            }

            renderer.sprite = open ? _gateOpen : _gateClosed;
            renderer.sortingOrder = open ? 10 : 11;
        }

        /// <summary>테스트·디버그용: 오른쪽 문 그림이 지금 보여 주는 스프라이트.</summary>
        public Sprite RightGateSprite => _rightRenderer != null ? _rightRenderer.sprite : null;

        private void OnTriggerEnter2D(Collider2D other) => CheckEntry(other);
        private void OnTriggerStay2D(Collider2D other) => CheckEntry(other);

        private void CheckEntry(Collider2D other)
        {
            if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<PlayerStats>(out _))
                Entered?.Invoke(this);
        }

        private void Update()
        {
            if (RightDoorOpen)
                _exitSign.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * _signPulseSpeed) * _signPulseAmount);
            if (HasGateArt)
                return; // 그림 문은 미끄러지지 않고 그림만 바뀐다.

            float t = 1f - Mathf.Exp(-_gateSpeed * Time.unscaledDeltaTime);
            _leftVisual.localPosition = Vector3.Lerp(_leftVisual.localPosition,
                _leftClosed + Vector3.up * (_leftOpen ? _gateSlideHeight : 0f), t);
            _rightVisual.localPosition = Vector3.Lerp(_rightVisual.localPosition,
                _rightClosed + Vector3.up * (RightDoorOpen ? _gateSlideHeight : 0f), t);
        }
    }
}
