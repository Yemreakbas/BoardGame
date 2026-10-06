using BoardGame.Core.Logic;
using BoardGame.Core.Pooling;
using NUnit.Framework;

namespace BoardGame.Tests
{
    /// <summary>EditMode tests for the static, allocation-free <see cref="MatchDetector"/>.</summary>
    public class MatchDetectorTests
    {
        private const int Width = 5;
        private const int Height = 5;
        private const int CellCount = Width * Height;

        // Colors at or above this value are unique per cell, so the background can never match.
        private const int UniqueColorBase = 10;
        private const int Red = 1;
        private const int Blue = 2;

        [SetUp]
        public void SetUp()
        {
            GlobalBuffer.EnsureCapacity(CellCount);
        }

        [Test]
        public void FindMatches_ThreeInARow_ReturnsMatchCountAndFlagsThem()
        {
            int[] cells = CreateUniqueBoard();
            int a = IndexOf(1, 2), b = IndexOf(2, 2), c = IndexOf(3, 2);
            cells[a] = Red;
            cells[b] = Red;
            cells[c] = Red;

            int count = MatchDetector.FindMatches(cells, Width, Height);

            Assert.AreEqual(3, count);
            Assert.IsTrue(GlobalBuffer.MatchFlags[a]);
            Assert.IsTrue(GlobalBuffer.MatchFlags[b]);
            Assert.IsTrue(GlobalBuffer.MatchFlags[c]);
            Assert.AreEqual(a, GlobalBuffer.MatchResultIndices[0]);
            Assert.AreEqual(b, GlobalBuffer.MatchResultIndices[1]);
            Assert.AreEqual(c, GlobalBuffer.MatchResultIndices[2]);

            for (int i = 0; i < CellCount; i++)
            {
                if (i == a || i == b || i == c) continue;
                Assert.IsFalse(GlobalBuffer.MatchFlags[i], $"Cell {i} should not be flagged.");
            }
        }

        [Test]
        public void FindMatches_NoMatch_ReturnsZero()
        {
            // Checkerboard: no two orthogonal neighbours share a color, so no run can reach 3.
            int[] cells = new int[CellCount];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    cells[IndexOf(x, y)] = (x + y) % 2 == 0 ? Red : Blue;
                }
            }

            int count = MatchDetector.FindMatches(cells, Width, Height);

            Assert.AreEqual(0, count);
            for (int i = 0; i < CellCount; i++)
            {
                Assert.IsFalse(GlobalBuffer.MatchFlags[i], $"Cell {i} should not be flagged.");
            }
        }

        [Test]
        public void FindPossibleMove_ValidSwap_ReturnsTrue()
        {
            // Row 2: R R . . .  and a lone R at (2, 3). Swapping (2, 2) with (2, 3) completes the row,
            // and every other swap only moves unique colors around.
            int[] cells = CreateUniqueBoard();
            cells[IndexOf(0, 2)] = Red;
            cells[IndexOf(1, 2)] = Red;
            cells[IndexOf(2, 3)] = Red;
            int[] before = (int[])cells.Clone();

            Assert.AreEqual(0, MatchDetector.FindMatches(cells, Width, Height), "Precondition: board must be stable.");

            bool found = MatchDetector.FindPossibleMove(cells, Width, Height, out int indexA, out int indexB);

            Assert.IsTrue(found);
            Assert.AreEqual(IndexOf(2, 2), indexA);
            Assert.AreEqual(IndexOf(2, 3), indexB);
            CollectionAssert.AreEqual(before, cells, "Trial swaps must restore the board.");
        }

        private static int IndexOf(int x, int y) => y * Width + x;

        private static int[] CreateUniqueBoard()
        {
            int[] cells = new int[CellCount];
            for (int i = 0; i < CellCount; i++) cells[i] = UniqueColorBase + i;
            return cells;
        }
    }
}
