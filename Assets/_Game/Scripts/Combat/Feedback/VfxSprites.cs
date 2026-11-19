using UnityEngine;
using Roglike.Data;

namespace Roglike.Combat
{
    /// <summary>
    /// 자리표시 이펙트용 공용 스프라이트·머티리얼. 씬의 VfxSprites 컴포넌트가 에셋을 넘겨준다.
    /// 아트가 들어오면 이펙트별 프리팹으로 교체한다.
    /// </summary>
    public class VfxSprites : MonoBehaviour
    {
        [SerializeField] private Sprite _square;
        [SerializeField] private Sprite _circle;
        [SerializeField] private Material _material;
        [SerializeField] private SpriteAnimationData _slashAnimation;
        [SerializeField] private SpriteAnimationData _impactAnimation;
        [SerializeField] private SpriteAnimationData _slamAnimation;

        public static SpriteAnimationData SlashAnimation { get; private set; }
        public static SpriteAnimationData ImpactAnimation { get; private set; }
        public static SpriteAnimationData SlamAnimation { get; private set; }
        [Header("투사체 그림 (없으면 자리표시 도형)")]
        [SerializeField] private Sprite _gearProjectile;
        [SerializeField] private Sprite _leafProjectile;

        public static Sprite Square { get; private set; }
        public static Sprite Circle { get; private set; }
        public static Material Material { get; private set; }
        public static Sprite GearSprite { get; private set; }
        public static Sprite LeafSprite { get; private set; }

        private void Awake()
        {
            Square = _square;
            Circle = _circle;
            Material = _material;
            SlashAnimation = _slashAnimation;
            ImpactAnimation = _impactAnimation;
            SlamAnimation = _slamAnimation;
            GearSprite = _gearProjectile;
            LeafSprite = _leafProjectile;
        }

        public static SpriteRenderer CreateRenderer(string name, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (Material != null)
            {
                renderer.sharedMaterial = Material;
            }

            return renderer;
        }
    }
}
