using Roglike.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    /// <summary>런 진행 HUD: 구역 진행도, 처치 수, 시간, 구역 시작·클리어 배너.</summary>
    public class RunHud : MonoBehaviour
    {
        [SerializeField] private RunManager _run;

        private Text _progress;
        private Text _stats;
        private Text _banner;
        private string _lastBanner;
        private float _bannerShownAt;

        private void Awake()
        {
            Canvas canvas = UiFactory.CreateCanvas("RunHud", 31, transform);

            _progress = UiFactory.CreateText("Progress", canvas.transform, 30, Color.white, TextAnchor.UpperRight);
            PlaceTopRight(_progress, new Vector2(-40f, -36f));

            _stats = UiFactory.CreateText("Stats", canvas.transform, 24, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperRight);
            PlaceTopRight(_stats, new Vector2(-40f, -78f));

            _banner = UiFactory.CreateText("Banner", canvas.transform, 84, Color.white, TextAnchor.MiddleCenter);
            var rect = (RectTransform)_banner.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.68f);
            rect.sizeDelta = new Vector2(1400f, 140f);
            _banner.enabled = false;
        }

        private void LateUpdate()
        {
            if (_run == null)
            {
                return;
            }

            int room = Mathf.Min(_run.RoomIndex + 1, _run.RoomCount);
            _progress.text = _run.IsBossRoom ? $"보스  ({room} / {_run.RoomCount})" : $"{room}구역 / {_run.RoomCount}";

            int minutes = Mathf.FloorToInt(_run.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(_run.ElapsedSeconds % 60f);
            _stats.text = $"처치 {_run.Kills}   남은 적 {_run.EnemiesAlive}   {minutes:00}:{seconds:00}";

            UpdateBanner(_run.Banner);
        }

        private void UpdateBanner(string text)
        {
            if (text != _lastBanner)
            {
                _lastBanner = text;
                _bannerShownAt = Time.unscaledTime;
                _banner.text = text;
            }

            _banner.enabled = !string.IsNullOrEmpty(text);
            if (!_banner.enabled)
            {
                return;
            }

            // 등장 시 크게 튀었다가 제자리.
            float t = Mathf.Clamp01((Time.unscaledTime - _bannerShownAt) / 0.18f);
            float scale = Mathf.Lerp(1.6f, 1f, 1f - (1f - t) * (1f - t));
            _banner.rectTransform.localScale = new Vector3(scale, scale, 1f);
            bool boss = text == "BOSS";
            bool clear = text == "CLEAR!";
            _banner.color = boss ? new Color(1f, 0.35f, 0.45f) : clear ? new Color(1f, 0.85f, 0.35f) : Color.white;
        }

        private static void PlaceTopRight(Text text, Vector2 position)
        {
            var rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(700f, 40f);
        }
    }
}
