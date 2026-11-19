using Roglike.Data;
using UnityEngine;

namespace Roglike.UI
{
    public static class UiTheme
    {
        public static readonly Color Panel = new Color32(20, 26, 42, 224);
        public static readonly Color CardBackground = new Color32(16, 21, 34, 250);
        public static readonly Color BadgeBackground = new Color32(16, 21, 34, 245);
        public static readonly Color ReadyFlash = Color.white;
        public const float InactiveAlpha = 0.5f;
        public const float TraitIconSize = 32f;
        public const float TraitPlateSize = 40f;
        public const float TraitCountOffset = 48f;
        public const float TraitBadgeWidth = 132f;
        public const float DetailPadding = 24f;
        public const float DetailIconGap = 12f;
        public const float TagIconSize = 22f;
        public const float TagIconInset = 5f;
        public const float CardIconSize = 64f;
        public const float CardIconY = 174f;
        public const float CardRarityY = 232f;
        public const float CardNameY = 100f;
        public const float CardNameHeight = 80f;
        public const float CardTagsY = 35f;
        public const float CardBodyY = -65f;
        public static readonly Color Gold = new Color32(232, 196, 106, 255);
        public static readonly Color Text = Color.white;
        public static readonly Color Muted = new Color32(185, 194, 214, 255);
        public static readonly Color Health = new Color32(229, 72, 77, 255);
        public static readonly Color HealthBack = new Color32(90, 30, 37, 255);
        public static readonly Color Cyan = new Color32(86, 219, 244, 255);
        public static readonly Color Magenta = new Color32(234, 100, 220, 255);
        public static readonly Color Enraged = new Color32(255, 137, 60, 255);
        public const float Border = 2f;
        public static readonly Vector2 ClusterSize = new Vector2(560f, 180f);
        public static Color Rarity(UpgradeRarity rarity) => rarity == UpgradeRarity.Epic ? Gold
            : rarity == UpgradeRarity.Rare ? new Color32(89, 191, 255, 255) : new Color32(209, 216, 228, 255);
    }
}
