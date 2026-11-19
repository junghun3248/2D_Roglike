using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    /// <summary>
    /// Assets/_Game/Art/Generated 아래 그림(Tools/art/process_gpt_pilot.py 출력)의 가져오기 설정.
    /// 픽셀아트: Point 필터, 압축 없음, 밉맵 없음. PPU 48 → 주인공(96px)이 약 2 월드 단위.
    /// 애니메이션 프레임(하위 폴더)은 발밑 가운데 피벗, 바닥·발판은 반복(Repeat)+FullRect (Tiled 그리기용).
    /// </summary>
    public class GeneratedArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/_Game/Art/Generated/";
        public const float PixelsPerUnit = 48f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool isFrame = assetPath.Replace('\\', '/').Substring(Folder.Length).Contains("/");
            bool tiles = fileName.StartsWith("ground_") || fileName == "platform";
            bool background = fileName.StartsWith("bg_");

            importer.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (assetPath.Replace('\\', '/').StartsWith(Folder + "ui/")
                || assetPath.Replace('\\', '/').StartsWith(Folder + "vfx/"))
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = Vector2.one * 0.5f;
            }
            else if (isFrame)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0f);
            }
            else if (tiles)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            else if (background)
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            }

            importer.SetTextureSettings(settings);
        }
    }
}
