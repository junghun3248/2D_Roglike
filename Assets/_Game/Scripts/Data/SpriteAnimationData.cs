using UnityEngine;

namespace Roglike.Data
{
    /// <summary>프레임 스프라이트 애니메이션 한 동작 (Animator 없이 SpriteAnimator가 재생).</summary>
    [CreateAssetMenu(menuName = "Roglike/Sprite Animation", fileName = "SpriteAnimation")]
    public class SpriteAnimationData : ScriptableObject
    {
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop = true;

        public bool IsValid => frames != null && frames.Length > 0;
        public float Duration => IsValid ? frames.Length / Mathf.Max(0.01f, fps) : 0f;
    }
}
