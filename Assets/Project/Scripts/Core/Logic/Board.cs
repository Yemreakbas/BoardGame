using System;
using BoardGame.Core.Pooling;

namespace BoardGame.Core.Logic
{
    /// <summary>Receives the cells cleared by one resolution step. The span is only valid during the call.</summary>
    public delegate void MatchedHandler(ReadOnlySpan<int> matchedIndices);

    /// <summary>
    /// Receives a shuffle as, for each cell, the index its piece came from. The span is only valid during the call.
    /// </summary>
    public delegate void ShuffledHandler(ReadOnlySpan<int> sourceIndices);

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

        /// <summary>
        /// The settled board had no possible move, so its pieces were rearranged. If no shuffle of the same
        /// pieces worked, the board was regenerated and some piece IDs changed: read them back from the board.
        /// </summary>
        public event ShuffledHandler OnShuffled;

        /// <summary>Shuffles tried before falling back to regenerating the board.</summary>
        private const int MaxShuffleAttempts = 100;

        private readonly int[] _cells;
        private readonly int[] _shuffleSources;
        private readonly BoardGenerator _generator;
        private bool _hasHoles; // Matches were cleared; the next step collapses and refills.
        private bool _shuffledThisResolve;

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
            _shuffleSources = new int[cellCount];
            _generator.Fill(_cells, width, height);
            if (!FindPossibleMove(out _, out _)) Shuffle(); // Nobody listens yet, so this is silent.
        }

        public int ToIndex(int x, int y) => y * Width + x;
        public int ToX(int index) => index % Width;
        public int ToY(int index) => index / Width;
        public bool IsInside(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        public int GetPiece(int index) => _cells[index];
        public int GetPiece(int x, int y) => _cells[ToIndex(x, y)];

        /// <summary>Finds one swap that would create a match (e.g. for a hint). Only meaningful while not resolving.</summary>
        public bool FindPossibleMove(out int indexA, out int indexB)
        {
            return MatchDetector.FindPossibleMove(_cells, Width, Height, out indexA, out indexB);
        }

        /// <summary>
        /// True from an accepted <see cref="Swap"/> until <see cref="ResolveStep"/> reports the board stable.
        /// No swap is accepted meanwhile.
        /// </summary>
        public bool IsResolving { get; private set; }

        /// <summary>
        /// Swaps two orthogonally adjacent pieces if that creates a match. The board is then resolving:
        /// drive it with <see cref="ResolveStep"/> (one step per animation beat) or <see cref="ResolveAll"/>.
        /// </summary>
        /// <returns>
        /// True if the swap was accepted. A rejected swap (not adjacent, off the board, no match, or the
        /// board still resolving) leaves the board untouched and raises no event.
        /// </returns>
        public bool Swap(int x1, int y1, int x2, int y2)
        {
            if (IsResolving) return false;
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

            IsResolving = true;
            OnPiecesSwapped?.Invoke(a, b);
            return true;
        }

        /// <summary>
        /// Advances resolution by one visible beat, alternating two kinds of step: clear every current match
        /// (<see cref="OnMatched"/>), then collapse and refill the holes (<see cref="OnPieceMoved"/>,
        /// <see cref="OnPieceSpawned"/>). Refills can create new matches, so cascades simply keep stepping.
        /// </summary>
        /// <returns>True if a step ran; false once the board is stable (and <see cref="IsResolving"/> is cleared).</returns>
        public bool ResolveStep()
        {
            if (!IsResolving) return false;

            if (_hasHoles)
            {
                for (int x = 0; x < Width; x++) RefillColumn(x, CollapseColumn(x));
                _hasHoles = false;
                return true;
            }

            // Detection runs here rather than reusing the swap check, so the shared match buffer is never
            // read across an event (listeners may use it too).
            int matchCount = MatchDetector.FindMatches(_cells, Width, Height);
            if (matchCount == 0)
            {
                // Settled. A dead board gets one shuffle step (never a second: a board too small to ever
                // have a move would otherwise shuffle forever).
                if (!_shuffledThisResolve && !FindPossibleMove(out _, out _))
                {
                    _shuffledThisResolve = true;
                    Shuffle();
                    OnShuffled?.Invoke(_shuffleSources.AsSpan(0, _cells.Length));
                    return true;
                }

                _shuffledThisResolve = false;
                IsResolving = false;
                return false;
            }

            ClearMatches(matchCount);
            _hasHoles = true;
            return true;
        }

        /// <summary>Runs <see cref="ResolveStep"/> until the board is stable, for callers that do not animate.</summary>
        public void ResolveAll()
        {
            while (ResolveStep()) { }
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

        // Rearranges the pieces into a layout with no match and at least one move, recording in
        // _shuffleSources where each cell's piece came from. Falls back to fresh boards (which never start
        // with a match) if shuffling keeps failing, e.g. when one piece type dominates the board.
        private void Shuffle()
        {
            int cellCount = _cells.Length;
            for (int i = 0; i < cellCount; i++) _shuffleSources[i] = i;

            for (int attempt = 0; attempt < MaxShuffleAttempts; attempt++)
            {
                _generator.Shuffle(_cells, _shuffleSources, cellCount);
                if (MatchDetector.FindMatches(_cells, Width, Height) == 0 && FindPossibleMove(out _, out _)) return;
            }

            for (int attempt = 0; attempt < MaxShuffleAttempts; attempt++)
            {
                _generator.Fill(_cells, Width, Height);
                if (FindPossibleMove(out _, out _)) return;
            }
            // Still no move: the board is too small to ever have one. Leave the last match-free fill.
        }

        private void Exchange(int a, int b)
        {
            int piece = _cells[a];
            _cells[a] = _cells[b];
            _cells[b] = piece;
        }
    }
}
