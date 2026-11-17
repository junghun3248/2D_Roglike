using UnityEngine;

namespace Roglike.Combat
{
    /// <summary>스프라이트 잔상. 대시·오버드라이브에서 쓴다. 감속 중에도 일정하게 사라지도록 실시간 기준.</summary>
    public class Afterimage : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _color;
        private float _life;
        private float _age;

        public static void Spawn(SpriteRenderer source, Color tint, float realSeconds)
        {
            if (source == null || source.sprite == null)
            {
                return;
            }

            var renderer = VfxSprites.CreateRenderer("Afterimage", source.sprite, tint, source.sortingOrder - 1);
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.flipX = source.flipX;
            Transform t = renderer.transform;
            t.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            t.localScale = source.transform.lossyScale;

            var afterimage = renderer.gameObject.AddComponent<Afterimage>();
            afterimage._renderer = renderer;
            afterimage._color = tint;
            afterimage._life = realSeconds;
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

            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
        }
    }
}
