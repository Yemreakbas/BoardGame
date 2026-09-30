using System;

namespace BoardGame.Core.Pooling
{
    /// <summary>
    /// Scratch memory shared by the logic core. Each array is allocated once, when the type is first
    /// touched, and reused for the rest of the session, so match queries never allocate.
    /// </summary>
    /// <remarks>
    /// A buffer only holds the result of the latest query that wrote to it: read it immediately and
    /// never keep a reference to it. The core is single-threaded by design.
    /// </remarks>
    public static class GlobalBuffer
    {
        /// <summary>Largest board, in cells, the buffers can serve (e.g. 32 x 32).</summary>
        public const int MaxCellCount = 1024;

        /// <summary>
        /// Written by <see cref="Logic.MatchDetector.FindMatches"/>: matched cell indices in ascending order,
        /// each listed once. Only the first <c>count</c> entries (the method's return value) are valid.
        /// </summary>
        public static readonly int[] MatchResultIndices = new int[MaxCellCount];

        /// <summary>
        /// Per-cell "part of a match" flags. They let a cell shared by a horizontal and a vertical run
        /// (L and T shapes) be reported once.
        /// </summary>
        public static readonly bool[] MatchFlags = new bool[MaxCellCount];

        /// <summary>
        /// Written by <see cref="Logic.MatchDetector.FindMatches"/>: every run it found, as first cell index,
        /// length and stride (1 for a row, the board width for a column). <see cref="RunCount"/> are valid.
        /// A run needs 3 cells, so there are fewer runs than cells.
        /// </summary>
        public static readonly int[] RunStart = new int[MaxCellCount];
        public static readonly int[] RunLength = new int[MaxCellCount];
        public static readonly int[] RunStride = new int[MaxCellCount];
        public static readonly bool[] RunIsRow = new bool[MaxCellCount]; // stride alone is ambiguous on 1-wide boards
        public static int RunCount;

        /// <summary>Board scratch: cells that receive a new special this step and so must not be cleared.</summary>
        public static readonly bool[] ProtectedFlags = new bool[MaxCellCount];

        /// <summary>Board scratch: specials waiting to fire. Each cell is queued at most once per step.</summary>
        public static readonly int[] ActivationQueue = new int[MaxCellCount];

        /// <summary>Board scratch: specials created this step, as (cell index, piece ID) pairs.</summary>
        public static readonly int[] SpawnIndices = new int[MaxCellCount];
        public static readonly int[] SpawnPieces = new int[MaxCellCount];

        /// <summary>Board scratch: per-color piece counts, indexed by color (colors fit in 8 bits).</summary>
        public static readonly int[] ColorCounts = new int[256];

        /// <summary>
        /// Throws if a board of <paramref name="cellCount"/> cells does not fit. Call it during setup:
        /// touching the type there also makes sure the arrays are allocated before gameplay starts.
        /// </summary>
        public static void EnsureCapacity(int cellCount)
        {
            if (cellCount > MatchResultIndices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(cellCount), cellCount,
                    $"Boards are limited to {MaxCellCount} cells (GlobalBuffer.MaxCellCount).");
            }
        }
    }
}
