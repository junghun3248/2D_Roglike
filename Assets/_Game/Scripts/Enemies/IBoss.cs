using System;

namespace Roglike.Enemies
{
    /// <summary>보스 공통 계약. RunManager는 보스 종류와 상관없이 이것만 보고 체력바·승리를 처리한다.</summary>
    public interface IBoss
    {
        string DisplayName { get; }
        bool IsEnraged { get; }

        /// <summary>보스가 쓰러질 때 한 번 (인자: 보스 자신).</summary>
        event Action<IBoss> BossDefeated;
    }
}
