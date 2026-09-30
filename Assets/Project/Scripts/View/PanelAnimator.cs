using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Plays an opening animation whenever its panel is activated: the dimmer fades in and the card springs
    /// up from smaller. Uses unscaled time, so it also plays while the game is paused.
    /// </summary>
    public sealed class PanelAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform _card;
        [SerializeField] private Image _dimmer;
        [SerializeField, Min(0.05f)] private float _duration = 0.38f;

        private float _dimAlpha = -1f;
        private float _time;

        private void OnEnable()
        {
            if (_dimmer != null && _dimAlpha < 0f) _dimAlpha = _dimmer.color.a; // remember the designed strength
            _time = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (_time >= _duration) return;
            _time += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(_time / _duration));
        }

        private void Apply(float t)
        {
            if (_card != null)
            {
                // Ease-out-back: overshoots a little past full size, then settles.
                const float back = 1.9f;
                float u = t - 1f;
                float eased = 1f + (back + 1f) * u * u * u + back * u * u;
                _card.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, eased);
            }
            if (_dimmer != null)
            {
                Color color = _dimmer.color;
                color.a = _dimAlpha * Mathf.Clamp01(t * 1.6f);
                _dimmer.color = color;
            }
        }
    }
}
