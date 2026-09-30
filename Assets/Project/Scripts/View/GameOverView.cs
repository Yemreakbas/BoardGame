using BoardGame.Core.Logic;
using BoardGame.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Shows the end-of-level panel once <see cref="LevelState"/> decides the outcome. A win is saved right
    /// away through <see cref="LevelProgress"/>, so the button then loads the next level; a loss retries the
    /// same one. Both reload the scene. Lives on an always-active object; the panel itself starts hidden.
    /// </summary>
    public sealed class GameOverView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Button _restartButton;
        [Tooltip("Optional: the button's label, set to Next Level / Try Again / Play Again.")]
        [SerializeField] private TMP_Text _buttonLabel;
        [Tooltip("Optional: plays the win or lose jingle.")]
        [SerializeField] private EffectsView _effects;

        private LevelState _level;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // Start, not Awake: BoardView creates the LevelState in its own Awake.
        private void Start()
        {
            if (_boardView == null || _boardView.Level == null || _panel == null ||
                _titleText == null || _scoreText == null || _restartButton == null)
            {
                Debug.LogError($"{nameof(GameOverView)} needs a running BoardView, the panel, both texts and the button.", this);
                enabled = false;
                return;
            }

            _level = _boardView.Level;
            _level.OnOutcomeChanged += Show;
            _restartButton.onClick.AddListener(Restart);
        }

        private void OnDestroy()
        {
            if (_level != null) _level.OnOutcomeChanged -= Show;
            if (_restartButton != null) _restartButton.onClick.RemoveListener(Restart);
        }

        private void Show()
        {
            bool won = _level.Outcome == LevelOutcome.Won;
            bool hasLevels = _boardView.LevelCount > 0;
            bool finishedAll = won && hasLevels && LevelProgress.CompleteCurrent(_boardView.LevelCount);

            if (!won) _titleText.SetText("Out of Moves");
            else if (finishedAll) _titleText.SetText("All Levels Complete!");
            else if (hasLevels) _titleText.SetText("Level {0} Complete!", (float)_boardView.LevelNumber);
            else _titleText.SetText("Level Complete!");

            // Float arguments pick the formatting overload; (ReadOnlySpan<char>, int, int) would be a substring.
            _scoreText.SetText("Score {0} / {1}", (float)_boardView.Score.Score, (float)_level.TargetScore);

            if (_buttonLabel != null)
            {
                if (!won) _buttonLabel.SetText("Try Again");
                else if (hasLevels && !finishedAll) _buttonLabel.SetText("Next Level");
                else _buttonLabel.SetText("Play Again");
            }

            _panel.SetActive(true);
            if (_effects != null) _effects.PlayOutcome(won);
        }

        // The level to load was already chosen in Show: the next one after a win, the same one after a loss.
        private void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
