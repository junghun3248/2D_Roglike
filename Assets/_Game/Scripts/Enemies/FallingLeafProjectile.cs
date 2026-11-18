using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Enemies
{
    public class FallingLeafProjectile : MonoBehaviour
    {
        [SerializeField] private double _damage = 12;
        [SerializeField] private float _warningTime = 0.6f;
        [SerializeField] private float _radius = 0.25f;
        [SerializeField] private float _knockback = 6f;
        [SerializeField] private float _hitStun = 0.2f;
        [SerializeField] private float _groundSearchDepth = 100f;
        [SerializeField] private float _markerLift = 0.04f;
        [SerializeField] private Vector2 _markerSize = new Vector2(0.9f, 0.08f);
        [SerializeField] private Vector2 _leafSize = new Vector2(0.35f, 0.7f);
        [SerializeField] private Color _leafColor = new Color(1f, 0.65f, 0.15f);
        [SerializeField] private Color _markerColor = new Color(1f, 0.2f, 0.1f, 0.75f);
        private Vector2 _origin;
        private Vector2 _landing;
        private GameObject _source;
        private GameObject _marker;
        private float _age;
        private bool _finished;

        public static FallingLeafProjectile Spawn(Vector2 origin, GameObject source)
        {
            var go = new GameObject("FallingLeafProjectile");
            go.transform.position = origin;
            var leaf = go.AddComponent<FallingLeafProjectile>();
            leaf._source = source;
            leaf._origin = origin;
            RaycastHit2D ground = Physics2D.Raycast(origin, Vector2.down, leaf._groundSearchDepth, GameLayers.GroundMask);
            leaf._landing = ground.collider != null ? ground.point : origin + Vector2.down * leaf._groundSearchDepth;
            SpriteRenderer marker = VfxSprites.CreateRenderer("LeafLandingMarker", VfxSprites.Square, leaf._markerColor, 19);
            leaf._marker = marker.gameObject;
            marker.transform.position = leaf._landing + Vector2.up * leaf._markerLift;
            marker.transform.localScale = new Vector3(leaf._markerSize.x, leaf._markerSize.y, 1f);
            if (VfxSprites.LeafSprite != null)
            {
                SpriteRenderer art = VfxSprites.CreateRenderer("Leaf", VfxSprites.LeafSprite, Color.white, 20);
                art.transform.SetParent(go.transform, false);
                // 낙엽은 원본(22px)이 작아 잘 안 보이므로 2배로 키운다.
                art.transform.localScale = Vector3.one * 2f;
                return leaf;
            }

            SpriteRenderer visual = VfxSprites.CreateRenderer("Leaf", VfxSprites.Circle, leaf._leafColor, 20);
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(leaf._leafSize.x, leaf._leafSize.y, 1f);
            return leaf;
        }

        private void FixedUpdate()
        {
            if (_finished) return;
            _age += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_age / Mathf.Max(Time.fixedDeltaTime, _warningTime));
            Vector2 next = Vector2.Lerp(_origin, _landing, t * t);
            Vector2 delta = next - (Vector2)transform.position;
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, _radius, delta.normalized,
                delta.magnitude, GameLayers.PlayerMask);
            if (hit.collider != null)
            {
                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null && health.Team == Team.Player)
                    health.TryApplyDamage(new DamageInfo(_damage, false, hit.point, Vector2.down * _knockback, _hitStun, _source));
                Finish();
                return;
            }
            transform.position = next;
            if (t >= 1f) Finish();
        }

        private void Finish()
        {
            _finished = true;
            if (_marker != null) Destroy(_marker);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_marker != null) Destroy(_marker);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _markerColor;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
