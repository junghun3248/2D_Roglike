using System;
using System.Collections.Generic;

namespace Roglike.Progression
{
    [Serializable]
    public class MetaSave
    {
        public int crystals;
        public int totalRuns;
        public int victories;
        public int bestRoom;
        public double bestHit;
        public List<MetaLevel> levels = new List<MetaLevel>();
    }

    [Serializable]
    public class MetaLevel
    {
        public string id;
        public int level;
    }
}
