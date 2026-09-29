using System;

namespace BoardGame.Core.Logic
{
    /// <summary>
    /// Turns board events into score, move count and combo. Every cleared cell is worth
    /// <see cref="PointsPerPiece"/> times the combo, where the combo is the cascade wave number within
    /// the current move (first wave x1, second x2, ...). Allocation-free once constructed.
    /// </summary>
    public sealed class ScoreKeeper : IDisposable
    {
        public const int PointsPerPiece = 10;

        /// <summary>Score, moves or combo changed. Read the new values from the properties.</summary>
        public event Action OnChanged;

        private readonly Board _board;

        public int Score { get; private set; }
        public int Moves { get; private set; }

        /// <summary>Waves cleared so far by the current move; 0 before its first match.</summary>
        public int Combo { get; private set; }

        public ScoreKeeper(Board board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _board.OnPiecesSwapped += HandlePiecesSwapped;
            _board.OnMatched += HandleMatched;
        }

        /// <summary>Stops listening to the board.</summary>
        public void Dispose()
        {
            _board.OnPiecesSwapped -= HandlePiecesSwapped;
            _board.OnMatched -= HandleMatched;
        }

        private void HandlePiecesSwapped(int indexA, int indexB)
        {
            Moves++;
            Combo = 0;
            OnChanged?.Invoke();
        }

        private void HandleMatched(ReadOnlySpan<int> matchedIndices)
        {
            Combo++;
            Score += matchedIndices.Length * PointsPerPiece * Combo;
            OnChanged?.Invoke();
        }
    }
}
