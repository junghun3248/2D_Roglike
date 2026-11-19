#if UNITY_EDITOR
using NUnit.Framework;
using Roglike.Data;
using Roglike.UI;
using UnityEditor;
using UnityEngine;

namespace Roglike.Tests
{
    public class GeneratedUiArtTests
    {
        [Test]
        public void EverySkillAssetHasAnIcon()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Game/Data" });
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(5));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SkillDefinition>(path).Icon, path);
            }
        }

        [Test]
        public void EverySynergyAssetHasAnIcon()
        {
            string[] guids = AssetDatabase.FindAssets("t:SynergyData", new[] { "Assets/_Game/Data" });
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(6));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SynergyData>(path).Icon, path);
            }
        }

        [Test]
        public void GeneratedUiSpritesUseCenteredUncompressedPointImport()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art/Generated/ui" });
            Assert.That(guids.Length, Is.EqualTo(12));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.AreEqual(sprite.rect.size * 0.5f, sprite.pivot, path);
            }
            Assert.GreaterOrEqual(UiTheme.CardBackground.a, 0.92f);
        }
    }
}
#endif
