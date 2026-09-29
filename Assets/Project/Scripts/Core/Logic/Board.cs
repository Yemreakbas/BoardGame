using System;
using BoardGame.Core.Pooling;

namespace BoardGame.Core.Logic
{
    /// <summary>Receives the cells cleared by one resolution step. The span is only valid during the call.</summary>
    public delegate void MatchedHandler(ReadOnlySpan<int> matchedIndices);

    /// <summary>
    /// Authoritative match-3 state: a flat, row-major array of piece IDs
    /// (index = y * Width + x, y = 0 is the bottom row, 0 is <see cref="Empty"/>).
    /// Owns the swap / match / collapse / refill rules and reports every change through events.
    /// Nothing allocates after the constructor returns.
    /// </summary>
    public sealed class Board
    {
        public const int Empty = 0;

        /// <summary>A swap was accepted: the pieces at (indexA, indexB) exchanged cells.</summary>
        public event Action<int, int> OnPiecesSwapped;

        /// <summary>A piece fell from (fromIndex) into the empty cell (toIndex); fromIndex is now empty.</summary>
        public event Action<int, int> OnPieceMoved;

        /// <summary>Cells matched in one resolution step. They are already <see cref="Empty"/> when this is raised.</summary>
        public event MatchedHandler OnMatched;

        /// <summary>A refill placed a new piece: (index, pieceId).</summary>
        public event Action<int, int> OnPieceSpawned;

        private readonly int[] _cells;
        private readonly BoardGenerator _generator;

        public int Width { get; }
        public int Height { get; }
        public int CellCount => _cells.Length;

        public Board(int width, int height, BoardGenerator generator)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

            int cellCount = checked(width * height);
            GlobalBuffer.EnsureCapacity(cellCount);

            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            Width = width;
            Height = height;
            _cells = new int[cellCount];
            _generator.Fill(_cells, width, height);
        }

        public int ToIndex(int x, int y) => y * Width + x;
        public int ToX(int index) => index % Width;
        public int ToY(int index) => index / Width;
        public bool IsInside(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        public int GetPiece(int index) => _cells[index];
        public int GetPiece(int x, int y) => _cells[ToIndex(x, y)];

        /// <summary>
        /// Swaps two orthogonally adjacent pieces if that creates a match, then resolves the board
        /// completely (clear, collapse, refill, cascades) before returning.
        /// </summary>
        /// <returns>True if the swap was accepted. A rejected swap leaves the board untouched and raises no event.</returns>
        public bool Swap(int x1, int y1, int x2, int y2)
        {
            if (!IsInside(x1, y1) || !IsInside(x2, y2)) return false;
            if (Math.Abs(x1 - x2) + Math.Abs(y1 - y2) != 1) return false;

            int a = ToIndex(x1, y1);
            int b = ToIndex(x2, y2);

            Exchange(a, b);
            if (MatchDetector.FindMatches(_cells, Width, Height) == 0)
            {
                Exchange(a, b); // No match: undo silently, a rejected swap is not a state change.
                return false;
            }

            OnPiecesSwapped?.Invoke(a, b);
            Resolve();
            return true;
        }

        // Clear -> collapse -> refill until no match is left. Detection runs again here rather than reusing
        // the swap check, so the shared match buffer is never read across an event (listeners may use it too).
        private void Resolve()
        {
            int matchCount = MatchDetector.FindMatches(_cells, Width, Height);
            while (matchCount > 0)
            {
                ClearMatches(matchCount);
                for (int x = 0; x < Width; x++)
                {
                    RefillColumn(x, CollapseColumn(x));
                }
                matchCount = MatchDetector.FindMatches(_cells, Width, Height);
            }
        }

        private void ClearMatches(int matchCount)
        {
            int[] matched = GlobalBuffer.MatchResultIndices;
            for (int i = 0; i < matchCount; i++) _cells[matched[i]] = Empty;
            OnMatched?.Invoke(matched.AsSpan(0, matchCount));
        }

        // Moves the column's pieces down over the empty cells below them, keeping their order.
        // Returns the lowest row left empty, which is where the refill starts.
        private int CollapseColumn(int x)
        {
            int writeY = 0;
            for (int y = 0; y < Height; y++)
            {
                int from = ToIndex(x, y);
                int piece = _cells[from];
                if (piece == Empty) continue;

                if (y != writeY)
                {
                    int to = ToIndex(x, writeY);
                    _cells[to] = piece;
                    _cells[from] = Empty;
                    OnPieceMoved?.Invoke(from, to);
                }
                writeY++;
            }
            return writeY;
        }

        // Spawns bottom-up, so listeners receive a column's new pieces in stacking order.
        private void RefillColumn(int x, int firstEmptyY)
        {
            for (int y = firstEmptyY; y < Height; y++)
            {
                int index = ToIndex(x, y);
                int piece = _generator.NextPiece();
                _cells[index] = piece;
                OnPieceSpawned?.Invoke(index, piece);
            }
        }

        private void Exchange(int a, int b)
        {
            int piece = _cells[a];
            _cells[a] = _cells[b];
            _cells[b] = piece;
        }
    }
}
