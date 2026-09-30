using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Menu backdrop decoration: faint rounded pieces drifting up and slowly turning. Created once in Start,
    /// recycled from the bottom when they leave the top.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class FloatingPiecesView : MonoBehaviour
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField, Range(1, 40)] private int _count = 16;
        [SerializeField, Range(0f, 1f)] private float _alpha = 0.12f;
        [SerializeField] private Vector2 _sizeRange = new Vector2(60f, 150f);
        [SerializeField] private Vector2 _speedRange = new Vector2(25f, 70f);
        [SerializeField] private Color[] _colors =
        {
            new Color(0.91f, 0.30f, 0.24f), new Color(0.95f, 0.77f, 0.06f), new Color(0.18f, 0.80f, 0.44f),
            new Color(0.20f, 0.60f, 0.86f), new Color(0.61f, 0.35f, 0.71f), new Color(0.98f, 0.55f, 0.16f),
        };

        private RectTransform _area;
        private RectTransform[] _pieces;
        private float[] _speed;
        private float[] _spin;

        private void Start()
        {
            _area = (RectTransform)transform;
            _pieces = new RectTransform[_count];
            _speed = new float[_count];
            _spin = new float[_count];
            Vector2 size = _area.rect.size;
            for (int i = 0; i < _count; i++)
            {
                var go = new GameObject("Floater", typeof(RectTransform));
                go.transform.SetParent(_area, false);
                var image = go.AddComponent<Image>();
                image.sprite = _sprite;
                image.raycastTarget = false;
                Color color = _colors[i % _colors.Length];
                color.a = _alpha;
                image.color = color;
                _pieces[i] = (RectTransform)go.transform;
                float side = Random.Range(_sizeRange.x, _sizeRange.y);
                _pieces[i].sizeDelta = new Vector2(side, side);
                _pieces[i].anchoredPosition = new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
                _pieces[i].localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                _speed[i] = Random.Range(_speedRange.x, _speedRange.y);
                _spin[i] = Random.Range(-25f, 25f);
            }
        }

        private void Update()
        {
            if (_pieces == null) return;
            Vector2 size = _area.rect.size;
            float dt = Time.deltaTime;
            for (int i = 0; i < _pieces.Length; i++)
            {
                RectTransform piece = _pieces[i];
                Vector2 position = piece.anchoredPosition;
                position.y += _speed[i] * dt;
                float margin = piece.sizeDelta.y;
                if (position.y > 0.5f * size.y + margin)
                {
                    position.y = -0.5f * size.y - margin; // back in from the bottom, somewhere new
                    position.x = Random.Range(-0.5f, 0.5f) * size.x;
                }
                piece.anchoredPosition = position;
                piece.localRotation = Quaternion.Euler(0f, 0f, piece.localEulerAngles.z + _spin[i] * dt);
            }
        }
    }
}
