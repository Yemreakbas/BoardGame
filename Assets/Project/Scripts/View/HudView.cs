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
        [Tooltip("Optional: score orbs; the shown score only counts points whose orb has landed.")]
        [SerializeField] private ScoreFlyView _flyer;
        [Tooltip("Optional: the score bar, punched each time an orb lands.")]
        [SerializeField] private RectTransform _scoreBar;

        private float _shownScore;        // counts up toward the landed score
        private int _writtenScore = -1;   // last value written to the text, to skip identical SetText calls
        private float _barPunch;          // 1 when an orb lands, decays to 0
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
            if (_flyer != null) _flyer.Arrived += HandleOrbArrived;
            WriteScore(0);
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

            // Count up toward the points that have actually landed in the bar.
            float landed = _score.Score - (_flyer != null ? _flyer.PendingPoints : 0);
            if (_shownScore < landed)
            {
                float step = Time.deltaTime * Mathf.Max(60f, (landed - _shownScore) * 6f);
                _shownScore = Mathf.Min(landed, _shownScore + step);
            }
            else _shownScore = landed;
            WriteScore(Mathf.RoundToInt(_shownScore));

            if (_progressFill != null)
            {
                float target = Mathf.Clamp01(_shownScore / _level.TargetScore);
                _shownProgress = Mathf.MoveTowards(_shownProgress, target, Time.deltaTime * (0.4f + 2f * Mathf.Abs(target - _shownProgress)));
                _progressFill.fillAmount = _shownProgress;
            }

            if (_barPunch > 0f && _scoreBar != null)
            {
                _barPunch = Mathf.Max(0f, _barPunch - Time.deltaTime * 6f);
                float s = 1f + 0.06f * Mathf.Sin(_barPunch * Mathf.PI);
                _scoreBar.localScale = new Vector3(s, s, 1f);
            }

            if (_movesPulse > 0f)
            {
                _movesPulse = Mathf.Max(0f, _movesPulse - Time.deltaTime * 4f);
                float strength = _lastMovesLeft <= _lowMoves ? 0.3f : 0.15f;
                _movesText.transform.localScale = Vector3.one * (1f + strength * Mathf.Sin(_movesPulse * Mathf.PI));
            }
        }

        private void HandleOrbArrived() => _barPunch = 1f;

        private void WriteScore(int shown)
        {
            if (shown == _writtenScore) return;
            _writtenScore = shown;
            // Float arguments pick the formatting overload; (ReadOnlySpan<char>, int, int) would be a substring.
            _scoreText.SetText("{0} / {1}", (float)shown, (float)_level.TargetScore);
        }

        private void RefreshLocks()
        {
            _lockText.SetText("Locks left {0}", (float)_boardView.LocksLeft);
        }

        private void OnDestroy()
        {
            if (_score != null) _score.OnChanged -= Refresh;
            if (_flyer != null) _flyer.Arrived -= HandleOrbArrived;
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
            // The score text is written in Update, counting up as orbs land.
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
