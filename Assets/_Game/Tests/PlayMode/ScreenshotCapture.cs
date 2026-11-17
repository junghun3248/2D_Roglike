using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roglike.Enemies;
using Roglike.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Roglike.Tests
{
    /// <summary>
    /// AI·사람이 화면을 눈으로 확인하기 위한 스크린샷 캡처 (월드만, 오버레이 UI 제외).
    /// 평소 테스트 실행에서는 건너뛴다. 실행: 환경변수 ROGLIKE_SCREENSHOT_DIR 지정 후
    /// -runTests -testPlatform PlayMode -testFilter Roglike.Tests.ScreenshotCapture
    /// </summary>
    [Explicit("수동 실행 전용")]
    public class ScreenshotCapture
    {
        [UnityTest]
        public IEnumerator CaptureCombatMoments()
        {
            string directory = Environment.GetEnvironmentVariable("ROGLIKE_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.Combine(Application.temporaryCachePath, "Screenshots");
            }

            Directory.CreateDirectory(directory);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>("CaptureKeyboard");

            SceneManager.LoadScene("Prototype");
            yield return new WaitForSecondsRealtime(1.5f);
            Capture(directory, "1_idle");

            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            SlimeEnemy slime = UnityEngine.Object.FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None).First();
            var body = slime.GetComponent<Rigidbody2D>();
            body.position = (Vector2)player.transform.position + new Vector2(1.3f, -0.35f);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            yield return new WaitForSecondsRealtime(0.12f);
            Capture(directory, "2_attack_hit");
            yield return new WaitForSecondsRealtime(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.L));
            yield return new WaitForSecondsRealtime(0.4f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J, Key.D));
            yield return new WaitForSecondsRealtime(0.35f);
            Capture(directory, "3_overdrive");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            InputSystem.RemoveDevice(keyboard);
            Debug.Log("[Roglike] Screenshots saved to " + directory);
        }

        private static void Capture(string directory, string name)
        {
            var overdrive = UnityEngine.Object.FindFirstObjectByType<OverdriveController>();
            int slashes = UnityEngine.Object.FindObjectsByType<Roglike.Combat.SlashEffect>(FindObjectsSortMode.None).Length;
            int sparks = UnityEngine.Object.FindObjectsByType<Roglike.Combat.HitSpark>(FindObjectsSortMode.None).Length;
            int afterimages = UnityEngine.Object.FindObjectsByType<Roglike.Combat.Afterimage>(FindObjectsSortMode.None).Length;
            Debug.Log($"[Roglike] capture {name}: frame={Time.frameCount} timeScale={Time.timeScale} overdrive={(overdrive != null && overdrive.IsActive)} " +
                      $"slashes={slashes} sparks={sparks} afterimages={afterimages} camSize={Camera.main.orthographicSize:0.00}");

            Camera camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;

            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            target.Release();
            UnityEngine.Object.Destroy(target);
        }
    }
}
