using System.Collections.Generic;
using Roglike.Combat;
using UnityEngine;

namespace Roglike.Enemies
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private int _maxAlive = 5;
        [SerializeField] private float _respawnDelay = 1.5f;
        [SerializeField] private float _minimumPlayerDistance = 4f;

        private readonly HashSet<SlimeEnemy> _alive = new HashSet<SlimeEnemy>();
        private readonly List<float> _respawnTimers = new List<float>();
        private Health _player;
        private bool _warned;

        private void Start()
        {
            if (_enemyPrefab == null || _enemyPrefab.GetComponent<SlimeEnemy>() == null || ChooseSpawnPoint() == null)
            {
                WarnInvalidSetup();
                return;
            }

            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team == Team.Player)
                {
                    _player = health;
                    break;
                }
            }

            for (int i = 0; i < _maxAlive; i++)
            {
                if (!SpawnEnemy()) break;
            }
        }

        private void Update()
        {
            for (int i = _respawnTimers.Count - 1; i >= 0; i--)
            {
                _respawnTimers[i] -= Time.deltaTime;
                if (_respawnTimers[i] > 0f) continue;
                _respawnTimers.RemoveAt(i);
                if (_alive.Count < _maxAlive) SpawnEnemy();
            }
        }

        private bool SpawnEnemy()
        {
            Transform point = ChooseSpawnPoint();
            if (_enemyPrefab == null || point == null)
            {
                WarnInvalidSetup();
                return false;
            }

            GameObject instance = Instantiate(_enemyPrefab, point.position, point.rotation);
            SlimeEnemy enemy = instance.GetComponent<SlimeEnemy>();
            if (enemy == null)
            {
                Destroy(instance);
                WarnInvalidSetup();
                return false;
            }
            _alive.Add(enemy);
            enemy.Despawned += OnDespawned;
            return true;
        }

        private Transform ChooseSpawnPoint()
        {
            if (_spawnPoints == null) return null;
            Transform fallback = null;
            Transform preferred = null;
            int validCount = 0;
            int preferredCount = 0;
            float minimumSquared = _minimumPlayerDistance * _minimumPlayerDistance;
            foreach (Transform point in _spawnPoints)
            {
                if (point == null) continue;
                validCount++;
                if (Random.Range(0, validCount) == 0) fallback = point;
                if (_player != null && !_player.IsDead
                    && ((Vector2)(point.position - _player.transform.position)).sqrMagnitude <= minimumSquared) continue;
                preferredCount++;
                if (Random.Range(0, preferredCount) == 0) preferred = point;
            }
            return preferred != null ? preferred : fallback;
        }

        private void OnDespawned(SlimeEnemy enemy)
        {
            enemy.Despawned -= OnDespawned;
            if (_alive.Remove(enemy)) _respawnTimers.Add(Mathf.Max(0f, _respawnDelay));
        }

        private void OnDestroy()
        {
            foreach (SlimeEnemy enemy in _alive)
            {
                if (enemy != null) enemy.Despawned -= OnDespawned;
            }
            _alive.Clear();
            _respawnTimers.Clear();
        }

        private void WarnInvalidSetup()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("EnemySpawner requires a prefab with SlimeEnemy and at least one valid spawn point.", this);
        }
    }
}
