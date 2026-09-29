using System;
using BoardGame.Core.Logic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoardGame.View
{
    /// <summary>
    /// Unity-side mirror of a <see cref="Board"/>. Awake builds the board and every <see cref="PieceView"/> it
    /// will ever need; afterwards the view only reacts to board events and turns pointer swipes (mouse in the
    /// editor, touch on device) into <see cref="Board.Swap"/> calls. Nothing here allocates after Awake.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField, Min(3)] private int _width = 8;
        [SerializeField, Min(3)] private int _height = 8;
        [Tooltip("Same seed, same session. 0 picks a new seed every run.")]
        [SerializeField] private int _seed;
        [Tooltip("One color per piece type; the entry count is the number of types (at least 3).")]
        [SerializeField] private Color[] _pieceColors =
        {
            new Color(0.91f, 0.30f, 0.24f), // red
            new Color(0.95f, 0.77f, 0.06f), // yellow
            new Color(0.18f, 0.80f, 0.44f), // green
            new Color(0.20f, 0.60f, 0.86f), // blue
            new Color(0.61f, 0.35f, 0.71f), // purple
        };

        [Header("Presentation")]
        [SerializeField] private PieceView _piecePrefab;
        [SerializeField, Min(0.01f)] private float _cellSize = 1f;

        [Header("Input")]
        [Tooltip("Falls back to Camera.main when empty.")]
        [SerializeField] private Camera _camera;
        [Tooltip("Drag distance, in cells, that turns a press into a swipe.")]
        [SerializeField, Range(0.1f, 1f)] private float _swipeThreshold = 0.35f;

        private Board _board;
        private Transform _transform;
        private Transform _cameraTransform;
        private Vector3 _cellOrigin;         // local position of cell (0, 0)
        private PieceView[] _viewsByCell;    // cell index -> view showing that cell's piece
        private PieceView[] _hiddenViews;    // stack of views released by matches, reused by refills
        private int _hiddenCount;
        private int[] _spawnRowOffset;       // per column: refills already stacked above the board this swap

        private bool _isSwiping;
        private int _swipeX;
        private int _swipeY;
        private Vector2 _swipeStart;         // board-local

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

            int seed = _seed != 0 ? _seed : Environment.TickCount;
            _board = new Board(_width, _height, new BoardGenerator(_pieceColors.Length, seed));

            int cellCount = _board.CellCount;
            _viewsByCell = new PieceView[cellCount];
            _hiddenViews = new PieceView[cellCount];
            _spawnRowOffset = new int[_board.Width];
            _cellOrigin = new Vector3(-0.5f * (_board.Width - 1) * _cellSize, -0.5f * (_board.Height - 1) * _cellSize, 0f);

            // The only instantiation in the game: one view per cell, recycled from here on.
            for (int index = 0; index < cellCount; index++)
            {
                PieceView view = Instantiate(_piecePrefab, _transform);
                view.Show(ColorOf(_board.GetPiece(index)), CellToLocal(index));
                _viewsByCell[index] = view;
            }

            _board.OnPiecesSwapped += HandlePiecesSwapped;
            _board.OnPieceMoved += HandlePieceMoved;
            _board.OnMatched += HandleMatched;
            _board.OnPieceSpawned += HandlePieceSpawned;
        }

        private void OnDestroy()
        {
            if (_board == null) return;
            _board.OnPiecesSwapped -= HandlePiecesSwapped;
            _board.OnPieceMoved -= HandlePieceMoved;
            _board.OnMatched -= HandleMatched;
            _board.OnPieceSpawned -= HandlePieceSpawned;
        }

        private void Update()
        {
            Pointer pointer = Pointer.current; // Last used mouse, pen or touchscreen.
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                BeginSwipe(pointer.position.ReadValue());
            }
            else if (_isSwiping)
            {
                if (pointer.press.isPressed) TrackSwipe(pointer.position.ReadValue());
                else _isSwiping = false;
            }
        }

        private void BeginSwipe(Vector2 screenPosition)
        {
            _isSwiping = false;
            if (IsAnyPieceMoving()) return; // Let the board settle before taking new input.

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

            Array.Clear(_spawnRowOffset, 0, _spawnRowOffset.Length);
            _board.Swap(_swipeX, _swipeY, targetX, targetY); // False (no match, or off the board) changes nothing.
        }

        private void HandlePiecesSwapped(int indexA, int indexB)
        {
            PieceView viewA = _viewsByCell[indexA];
            PieceView viewB = _viewsByCell[indexB];
            _viewsByCell[indexA] = viewB;
            _viewsByCell[indexB] = viewA;
            viewA.MoveTo(CellToLocal(indexB));
            viewB.MoveTo(CellToLocal(indexA));
        }

        private void HandlePieceMoved(int fromIndex, int toIndex)
        {
            PieceView view = _viewsByCell[fromIndex];
            _viewsByCell[fromIndex] = null;
            _viewsByCell[toIndex] = view;
            view.MoveTo(CellToLocal(toIndex));
        }

        private void HandleMatched(ReadOnlySpan<int> matchedIndices)
        {
            for (int i = 0; i < matchedIndices.Length; i++)
            {
                int index = matchedIndices[i];
                PieceView view = _viewsByCell[index];
                _viewsByCell[index] = null;
                view.Hide();
                _hiddenViews[_hiddenCount++] = view;
            }
        }

        private void HandlePieceSpawned(int index, int pieceId)
        {
            PieceView view = _hiddenViews[--_hiddenCount];
            _viewsByCell[index] = view;

            // Stack this swap's refills above the column so they drop in, in order, from off the board.
            int x = _board.ToX(index);
            int spawnRow = _board.Height + _spawnRowOffset[x]++;
            view.Show(ColorOf(pieceId), CellToLocal(x, spawnRow));
            view.MoveTo(CellToLocal(index));
        }

        private bool IsAnyPieceMoving()
        {
            for (int i = 0; i < _viewsByCell.Length; i++)
            {
                if (_viewsByCell[i].IsMoving) return true;
            }
            return false;
        }

        private Color ColorOf(int pieceId) => _pieceColors[pieceId - BoardGenerator.FirstPieceId];

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
