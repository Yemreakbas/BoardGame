using BoardGame.Core.Logic;
using TMPro;
using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Shows the session's score, move count and cascade combo. Text is written with
    /// <see cref="TMP_Text.SetText(string, float)"/>, which formats numbers without allocating strings.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _movesText;
        [Tooltip("Shown only while a move chains two or more cascade waves.")]
        [SerializeField] private TMP_Text _comboText;

        private ScoreKeeper _score;

        // Start, not Awake: BoardView creates the ScoreKeeper in its own Awake.
        private void Start()
        {
            if (_boardView == null || _boardView.Score == null || _scoreText == null || _movesText == null || _comboText == null)
            {
                Debug.LogError($"{nameof(HudView)} needs a running BoardView and all three texts.", this);
                enabled = false;
                return;
            }

            _score = _boardView.Score;
            _score.OnChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_score != null) _score.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            _scoreText.SetText("Score {0}", _score.Score);
            _movesText.SetText("Moves {0}", _score.Moves);

            bool showCombo = _score.Combo >= 2;
            if (showCombo) _comboText.SetText("Combo x{0}", _score.Combo);
            _comboText.enabled = showCombo;
        }
    }
}
