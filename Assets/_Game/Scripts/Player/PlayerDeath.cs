using System.Collections;
using Roglike.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roglike.Player
{
    /// <summary>M1 임시: 플레이어가 죽으면 잠시 후 씬을 다시 불러온다. M2에서 런 종료 화면으로 교체.</summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] private float _restartDelay = 1.5f;
        [Tooltip("끄면 씬을 다시 불러오지 않는다 (런 씬에서는 RunManager가 결과 화면을 띄운다)")]
        [SerializeField] private bool _restartOnDeath = true;

        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
        }

        private void OnDied(DamageInfo info)
        {
            if (TryGetComponent(out PlayerController controller))
            {
                controller.enabled = false;
            }

            if (TryGetComponent(out PlayerCombat combat))
            {
                combat.enabled = false;
            }

            if (TryGetComponent(out Rigidbody2D body))
            {
                body.linearVelocity = Vector2.zero;
            }

            HitSpark.Spawn(transform.position, new Color(0.5f, 0.85f, 1f), 20, 12f, 0.3f);
            if (_restartOnDeath)
            {
                StartCoroutine(RestartAfterDelay());
            }
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_restartDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
