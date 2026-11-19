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
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private RunConfig _config;
        [SerializeField] private UpgradeData[] _upgrades;
        [SerializeField] private SynergyData[] _synergies;
        [SerializeField] private MetaUpgradeData[] _metaUpgrades;

        [Header("Scene")]
        [SerializeField] private PlayerStats _player;
        [SerializeField] private Room[] _rooms;
        private Transform[] _spawnPoints;

        [Header("UI")]
        [SerializeField] private UpgradeChoiceUI _upgradeChoice;
        [SerializeField] private BossHealthBar _bossBar;
        [SerializeField] private RestChoiceUI _restChoice;
        [SerializeField] private RunResultScreen _result;
        [SerializeField] private SynergyPanelUI _synergyPanel;
        [SerializeField] private MetaShopUI _metaShop;

        [Header("Timing (게임 시간 / 클리어·결과는 실시간)")]
        [SerializeField] private float _roomIntroDelay = 0.6f;
        [SerializeField] private float _waveDelay = 0.8f;
        [SerializeField, Min(1)] private int _maxAlivePerRoom = 4;
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
        private IBoss _activeBoss;
        private bool _waitingForEntry;
        private int _enteredRoom = -1;
        private bool _ended;
        private int _upgradesTaken;
        private int _rerollsLeft;
        private SynergyTracker _synergy;

        public SynergyTracker Synergy => _synergy;
        public int CrystalsEarned { get; private set; }

        /// <summary>HUD 갱신용.</summary>
        public event Action Changed;

        /// <summary>현재 지역의 방 인덱스 (0부터).</summary>
        public int RoomIndex { get; private set; }
        public int AreaIndex => 0; // 다음 지역 콘텐츠가 준비되면 지역 전환을 연결한다.
        public AreaConfig Area => _config != null && _config.areas != null && AreaIndex < _config.areas.Length
            ? _config.areas[AreaIndex] : null;
        public int RoomCount => _config != null && _config.areas != null && _config.areas.Length > 0
            ? Area?.rooms?.Length ?? 0 : 0;
        public RoomType CurrentRoomType => RoomCount > 0 ? Area.rooms[RoomIndex].type : RoomType.Entrance;
        public bool IsBossRoom => CurrentRoomType == RoomType.Boss;
        public Room CurrentRoom => _rooms[RoomIndex];
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
            if (_rooms != null)
                foreach (Room room in _rooms) if (room != null) room.Entered -= OnRoomEntered;
            if (_activeBoss != null) _activeBoss.BossDefeated -= OnBossDefeated;
            if (_playerHealth != null)
            {
                _playerHealth.Died -= OnPlayerDied;
            }
        }

        private void Start()
        {
            if (_config == null || _player == null || RoomCount == 0 || _rooms == null || _rooms.Length != RoomCount)
            {
                Debug.LogError("[Roglike] RunManager: RunConfig 또는 Player가 연결되지 않았습니다.", this);
                return;
            }

            // 영구 성장 → 런 시작 능력치, 시너지 추적 시작.
            MetaProgression.ApplyTo(_player, _metaUpgrades ?? Array.Empty<MetaUpgradeData>());
            _rerollsLeft = Mathf.Max(0, Mathf.RoundToInt(_player.Get(StatType.Rerolls)));
            _synergy = new SynergyTracker(_player, _synergies ?? Array.Empty<SynergyData>());
            _synergyPanel?.Bind(_synergy);
            _upgradeChoice?.SetTagStyle(TagStyle, tag => _synergy.Get(tag)?.Icon);

            foreach (Room room in _rooms) room.Entered += OnRoomEntered;
            _loop = StartCoroutine(RunLoop());
        }

        private (string name, Color color) TagStyle(UpgradeTag tag)
        {
            SynergyData data = _synergy?.Get(tag);
            return data != null ? (data.displayName, data.color) : (tag.ToString(), Color.white);
        }

        private void Update()
        {
            bool paused = TimeController.Instance != null && TimeController.Instance.IsPaused;
            if (!_ended && !paused)
            {
                ElapsedSeconds += Time.unscaledDeltaTime;
            }
        }

        private void OnRoomEntered(Room room)
        {
            if (!_waitingForEntry || _ended || _playerHealth.IsDead || !CurrentRoom.RightDoorOpen) return;
            if (RoomIndex + 1 < RoomCount && room == _rooms[RoomIndex + 1])
                _enteredRoom = RoomIndex + 1;
        }

        private IEnumerator RunLoop()
        {
            for (RoomIndex = 0; RoomIndex < RoomCount; RoomIndex++)
            {
                RoomEntry entry = Area.rooms[RoomIndex];
                CurrentRoom.LockBehind();
                // 지나온 방의 "→" 표시가 뒤쪽 문 위에 남아 헷갈리지 않게 끈다.
                if (RoomIndex > 0) _rooms[RoomIndex - 1].HideExitSign();
                _spawnPoints = CurrentRoom.SpawnPoints;
                GameCamera.Instance?.SetBounds(CurrentRoom.MinX, CurrentRoom.MaxX);
                if (entry.type == RoomType.Entrance) CurrentRoom.SetRightOpen(true);
                Banner = entry.type == RoomType.Entrance ? $"{AreaIndex + 1}지역 · {Area.displayName}"
                    : entry.type == RoomType.Elite ? "엘리트!"
                    : entry.type == RoomType.Rest ? "휴식"
                    : entry.type == RoomType.Boss ? "보스" : entry.name;
                Changed?.Invoke();
                yield return new WaitForSeconds(_roomIntroDelay);
                Banner = null;
                Changed?.Invoke();

                switch (entry.type)
                {
                    case RoomType.Combat:
                    case RoomType.Elite:
                        yield return FightRoom(entry);
                        yield return RoomCleared();
                        CurrentRoom.SetRightOpen(true);
                        yield return ChooseUpgrade(entry.type == RoomType.Elite ? UpgradeRarity.Rare : UpgradeRarity.Common);
                        break;
                    case RoomType.Rest:
                        yield return Rest();
                        break;
                    case RoomType.Boss:
                        yield return FightBoss();
                        yield return new WaitForSecondsRealtime(_clearCelebrateTime);
                        EndRun(true);
                        yield break;
                }

                CurrentRoom.SetRightOpen(true);
                Changed?.Invoke();
                if (RoomIndex + 1 >= RoomCount) break;
                _waitingForEntry = true;
                while (_enteredRoom != RoomIndex + 1) yield return null;
                _waitingForEntry = false;
            }
        }

        private IEnumerator Rest()
        {
            bool? heal = null;
            SetGameplayPaused(true);
            _restChoice.Show(_config.healOnRest, value => heal = value);
            while (!heal.HasValue) yield return null;
            if (heal.Value) _playerHealth.Heal(_playerHealth.Max * _config.healOnRest);
            SetGameplayPaused(false);
            if (!heal.Value) yield return ChooseUpgrade(UpgradeRarity.Rare);
        }

        private IEnumerator FightRoom(RoomEntry room)
        {
            float healthScale = Mathf.Pow(_config.enemyHealthGrowth, RoomIndex);
            if (room.waves == null) yield break;
            for (int waveIndex = 0; waveIndex < room.waves.Length; waveIndex++)
            {
                WaveEntry wave = room.waves[waveIndex];
                if (wave == null)
                {
                    continue;
                }

                for (int i = 0; i < wave.count; i++)
                {
                    while (_alive.Count >= _maxAlivePerRoom) yield return null;
                    GameObject prefab = wave.enemyPrefab;
                    if (prefab == null && Area.enemyPool != null && Area.enemyPool.Length > 0)
                        prefab = Area.enemyPool[UnityEngine.Random.Range(0, Area.enemyPool.Length)];
                    if (prefab == null) continue;
                    Health enemy = Spawn(prefab, PickSpawnPoint(), healthScale);
                    if (room.type == RoomType.Elite && i == 0 && waveIndex == 0 && enemy != null) EliteModifier.Apply(enemy.gameObject);
                    yield return new WaitForSeconds(_spawnStagger);
                }

                while (_alive.Count > 0)
                {
                    yield return null;
                }

                yield return new WaitForSeconds(_waveDelay);

                // 마지막 웨이브 뒤에는 구역 클리어 보상으로 고르므로 여기선 건너뛴다.
                bool lastWave = waveIndex == room.waves.Length - 1;
                if (_config.upgradeAfterEveryWave && !lastWave)
                {
                    yield return ChooseUpgrade();
                }
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

        private IEnumerator ChooseUpgrade(UpgradeRarity minRarity = UpgradeRarity.Common)
        {
            List<UpgradeData> options = _pool.Roll(_config.upgradeChoices, minRarity);
            if (options.Count == 0 || _upgradeChoice == null)
            {
                yield break;
            }

            UpgradeData chosen = null;
            SetGameplayPaused(true);
            _upgradeChoice.Show(options, _pool.Stacks, upgrade => chosen = upgrade,
                () => _pool.Roll(_config.upgradeChoices, minRarity), _rerollsLeft);
            while (chosen == null)
            {
                yield return null;
            }

            _rerollsLeft = _upgradeChoice.RerollsLeft;
            _pool.Take(chosen);
            _upgradesTaken++;
            if (chosen.modifiers != null)
            {
                _player.AddModifiers(chosen.modifiers);
            }

            _synergy.Add(chosen);

            SetGameplayPaused(false);
            Changed?.Invoke();
        }

        private IEnumerator FightBoss()
        {
            if (Area.boss == null)
            {
                Debug.LogError("[Roglike] 지역 보스 프리팹이 없습니다.", this);
                while (true) yield return null;
            }

            float scale = Mathf.Pow(_config.enemyHealthGrowth, RoomIndex) * _config.bossHealthMultiplier;
            Vector3 position = _spawnPoints.Length > 0 ? _spawnPoints[_spawnPoints.Length - 1].position : CurrentRoom.PlayerEntry.position;
            Health health = Spawn(Area.boss, position, scale);
            _activeBoss = health != null ? health.GetComponent<IBoss>() : null;
            if (_activeBoss == null)
            {
                Debug.LogError("[Roglike] 보스에 Health/IBoss가 필요합니다.", this);
                while (true) yield return null;
            }
            _bossDefeated = false;
            _activeBoss.BossDefeated += OnBossDefeated;
            _bossBar?.Show(health, _activeBoss.DisplayName);
            while (!_bossDefeated) yield return null;
            _activeBoss.BossDefeated -= OnBossDefeated;
            _activeBoss = null;
            KillRemainingEnemies();
        }

        private void OnBossDefeated(IBoss boss) => _bossDefeated = true;

        private Health Spawn(GameObject prefab, Vector3 position, float healthScale)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            var health = instance.GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError("[Roglike] 적 프리팹에 Health가 없습니다.", prefab);
                Destroy(instance);
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
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team == Team.Enemy && !health.IsDead)
                {
                    health.ClearInvulnerable();
                    health.TryApplyDamage(new DamageInfo(health.Current + 1, false, health.transform.position, Vector2.zero, 0f, gameObject));
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

            _upgradeChoice?.Hide();
            _restChoice?.Hide();
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
            CrystalsEarned = MetaProgression.AwardRun(summary);
            Changed?.Invoke();
            if (_result != null)
            {
                _result.Show(summary, CrystalsEarned, OpenShopOrRestart);
            }
        }

        /// <summary>결과 화면 다음: 강화소(영구 성장)에서 결정을 쓰고 다음 런 출발.</summary>
        private void OpenShopOrRestart()
        {
            if (_metaShop != null && _metaUpgrades != null && _metaUpgrades.Length > 0)
            {
                _metaShop.Show(_metaUpgrades, CrystalsEarned, Restart);
            }
            else
            {
                Restart();
            }
        }

        public int RerollsLeft => _rerollsLeft;

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
