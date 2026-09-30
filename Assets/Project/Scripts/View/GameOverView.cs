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
        [Tooltip("Optional: three star images, lit for the stars earned (hidden after a loss).")]
        [SerializeField] private Image[] _stars = new Image[0];
        [SerializeField] private Color _starEarned = new Color(1f, 0.82f, 0.2f);
        [SerializeField] private Color _starEmpty = new Color(1f, 1f, 1f, 0.18f);
        [Tooltip("Optional: goes back to the level select scene.")]
        [SerializeField] private Button _menuButton;
        [SerializeField] private string _menuScene = "LevelSelect";

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
            if (_menuButton != null) _menuButton.onClick.AddListener(OpenMenu);
        }

        private void OnDestroy()
        {
            if (_level != null) _level.OnOutcomeChanged -= Show;
            if (_restartButton != null) _restartButton.onClick.RemoveListener(Restart);
            if (_menuButton != null) _menuButton.onClick.RemoveListener(OpenMenu);
        }

        private void Show()
        {
            bool won = _level.Outcome == LevelOutcome.Won;
            bool hasLevels = _boardView.LevelCount > 0;
            int stars = won ? LevelProgress.StarsFor(_level.MovesLeft, _level.MoveLimit) : 0;
            if (won && hasLevels) LevelProgress.RecordStars(_boardView.LevelNumber - 1, stars); // before advancing
            bool finishedAll = won && hasLevels && LevelProgress.CompleteCurrent(_boardView.LevelCount);

            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].enabled = won;
                _stars[i].color = i < stars ? _starEarned : _starEmpty;
            }

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

        private void OpenMenu()
        {
            SceneManager.LoadScene(_menuScene);
        }
    }
}
