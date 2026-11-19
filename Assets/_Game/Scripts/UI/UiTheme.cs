using Roglike.Data;
using UnityEngine;

namespace Roglike.UI
{
    public static class UiTheme
    {
        public static readonly Color Panel = new Color32(20, 26, 42, 224);
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
