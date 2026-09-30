using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Level start card: shows the level number and its goals (score, ice, moves) over a paused board,
    /// and starts play when the player taps Play.
    /// </summary>
    public sealed class IntroView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _scoreGoalText;
        [Tooltip("Hidden on levels without ice.")]
        [SerializeField] private TMP_Text _iceGoalText;
        [Tooltip("Optional; hidden on levels without locks.")]
        [SerializeField] private TMP_Text _lockGoalText;
        [SerializeField] private TMP_Text _movesText;
        [SerializeField] private Button _playButton;

        // Start, not Awake: BoardView creates the level in its own Awake.
        private void Start()
        {
            if (_boardView == null || _boardView.Level == null || _panel == null || _titleText == null ||
                _scoreGoalText == null || _iceGoalText == null || _movesText == null || _playButton == null)
            {
                Debug.LogError($"{nameof(IntroView)} needs a running BoardView, the panel, its texts and the play button.", this);
                enabled = false;
                if (_panel != null) _panel.SetActive(false);
                return;
            }

            // Float arguments pick TMP's formatting overload; (ReadOnlySpan<char>, int, int) would be a substring.
            _titleText.SetText("Level {0}", (float)_boardView.LevelNumber);
            _scoreGoalText.SetText("Score {0}", (float)_boardView.Level.TargetScore);
            _iceGoalText.gameObject.SetActive(_boardView.HasIce);
            if (_boardView.HasIce) _iceGoalText.SetText("Break all ice ({0})", (float)_boardView.IceLeft);
            if (_lockGoalText != null)
            {
                _lockGoalText.gameObject.SetActive(_boardView.HasLocks);
                if (_boardView.HasLocks) _lockGoalText.SetText("Unlock all pieces ({0})", (float)_boardView.LocksLeft);
            }
            _movesText.SetText("in {0} moves", (float)_boardView.Level.MoveLimit);

            _playButton.onClick.AddListener(Play);
            _panel.SetActive(true);
            _boardView.IsPaused = true;
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(Play);
        }

        private void Play()
        {
            _panel.SetActive(false);
            _boardView.IsPaused = false;
        }
    }
}
