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

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return new WaitForSecondsRealtime(0.12f);
            Capture(directory, "2_attack_hit");
            yield return new WaitForSecondsRealtime(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return new WaitForSecondsRealtime(0.4f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.RightArrow));
            yield return new WaitForSecondsRealtime(0.35f);
            Capture(directory, "3_overdrive");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            InputSystem.RemoveDevice(keyboard);
            Debug.Log("[Roglike] Screenshots saved to " + directory);
        }

        /// <summary>Run 씬: 강화 선택 화면, 보스전, 결과 화면 (오버레이 UI 포함 전체 화면 캡처).</summary>
        [UnityTest]
        public IEnumerator CaptureRunMoments()
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
            Roglike.Progression.MetaProgression.SavePathOverride = Path.Combine(directory, "meta_capture.json");
            Roglike.Progression.MetaProgression.ResetData();

            SceneManager.LoadScene("Run");
            yield return new WaitForSecondsRealtime(0.2f);
            var playerHealth = UnityEngine.Object.FindFirstObjectByType<Roglike.Progression.PlayerStats>().GetComponent<Roglike.Combat.Health>();
            playerHealth.SetInvulnerable(10000f);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return CaptureScreen(directory, "4_run_room1");

            var choice = UnityEngine.Object.FindFirstObjectByType<Roglike.UI.UpgradeChoiceUI>();
            var run = UnityEngine.Object.FindFirstObjectByType<Roglike.Run.RunManager>();
            int picks = 0;
            float elapsed = 0f;
            while (!run.IsBossRoom && elapsed < 150f)
            {
                if (choice.IsOpen)
                {
                    yield return new WaitForSecondsRealtime(0.6f);
                    if (picks == 0)
                    {
                        yield return CaptureScreen(directory, "5_upgrade_choice");
                    }

                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
                    yield return new WaitForSecondsRealtime(0.1f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSecondsRealtime(0.1f);
                    picks++;
                    elapsed += 0.8f;
                    continue;
                }

                KillEnemies();
                elapsed += 0.2f;
                yield return new WaitForSecondsRealtime(0.2f);
            }

            yield return new WaitForSecondsRealtime(4f);
            var boss = UnityEngine.Object.FindFirstObjectByType<BossSlime>();
            var bossHealth = boss.GetComponent<Roglike.Combat.Health>();
            bossHealth.TryApplyDamage(new Roglike.Combat.DamageInfo(bossHealth.Max * 0.55, true, boss.transform.position, Vector2.zero, 0f, null));
            yield return new WaitForSecondsRealtime(0.25f);
            yield return CaptureScreen(directory, "6_boss");

            bossHealth.TryApplyDamage(new Roglike.Combat.DamageInfo(bossHealth.Max, true, boss.transform.position, Vector2.zero, 0f, null));
            yield return new WaitForSecondsRealtime(4f);
            yield return CaptureScreen(directory, "7_result");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.8f);
            yield return CaptureScreen(directory, "8_meta_shop");

            InputSystem.RemoveDevice(keyboard);
            Roglike.Progression.MetaProgression.SavePathOverride = null;
            Time.timeScale = 1f;
        }

        private static void KillEnemies()
        {
            foreach (var health in UnityEngine.Object.FindObjectsByType<Roglike.Combat.Health>(FindObjectsSortMode.None))
            {
                if (health.Team == Roglike.Combat.Team.Enemy && !health.IsDead && health.GetComponent<BossSlime>() == null)
                {
                    health.TryApplyDamage(new Roglike.Combat.DamageInfo(health.Current * 0.999 + 1, true, health.transform.position, Vector2.zero, 0f, null));
                }
            }
        }

        /// <summary>
        /// 배치 모드에선 화면 캡처(WaitForEndOfFrame)가 동작하지 않으므로, 오버레이 캔버스를 잠시
        /// 카메라 렌더 방식으로 바꿔 카메라 렌더에 UI까지 담는다.
        /// </summary>
        private static IEnumerator CaptureScreen(string directory, string name)
        {
            Camera camera = Camera.main;
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var switched = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.gameObject.activeInHierarchy)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                    switched.Add(canvas);
                }
            }

            Canvas.ForceUpdateCanvases();
            Capture(directory, name);

            foreach (Canvas canvas in switched)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            yield return null;
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
