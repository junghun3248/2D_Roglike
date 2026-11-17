using UnityEngine;

namespace Roglike.Combat
{
    /// <summary>공격 궤적 자리표시: 휘두르는 방향으로 늘어나며 사라지는 띠. 아트가 들어오면 스프라이트 애니메이션으로 교체.</summary>
    public class SlashEffect : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _color;
        private Vector2 _size;
        private float _life;
        private float _age;

        public static void Spawn(Vector2 center, Vector2 size, float angle, Color color, float duration)
        {
            var renderer = VfxSprites.CreateRenderer("Slash", VfxSprites.Square, color, 40);
            var slash = renderer.gameObject.AddComponent<SlashEffect>();
            slash._renderer = renderer;
            slash._color = color;
            slash._size = size;
            slash._life = duration;
            renderer.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
            slash.Apply(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Apply(t);
        }

        private void Apply(float t)
        {
            float width = _size.x * Mathf.Lerp(0.6f, 1.1f, Mathf.Sqrt(t));
            float height = _size.y * Mathf.Lerp(0.35f, 0.05f, t);
            transform.localScale = new Vector3(width, height, 1f);
            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
        }
    }
}
