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
