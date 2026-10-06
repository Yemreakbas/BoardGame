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

        /// <summary>How many times <see cref="FillAround"/> restarts before giving up on a layout.</summary>
        public const int MaxLayoutFillAttempts = 32;

        /// <summary>
        /// Copies <paramref name="layout"/> into <paramref name="cells"/> and fills its <see cref="Board.Empty"/>
        /// cells so that no run is completed, around fixed pieces on any side. The layout's own pieces must not
        /// already match. Returns false (leaving <paramref name="cells"/> unspecified) if a cell keeps ending up
        /// with every type excluded.
        /// </summary>
        /// <remarks>
        /// Every 3-cell window is checked when its last empty cell is filled, so a successful fill has no match.
        /// A cell can be blocked by up to six windows, more than the minimum three types, hence the retries.
        /// </remarks>
        public bool FillAround(ReadOnlySpan<int> layout, Span<int> cells, int width, int height)
        {
            for (int attempt = 0; attempt < MaxLayoutFillAttempts; attempt++)
            {
                layout.CopyTo(cells);
                if (TryFillEmpty(cells, width, height)) return true;
            }
            return false;
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

        private bool TryFillEmpty(Span<int> cells, int width, int height)
        {
            Span<int> excluded = stackalloc int[6]; // 3 windows per axis
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (cells[index] != Board.Empty) continue;

                    int excludedCount = 0;
                    CollectRunColors(cells, width, height, x, y, 1, 0, excluded, ref excludedCount);
                    CollectRunColors(cells, width, height, x, y, 0, 1, excluded, ref excludedCount);

                    int piece = NextPieceExcluding(excluded.Slice(0, excludedCount));
                    if (piece == Board.Empty) return false;
                    cells[index] = piece;
                }
            }
            return true;
        }

        // For each 3-cell window along (dx, dy) that contains (x, y), records the color that would complete
        // it: the color of its other two cells, when both are filled and equal.
        private static void CollectRunColors(ReadOnlySpan<int> cells, int width, int height, int x, int y,
                                             int dx, int dy, Span<int> excluded, ref int count)
        {
            for (int start = -2; start <= 0; start++)
            {
                int color = Board.Empty;
                bool completes = true;
                for (int k = start; k <= start + 2; k++)
                {
                    if (k == 0) continue;
                    int cx = x + k * dx, cy = y + k * dy;
                    if (cx < 0 || cx >= width || cy < 0 || cy >= height)
                    {
                        completes = false;
                        break;
                    }

                    int piece = cells[cy * width + cx];
                    if (piece == Board.Empty || (color != Board.Empty && piece != color))
                    {
                        completes = false;
                        break;
                    }
                    color = piece;
                }
                if (completes) excluded[count++] = color;
            }
        }

        // Uniform draw among the IDs not in `excluded`; Board.Empty when every ID is excluded.
        private int NextPieceExcluding(ReadOnlySpan<int> excluded)
        {
            int candidateCount = 0;
            for (int id = FirstPieceId; id < FirstPieceId + PieceTypeCount; id++)
            {
                if (excluded.IndexOf(id) < 0) candidateCount++;
            }
            if (candidateCount == 0) return Board.Empty;

            int skip = _random.Next(candidateCount);
            for (int id = FirstPieceId; ; id++)
            {
                if (excluded.IndexOf(id) >= 0) continue;
                if (skip == 0) return id;
                skip--;
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
