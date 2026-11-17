using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roglike.Progression
{
    /// <summary>
    /// 플레이어 능력치와 런 동안 쌓이는 수정치. 전투·이동 스크립트는 여기서 최종 값을 읽는다.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base")]
        [SerializeField] private float _attackPower = 12f;
        [SerializeField] private float _critChance = 0.2f;
        [SerializeField] private float _critDamage = 2f;
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _overdriveDuration = 5f;

        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private readonly float[] _cache = new float[Enum.GetValues(typeof(StatType)).Length];
        private bool _dirty = true;

        /// <summary>수정치가 바뀌었을 때 (최대 체력 갱신, HUD 등).</summary>
        public event Action Changed;

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        private Roglike.Combat.Health _health;

        private void Awake()
        {
            _health = GetComponent<Roglike.Combat.Health>();
        }

        private void Start()
        {
            ApplyMaxHealth();
        }

        private void ApplyMaxHealth()
        {
            if (_health != null)
            {
                _health.SetMax(Mathf.Max(1f, Get(StatType.MaxHealth)));
            }
        }

        public float Get(StatType stat)
        {
            if (_dirty)
            {
                Recalculate();
            }

            return _cache[(int)stat];
        }

        public void AddModifiers(IEnumerable<StatModifier> modifiers)
        {
            foreach (StatModifier modifier in modifiers)
            {
                _modifiers.Add(modifier);
            }

            _dirty = true;
            ApplyMaxHealth();
            Changed?.Invoke();
        }

        public void ClearModifiers()
        {
            _modifiers.Clear();
            _dirty = true;
            ApplyMaxHealth();
            Changed?.Invoke();
        }

        /// <summary>치명타·편차를 제외한 한 방 데미지 (공격력 × 배율 × 피해 증가 × 최종 피해).</summary>
        public double BaseHitDamage(float skillMultiplier)
        {
            return (double)Get(StatType.AttackPower) * skillMultiplier
                * (1.0 + Get(StatType.DamageIncrease))
                * Get(StatType.FinalDamage);
        }

        public float BaseValue(StatType stat)
        {
            switch (stat)
            {
                case StatType.AttackPower: return _attackPower;
                case StatType.CritChance: return _critChance;
                case StatType.CritDamage: return _critDamage;
                case StatType.MaxHealth: return _maxHealth;
                case StatType.OverdriveDuration: return _overdriveDuration;
                case StatType.DamageIncrease: return 0f;
                default: return 1f;
            }
        }

        private void Recalculate()
        {
            int count = _cache.Length;
            Span<float> flat = stackalloc float[count];
            Span<float> percent = stackalloc float[count];
            Span<float> multiply = stackalloc float[count];
            for (int i = 0; i < count; i++)
            {
                multiply[i] = 1f;
            }

            foreach (StatModifier modifier in _modifiers)
            {
                int index = (int)modifier.stat;
                switch (modifier.kind)
                {
                    case ModifierKind.Flat:
                        flat[index] += modifier.value;
                        break;
                    case ModifierKind.PercentAdd:
                        percent[index] += modifier.value;
                        break;
                    case ModifierKind.Multiply:
                        multiply[index] *= modifier.value;
                        break;
                }
            }

            for (int i = 0; i < count; i++)
            {
                _cache[i] = (BaseValue((StatType)i) + flat[i]) * (1f + percent[i]) * multiply[i];
            }

            _cache[(int)StatType.CritChance] = Mathf.Clamp01(_cache[(int)StatType.CritChance]);
            _dirty = false;
        }
    }
}
