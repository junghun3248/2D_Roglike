using System;
using System.Collections;
using System.Collections.Generic;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using Roglike.Enemies;
using Roglike.Player;
using Roglike.Progression;
using Roglike.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roglike.Run
{
    /// <summary>
    /// 한 런의 흐름: 구역(웨이브 전투) → 클리어 → 강화 3택1 → 다음 구역 … → 보스 → 승리/패배 결과.
    /// M2에서는 같은 전투장을 구역마다 재사용한다 (맵 생성은 M4에서).
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private RunConfig _config;
        [SerializeField] private UpgradeData[] _upgrades;

        [Header("Scene")]
        [SerializeField] private PlayerStats _player;
        [SerializeField] private Transform _playerSpawn;
        [SerializeField] private Transform _bossSpawn;
        [SerializeField] private Transform[] _spawnPoints;

        [Header("UI")]
        [SerializeField] private UpgradeChoiceUI _upgradeChoice;
        [SerializeField] private BossHealthBar _bossBar;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private RunResultScreen _result;

        [Header("Timing (실시간 초)")]
        [SerializeField] private float _roomIntroDelay = 0.6f;
        [SerializeField] private float _waveDelay = 0.8f;
        [SerializeField] private float _spawnStagger = 0.15f;
        [SerializeField] private float _clearCelebrateTime = 0.8f;
        [SerializeField] private float _deathResultDelay = 1.2f;
        [SerializeField] private float _minSpawnDistanceFromPlayer = 4f;

        private readonly List<Health> _alive = new List<Health>();
        private Health _playerHealth;
        private PlayerInputReader _playerInput;
        private UpgradePool _pool;
        private Coroutine _loop;
        private bool _bossDefeated;
        private bool _ended;
        private int _upgradesTaken;

        /// <summary>HUD 갱신용.</summary>
        public event Action Changed;

        /// <summary>0부터. RoomCount - 1이 보스 구역.</summary>
        public int RoomIndex { get; private set; }
        public int RoomCount => (_config != null && _config.rooms != null ? _config.rooms.Length : 0) + 1;
        public bool IsBossRoom => RoomIndex >= RoomCount - 1;
        public int Kills { get; private set; }
        public double HighestHit { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int EnemiesAlive => _alive.Count;
        public string Banner { get; private set; }

        private void Awake()
        {
            if (_player != null)
            {
                _playerHealth = _player.GetComponent<Health>();
                _playerInput = _player.GetComponent<PlayerInputReader>();
            }

            _pool = new UpgradePool(_upgrades ?? Array.Empty<UpgradeData>());
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
            if (_playerHealth != null)
            {
                _playerHealth.Died += OnPlayerDied;
            }
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            if (_playerHealth != null)
            {
                _playerHealth.Died -= OnPlayerDied;
            }
        }

        private void Start()
        {
            if (_config == null || _player == null)
            {
                Debug.LogError("[Roglike] RunManager: RunConfig 또는 Player가 연결되지 않았습니다.", this);
                return;
            }

            _loop = StartCoroutine(RunLoop());
        }

        private void Update()
        {
            bool paused = TimeController.Instance != null && TimeController.Instance.IsPaused;
            if (!_ended && !paused)
            {
                ElapsedSeconds += Time.unscaledDeltaTime;
            }
        }

        private IEnumerator RunLoop()
        {
            int normalRooms = RoomCount - 1;
            for (RoomIndex = 0; RoomIndex < normalRooms; RoomIndex++)
            {
                yield return EnterRoom(_config.rooms[RoomIndex].name);
                yield return FightRoom(_config.rooms[RoomIndex]);
                yield return RoomCleared();
                yield return ChooseUpgrade();
            }

            RoomIndex = normalRooms;
            yield return EnterRoom("보스");
            yield return FightBoss();
            yield return new WaitForSecondsRealtime(1.2f);
            EndRun(true);
        }

        private IEnumerator EnterRoom(string roomName)
        {
            if (_fader != null)
            {
                yield return _fader.FadeTo(1f, 0.3f);
            }

            ResetPlayerPosition();
            Banner = IsBossRoom ? "BOSS" : $"{RoomIndex + 1}구역 · {roomName}";
            Changed?.Invoke();

            if (_fader != null)
            {
                yield return _fader.FadeTo(0f, 0.35f);
            }

            yield return new WaitForSecondsRealtime(_roomIntroDelay);
            Banner = null;
            Changed?.Invoke();
        }

        private IEnumerator FightRoom(RoomEntry room)
        {
            float healthScale = Mathf.Pow(_config.enemyHealthGrowth, RoomIndex);
            foreach (WaveEntry wave in room.waves)
            {
                if (wave?.enemyPrefab == null)
                {
                    continue;
                }

                for (int i = 0; i < wave.count; i++)
                {
                    Spawn(wave.enemyPrefab, PickSpawnPoint(), healthScale);
                    yield return new WaitForSecondsRealtime(_spawnStagger);
                }

                while (_alive.Count > 0)
                {
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(_waveDelay);
            }
        }

        private IEnumerator RoomCleared()
        {
            Banner = "CLEAR!";
            Changed?.Invoke();
            if (_playerHealth != null && !_playerHealth.IsDead)
            {
                _playerHealth.Heal(_playerHealth.Max * _config.healOnRoomClear);
            }

            yield return new WaitForSecondsRealtime(_clearCelebrateTime);
            Banner = null;
            Changed?.Invoke();
        }

        private IEnumerator ChooseUpgrade()
        {
            List<UpgradeData> options = _pool.Roll(_config.upgradeChoices);
            if (options.Count == 0 || _upgradeChoice == null)
            {
                yield break;
            }

            UpgradeData chosen = null;
            SetGameplayPaused(true);
            _upgradeChoice.Show(options, _pool.Stacks, upgrade => chosen = upgrade);
            while (chosen == null)
            {
                yield return null;
            }

            _pool.Take(chosen);
            _upgradesTaken++;
            if (chosen.modifiers != null)
            {
                _player.AddModifiers(chosen.modifiers);
            }

            SetGameplayPaused(false);
            Changed?.Invoke();
        }

        private IEnumerator FightBoss()
        {
            if (_config.bossPrefab == null)
            {
                yield break;
            }

            float healthScale = Mathf.Pow(_config.enemyHealthGrowth, RoomIndex) * _config.bossHealthMultiplier;
            Vector3 position = _bossSpawn != null ? _bossSpawn.position : PickSpawnPoint();
            Health bossHealth = Spawn(_config.bossPrefab, position, healthScale);
            var boss = bossHealth.GetComponent<BossSlime>();
            _bossDefeated = false;
            if (boss != null)
            {
                boss.Defeated += _ => _bossDefeated = true;
                _bossBar?.Show(bossHealth, boss.DisplayName);
            }

            while (!_bossDefeated && !bossHealth.IsDead)
            {
                yield return null;
            }

            KillRemainingEnemies();
        }

        private Health Spawn(GameObject prefab, Vector3 position, float healthScale)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            var health = instance.GetComponent<Health>();
            if (health == null)
            {
                return null;
            }

            health.Configure(Team.Enemy, health.Max * healthScale);
            Track(health);
            HitSpark.Spawn(position, new Color(1f, 1f, 1f, 0.8f), 8, 6f, 0.2f);
            return health;
        }

        /// <summary>보스가 소환한 졸개 등 RunManager 밖에서 생긴 적도 처치 수·생존 수에 넣는다.</summary>
        public void Track(Health health)
        {
            if (health == null || _alive.Contains(health))
            {
                return;
            }

            _alive.Add(health);
            health.Died += _ => OnEnemyDied(health);
            Changed?.Invoke();
        }

        private void OnEnemyDied(Health health)
        {
            if (_alive.Remove(health))
            {
                Kills++;
                Changed?.Invoke();
            }
        }

        private void OnAnyDamaged(Health target, DamageInfo info)
        {
            if (target.Team != Team.Enemy)
            {
                return;
            }

            // 보스 소환 졸개처럼 직접 스폰하지 않은 적도 맞는 순간 추적한다.
            Track(target);
            if (info.Amount > HighestHit)
            {
                HighestHit = info.Amount;
            }
        }

        private void LateUpdate()
        {
            // 파괴됐지만 사망 이벤트 없이 사라진 적 정리.
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null)
                {
                    _alive.RemoveAt(i);
                }
            }
        }

        private void KillRemainingEnemies()
        {
            foreach (SlimeEnemy enemy in FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None))
            {
                var health = enemy.GetComponent<Health>();
                if (health != null && !health.IsDead)
                {
                    health.TryApplyDamage(new DamageInfo(health.Current + 1, false, enemy.transform.position, Vector2.zero, 0f, gameObject));
                }
            }
        }

        private void OnPlayerDied(DamageInfo info)
        {
            if (_ended)
            {
                return;
            }

            if (_loop != null)
            {
                StopCoroutine(_loop);
            }

            StartCoroutine(ShowDefeat());
        }

        private IEnumerator ShowDefeat()
        {
            yield return new WaitForSecondsRealtime(_deathResultDelay);
            EndRun(false);
        }

        private void EndRun(bool victory)
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            _bossBar?.Hide();
            SetGameplayPaused(true);
            var summary = new RunSummary(victory, ElapsedSeconds, RoomIndex + 1, RoomCount, Kills, HighestHit, _upgradesTaken);
            Changed?.Invoke();
            if (_result != null)
            {
                _result.Show(summary, Restart);
            }
        }

        public void Restart()
        {
            TimeController.Instance?.SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void SetGameplayPaused(bool paused)
        {
            TimeController.Instance?.SetPaused(paused);
            if (_playerInput != null)
            {
                _playerInput.enabled = !paused;
            }
        }

        private void ResetPlayerPosition()
        {
            if (_playerSpawn == null || _player == null)
            {
                return;
            }

            var body = _player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = _playerSpawn.position;
                body.linearVelocity = Vector2.zero;
            }

            _player.transform.position = _playerSpawn.position;
        }

        private Vector3 PickSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                return transform.position;
            }

            Vector3 playerPosition = _player != null ? _player.transform.position : Vector3.zero;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Transform point = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];
                if (point != null && Vector2.Distance(point.position, playerPosition) >= _minSpawnDistanceFromPlayer)
                {
                    return point.position;
                }
            }

            return _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)].position;
        }
    }
}
