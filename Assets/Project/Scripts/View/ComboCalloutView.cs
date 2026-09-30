using BoardGame.Core.Logic;
using TMPro;
using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Big "Nice! / Great! / Amazing! / Incredible!" word that pops in the middle of the screen when a move
    /// chains cascades. Animated in Update; never allocates.
    /// </summary>
    public sealed class ComboCalloutView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TMP_Text _text;
        [SerializeField, Min(0.1f)] private float _duration = 1.1f;

        private static readonly string[] Words = { "Nice!", "Great!", "Amazing!", "Incredible!" };
        private static readonly Color[] Colors =
        {
            new Color(1f, 0.9f, 0.3f), new Color(1f, 0.62f, 0.2f), new Color(1f, 0.4f, 0.55f), new Color(0.75f, 0.5f, 1f),
        };

        private ScoreKeeper _score;
        private RectTransform _rect;
        private int _lastCombo;
        private float _time = -1f;

        private void Start()
        {
            if (_boardView == null || _boardView.Score == null || _text == null)
            {
                enabled = false;
                return;
            }
            _rect = (RectTransform)_text.transform;
            _text.enabled = false;
            _score = _boardView.Score;
            _score.OnChanged += HandleScoreChanged;
        }

        private void OnDestroy()
        {
            if (_score != null) _score.OnChanged -= HandleScoreChanged;
        }

        private void HandleScoreChanged()
        {
            int combo = _score.Combo;
            if (combo > _lastCombo && combo >= 2)
            {
                int tier = Mathf.Min(combo - 2, Words.Length - 1);
                _text.SetText(Words[tier]);
                _text.color = Colors[tier];
                _text.enabled = true;
                _time = 0f;
            }
            _lastCombo = combo;
        }

        private void Update()
        {
            if (_time < 0f) return;
            _time += Time.deltaTime;
            float t = _time / _duration;
            if (t >= 1f)
            {
                _time = -1f;
                _text.enabled = false;
                return;
            }

            // Overshooting pop-in, a gentle wobble, then a fade.
            float scale = t < 0.18f ? Mathf.Lerp(0.2f, 1.3f, t / 0.18f) : Mathf.Lerp(1.3f, 1f, Mathf.Min(1f, (t - 0.18f) / 0.15f));
            _rect.localScale = Vector3.one * scale;
            _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 4f * (1f - t));
            Color color = _text.color;
            color.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            _text.color = color;
        }
    }
}
