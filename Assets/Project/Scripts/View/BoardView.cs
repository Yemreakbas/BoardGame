using System;
using BoardGame.Core.Logic;
using BoardGame.Levels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoardGame.View
{
    /// <summary>
    /// Unity-side mirror of a <see cref="Board"/>. Awake builds the board and every <see cref="PieceView"/> it
    /// will ever need; afterwards the view only reacts to board events and turns pointer swipes (mouse in the
    /// editor, touch on device) into <see cref="Board.Swap"/> calls. After an accepted swap it paces the board
    /// with <see cref="Board.ResolveStep"/>, one step each time every piece has finished animating, so pops,
    /// falls and cascades play in sequence. Nothing here allocates after Awake.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField, Min(3)] private int _width = 8;
        [SerializeField, Min(3)] private int _height = 8;
        [Tooltip("Same seed, same session. 0 picks a new seed every run.")]
        [SerializeField] private int _seed;
        [Tooltip("Palette, one color per piece type. Without a catalog every entry is in play; a level uses its first ColorCount.")]
        [SerializeField] private Color[] _pieceColors =
        {
            new Color(0.91f, 0.30f, 0.24f), // red
            new Color(0.95f, 0.77f, 0.06f), // yellow
            new Color(0.18f, 0.80f, 0.44f), // green
            new Color(0.20f, 0.60f, 0.86f), // blue
            new Color(0.61f, 0.35f, 0.71f), // purple
            new Color(0.98f, 0.55f, 0.16f), // orange
            new Color(0.20f, 0.85f, 0.85f), // cyan
        };

        [Tooltip("Body color of color bombs, which have no piece color of their own.")]
        [SerializeField] private Color _bombColor = new Color(0.42f, 0.40f, 0.52f);

        [Header("Level")]
        [Tooltip("When set, board size, colors, moves and target come from the player's current level; the fields below are ignored.")]
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField, Min(1)] private int _moveLimit = 20;
        [SerializeField, Min(1)] private int _targetScore = 2500;

        [Header("Presentation")]
        [SerializeField] private PieceView _piecePrefab;
        [Tooltip("Optional: particles, beams and sounds for board events.")]
        [SerializeField] private EffectsView _effects;
        [SerializeField, Min(0.01f)] private float _cellSize = 1f;
        [Tooltip("Keeps an orthographic camera centred on the board and zoomed to fit it on any aspect ratio.")]
        [SerializeField] private bool _fitCamera = true;
        [Tooltip("Empty space kept around the board when fitting the camera, in cells.")]
        [SerializeField, Min(0f)] private float _fitPadding = 0.5f;

        [Header("Input")]
        [Tooltip("Falls back to Camera.main when empty.")]
        [SerializeField] private Camera _camera;
        [Tooltip("Drag distance, in cells, that turns a press into a swipe.")]
        [SerializeField, Range(0.1f, 1f)] private float _swipeThreshold = 0.35f;
        [Tooltip("Seconds of inactivity on a settled board before a possible move is pointed out.")]
        [SerializeField, Min(0.5f)] private float _hintDelay = 5f;

        /// <summary>Score of the running session; created in Awake, so read it from Start onwards.</summary>
        public ScoreKeeper Score { get; private set; }

        /// <summary>Move limit, target and outcome of the running level; created in Awake, like <see cref="Score"/>.</summary>
        public LevelState Level { get; private set; }

        /// <summary>Levels in the catalog; 0 when playing the inspector settings without one.</summary>
        public int LevelCount => _catalog != null ? _catalog.Count : 0;

        /// <summary>One-based number of the level being played (1 without a catalog).</summary>
        public int LevelNumber { get; private set; } = 1;

        private Board _board;
        private Transform _transform;
        private Transform _cameraTransform;
        private Vector3 _cellOrigin;         // local position of cell (0, 0)
        private PieceView[] _viewsByCell;    // cell index -> view showing that cell's piece
        private PieceView[] _hiddenViews;    // stack of views released by matches, reused by refills
        private int _hiddenCount;
        private int[] _spawnRowOffset;       // per column: refills already stacked above the board this step
        private PieceView[] _shuffleScratch; // copy of _viewsByCell while a shuffle remaps it
        private float _fittedAspect;         // camera aspect the fit was computed for; 0 forces a refit

        private bool _isSwiping;
        private int _swipeX;
        private int _swipeY;
        private Vector2 _swipeStart;         // board-local

        private float _idleTime;             // seconds the board has been settled without a press
        private int _hintA = -1;             // cells of the hint being shown, -1 when none
        private int _hintB = -1;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            if (_piecePrefab == null || _camera == null || _pieceColors.Length < BoardGenerator.MinPieceTypeCount)
            {
                Debug.LogError($"{nameof(BoardView)} needs a piece prefab, a camera and at least " +
                               $"{BoardGenerator.MinPieceTypeCount} piece colors.", this);
                enabled = false;
                return;
            }

            _transform = transform;
            _cameraTransform = _camera.transform;

            int width = _width, height = _height, colors = _pieceColors.Length;
            int moveLimit = _moveLimit, targetScore = _targetScore, seed = _seed;
            LevelDefinition level = LevelCount > 0 ? _catalog.Get(LevelProgress.CurrentIndex) : null;
            if (level != null)
            {
                LevelNumber = Mathf.Clamp(LevelProgress.CurrentIndex, 0, LevelCount - 1) + 1;
                width = level.Width;
                height = level.Height;
                colors = Mathf.Clamp(level.ColorCount, BoardGenerator.MinPieceTypeCount, _pieceColors.Length);
                moveLimit = level.MoveLimit;
                targetScore = level.TargetScore;
                seed = level.Seed;
            }
            if (seed == 0) seed = Environment.TickCount;

            _board = new Board(width, height, new BoardGenerator(colors, seed));
            Score = new ScoreKeeper(_board);
            Level = new LevelState(_board, Score, moveLimit, targetScore);

            int cellCount = _board.CellCount;
            _viewsByCell = new PieceView[cellCount];
            _hiddenViews = new PieceView[cellCount];
            _spawnRowOffset = new int[_board.Width];
            _shuffleScratch = new PieceView[cellCount];
            _cellOrigin = new Vector3(-0.5f * (_board.Width - 1) * _cellSize, -0.5f * (_board.Height - 1) * _cellSize, 0f);

            // The only instantiation in the game: one view per cell, recycled from here on.
            for (int index = 0; index < cellCount; index++)
            {
                PieceView view = Instantiate(_piecePrefab, _transform);
                int piece = _board.GetPiece(index);
                view.Show(ColorOf(piece), Piece.SpecialOf(piece), CellToLocal(index));
                _viewsByCell[index] = view;
            }

            _board.OnPiecesSwapped += HandlePiecesSwapped;
            _board.OnPieceMoved += HandlePieceMoved;
            _board.OnMatched += HandleMatched;
            _board.OnPieceSpawned += HandlePieceSpawned;
            _board.OnShuffled += HandleShuffled;
            _board.OnSpecialCreated += HandleSpecialCreated;

            FitCamera();
        }

        private void OnDestroy()
        {
            if (_board == null) return;
            Level.Dispose();
            Score.Dispose();
            _board.OnPiecesSwapped -= HandlePiecesSwapped;
            _board.OnPieceMoved -= HandlePieceMoved;
            _board.OnMatched -= HandleMatched;
            _board.OnPieceSpawned -= HandlePieceSpawned;
            _board.OnShuffled -= HandleShuffled;
            _board.OnSpecialCreated -= HandleSpecialCreated;
        }

        private void Update()
        {
            if (_fitCamera && _camera.aspect != _fittedAspect) FitCamera(); // Rotation or window resize.

            if (_board.IsResolving)
            {
                _isSwiping = false;
                if (IsAnyPieceBusy()) return;

                // Fresh spawn stacks per collapse step, so each wave of refills drops in from just above the board.
                Array.Clear(_spawnRowOffset, 0, _spawnRowOffset.Length);
                _board.ResolveStep();
                return;
            }

            if (!Level.CanMove)
            {
                _isSwiping = false; // Level over: the board stays on screen but takes no more input.
                StopHint();
                return;
            }

            Pointer pointer = Pointer.current; // Last used mouse, pen or touchscreen.
            if (pointer != null)
            {
                if (pointer.press.wasPressedThisFrame)
                {
                    StopHint(); // Any press counts as activity and clears the hint.
                    BeginSwipe(pointer.position.ReadValue());
                }
                else if (_isSwiping)
                {
                    if (pointer.press.isPressed) TrackSwipe(pointer.position.ReadValue());
                    else _isSwiping = false;
                }
            }

            UpdateHint();
        }

        // Counts settled, untouched time and, past the delay, pulses the two pieces of one possible move.
        private void UpdateHint()
        {
            if (_hintA >= 0) return;
            if (_isSwiping || IsAnyPieceBusy())
            {
                _idleTime = 0f;
                return;
            }

            _idleTime += Time.deltaTime;
            if (_idleTime < _hintDelay) return;
            if (!_board.FindPossibleMove(out _hintA, out _hintB))
            {
                _idleTime = 0f; // Only a board too small for any move; retry after another delay.
                return;
            }

            _viewsByCell[_hintA].StartHint();
            _viewsByCell[_hintB].StartHint();
        }

        private void StopHint()
        {
            _idleTime = 0f;
            if (_hintA < 0) return;
            _viewsByCell[_hintA].StopHint();
            _viewsByCell[_hintB].StopHint();
            _hintA = -1;
            _hintB = -1;
        }

        private void BeginSwipe(Vector2 screenPosition)
        {
            _isSwiping = false;
            if (IsAnyPieceBusy()) return; // Let the board settle before taking new input.

            Vector2 local = ScreenToLocal(screenPosition);
            int x = Mathf.FloorToInt((local.x - _cellOrigin.x) / _cellSize + 0.5f);
            int y = Mathf.FloorToInt((local.y - _cellOrigin.y) / _cellSize + 0.5f);
            if (!_board.IsInside(x, y)) return;

            _isSwiping = true;
            _swipeX = x;
            _swipeY = y;
            _swipeStart = local;
        }

        private void TrackSwipe(Vector2 screenPosition)
        {
            Vector2 drag = ScreenToLocal(screenPosition) - _swipeStart;
            float absX = Mathf.Abs(drag.x);
            float absY = Mathf.Abs(drag.y);
            if (Mathf.Max(absX, absY) < _swipeThreshold * _cellSize) return;

            // One swipe, one swap attempt, towards the dominant drag axis.
            _isSwiping = false;
            int targetX = _swipeX;
            int targetY = _swipeY;
            if (absX >= absY) targetX += drag.x > 0f ? 1 : -1;
            else targetY += drag.y > 0f ? 1 : -1;

            if (_board.Swap(_swipeX, _swipeY, targetX, targetY)) return;

            // Rejected (no match): show the attempt by bouncing both pieces. Off-board swipes do nothing.
            if (!_board.IsInside(targetX, targetY)) return;
            int indexA = _board.ToIndex(_swipeX, _swipeY);
            int indexB = _board.ToIndex(targetX, targetY);
            _viewsByCell[indexA].MoveToAndBack(CellToLocal(indexB));
            _viewsByCell[indexB].MoveToAndBack(CellToLocal(indexA));
            if (_effects != null) _effects.PlayInvalid();
        }

        private void HandlePiecesSwapped(int indexA, int indexB)
        {
            PieceView viewA = _viewsByCell[indexA];
            PieceView viewB = _viewsByCell[indexB];
            _viewsByCell[indexA] = viewB;
            _viewsByCell[indexB] = viewA;
            viewA.MoveTo(CellToLocal(indexB));
            viewB.MoveTo(CellToLocal(indexA));
            if (_effects != null) _effects.PlaySwap();
        }

        private void HandlePieceMoved(int fromIndex, int toIndex)
        {
            PieceView view = _viewsByCell[fromIndex];
            _viewsByCell[fromIndex] = null;
            _viewsByCell[toIndex] = view;
            view.MoveTo(CellToLocal(toIndex), MoveStyle.Fall);
        }

        private void HandleMatched(ReadOnlySpan<int> matchedIndices)
        {
            for (int i = 0; i < matchedIndices.Length; i++)
            {
                int index = matchedIndices[i];
                PieceView view = _viewsByCell[index];
                _viewsByCell[index] = null;
                if (_effects != null) PlayClearEffects(index, view);
                view.Pop();
                _hiddenViews[_hiddenCount++] = view; // Reused by the next refill step, once the pop has finished.
            }
            if (_effects != null) _effects.PlayPop(Score.Combo); // ScoreKeeper already counted this wave
        }

        // Particles for every cleared piece; a beam or a flash when the piece was a special firing.
        private void PlayClearEffects(int index, PieceView view)
        {
            Vector3 local = CellToLocal(index);
            _effects.Burst(_transform.TransformPoint(local), view.Color);

            Vector3 scale = _transform.lossyScale;
            switch (view.Special)
            {
                case SpecialKind.RowRocket:
                    _effects.Beam(_transform.TransformPoint(new Vector3(0f, local.y, 0f)), true,
                        _board.Width * _cellSize * Mathf.Abs(scale.x), view.Color);
                    break;
                case SpecialKind.ColumnRocket:
                    _effects.Beam(_transform.TransformPoint(new Vector3(local.x, 0f, 0f)), false,
                        _board.Height * _cellSize * Mathf.Abs(scale.y), view.Color);
                    break;
                case SpecialKind.ColorBomb:
                    _effects.Bomb(_transform.TransformPoint(local), _transform.position,
                        Mathf.Max(_board.Width, _board.Height) * _cellSize * Mathf.Abs(scale.x));
                    break;
            }
        }

        private void HandlePieceSpawned(int index, int pieceId)
        {
            PieceView view = _hiddenViews[--_hiddenCount];
            _viewsByCell[index] = view;

            // Stack this step's refills above the column so they drop in, in order, from off the board.
            int x = _board.ToX(index);
            int spawnRow = _board.Height + _spawnRowOffset[x]++;
            view.Show(ColorOf(pieceId), Piece.SpecialOf(pieceId), CellToLocal(x, spawnRow));
            view.MoveTo(CellToLocal(index), MoveStyle.Fall);
        }

        // Every piece flies from its old cell to its new one. Colors are refreshed too, because a shuffle
        // that fell back to regenerating the board changes piece IDs.
        private void HandleShuffled(ReadOnlySpan<int> sourceIndices)
        {
            Array.Copy(_viewsByCell, _shuffleScratch, _viewsByCell.Length);
            for (int index = 0; index < sourceIndices.Length; index++)
            {
                PieceView view = _shuffleScratch[sourceIndices[index]];
                _viewsByCell[index] = view;
                int piece = _board.GetPiece(index);
                view.SetLook(ColorOf(piece), Piece.SpecialOf(piece));
                view.MoveTo(CellToLocal(index));
            }
        }

        // Board cells are empty (null) between a clear step and its refill; popping views sit in the hidden stack.
        private bool IsAnyPieceBusy()
        {
            for (int i = 0; i < _viewsByCell.Length; i++)
            {
                PieceView view = _viewsByCell[i];
                if (view != null && view.IsBusy) return true;
            }
            for (int i = 0; i < _hiddenCount; i++)
            {
                if (_hiddenViews[i].IsBusy) return true;
            }
            return false;
        }

        // Centres the camera on the board and picks the smallest orthographic size that shows the whole
        // board plus padding: height-bound on landscape screens, width-bound on portrait ones.
        private void FitCamera()
        {
            _fittedAspect = _camera.aspect;
            if (!_fitCamera || !_camera.orthographic) return;

            Vector3 scale = _transform.lossyScale;
            float halfWidth = (0.5f * _board.Width + _fitPadding) * _cellSize * Mathf.Abs(scale.x);
            float halfHeight = (0.5f * _board.Height + _fitPadding) * _cellSize * Mathf.Abs(scale.y);
            _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / _fittedAspect);

            Vector3 boardCentre = _transform.position;
            boardCentre.z = _cameraTransform.position.z;
            _cameraTransform.position = boardCentre;
        }

        private Color ColorOf(int pieceId)
        {
            int color = Piece.ColorOf(pieceId);
            return color == Board.Empty ? _bombColor : _pieceColors[color - BoardGenerator.FirstPieceId];
        }

        // A run of 4+ turned this piece into a special where it stands: only its look changes.
        private void HandleSpecialCreated(int index, int pieceId)
        {
            _viewsByCell[index].SetLook(ColorOf(pieceId), Piece.SpecialOf(pieceId));
        }

        private Vector3 CellToLocal(int index) => CellToLocal(_board.ToX(index), _board.ToY(index));

        private Vector3 CellToLocal(int x, int y)
        {
            Vector3 position = _cellOrigin;
            position.x += x * _cellSize;
            position.y += y * _cellSize;
            return position;
        }

        private Vector2 ScreenToLocal(Vector2 screenPosition)
        {
            Vector3 screenPoint = screenPosition;
            screenPoint.z = _transform.position.z - _cameraTransform.position.z; // Depth of the board plane.
            return _transform.InverseTransformPoint(_camera.ScreenToWorldPoint(screenPoint));
        }
    }
}
