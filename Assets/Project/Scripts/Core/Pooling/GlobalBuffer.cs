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
