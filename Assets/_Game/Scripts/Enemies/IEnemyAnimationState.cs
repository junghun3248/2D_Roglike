namespace Roglike.Enemies
{
    /// <summary>
    /// 적 AI가 지금 어떤 동작을 보여 줘야 하는지 알려 주는 계약. EnemySpriteAnimations가 이 이름으로 그림을 고른다.
    /// 쓰는 이름: Idle, Move, Telegraph, Attack, Stunned, Charge, Sweep, Rear, Windup, Throw, Enraged, Dead.
    /// </summary>
    public interface IEnemyAnimationState
    {
        string AnimationState { get; }
    }
}
