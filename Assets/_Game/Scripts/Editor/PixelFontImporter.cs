using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    /// <summary>
    /// 픽셀 글꼴(Resources/Fonts/Galmuri*) 가져오기 설정: 동적 글꼴 + 힌팅 래스터(안티앨리어싱 없이 또렷하게).
    /// </summary>
    public class PixelFontImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/_Game/Resources/Fonts/";

        private void OnPreprocessAsset()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder) || !(assetImporter is TrueTypeFontImporter importer))
            {
                return;
            }

            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.includeFontData = true;
        }
    }
}
