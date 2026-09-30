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
        [Tooltip("Optional: locked pieces left, shown only on levels that have locks.")]
        [SerializeField] private TMP_Text _lockText;
        [Tooltip("Optional: a Filled image that grows toward the target score.")]
        [SerializeField] private UnityEngine.UI.Image _progressFill;
        [Tooltip("Moves left at or below this turn red and pulse.")]
        [SerializeField, Min(0)] private int _lowMoves = 5;
        [SerializeField] private Color _lowMovesColor = new Color(1f, 0.42f, 0.42f);

        private float _shownProgress;
        private float _movesPulse;        // 1 right after a move, decays to 0
        private int _lastMovesLeft = -1;
        private Color _movesColor;

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
            _movesColor = _movesText.color;
            if (_progressFill != null) _progressFill.fillAmount = 0f;
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

            if (_lockText != null)
            {
                _lockText.gameObject.SetActive(_boardView.HasLocks);
                if (_boardView.HasLocks)
                {
                    _boardView.LocksChanged += RefreshLocks;
                    RefreshLocks();
                }
            }
        }

        // Smooth progress bar and the moves pulse; both settle and then cost almost nothing.
        private void Update()
        {
            if (_score == null) return;

            if (_progressFill != null)
            {
                float target = Mathf.Clamp01((float)_score.Score / _level.TargetScore);
                _shownProgress = Mathf.MoveTowards(_shownProgress, target, Time.deltaTime * (0.4f + 2f * Mathf.Abs(target - _shownProgress)));
                _progressFill.fillAmount = _shownProgress;
            }

            if (_movesPulse > 0f)
            {
                _movesPulse = Mathf.Max(0f, _movesPulse - Time.deltaTime * 4f);
                float strength = _lastMovesLeft <= _lowMoves ? 0.3f : 0.15f;
                _movesText.transform.localScale = Vector3.one * (1f + strength * Mathf.Sin(_movesPulse * Mathf.PI));
            }
        }

        private void RefreshLocks()
        {
            _lockText.SetText("Locks left {0}", (float)_boardView.LocksLeft);
        }

        private void OnDestroy()
        {
            if (_score != null) _score.OnChanged -= Refresh;
            if (_boardView != null)
            {
                _boardView.IceChanged -= RefreshIce;
                _boardView.LocksChanged -= RefreshLocks;
            }
        }

        private void RefreshIce()
        {
            _iceText.SetText("Ice left {0}", (float)_boardView.IceLeft);
        }

        private void Refresh()
        {
            // Float arguments pick the formatting overload; (ReadOnlySpan<char>, int, int) would be a substring.
            _scoreText.SetText("Score {0} / {1}", (float)_score.Score, (float)_level.TargetScore);
            int movesLeft = _level.MovesLeft;
            _movesText.SetText("Moves left {0}", (float)movesLeft);
            if (movesLeft != _lastMovesLeft)
            {
                if (_lastMovesLeft >= 0) _movesPulse = 1f; // no pulse on the first refresh
                _lastMovesLeft = movesLeft;
                _movesText.color = movesLeft <= _lowMoves ? _lowMovesColor : _movesColor;
            }

            bool showCombo = _score.Combo >= 2;
            if (showCombo) _comboText.SetText("Combo x{0}", (float)_score.Combo);
            _comboText.enabled = showCombo;
        }
    }
}
