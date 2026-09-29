using System;

namespace BoardGame.Core.Logic
{
    /// <summary>
    /// The core's only source of randomness: builds the opening layout (guaranteed free of matches)
    /// and draws the pieces that refill cleared cells. The same seed reproduces the same session.
    /// </summary>
    public sealed class BoardGenerator
    {
        /// <summary>Piece IDs run from 1 to <see cref="PieceTypeCount"/>; 0 is <see cref="Board.Empty"/>.</summary>
        public const int FirstPieceId = 1;

        /// <summary>A cell can be blocked from two types at once (one run to its left, one below it), so a third must remain.</summary>
        public const int MinPieceTypeCount = 3;

        private readonly Random _random;

        public int PieceTypeCount { get; }

        public BoardGenerator(int pieceTypeCount, int seed)
        {
            if (pieceTypeCount < MinPieceTypeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceTypeCount), pieceTypeCount,
                    $"A match-3 board needs at least {MinPieceTypeCount} piece types.");
            }

            PieceTypeCount = pieceTypeCount;
            _random = new Random(seed);
        }

        /// <summary>Fills every cell so that the board starts without any horizontal or vertical match.</summary>
        /// <remarks>
        /// Cells are filled row by row from (0, 0), so when a cell is chosen only the two cells to its left
        /// and the two below it are known, and those are the only ones that can complete a run through it.
        /// Excluding the (at most two) types that would do so and drawing among the rest costs exactly one
        /// random draw per cell, with no retry loop.
        /// </remarks>
        public void Fill(int[] cells, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    int blockedLeft = Board.Empty;
                    if (x >= 2 && cells[index - 1] == cells[index - 2]) blockedLeft = cells[index - 1];

                    int blockedBelow = Board.Empty;
                    if (y >= 2 && cells[index - width] == cells[index - 2 * width]) blockedBelow = cells[index - width];

                    cells[index] = NextPieceExcluding(blockedLeft, blockedBelow);
                }
            }
        }

        /// <summary>Draws a uniformly random piece for a refill. Refills may create matches, which drives cascades.</summary>
        public int NextPiece()
        {
            return _random.Next(FirstPieceId, FirstPieceId + PieceTypeCount);
        }

        /// <summary>
        /// Fisher-Yates shuffle of the pieces in <paramref name="cells"/>, applying the same exchanges to
        /// <paramref name="sourceIndices"/> so it keeps recording where each cell's piece came from.
        /// </summary>
        public void Shuffle(int[] cells, int[] sourceIndices, int cellCount)
        {
            for (int i = cellCount - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);

                int piece = cells[i];
                cells[i] = cells[j];
                cells[j] = piece;

                int source = sourceIndices[i];
                sourceIndices[i] = sourceIndices[j];
                sourceIndices[j] = source;
            }
        }

        // Uniform draw among the IDs that are neither excludedA nor excludedB (Board.Empty excludes nothing).
        private int NextPieceExcluding(int excludedA, int excludedB)
        {
            int candidateCount = PieceTypeCount;
            if (excludedA != Board.Empty) candidateCount--;
            if (excludedB != Board.Empty && excludedB != excludedA) candidateCount--;

            int skip = _random.Next(candidateCount);
            for (int id = FirstPieceId; ; id++)
            {
                if (id == excludedA || id == excludedB) continue;
                if (skip == 0) return id;
                skip--;
            }
        }
    }
}
