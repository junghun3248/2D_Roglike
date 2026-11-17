using System;
using System.Collections.Generic;
using System.IO;
using Roglike.Data;
using Roglike.Run;
using UnityEngine;

namespace Roglike.Progression
{
    public static class MetaProgression
    {
        private static readonly object MetaSourceKey = new object();
        private static MetaSave _data;
        private static string _savePathOverride;

        public static string SavePathOverride
        {
            get => _savePathOverride;
            set
            {
                if (_savePathOverride == value) return;
                _savePathOverride = value;
                _data = null;
            }
        }

        private static string SavePath => SavePathOverride ?? Path.Combine(Application.persistentDataPath, "meta.json");

        public static MetaSave Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static int Crystals => Data.crystals;

        public static void Load()
        {
            _data = new MetaSave();
            try
            {
                string path = SavePath;
                if (!File.Exists(path)) return;
                _data = JsonUtility.FromJson<MetaSave>(File.ReadAllText(path)) ?? new MetaSave();
                if (_data.levels == null) _data.levels = new List<MetaLevel>();
                _data.crystals = Math.Max(0, _data.crystals);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"영구 성장 불러오기 실패: {exception.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                string path = SavePath;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(Data, true));
                // 기존 파일을 먼저 지우지 않아 교체 실패 시에도 이전 저장을 보존한다.
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"영구 성장 저장 실패: {exception.Message}");
            }
        }

        public static void ResetData() => _data = new MetaSave();

        public static int Level(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            foreach (MetaLevel entry in Data.levels)
            {
                if (entry != null && entry.id == id) return Math.Max(0, entry.level);
            }
            return 0;
        }

        public static bool CanBuy(MetaUpgradeData u)
        {
            if (u == null || string.IsNullOrWhiteSpace(u.id)) return false;
            int level = Level(u.id);
            if (level >= u.maxLevel) return false;
            int cost = u.CostForLevel(level);
            return cost >= 0 && Crystals >= cost;
        }

        public static bool TryBuy(MetaUpgradeData u)
        {
            if (!CanBuy(u)) return false;
            int level = Level(u.id);
            Data.crystals -= u.CostForLevel(level);
            MetaLevel owned = null;
            foreach (MetaLevel entry in Data.levels)
            {
                if (entry != null && entry.id == u.id)
                {
                    owned = entry;
                    break;
                }
            }
            if (owned == null)
            {
                owned = new MetaLevel { id = u.id };
                Data.levels.Add(owned);
            }
            owned.level = level + 1;
            Save();
            return true;
        }

        public static void ApplyTo(PlayerStats stats, IReadOnlyList<MetaUpgradeData> upgrades)
        {
            if (stats == null) return;
            var modifiers = new List<StatModifier>();
            var appliedIds = new HashSet<string>();
            if (upgrades != null)
            {
                foreach (MetaUpgradeData upgrade in upgrades)
                {
                    if (upgrade == null || !appliedIds.Add(upgrade.id)) continue;
                    int level = Mathf.Clamp(Level(upgrade.id), 0, Mathf.Max(0, upgrade.maxLevel));
                    if (level == 0 || upgrade.modifiersPerLevel == null) continue;
                    foreach (StatModifier modifier in upgrade.modifiersPerLevel)
                    {
                        float value = modifier.kind == ModifierKind.Multiply
                            ? Mathf.Pow(modifier.value, level) : modifier.value * level;
                        modifiers.Add(new StatModifier(modifier.stat, modifier.kind, value));
                    }
                }
            }
            stats.SetModifiers(MetaSourceKey, modifiers);
        }

        public static int AwardRun(RunSummary summary)
        {
            long roomsCleared = Math.Max(0L, summary.Victory ? summary.RoomCount : (long)summary.RoomReached - 1);
            int earned = (int)Math.Min(int.MaxValue,
                5L * roomsCleared + Math.Max(0, summary.Kills) / 2 + (summary.Victory ? 40 : 0));
            Data.crystals = (int)Math.Min(int.MaxValue, (long)Data.crystals + earned);
            Data.totalRuns = (int)Math.Min(int.MaxValue, (long)Data.totalRuns + 1);
            if (summary.Victory) Data.victories = (int)Math.Min(int.MaxValue, (long)Data.victories + 1);
            Data.bestRoom = Math.Max(Data.bestRoom, summary.RoomReached);
            Data.bestHit = Math.Max(Data.bestHit, summary.HighestHit);
            Save();
            return earned;
        }
    }
}
