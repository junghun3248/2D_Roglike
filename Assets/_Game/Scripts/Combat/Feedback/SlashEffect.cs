using UnityEngine;
using Roglike.Data;

namespace Roglike.Combat
{
    /// <summary>비감속 시간으로 재생하는 타격 VFX. 그림이 없는 씬에서는 기존 띠를 사용한다.</summary>
    public class SlashEffect : MonoBehaviour
    {
        [SerializeField] private Color _coreColor = new Color(0.97f, 1f, 1f, 1f);
        [SerializeField] private Color _edgeColor = new Color(0f, 0.85f, 0.65f, 1f);
        [SerializeField] private Color _outlineColor = new Color(0.015f, 0.09f, 0.1f, 0.9f);
        [SerializeField] private Vector2 _coreScale = new Vector2(0.95f, 0.45f);
        [SerializeField] private Vector2 _outlineScale = new Vector2(1.04f, 1.25f);
        [SerializeField, Min(0.01f)] private float _minimumFadeDuration = 0.1f;
        [SerializeField] private int _sortingOrder = 40;
        [SerializeField] private SpriteAnimationData _animation;
        private SpriteRenderer _core;
        private SpriteRenderer _outline;
        private SpriteRenderer _renderer;
        private Color _color;
        private Vector2 _size;
        private float _life;
        private float _age;

        public static SlashEffect Spawn(Vector2 center, Vector2 size, float angle, Color color, float duration,
            bool flipX = false, bool flipY = false)
        {
            return SpawnAnimation(VfxSprites.SlashAnimation, center, size, angle, color, duration, flipX, flipY);
        }

        public static SlashEffect SpawnAnimation(SpriteAnimationData animation, Vector2 center, Vector2 size,
            float angle, Color color, float duration, bool flipX = false, bool flipY = false)
        {
            var slash = new GameObject("Slash").AddComponent<SlashEffect>();
            var renderer = slash.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = slash._sortingOrder;
            if (VfxSprites.Material != null) renderer.sharedMaterial = VfxSprites.Material;
            slash._renderer = renderer;
            slash._color = color;
            slash._size = size;
            renderer.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
            if (animation != null && animation.IsValid)
            {
                slash._animation = animation;
                slash._life = animation.Duration;
                renderer.sprite = animation.frames[0];
                renderer.flipX = flipX;
                renderer.flipY = flipY;
                // 원본의 흰 코어와 청록/금색을 보존한다. 크기는 첫 프레임부터 완전한 크기다.
                renderer.color = new Color(1f, 1f, 1f, color.a);
                Vector2 bounds = renderer.sprite.bounds.size;
                slash.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
                return slash;
            }
            renderer.sprite = VfxSprites.Square;
            slash._life = Mathf.Max(duration, slash._minimumFadeDuration);
            slash._core = slash.CreateLayer("Core", slash._coreColor, 41, slash._coreScale);
            slash._outline = slash.CreateLayer("Outline", slash._outlineColor, 39, slash._outlineScale);
            slash.Apply(0f);
            return slash;
        }

        private SpriteRenderer CreateLayer(string name, Color color, int order, Vector2 scale)
        {
            var layer = VfxSprites.CreateRenderer(name, VfxSprites.Square, color, order);
            layer.transform.SetParent(transform, false);
            layer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            return layer;
        }

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = _age / _life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            if (_animation != null)
            {
                int frame = Mathf.Min((int)(_age * Mathf.Max(0.01f, _animation.fps)), _animation.frames.Length - 1);
                _renderer.sprite = _animation.frames[frame];
                _renderer.color = new Color(1f, 1f, 1f, _color.a * (1f - t));
            }
            else Apply(t);
        }

        private void Apply(float t)
        {
            float width = _size.x * Mathf.Lerp(0.6f, 1.1f, Mathf.Sqrt(t));
            float height = _size.y * Mathf.Lerp(0.35f, 0.05f, t);
            transform.localScale = new Vector3(width, height, 1f);
            float alpha = _color.a * (1f - t);
            _renderer.color = new Color(_edgeColor.r, _edgeColor.g, _edgeColor.b, _edgeColor.a * alpha);
            _core.color = new Color(_coreColor.r, _coreColor.g, _coreColor.b, _coreColor.a * alpha);
            _outline.color = new Color(_outlineColor.r, _outlineColor.g, _outlineColor.b, _outlineColor.a * alpha);
        }
    }
}
