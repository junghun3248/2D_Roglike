using Roglike.Enemies;
using UnityEngine;

namespace Roglike.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class StatusEffects : MonoBehaviour
    {
        [SerializeField] private float _bossFreezeLimit = 0.5f;
        [SerializeField] private float _frozenDamageMultiplier = 1.3f;
        [SerializeField] private Color _freezeTint = new Color(0.55f, 0.9f, 1f);
        private Health _health;
        private Rigidbody2D _body;
        private HitFlash _flash;
        private bool _boss;
        private float _remaining;
        private RigidbodyConstraints2D _constraints;
        private Vector2 _velocity;
        private float _angularVelocity;

        public bool IsFrozen => _remaining > 0f;
        public float DamageTakenMultiplier => IsFrozen ? _frozenDamageMultiplier : 1f;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _body = GetComponent<Rigidbody2D>();
            _flash = GetComponent<HitFlash>();
            _boss = GetComponent<IBoss>() != null;
        }

        private void OnEnable() => _health.Died += OnDied;

        public void Freeze(float seconds)
        {
            if (seconds <= 0f || _health.IsDead || _health.Team == Team.Player) return;
            if (!IsFrozen && _body != null)
            {
                _constraints = _body.constraints;
                _velocity = _body.linearVelocity;
                _angularVelocity = _body.angularVelocity;
                // Colliders must remain queryable so frozen enemies can still be hit.
                _body.constraints = RigidbodyConstraints2D.FreezeAll;
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
            _remaining = Mathf.Max(_remaining, _boss ? Mathf.Min(seconds, _bossFreezeLimit) : seconds);
            _flash?.SetFreezeTint(true, _freezeTint);
        }

        private void Update()
        {
            if (!IsFrozen) return;
            if (_remaining <= Time.deltaTime) Thaw();
            else _remaining -= Time.deltaTime;
        }

        private void Thaw()
        {
            if (!IsFrozen) return;
            _remaining = 0f;
            if (_body != null)
            {
                _body.constraints = _constraints;
                _body.linearVelocity = _health.IsDead ? Vector2.zero : _velocity;
                _body.angularVelocity = _health.IsDead ? 0f : _angularVelocity;
            }
            _flash?.SetFreezeTint(false, _freezeTint);
        }

        private void OnDied(DamageInfo info) => Thaw();

        private void OnDisable()
        {
            _health.Died -= OnDied;
            Thaw();
        }
    }
}
