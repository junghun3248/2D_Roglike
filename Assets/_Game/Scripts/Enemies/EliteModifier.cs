using Roglike.Combat;
using UnityEngine;

namespace Roglike.Enemies
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public class EliteModifier : MonoBehaviour
    {
        [SerializeField] private float _healthMultiplier = 2.5f;
        [SerializeField] private float _speedMultiplier = 1.25f;
        [SerializeField] private float _visualMultiplier = 1.2f;
        [SerializeField] private float _pulseFrequency = 4f;
        [SerializeField] private Color _dimColor = new Color(0.85f, 0.15f, 0.05f);
        [SerializeField] private Color _brightColor = new Color(1f, 0.55f, 0.1f);
        private HitFlash _flash;
        private Health _health;
        public bool IsElite { get; private set; }

        public static void Apply(GameObject enemy)
        {
            if (enemy == null || !enemy.TryGetComponent(out Health health) || health.IsDead) return;
            EliteModifier modifier = enemy.GetComponent<EliteModifier>();
            if (modifier == null) modifier = enemy.AddComponent<EliteModifier>();
            if (modifier.IsElite) return;
            modifier.IsElite = true;
            modifier._health = health;
            modifier._flash = enemy.GetComponent<HitFlash>();
            health.Configure(health.Team, health.Max * modifier._healthMultiplier);
            foreach (MonoBehaviour component in enemy.GetComponents<MonoBehaviour>())
                if (component is ISpeedScalable scalable) scalable.ScaleSpeed(modifier._speedMultiplier);

            // AI와 HitFlash가 복원하는 Visual 자체 대신, 그 아래 그림만 확대한다.
            Transform visual = enemy.transform.Find("Visual");
            if (visual != null)
            {
                int childCount = visual.childCount;
                var scaledVisual = new GameObject("EliteVisual").transform;
                scaledVisual.SetParent(visual, false);
                for (int i = 0; i < childCount; i++) visual.GetChild(0).SetParent(scaledVisual, false);
                scaledVisual.localScale = Vector3.one * modifier._visualMultiplier;
            }
            else
                enemy.transform.localScale *= modifier._visualMultiplier;
        }

        private void OnDisable()
        {
            if (_flash != null) _flash.ClearStatusTint();
        }

        private void LateUpdate()
        {
            if (!IsElite || _flash == null || _health == null || _health.IsDead) return;
            float pulse = (Mathf.Sin(Time.time * _pulseFrequency) + 1f) * 0.5f;
            _flash.SetStatusTint(Color.Lerp(_dimColor, _brightColor, pulse), 1f);
        }
    }
}
