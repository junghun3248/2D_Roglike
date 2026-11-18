using System;
using UnityEngine;

namespace Roglike.Data
{
    [Serializable]
    public class AreaConfig
    {
        public string displayName;
        public GameObject[] enemyPool;
        public GameObject boss;
        public RoomEntry[] rooms;
    }
}
