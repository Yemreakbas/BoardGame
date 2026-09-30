using BoardGame.Core.Logic;
using TMPro;
using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Shows the score against the level target, the moves left and the cascade combo. Text is written with
    /// <see cref="TMP_Text.SetText(string, float)"/>, which formats numbers without allocating strings.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _movesText;
        [Tooltip("Shown only while a move chains two or more cascade waves.")]
        [SerializeField] private TMP_Text _comboText;
        [Tooltip("Optional: shows the level number (set once, it does not change during play).")]
        [SerializeField] private TMP_Text _levelText;
        [Tooltip("Optional: ice cells left, shown only on levels that have ice.")]
        [SerializeField] private TMP_Text _iceText;

        private ScoreKeeper _score;
        private LevelState _level;

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
            _level = _boardView.Level;
            _score.OnChanged += Refresh;
            Refresh();

            if (_levelText != null)
            {
                _levelText.SetText("Level {0} / {1}", (float)_boardView.LevelNumber, (float)Mathf.Max(1, _boardView.LevelCount));
            }

            if (_iceText != null)
            {
                _iceText.gameObject.SetActive(_boardView.HasIce);
                if (_boardView.HasIce)
                {
                    _boardView.IceChanged += RefreshIce;
                    RefreshIce();
                }
            }
        }

        private void OnDestroy()
        {
            if (_score != null) _score.OnChanged -= Refresh;
            if (_boardView != null) _boardView.IceChanged -= RefreshIce;
        }

        private void RefreshIce()
        {
            _iceText.SetText("Ice left {0}", (float)_boardView.IceLeft);
        }

        private void Refresh()
        {
            // Float arguments pick the formatting overload; (ReadOnlySpan<char>, int, int) would be a substring.
            _scoreText.SetText("Score {0} / {1}", (float)_score.Score, (float)_level.TargetScore);
            _movesText.SetText("Moves left {0}", (float)_level.MovesLeft);

            bool showCombo = _score.Combo >= 2;
            if (showCombo) _comboText.SetText("Combo x{0}", (float)_score.Combo);
            _comboText.enabled = showCombo;
        }
    }
}
