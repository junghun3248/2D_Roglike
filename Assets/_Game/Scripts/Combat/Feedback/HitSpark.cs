using UnityEngine;

namespace Roglike.Combat
{
    /// <summary>
    /// 타격 지점에서 사방으로 튀는 파편. 게임 시간 기준이라 히트스톱 중엔 멈춰 있다가 풀리면 터진다.
    /// </summary>
    public class HitSpark : MonoBehaviour
    {
        private Vector2 _velocity;
        private float _life;
        private float _age;
        private float _startScale;
        private SpriteRenderer _renderer;
        private Color _color;

        /// <summary>파편 묶음 생성. 적 처치 폭발에도 같은 함수를 쓴다 (count·speed를 키워서).</summary>
        public static void Spawn(Vector2 point, Color color, int count = 7, float speed = 9f, float size = 0.22f)
        {
            for (int i = 0; i < count; i++)
            {
                var renderer = VfxSprites.CreateRenderer("HitSpark", VfxSprites.Square, color, 50);
                var spark = renderer.gameObject.AddComponent<HitSpark>();
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float s = speed * Random.Range(0.5f, 1.2f);
                spark._velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * s;
                spark._life = Random.Range(0.18f, 0.32f);
                spark._startScale = size * Random.Range(0.7f, 1.3f);
                spark._renderer = renderer;
                spark._color = color;
                spark.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg));
                spark.transform.localScale = Vector3.one * spark._startScale;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float t = _age / _life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            _velocity *= Mathf.Exp(-6f * dt);
            transform.position += (Vector3)(_velocity * dt);
            transform.localScale = new Vector3(_startScale * (1f + _velocity.magnitude * 0.08f), _startScale * (1f - t), 1f);
            _renderer.color = new Color(_color.r, _color.g, _color.b, 1f - t * t);
        }
    }
}
