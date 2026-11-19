#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Roglike.Combat;
using Roglike.Core;
using Roglike.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Roglike.Tests
{
    public class VfxArtTests
    {
        [TestCase("slash_arc", 96, 64)]
        [TestCase("impact_burst", 48, 48)]
        [TestCase("slam_wave", 128, 48)]
        public void FramesExistAndAreAssigned(string name, int width, int height)
        {
            var animation = AssetDatabase.LoadAssetAtPath<SpriteAnimationData>($"Assets/_Game/Data/Vfx/{name}.asset");
            Assert.IsNotNull(animation);
            Assert.AreEqual(4, animation.frames.Length);
            Assert.IsFalse(animation.loop);
            for (int i = 0; i < 4; i++)
            {
                string path = $"Assets/_Game/Art/Generated/vfx/{name}_{i}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreSame(sprite, animation.frames[i]);
                Assert.AreEqual(new Vector2(width, height), sprite.rect.size);
                Assert.AreEqual(sprite.rect.size * 0.5f, sprite.pivot);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.AreEqual(FilterMode.Point, importer.filterMode);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
                Assert.AreEqual(48f, importer.spritePixelsPerUnit);
            }
        }

        [UnityTest]
        public IEnumerator ScenesAssignAllAnimations_AndHitStopDoesNotFreezeSlash()
        {
            foreach (string scene in new[] { "Prototype", "Run" })
            {
                SceneManager.LoadScene(scene);
                yield return null;
                Assert.IsNotNull(VfxSprites.SlashAnimation, scene);
                Assert.IsNotNull(VfxSprites.ImpactAnimation, scene);
                Assert.IsNotNull(VfxSprites.SlamAnimation, scene);
            }
            SlashEffect slash = SlashEffect.Spawn(Vector2.zero, new Vector2(2f, 1f), 25f, Color.white, 1f, true, true);
            var renderer = slash.GetComponent<SpriteRenderer>();
            Assert.AreSame(VfxSprites.SlashAnimation.frames[0], renderer.sprite);
            Assert.IsTrue(renderer.flipX);
            Assert.IsTrue(renderer.flipY);
            Assert.That(VfxSprites.SlashAnimation.Duration, Is.InRange(0.1f, 0.12f));
            float previousScale = Time.timeScale;
            try
            {
                TimeController.Instance.SetPaused(true);
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(VfxSprites.SlashAnimation.Duration + 0.08f);
                Assert.IsTrue(slash == null, "VFX must finish even during hit stop.");
            }
            finally
            {
                TimeController.Instance.SetPaused(false);
                Time.timeScale = previousScale;
            }
        }

        [UnityTest]
        public IEnumerator MissingAnimationKeepsRectangleFallback()
        {
            SceneManager.LoadScene("Prototype");
            yield return null;
            var slash = SlashEffect.SpawnAnimation(null, Vector2.zero, Vector2.one, 0f, Color.white, 0.1f);
            Assert.AreSame(VfxSprites.Square, slash.GetComponent<SpriteRenderer>().sprite);
            Assert.IsNotNull(slash.transform.Find("Core"));
            Object.Destroy(slash.gameObject);
        }
    }
}
#endif
