using Roglike.Data;
using UnityEngine;

namespace Roglike.Core
{
    /// <summary>
    /// SpriteRenderer에 프레임 애니메이션을 재생한다. 게임 시간 기준이라 히트스톱 중엔 멈춘다.
    /// 플레이어는 오버드라이브 중에도 정상 속도로 움직여야 하므로 _usePlayerTime을 켠다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private bool _usePlayerTime;

        private SpriteRenderer _renderer;
        private SpriteAnimationData _current;
        private float _time;
        private float _speed = 1f;
        private bool _finished;

        public SpriteAnimationData Current => _current;
        public bool IsFinished => _finished;
        public SpriteRenderer Renderer => _renderer != null ? _renderer : (_renderer = GetComponent<SpriteRenderer>());

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>반복 재생. 이미 같은 동작이면 restart가 아닐 때 이어서 재생한다.</summary>
        public void Play(SpriteAnimationData data, bool restart = false)
        {
            if (data == null || !data.IsValid)
            {
                return;
            }

            if (data == _current && !restart && !_finished && _speed == 1f)
            {
                return;
            }

            _current = data;
            _time = 0f;
            _speed = 1f;
            _finished = false;
            Apply();
        }

        /// <summary>한 번 재생하되 전체 길이를 duration(초)에 맞춘다. 공격 길이에 맞추는 데 쓴다.</summary>
        public void PlayOnce(SpriteAnimationData data, float duration)
        {
            if (data == null || !data.IsValid)
            {
                return;
            }

            _current = data;
            _time = 0f;
            _speed = duration > 0f ? data.Duration / duration : 1f;
            _finished = false;
            Apply();
        }

        private void Update()
        {
            if (_current == null || _finished)
            {
                return;
            }

            float dt = Time.deltaTime * (_usePlayerTime ? TimeController.PlayerTimeMultiplier : 1f);
            _time += dt * _speed;
            Apply();
        }

        private void Apply()
        {
            int count = _current.frames.Length;
            int index = Mathf.FloorToInt(_time * _current.fps);
            if (_current.loop)
            {
                index %= count;
            }
            else if (index >= count)
            {
                index = count - 1;
                _finished = true;
            }

            Renderer.sprite = _current.frames[Mathf.Max(0, index)];
        }
    }
}
