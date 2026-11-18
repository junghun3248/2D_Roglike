using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Roglike.Combat;
using UnityEngine;

namespace Roglike.Enemies
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private GameObject[] _enemyPool;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private int _maxAlive = 5;
        [SerializeField] private float _respawnDelay = 1.5f;
        [SerializeField] private float _minimumPlayerDistance = 4f;

        private readonly Dictionary<Health, Action<DamageInfo>> _alive = new Dictionary<Health, Action<DamageInfo>>();
        private readonly List<Health> _destroyed = new List<Health>();
        private readonly List<float> _respawnTimers = new List<float>();
        private Health _player;
        private bool _warned;

        private void Start()
        {
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
            _destroyed.Clear();
            foreach (Health enemy in _alive.Keys)
                if (enemy == null) _destroyed.Add(enemy);
            foreach (Health enemy in _destroyed) OnDespawned(enemy);

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
            GameObject prefab = _enemyPool != null && _enemyPool.Length > 0
                ? _enemyPool[Random.Range(0, _enemyPool.Length)] : _enemyPrefab;
            Transform point = ChooseSpawnPoint();
            if (prefab == null || point == null)
            {
                WarnInvalidSetup();
                return false;
            }

            GameObject instance = Instantiate(prefab, point.position, point.rotation);
            Health enemy = instance.GetComponent<Health>();
            if (enemy == null || enemy.Team != Team.Enemy)
            {
                Destroy(instance);
                WarnInvalidSetup();
                return false;
            }
            Action<DamageInfo> onDied = _ => OnDespawned(enemy);
            _alive.Add(enemy, onDied);
            enemy.Died += onDied;
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

        private void OnDespawned(Health enemy)
        {
            if (!_alive.TryGetValue(enemy, out Action<DamageInfo> onDied)) return;
            if (enemy != null) enemy.Died -= onDied;
            _alive.Remove(enemy);
            _respawnTimers.Add(Mathf.Max(0f, _respawnDelay));
        }

        private void OnDestroy()
        {
            foreach (var entry in _alive)
            {
                if (entry.Key != null) entry.Key.Died -= entry.Value;
            }
            _alive.Clear();
            _respawnTimers.Clear();
        }

        private void WarnInvalidSetup()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("EnemySpawner requires a prefab with enemy Health and at least one valid spawn point.", this);
        }
    }
}
