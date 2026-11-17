namespace Roglike.Run
{
    /// <summary>런 결과 화면에 보여줄 기록.</summary>
    public readonly struct RunSummary
    {
        public readonly bool Victory;
        public readonly float Seconds;
        public readonly int RoomReached;
        public readonly int RoomCount;
        public readonly int Kills;
        public readonly double HighestHit;
        public readonly int Upgrades;

        public RunSummary(bool victory, float seconds, int roomReached, int roomCount, int kills, double highestHit, int upgrades)
        {
            Victory = victory;
            Seconds = seconds;
            RoomReached = roomReached;
            RoomCount = roomCount;
            Kills = kills;
            HighestHit = highestHit;
            Upgrades = upgrades;
        }
    }
}
