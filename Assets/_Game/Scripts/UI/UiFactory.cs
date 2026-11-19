using Roglike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    /// <summary>M1용 코드 생성 UI 도우미. 정식 UI 디자인이 생기면 프리팹으로 교체.</summary>
    public static class UiFactory
    {
        private static Font _font;
        private static Sprite _circle;
        private static Sprite _roundedSquare;
        private static Sprite _panelBorder;

        private static Sprite PanelBorder
        {
            get
            {
                if (_panelBorder != null) return _panelBorder;
                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var pixels = new Color[64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                _panelBorder = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f,
                    100f, 0, SpriteMeshType.FullRect, Vector4.one * UiTheme.Border);
                return _panelBorder;
            }
        }

        public static Sprite RoundedSquare
        {
            get
            {
                if (_roundedSquare != null) return _roundedSquare;
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        var corner = new Vector2(Mathf.Max(0f, Mathf.Abs(x - 15.5f) - 9f), Mathf.Max(0f, Mathf.Abs(y - 15.5f) - 9f));
                        pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(6.5f - corner.magnitude));
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                _roundedSquare = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f,
                    100f, 0, SpriteMeshType.FullRect, Vector4.one * 8f);
                return _roundedSquare;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                texture.name = "UI Circle (runtime)";
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        pixels[y * 64 + x] = new Color(1f, 1f, 1f,
                            Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f))));
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                _circle = Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
                return _circle;
            }
        }

        public static void SetCircle(Sprite sprite) { if (sprite != null) _circle = sprite; }

        public static RectTransform CreatePanel(Transform parent, Vector2 size, string name = "Panel", Color? border = null, Color? background = null)
        {
            RectTransform panel = CreateRect(name, parent, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, size);
            Image frame = panel.gameObject.AddComponent<Image>();
            frame.sprite = PanelBorder;
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.color = border ?? UiTheme.Gold;
            frame.raycastTarget = false;
            Image shadow = CreateImage("InnerShadow", panel, new Color(0f, 0f, 0f, 0.22f));
            shadow.sprite = PanelBorder;
            shadow.type = Image.Type.Sliced;
            shadow.fillCenter = false;
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = Vector2.one * UiTheme.Border;
            shadow.rectTransform.offsetMax = -Vector2.one * UiTheme.Border;
            Image fill = CreateImage("PanelFill", panel, background ?? UiTheme.Panel);
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(4f, 4f);
            fill.rectTransform.offsetMax = new Vector2(-4f, -6f);
            Image highlight = CreateImage("TopHighlight", panel, new Color(1f, 1f, 1f, 0.13f));
            highlight.rectTransform.anchorMin = new Vector2(0f, 1f);
            highlight.rectTransform.anchorMax = Vector2.one;
            highlight.rectTransform.offsetMin = new Vector2(4f, -5f);
            highlight.rectTransform.offsetMax = new Vector2(-4f, -3f);
            return panel;
        }

        public static Image CreateRadialFill(string name, Transform parent, Color color)
        {
            Image image = CreateImage(name, parent, color);
            image.sprite = Circle;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            Stretch(image.rectTransform);
            return image;
        }

        public static Image CreateIcon(string name, Transform parent, Sprite sprite, Color fallback)
        {
            Image image = CreateImage(name, parent, sprite != null ? Color.white : fallback);
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        public static RectTransform CreateTraitPlate(Transform parent, SynergyData data, bool active)
        {
            Image rim = CreateImage("TraitPlate", parent, active ? data.color : UiTheme.Muted);
            rim.sprite = RoundedSquare;
            rim.type = Image.Type.Sliced;
            rim.rectTransform.sizeDelta = Vector2.one * UiTheme.TraitPlateSize;
            Image fill = CreateImage("PlateFill", rim.transform, UiTheme.BadgeBackground);
            fill.sprite = RoundedSquare;
            fill.type = Image.Type.Sliced;
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = Vector2.one * UiTheme.Border;
            fill.rectTransform.offsetMax = -Vector2.one * UiTheme.Border;
            Image icon = CreateIcon("Icon", rim.transform, data.Icon, data.color);
            icon.rectTransform.sizeDelta = Vector2.one * UiTheme.TraitIconSize;
            return rim.rectTransform;
        }

        public static Text Label(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Color? color = null)
        {
            Text text = CreateText(name, parent, size, color ?? UiTheme.Text, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.one * 0.5f;
            text.rectTransform.sizeDelta = dimensions;
            text.rectTransform.anchoredPosition = position;
            text.text = value;
            return text;
        }

        // 픽셀 한글 글꼴 Galmuri11 Bold (SIL OFL 1.1, Resources/Fonts/Galmuri-OFL.txt). 없으면 Unity 기본 글꼴.
        // 굵은 글꼴 파일을 쓰므로 Text의 합성 굵게(fontStyle Bold)는 끈다 — 픽셀이 뭉개진다.
        private const string FontResource = "Fonts/Galmuri11-Bold";

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>(FontResource);
                    UsingPixelFont = _font != null;
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }

                return _font;
            }
        }

        public static bool UsingPixelFont { get; private set; }

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(string name, Transform parent, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = UsingPixelFont ? FontStyle.Normal : FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        /// <summary>부모를 꽉 채우도록 늘린다.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
