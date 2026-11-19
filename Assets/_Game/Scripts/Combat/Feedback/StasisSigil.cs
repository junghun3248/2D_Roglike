using UnityEngine;

namespace Roglike.Combat
{
    public class StasisSigil : MonoBehaviour
    {
        private const int Segments = 48;
        private LineRenderer[] _lines;
        private Material _material;
        private float _duration;
        private float _elapsed;
        private Color _color;

        public static void Spawn(Vector2 center, float radius, Color color, float duration)
        {
            var sigil = new GameObject("Stasis Clock Sigil").AddComponent<StasisSigil>();
            sigil.transform.position = center;
            sigil._duration = duration;
            sigil._color = color;
            sigil._material = new Material(Shader.Find("Sprites/Default"));
            sigil._lines = new LineRenderer[3];
            var ring = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                ring[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            sigil._lines[0] = sigil.Line("Ring", ring, true);
            sigil._lines[1] = sigil.Line("Minute", new[] { Vector3.zero, Vector3.up * radius * 0.8f }, false);
            sigil._lines[2] = sigil.Line("Hour", new[] { Vector3.zero, Vector3.right * radius * 0.5f }, false);
        }

        private LineRenderer Line(string name, Vector3[] points, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = _material;
            line.widthMultiplier = 0.055f;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.loop = loop;
            line.sortingOrder = 25;
            line.startColor = line.endColor = _color;
            return line;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime * Core.TimeController.PlayerTimeMultiplier;
            Color color = _color;
            color.a *= 1f - Mathf.Clamp01(_elapsed / _duration);
            foreach (LineRenderer line in _lines) line.startColor = line.endColor = color;
            if (_elapsed >= _duration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
