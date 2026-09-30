using BoardGame.Core.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Shows the end-of-level panel once <see cref="LevelState"/> decides the outcome, and restarts the
    /// level by reloading the scene. Lives on an always-active object; the panel itself starts hidden.
    /// </summary>
    public sealed class GameOverView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private Button _restartButton;
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
            _titleText.SetText(won ? "Level Complete!" : "Out of Moves");
            // Float arguments pick the formatting overload; (ReadOnlySpan<char>, int, int) would be a substring.
            _scoreText.SetText("Score {0} / {1}", (float)_boardView.Score.Score, (float)_level.TargetScore);
            _panel.SetActive(true);
            if (_effects != null) _effects.PlayOutcome(won);
        }

        private void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
