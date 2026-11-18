using UnityEngine;

namespace Roglike.Core
{
    /// <summary>
    /// 시차 배경 한 겹. 카메라를 factor 비율로 따라가고(1 = 고정된 하늘, 0 = 땅과 같이 움직임),
    /// 가로로 3장을 이어 붙여 끝없이 반복한다. 이음매가 티 나지 않도록 가운데 장을 좌우 반전한다.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _factor = 0.5f;
        [SerializeField] private float _verticalFactor = 0.9f;
        [SerializeField] private Vector2 _offset;
        [SerializeField] private float _tileWidth = 20f;

        private Transform _camera;

        private void Start()
        {
            if (Camera.main != null)
            {
                _camera = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            Vector3 cam = _camera.position;
            float x = cam.x * _factor;
            // 카메라 기준으로 가장 가까운 반복 위치에 맞춘다.
            float relative = cam.x - x;
            float wrap = Mathf.Round(relative / _tileWidth) * _tileWidth;
            transform.position = new Vector3(x + wrap + _offset.x, cam.y * _verticalFactor + _offset.y, transform.position.z);
        }
    }
}
