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

        // Star reveal: stars pop in one after another once the card has sprung open (unscaled time).
        private const float FirstStarDelay = 0.45f;
        private const float StarInterval = 0.32f;
        private const float StarPopDuration = 0.3f;
        private float _revealTime = -1f;
        private int _starsEarned;
        private int _starsRevealed;

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
                _stars[i].color = _starEmpty;              // earned ones light up during the reveal
                _stars[i].transform.localScale = Vector3.zero;
            }
            _starsEarned = stars;
            _starsRevealed = 0;
            _revealTime = won ? 0f : -1f;

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
            if (_effects != null)
            {
                _effects.PlayOutcome(won);
                if (won) _effects.Celebrate();
            }
        }

        private void Update()
        {
            if (_revealTime < 0f) return;
            _revealTime += Time.unscaledDeltaTime;

            bool running = false;
            for (int i = 0; i < _stars.Length; i++)
            {
                float t = (_revealTime - FirstStarDelay - i * StarInterval) / StarPopDuration;
                if (t < 0f) { running = true; continue; }

                if (i >= _starsRevealed)
                {
                    _starsRevealed = i + 1;
                    bool earned = i < _starsEarned;
                    _stars[i].color = earned ? _starEarned : _starEmpty;
                    if (earned && _effects != null) _effects.PlayStar(i);
                }

                // Pop past full size and settle; earned stars pop bigger.
                float peak = i < _starsEarned ? 1.45f : 1.1f;
                float scale = t >= 1f ? 1f : t < 0.5f ? Mathf.Lerp(0f, peak, t / 0.5f) : Mathf.Lerp(peak, 1f, (t - 0.5f) / 0.5f);
                _stars[i].transform.localScale = Vector3.one * scale;
                if (t < 1f) running = true;
            }
            if (!running) _revealTime = -1f;
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
