using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>Fades the scene in from a solid color when it loads, then switches itself off.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float _duration = 0.45f;

        private Image _image;
        private float _time;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
            SetAlpha(1f);
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_time / _duration);
            SetAlpha(1f - t * t * (3f - 2f * t));
            if (t >= 1f) gameObject.SetActive(false);
        }

        private void SetAlpha(float alpha)
        {
            Color color = _image.color;
            color.a = alpha;
            _image.color = color;
        }
    }
}
