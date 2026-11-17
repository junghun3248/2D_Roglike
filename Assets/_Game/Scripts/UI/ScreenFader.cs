using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Roglike.UI
{
    /// <summary>화면 전체 페이드. 일시정지 중에도 동작하도록 실시간 기준.</summary>
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private Color _color = new Color(0.05f, 0.04f, 0.08f, 1f);

        private Image _image;

        private void Awake()
        {
            Canvas canvas = UiFactory.CreateCanvas("ScreenFader", 90, transform);
            _image = UiFactory.CreateImage("Fade", canvas.transform, _color);
            UiFactory.Stretch((RectTransform)_image.transform);
            SetAlpha(1f);
        }

        public void SetAlpha(float alpha)
        {
            _image.color = new Color(_color.r, _color.g, _color.b, alpha);
            _image.enabled = alpha > 0.001f;
        }

        public IEnumerator FadeTo(float alpha, float realSeconds)
        {
            float start = _image.enabled ? _image.color.a : 0f;
            _image.enabled = true;
            float time = 0f;
            while (time < realSeconds)
            {
                time += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(start, alpha, Mathf.SmoothStep(0f, 1f, time / realSeconds)));
                yield return null;
            }

            SetAlpha(alpha);
        }
    }
}
