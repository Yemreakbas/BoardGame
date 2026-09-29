using System;
using BoardGame.Core.Pooling;

namespace BoardGame.Core.Logic
{
    /// <summary>
    /// Finds horizontal and vertical runs of <see cref="MinMatchLength"/> or more identical pieces.
    /// Stateless and allocation-free: results go to <see cref="GlobalBuffer.MatchResultIndices"/>.
    /// </summary>
    public static class MatchDetector
    {
        public const int MinMatchLength = 3;

        /// <summary>
        /// Scans every row and column and writes each matched cell index into
        /// <see cref="GlobalBuffer.MatchResultIndices"/>, in ascending order and without duplicates.
        /// </summary>
        /// <param name="cells">Row-major piece IDs (index = y * width + x). <see cref="Board.Empty"/> never matches.</param>
        /// <param name="width">Board width in cells.</param>
        /// <param name="height">Board height in cells.</param>
        /// <returns>How many entries of the buffer are valid; 0 when the board has no match.</returns>
        public static int FindMatches(int[] cells, int width, int height)
        {
            int cellCount = width * height;
            bool[] flags = GlobalBuffer.MatchFlags;
            Array.Clear(flags, 0, cellCount);

            for (int y = 0; y < height; y++) FlagRuns(cells, flags, y * width, width, 1);   // rows
            for (int x = 0; x < width; x++) FlagRuns(cells, flags, x, height, width);       // columns

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
        /// Looks for one swap of two orthogonally adjacent cells that would create a match. The board must
        /// be stable (no current match); cells are swapped in place for the test and always restored.
        /// </summary>
        /// <param name="indexA">First cell of the found swap, or -1.</param>
        /// <param name="indexB">Second cell of the found swap (right of or above indexA), or -1.</param>
        /// <returns>True if at least one matching swap exists.</returns>
        public static bool FindPossibleMove(int[] cells, int width, int height, out int indexA, out int indexB)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (x + 1 < width && SwapCreatesMatch(cells, width, height, index, index + 1))
                    {
                        indexA = index;
                        indexB = index + 1;
                        return true;
                    }
                    if (y + 1 < height && SwapCreatesMatch(cells, width, height, index, index + width))
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

        // Only runs through one of the two swapped cells can be new, so checking those two is enough.
        private static bool SwapCreatesMatch(int[] cells, int width, int height, int a, int b)
        {
            if (cells[a] == cells[b]) return false;

            Exchange(cells, a, b);
            bool matched = IsInRun(cells, width, height, a) || IsInRun(cells, width, height, b);
            Exchange(cells, a, b);
            return matched;
        }

        // True if the piece at `index` belongs to a horizontal or vertical run of MinMatchLength or more.
        private static bool IsInRun(int[] cells, int width, int height, int index)
        {
            int piece = cells[index];
            if (piece == Board.Empty) return false;

            int x = index % width;
            int y = index / width;

            int run = 1;
            for (int i = x - 1; i >= 0 && cells[y * width + i] == piece; i--) run++;
            for (int i = x + 1; i < width && cells[y * width + i] == piece; i++) run++;
            if (run >= MinMatchLength) return true;

            run = 1;
            for (int j = y - 1; j >= 0 && cells[j * width + x] == piece; j--) run++;
            for (int j = y + 1; j < height && cells[j * width + x] == piece; j++) run++;
            return run >= MinMatchLength;
        }

        private static void Exchange(int[] cells, int a, int b)
        {
            int piece = cells[a];
            cells[a] = cells[b];
            cells[b] = piece;
        }

        // Walks one line of `length` cells spaced `stride` apart (a row: stride 1, a column: stride width)
        // and flags every run of MinMatchLength or more equal, non-empty pieces.
        private static void FlagRuns(int[] cells, bool[] flags, int firstIndex, int length, int stride)
        {
            int runStart = 0;
            for (int i = 1; i <= length; i++)
            {
                int runPiece = cells[firstIndex + runStart * stride];
                if (i < length && cells[firstIndex + i * stride] == runPiece) continue;

                if (i - runStart >= MinMatchLength && runPiece != Board.Empty)
                {
                    for (int j = runStart; j < i; j++) flags[firstIndex + j * stride] = true;
                }
                runStart = i;
            }
        }
    }
}
