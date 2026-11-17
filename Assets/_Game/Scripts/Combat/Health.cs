using System;
using UnityEngine;

namespace Roglike.Combat
{
    /// <summary>체력과 피격 처리. 피격 반응(넉백·경직)은 각 캐릭터 스크립트가 Damaged 이벤트로 처리한다.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private Team _team = Team.Enemy;
        [SerializeField] private double _maxHealth = 100;

        private double _current;
        private float _invulnerableUntil;

        /// <summary>모든 Health의 피격을 전역으로 알린다 (데미지 숫자 등).</summary>
        public static event Action<Health, DamageInfo> AnyDamaged;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;
        public event Action Changed;

        public Team Team => _team;
        public double Max => _maxHealth;
        public double Current => _current;
        public float Normalized => _maxHealth <= 0 ? 0f : (float)(_current / _maxHealth);
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        private void Awake()
        {
            _current = _maxHealth;
        }

        public void Configure(Team team, double maxHealth)
        {
            _team = team;
            _maxHealth = maxHealth;
            _current = maxHealth;
            IsDead = false;
            Changed?.Invoke();
        }

        /// <summary>게임 시간 기준 무적.</summary>
        public void SetInvulnerable(float seconds)
        {
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);
        }

        /// <summary>피해를 적용한다. 죽었거나 무적이면 false.</summary>
        public bool TryApplyDamage(DamageInfo info)
        {
            if (IsDead || IsInvulnerable || info.Amount <= 0)
            {
                return false;
            }

            _current = Math.Max(0, _current - info.Amount);
            Damaged?.Invoke(info);
            AnyDamaged?.Invoke(this, info);
            Changed?.Invoke();

            if (_current <= 0)
            {
                IsDead = true;
                Died?.Invoke(info);
            }

            return true;
        }

        public void Heal(double amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            _current = Math.Min(_maxHealth, _current + amount);
            Changed?.Invoke();
        }
    }
}
