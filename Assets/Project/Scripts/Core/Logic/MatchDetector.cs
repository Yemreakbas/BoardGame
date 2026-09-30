using System;
using BoardGame.Core.Pooling;

namespace BoardGame.Core.Logic
{
    /// <summary>
    /// Finds horizontal and vertical runs of <see cref="MinMatchLength"/> or more pieces of the same color
    /// (specials match by their color; color bombs, being colorless, never do).
    /// Stateless and allocation-free: results go to <see cref="GlobalBuffer"/>.
    /// </summary>
    public static class MatchDetector
    {
        public const int MinMatchLength = 3;

        /// <summary>
        /// Scans every row and column. Writes each matched cell index into
        /// <see cref="GlobalBuffer.MatchResultIndices"/> (ascending, no duplicates), leaves those cells set in
        /// <see cref="GlobalBuffer.MatchFlags"/>, and records every run in <see cref="GlobalBuffer.RunStart"/>
        /// and its siblings.
        /// </summary>
        /// <param name="cells">Row-major piece IDs (index = y * width + x). <see cref="Board.Empty"/> never matches.</param>
        /// <param name="width">Board width in cells.</param>
        /// <param name="height">Board height in cells.</param>
        /// <returns>How many entries of the result buffer are valid; 0 when the board has no match.</returns>
        public static int FindMatches(int[] cells, int width, int height)
        {
            int cellCount = width * height;
            bool[] flags = GlobalBuffer.MatchFlags;
            Array.Clear(flags, 0, cellCount);
            GlobalBuffer.RunCount = 0;

            for (int y = 0; y < height; y++) FlagRuns(cells, flags, y * width, width, 1, true);   // rows
            for (int x = 0; x < width; x++) FlagRuns(cells, flags, x, height, width, false);      // columns

            // Crossing runs flag their shared cell once, so each matched cell is written once.
            int[] results = GlobalBuffer.MatchResultIndices;
            int count = 0;
            for (int i = 0; i < cellCount; i++)
            {
                if (flags[i]) results[count++] = i;
            }
            return count;
        }

        /// <summary>
        /// Looks for one swap of two orthogonally adjacent cells that the board would accept: one that
        /// creates a match, or one involving a color bomb. The board must be stable (no current match);
        /// cells are swapped in place for the test and always restored.
        /// </summary>
        /// <param name="indexA">First cell of the found swap, or -1.</param>
        /// <param name="indexB">Second cell of the found swap (right of or above indexA), or -1.</param>
        /// <returns>True if at least one such swap exists.</returns>
        public static bool FindPossibleMove(int[] cells, int width, int height, out int indexA, out int indexB)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (x + 1 < width && IsAcceptedSwap(cells, width, height, index, index + 1))
                    {
                        indexA = index;
                        indexB = index + 1;
                        return true;
                    }
                    if (y + 1 < height && IsAcceptedSwap(cells, width, height, index, index + width))
                    {
                        indexA = index;
                        indexB = index + width;
                        return true;
                    }
                }
            }

            indexA = -1;
            indexB = -1;
            return false;
        }

        /// <summary>True if swapping the two (adjacent) cells would be accepted by <see cref="Board.Swap"/>.</summary>
        public static bool IsAcceptedSwap(int[] cells, int width, int height, int a, int b)
        {
            if (cells[a] == Board.Empty || cells[b] == Board.Empty) return false;
            if (Piece.IsColorBomb(cells[a]) || Piece.IsColorBomb(cells[b])) return true;
            if (Piece.ColorOf(cells[a]) == Piece.ColorOf(cells[b])) return false;

            // Only runs through one of the two swapped cells can be new, so checking those two is enough.
            Exchange(cells, a, b);
            bool matched = IsInRun(cells, width, height, a) || IsInRun(cells, width, height, b);
            Exchange(cells, a, b);
            return matched;
        }

        // True if the piece at `index` belongs to a horizontal or vertical run of MinMatchLength or more.
        private static bool IsInRun(int[] cells, int width, int height, int index)
        {
            int color = Piece.ColorOf(cells[index]);
            if (color == Board.Empty) return false;

            int x = index % width;
            int y = index / width;

            int run = 1;
            for (int i = x - 1; i >= 0 && Piece.ColorOf(cells[y * width + i]) == color; i--) run++;
            for (int i = x + 1; i < width && Piece.ColorOf(cells[y * width + i]) == color; i++) run++;
            if (run >= MinMatchLength) return true;

            run = 1;
            for (int j = y - 1; j >= 0 && Piece.ColorOf(cells[j * width + x]) == color; j--) run++;
            for (int j = y + 1; j < height && Piece.ColorOf(cells[j * width + x]) == color; j++) run++;
            return run >= MinMatchLength;
        }

        private static void Exchange(int[] cells, int a, int b)
        {
            int piece = cells[a];
            cells[a] = cells[b];
            cells[b] = piece;
        }

        // Walks one line of `length` cells spaced `stride` apart (a row: stride 1, a column: stride width),
        // flags every run of MinMatchLength or more pieces of one color, and records the run.
        private static void FlagRuns(int[] cells, bool[] flags, int firstIndex, int length, int stride, bool isRow)
        {
            int runStart = 0;
            for (int i = 1; i <= length; i++)
            {
                int runColor = Piece.ColorOf(cells[firstIndex + runStart * stride]);
                if (i < length && Piece.ColorOf(cells[firstIndex + i * stride]) == runColor) continue;

                int runLength = i - runStart;
                if (runLength >= MinMatchLength && runColor != Board.Empty)
                {
                    int start = firstIndex + runStart * stride;
                    for (int j = 0; j < runLength; j++) flags[start + j * stride] = true;

                    int run = GlobalBuffer.RunCount++;
                    GlobalBuffer.RunStart[run] = start;
                    GlobalBuffer.RunLength[run] = runLength;
                    GlobalBuffer.RunStride[run] = stride;
                    GlobalBuffer.RunIsRow[run] = isRow;
                }
                runStart = i;
            }
        }
    }
}
