using Roglike.Core;
using Roglike.Data;
using Roglike.Progression;
using Roglike.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    public class RunHud : MonoBehaviour
    {
        [SerializeField] private RunManager _run;
        private Text _progress, _stats, _banner;
        private RectTransform _track, _plate;
        private CanvasGroup[] _nodes;
        private Image[] _nodeFills;
        private string _lastBanner;
        private float _bannerShownAt;
        private int _lastSecond = -1, _lastKills = -1, _lastCrystals = -1;

        private void Awake()
        {
            var canvas = UiFactory.CreateCanvas("RunHud", 31, transform);
            var root = UiFactory.CreateRect("RunProgress", canvas.transform, Vector2.one, Vector2.one,
                new Vector2(-32f, -32f), new Vector2(430f, 130f));
            _progress = UiFactory.Label("Area", root, "연습장", 26, new Vector2(0f, 45f), new Vector2(430f, 38f));
            _progress.alignment = TextAnchor.MiddleRight;
            _track = UiFactory.CreateRect("RoomTrack", root, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(400f, 36f));
            _stats = UiFactory.Label("Currency", root, "", 20, new Vector2(0f, -48f), new Vector2(430f, 30f), UiTheme.Muted);
            _stats.alignment = TextAnchor.MiddleRight;
            _plate = UiFactory.CreatePanel(canvas.transform, new Vector2(720f, 100f), "BannerPlate");
            _plate.anchorMin = _plate.anchorMax = new Vector2(0.5f, 0.68f);
            _banner = UiFactory.Label("Banner", _plate, "", 48, Vector2.zero, new Vector2(690f, 90f));
            foreach (float side in new[] { -1f, 1f })
            {
                var line = UiFactory.CreateImage("GoldLine", _plate, UiTheme.Gold);
                line.rectTransform.sizeDelta = new Vector2(140f, 2f);
                line.rectTransform.anchoredPosition = new Vector2(side * 440f, 0f);
            }
            _plate.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (_run == null) _run = FindFirstObjectByType<RunManager>();
            if (_run == null || _run.Area == null) { _track.gameObject.SetActive(false); return; }
            _progress.text = $"{_run.AreaIndex + 1}지역 · {_run.Area.displayName}";
            int count = _run.RoomCount;
            _nodes = new CanvasGroup[count];
            _nodeFills = new Image[count];
            float spacing = 360f / Mathf.Max(1, count - 1);
            var connector = UiFactory.CreateImage("Connection", _track, UiTheme.Gold);
            connector.rectTransform.sizeDelta = new Vector2(count > 1 ? 360f : 0f, 2f);
            for (int i = 0; i < count; i++)
            {
                RoomType type = _run.Area.rooms[i].type;
                var node = UiFactory.CreateRect("Room" + i, _track, Vector2.one * 0.5f, Vector2.one * 0.5f,
                    new Vector2(count == 1 ? 0f : -180f + i * spacing, 0f), Vector2.one * (type == RoomType.Boss ? 34f : 24f));
                _nodes[i] = node.gameObject.AddComponent<CanvasGroup>();
                Color color = type == RoomType.Rest ? new Color(0.4f, 0.9f, 0.6f)
                    : type == RoomType.Elite ? UiTheme.Health : UiTheme.Gold;
                var icon = UiFactory.CreateImage("Icon", node, color);
                UiFactory.Stretch(icon.rectTransform);
                if (type == RoomType.Rest) icon.sprite = UiFactory.Circle;
                else if (type == RoomType.Combat || type == RoomType.Elite)
                {
                    icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    icon.rectTransform.sizeDelta = -Vector2.one * 6f;
                }
                else if (type == RoomType.Boss)
                {
                    // 세 개의 뿔로 보스를 나타내는 임시 왕관.
                    for (int horn = -1; horn <= 1; horn++)
                    {
                        var point = UiFactory.CreateImage("CrownPoint", node, color);
                        point.rectTransform.sizeDelta = new Vector2(7f, 14f);
                        point.rectTransform.anchoredPosition = new Vector2(horn * 12f, 19f);
                    }
                }
                var fill = UiFactory.CreateImage("Uncleared", icon.transform, UiTheme.Panel);
                UiFactory.Stretch(fill.rectTransform);
                fill.rectTransform.offsetMin = Vector2.one * 4f;
                fill.rectTransform.offsetMax = -Vector2.one * 4f;
                if (type == RoomType.Rest) fill.sprite = UiFactory.Circle;
                _nodeFills[i] = fill;
            }
            var gem = UiFactory.CreateImage("CrystalGem", _stats.transform, UiTheme.Cyan);
            gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            gem.rectTransform.sizeDelta = Vector2.one * 15f;
            gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private void LateUpdate()
        {
            if (_run == null) return;
            int seconds = Mathf.FloorToInt(_run.ElapsedSeconds);
            int crystals = MetaProgression.Crystals;
            if (seconds != _lastSecond || _run.Kills != _lastKills || crystals != _lastCrystals)
            {
                _lastSecond = seconds; _lastKills = _run.Kills; _lastCrystals = crystals;
                _stats.text = $"{NumberFormat.ToKorean(crystals)} 결정   ·   {_run.Kills} 처치   {seconds / 60:00}:{seconds % 60:00}";
            }
            if (_nodes != null)
                for (int i = 0; i < _nodes.Length; i++)
                {
                    bool current = i == _run.RoomIndex;
                    _nodes[i].alpha = i < _run.RoomIndex ? 1f : current ? 0.75f + Mathf.Sin(Time.unscaledTime * 5f) * 0.25f : 0.3f;
                    _nodeFills[i].enabled = i > _run.RoomIndex;
                    _nodes[i].transform.localScale = Vector3.one * (current ? 1.12f : 1f);
                }
            string banner = _run.Banner;
            if (banner != _lastBanner)
            {
                _lastBanner = banner; _bannerShownAt = Time.unscaledTime; _banner.text = banner;
            }
            _plate.gameObject.SetActive(!string.IsNullOrEmpty(banner));
            float t = Mathf.Clamp01((Time.unscaledTime - _bannerShownAt) / 0.18f);
            _plate.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            _banner.color = banner == "보스" || banner == "엘리트!" ? UiTheme.Health : UiTheme.Gold;
        }
    }
}
