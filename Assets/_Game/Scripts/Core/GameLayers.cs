namespace Roglike.Core
{
    /// <summary>
    /// 물리 레이어 번호. ProjectSettings/TagManager의 레이어 이름과 일치해야 한다
    /// (Roglike > Build Prototype Scene 메뉴가 자동으로 설정한다).
    /// </summary>
    public static class GameLayers
    {
        public const int Ground = 6;
        public const int Player = 7;
        public const int Enemy = 8;

        public const int GroundMask = 1 << Ground;
        public const int PlayerMask = 1 << Player;
        public const int EnemyMask = 1 << Enemy;
    }
}
