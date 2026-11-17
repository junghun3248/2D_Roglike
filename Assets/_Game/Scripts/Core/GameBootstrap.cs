using UnityEngine;

namespace Roglike.Core
{
    /// <summary>씬 시작 시 전역 설정을 적용한다.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int _targetFrameRate = 120;

        private void Awake()
        {
            // 플레이어와 적은 서로 몸으로 밀지 않는다 (메이플식 통과). 접촉 피해는 적이 직접 판정한다.
            Physics2D.IgnoreLayerCollision(GameLayers.Player, GameLayers.Enemy, true);
            Physics2D.IgnoreLayerCollision(GameLayers.Enemy, GameLayers.Enemy, true);
            Application.targetFrameRate = _targetFrameRate;
        }
    }
}
