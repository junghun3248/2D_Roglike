using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Enemies
{
    public class BossShockwave : MonoBehaviour
    {
        [SerializeField] private Vector2 _size = new Vector2(0.9f, 1.2f);
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private float _lifetime = 2.5f;
        [SerializeField] private float _knockback = 12f;
        [SerializeField] private float _knockbackUpRatio = 0.4f;
        [SerializeField] private float _hitStun = 0.3f;
        [SerializeField] private float _flickerFrequency = 35f;
        [SerializeField] private float _flickerAmount = 0.15f;

        private readonly Collider2D[] _hits = new Collider2D[16];
        private SpriteRenderer _renderer;
        private Health _player;
        private ContactFilter2D _filter;
        private GameObject _source;
        private float _direction;
        private float _speed;
        private double _damage;
        private float _age;
        private bool _hasHit;

        // position은 바닥 좌표이며 렌더러 중심을 반 높이만큼 올린다.
        public static BossShockwave Spawn(Vector2 position, float direction, float speed, double damage, GameObject source)
        {
            SpriteRenderer renderer = VfxSprites.CreateRenderer("BossShockwave", VfxSprites.Square,
                new Color(1f, 0.55f, 0.2f), 30);
            BossShockwave wave = renderer.gameObject.AddComponent<BossShockwave>();
            wave._renderer = renderer;
            wave._direction = direction < 0f ? -1f : 1f;
            wave._speed = Mathf.Max(0f, speed);
            wave._damage = damage;
            wave._source = source;
            wave.transform.position = position + Vector2.up * (wave._size.y * 0.5f);
            Vector2 spriteSize = renderer.sprite != null ? (Vector2)renderer.sprite.bounds.size : Vector2.one;
            wave.transform.localScale = new Vector3(wave._size.x / Mathf.Max(spriteSize.x, Mathf.Epsilon),
                wave._size.y / Mathf.Max(spriteSize.y, Mathf.Epsilon), 1f);
            return wave;
        }

        private void Start()
        {
            _filter.SetLayerMask(GameLayers.PlayerMask);
            _filter.useTriggers = true;
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team != Team.Player) continue;
                _player = health;
                break;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }
            Vector2 position = transform.position;
            Vector2 direction = Vector2.right * _direction;
            float distance = _speed * dt;
            RaycastHit2D wall = Physics2D.Raycast(position, direction, distance + _size.x * 0.5f, GameLayers.GroundMask);
            if (wall) distance = Mathf.Max(0f, wall.distance - _size.x * 0.5f);
            ApplyDamage(position + direction * (distance * 0.5f), new Vector2(_size.x + distance, _size.y));
            transform.position = position + direction * distance;
            if (wall)
            {
                Destroy(gameObject);
                return;
            }
            Color color = _color;
            color.a *= 1f - _flickerAmount * (0.5f + 0.5f * Mathf.Sin(_age * _flickerFrequency));
            _renderer.color = color;
        }

        private void ApplyDamage(Vector2 center, Vector2 size)
        {
            if (_hasHit || _player == null || _player.IsDead || _player.IsInvulnerable) return;
            int count = Physics2D.OverlapBox(center, size, 0f, _filter, _hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (hit == null || !hit.transform.IsChildOf(_player.transform)) continue;
                Vector2 knockback = new Vector2(_direction, _knockbackUpRatio).normalized * _knockback;
                if (!_player.TryApplyDamage(new DamageInfo(_damage, false, hit.ClosestPoint(center), knockback, _hitStun, _source))) continue;
                _hasHit = true;
                return;
            }
        }
    }
}
