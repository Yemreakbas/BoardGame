using BoardGame.Core.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// In-game pause: a button that opens a Resume / Restart / Levels panel. While open, time stops and the
    /// board takes no input. The pause button hides once the level is decided (the game over panel takes over).
    /// </summary>
    public sealed class PauseView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _levelsButton;
        [SerializeField] private string _menuScene = "LevelSelect";

        private LevelState _level;

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // Start, not Awake: BoardView creates the LevelState in its own Awake.
        private void Start()
        {
            if (_boardView == null || _boardView.Level == null || _pauseButton == null || _panel == null ||
                _resumeButton == null || _restartButton == null || _levelsButton == null)
            {
                Debug.LogError($"{nameof(PauseView)} needs a running BoardView, the pause button, the panel and its three buttons.", this);
                enabled = false;
                return;
            }

            _level = _boardView.Level;
            _level.OnOutcomeChanged += HandleOutcome;
            _pauseButton.onClick.AddListener(Pause);
            _resumeButton.onClick.AddListener(Resume);
            _restartButton.onClick.AddListener(Restart);
            _levelsButton.onClick.AddListener(OpenLevels);
        }

        private void OnDestroy()
        {
            if (_level != null) _level.OnOutcomeChanged -= HandleOutcome;
            Time.timeScale = 1f; // never leave the next scene frozen
        }

        private void Pause()
        {
            _panel.SetActive(true);
            _boardView.IsPaused = true;
            Time.timeScale = 0f;
        }

        private void Resume()
        {
            _panel.SetActive(false);
            _boardView.IsPaused = false;
            Time.timeScale = 1f;
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OpenLevels()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_menuScene);
        }

        private void HandleOutcome()
        {
            _pauseButton.gameObject.SetActive(false);
        }
    }
}
