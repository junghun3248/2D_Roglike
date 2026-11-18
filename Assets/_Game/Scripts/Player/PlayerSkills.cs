using System;
using System.Collections;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using UnityEngine;

namespace Roglike.Player
{
    [RequireComponent(typeof(PlayerController), typeof(PlayerHitResolver), typeof(PlayerInputReader))]
    public class PlayerSkills : MonoBehaviour
    {
        [SerializeField] private SkillDefinition[] _startingSkills;
        [SerializeField, Range(1, 4)] private int _slotCount = 4;

        private SkillDefinition[] _slots;
        private IReadOnlyList<SkillDefinition> _readOnlySlots;
        private float[] _cooldowns;
        private float[] _cooldownDurations;
        private PlayerController _controller;
        private PlayerHitResolver _resolver;
        private PlayerInputReader _input;
        private Health _health;
        private Coroutine _routine;
        private bool _executing;

        public IReadOnlyList<SkillDefinition> Slots => _readOnlySlots;
        public event Action Changed;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _resolver = GetComponent<PlayerHitResolver>();
            _input = GetComponent<PlayerInputReader>();
            _health = GetComponent<Health>();
            _slots = new SkillDefinition[Mathf.Clamp(_slotCount, 1, 4)];
            _readOnlySlots = Array.AsReadOnly(_slots);
            _cooldowns = new float[_slots.Length];
            _cooldownDurations = new float[_slots.Length];
            if (_startingSkills != null)
                foreach (SkillDefinition skill in _startingSkills) Equip(skill);
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            CancelSkill();
        }

        public bool HasSkill(SkillDefinition skill)
        {
            if (skill == null) return false;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] == skill || (!string.IsNullOrEmpty(skill.Id) && _slots[i] != null && _slots[i].Id == skill.Id)) return true;
            return false;
        }

        public bool Equip(SkillDefinition skill)
        {
            if (skill == null || HasSkill(skill)) return false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) continue;
                _slots[i] = skill;
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        private bool ValidSlot(int slot) => slot >= 0 && slot < _slots.Length && _slots[slot] != null;
        public float CooldownRemaining(int slot) => ValidSlot(slot) ? _cooldowns[slot] : 0f;
        public float CooldownNormalized(int slot) => ValidSlot(slot) && _cooldownDurations[slot] > 0f
            ? Mathf.Clamp01(_cooldowns[slot] / _cooldownDurations[slot]) : 0f;
        public bool IsReady(int slot) => ValidSlot(slot) && _cooldowns[slot] <= 0f;
        protected virtual float GetCooldownMultiplier() => 1f;

        private void Update()
        {
            float dt = Time.deltaTime * TimeController.PlayerTimeMultiplier;
            for (int i = 0; i < _cooldowns.Length; i++)
            {
                bool cooling = _cooldowns[i] > 0f;
                _cooldowns[i] = Mathf.Max(0f, _cooldowns[i] - dt);
                if (cooling && _cooldowns[i] == 0f) Changed?.Invoke();
            }
            if (_controller.IsHurt || (_health != null && _health.IsDead))
            {
                CancelSkill();
                return;
            }
            if (_executing || !_controller.CanUseSkill || dt <= 0f) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_input.SkillPressed(i) || !IsReady(i)) continue;
                _controller.FaceInputDirection();
                _cooldownDurations[i] = Mathf.Max(0f, _slots[i].Cooldown * GetCooldownMultiplier());
                _cooldowns[i] = _cooldownDurations[i];
                _executing = true;
                _controller.SetSkillActionActive(true);
                _routine = StartCoroutine(Execute(_slots[i]));
                Changed?.Invoke();
                break;
            }
        }

        private IEnumerator Execute(SkillDefinition skill)
        {
            var context = new SkillContext(_controller, _resolver);
            try
            {
                yield return skill.Execute(context);
            }
            finally
            {
                _controller.SetSkillActionActive(false);
                _controller.EndSkillMotion();
                _executing = false;
                _routine = null;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (info.Source != gameObject) CancelSkill();
        }

        private void CancelSkill()
        {
            if (!_executing) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _executing = false;
            _controller.SetSkillActionActive(false);
            _controller.EndSkillMotion();
        }
    }
}
