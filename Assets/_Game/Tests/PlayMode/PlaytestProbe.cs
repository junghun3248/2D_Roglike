using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roglike.Combat;
using Roglike.Core;
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
    /// 손맛 점검용 자동 플레이: 가상 키 입력으로 평타 연타·공중 공격을 하며 매 프레임 위치·시간 배율·소리 요청을
    /// CSV로 남기고, 공격 순간을 연속 캡처한다. 실행: ROGLIKE_SCREENSHOT_DIR 지정 후 -testFilter Roglike.Tests.PlaytestProbe
    /// </summary>
    [Explicit("수동 실행 전용")]
    public class PlaytestProbe
    {
        private Keyboard _keyboard;
        private string _directory;
        private string _lastSound = "";

        [UnityTest]
        public IEnumerator RecordAttackFeel()
        {
            _directory = Environment.GetEnvironmentVariable("ROGLIKE_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(_directory)) _directory = Path.Combine(Application.temporaryCachePath, "Probe");
            Directory.CreateDirectory(_directory);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("ProbeKeyboard");

            SceneManager.LoadScene("Prototype");
            yield return new WaitForSecondsRealtime(1.5f);
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.SoundRequested += id => _lastSound += id + " ";

            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            UnityEngine.Object.Destroy(UnityEngine.Object.FindFirstObjectByType<EnemySpawner>().gameObject);
            Health[] enemies = Enemies();
            for (int i = 1; i < enemies.Length; i++) UnityEngine.Object.Destroy(enemies[i].gameObject);
            Health target = enemies[0];
            foreach (MonoBehaviour behaviour in target.GetComponents<MonoBehaviour>())
                if (behaviour is BoarEnemy || behaviour is SquirrelEnemy || behaviour is SlimeEnemy) behaviour.enabled = false;
            yield return Settle(player);

            // 1) 지상 평타 연타: 적을 앞에 세우고 X를 1.2초 누른다. 처음 0.6초는 연속 캡처.
            var body = target.GetComponent<Rigidbody2D>();
            body.position = (Vector2)player.transform.position + new Vector2(1.3f, -0.35f);
            Physics2D.SyncTransforms();
            var csv = new StringBuilder("phase,t,frame,px,py,vx,vy,timeScale,hitStop,attacking,grounded,ex,camx,camy,camSize,sound\n");
            Press(Key.X);
            float start = Time.realtimeSinceStartup;
            int shot = 0;
            float nextShot = 0f;
            while (Time.realtimeSinceStartup - start < 1.2f)
            {
                float t = Time.realtimeSinceStartup - start;
                Row(csv, "ground", t, player, target);
                if (t >= nextShot && t < 0.6f)
                {
                    Capture($"g_{shot++:00}");
                    nextShot += 1f / 30f;
                }
                yield return null;
            }
            Release();
            yield return new WaitForSecondsRealtime(0.4f);

            // 2) 점프만 / 점프+X 연타 / 점프+오른쪽+X: 체공 시간과 궤적 비교. 적은 치운다.
            if (target != null) UnityEngine.Object.Destroy(target.gameObject);
            yield return Settle(player);
            yield return Air(csv, "jump_only", player, Key.Z);
            yield return Air(csv, "jump_x", player, Key.Z, Key.X);
            yield return Air(csv, "jump_right_x", player, Key.Z, Key.X, Key.RightArrow);
            yield return Air(csv, "jump_right", player, Key.Z, Key.RightArrow);

            File.WriteAllText(Path.Combine(_directory, "probe.csv"), csv.ToString());
            InputSystem.RemoveDevice(_keyboard);
            Time.timeScale = 1f;
        }

        private IEnumerator Air(StringBuilder csv, string phase, PlayerController player, params Key[] keys)
        {
            yield return Settle(player);
            float start = Time.realtimeSinceStartup;
            Press(keys);
            bool left = false;
            while (Time.realtimeSinceStartup - start < 2f)
            {
                float t = Time.realtimeSinceStartup - start;
                Row(csv, phase, t, player, null);
                if (!player.IsGrounded) left = true;
                if (left && player.IsGrounded && t > 0.15f) break;
                yield return null;
            }
            Release();
            Row(csv, phase + "_end", Time.realtimeSinceStartup - start, player, null);
        }

        private void Row(StringBuilder csv, string phase, float t, PlayerController player, Health enemy)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            Camera cam = Camera.main;
            csv.Append(phase).Append(',').Append(t.ToString("0.000")).Append(',').Append(Time.frameCount).Append(',')
                .Append(player.transform.position.x.ToString("0.000")).Append(',').Append(player.transform.position.y.ToString("0.000")).Append(',')
                .Append(rb.linearVelocity.x.ToString("0.00")).Append(',').Append(rb.linearVelocity.y.ToString("0.00")).Append(',')
                .Append(Time.timeScale.ToString("0.00")).Append(',').Append(TimeController.Instance.IsHitStopped ? 1 : 0).Append(',')
                .Append(player.IsAttacking ? 1 : 0).Append(',').Append(player.IsGrounded ? 1 : 0).Append(',')
                .Append(enemy != null ? enemy.transform.position.x.ToString("0.000") : "").Append(',')
                .Append(cam.transform.position.x.ToString("0.000")).Append(',').Append(cam.transform.position.y.ToString("0.000")).Append(',')
                .Append(cam.orthographicSize.ToString("0.000")).Append(',').Append(_lastSound.Trim()).Append('\n');
            _lastSound = "";
        }

        private static IEnumerator Settle(PlayerController player)
        {
            float waited = 0f;
            while ((player.IsHurt || !player.IsGrounded || player.IsAttacking) && waited < 3f)
            {
                waited += 0.05f;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private static Health[] Enemies() => UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None)
            .Where(h => h.Team == Team.Enemy).ToArray();

        private void Press(params Key[] keys) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        private void Release() => InputSystem.QueueStateEvent(_keyboard, new KeyboardState());

        private void Capture(string name)
        {
            Camera camera = Camera.main;
            var rt = new RenderTexture(640, 360, 24);
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(_directory, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
        }
    }
}
