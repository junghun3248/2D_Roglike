using Roglike.Combat;
using Roglike.Core;
using UnityEngine;

namespace Roglike.Enemies
{
    public class GearProjectile : MonoBehaviour
    {
        [SerializeField] private double _damage = 8;
        [SerializeField] private float _gravity = 18f;
        [SerializeField] private float _aimFlightTime = 0.8f;
        [SerializeField] private float _lifetime = 3f;
        [SerializeField] private float _radius = 0.18f;
        [SerializeField] private float _spinSpeed = 540f;
        [SerializeField] private float _knockback = 5f;
        [SerializeField] private float _hitStun = 0.15f;
        [SerializeField] private float _visualSize = 0.4f;
        [SerializeField] private Color _color = new Color(1f, 0.8f, 0.3f);
        private Vector2 _velocity;
        private GameObject _source;
        private float _age;
        private bool _finished;

        public static GearProjectile Spawn(Vector2 origin, Vector2 target, GameObject source)
        {
            var go = new GameObject("GearProjectile");
            go.transform.position = origin;
            var projectile = go.AddComponent<GearProjectile>();
            projectile._source = source;
            float flight = Mathf.Max(Time.fixedDeltaTime, projectile._aimFlightTime);
            projectile._velocity = (target - origin) / flight + Vector2.up * (projectile._gravity * flight * 0.5f);
            if (VfxSprites.GearSprite != null)
            {
                // 그림 톱니 (게임 해상도에 맞춰 이미 축소됨): 원래 크기 그대로, 몸통 회전으로 돈다.
                SpriteRenderer art = VfxSprites.CreateRenderer("Gear", VfxSprites.GearSprite, Color.white, 20);
                art.transform.SetParent(go.transform, false);
                return projectile;
            }

            SpriteRenderer visual = VfxSprites.CreateRenderer("Gear", VfxSprites.Square, projectile._color, 20);
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * projectile._visualSize;
            SpriteRenderer cross = VfxSprites.CreateRenderer("Teeth", VfxSprites.Square, projectile._color, 20);
            cross.transform.SetParent(visual.transform, false);
            cross.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return projectile;
        }

        private void FixedUpdate()
        {
            if (_finished) return;
            float dt = Time.fixedDeltaTime;
            _age += dt;
            Vector2 delta = _velocity * dt + Vector2.down * (_gravity * dt * dt * 0.5f);
            _velocity += Vector2.down * (_gravity * dt);
            RaycastHit2D hit = Physics2D.CircleCast(transform.position, _radius, delta.normalized,
                delta.magnitude, GameLayers.PlayerMask | GameLayers.GroundMask);
            if (hit.collider != null)
            {
                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null && health.Team == Team.Player)
                    health.TryApplyDamage(new DamageInfo(_damage, false, hit.point,
                        _velocity.normalized * _knockback, _hitStun, _source));
                Finish();
                return;
            }
            transform.position += (Vector3)delta;
            transform.Rotate(0f, 0f, _spinSpeed * dt);
            if (_age >= _lifetime) Finish();
        }

        private void Finish()
        {
            _finished = true;
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _color;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
