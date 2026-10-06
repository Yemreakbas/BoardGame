using System;
using BoardGame.Core.Logic;
using BoardGame.Core.Pooling;
using NUnit.Framework;

namespace BoardGame.Tests
{
    /// <summary>EditMode tests for <see cref="Board.LoadLayout"/>: designed opening boards.</summary>
    public class BoardLayoutTests
    {
        private const int Width = 6;
        private const int Height = 6;
        private const int Colors = 4;

        [Test]
        public void LoadLayout_KeepsDesignedPiecesAndFillsEmptiesWithoutMatches()
        {
            int[] layout = new int[Width * Height];
            layout[0] = 1;  // (0, 0)
            layout[1] = 1;  // (1, 0): the empty (2, 0) must not become 1
            layout[8] = 2;  // (2, 1)
            layout[20] = 2; // (2, 3): the empty (2, 2) between them must not become 2

            for (int seed = 1; seed <= 50; seed++)
            {
                var board = new Board(Width, Height, new BoardGenerator(Colors, seed));
                board.LoadLayout(layout);

                for (int i = 0; i < layout.Length; i++)
                {
                    if (layout[i] != Board.Empty) Assert.AreEqual(layout[i], board.GetPiece(i), $"Seed {seed}, cell {i}");
                    else Assert.AreNotEqual(Board.Empty, board.GetPiece(i), $"Seed {seed}, cell {i}");
                }
                Assert.AreEqual(0, MatchDetector.FindMatches(CopyCells(board), Width, Height), $"Seed {seed}");
            }
        }

        [Test]
        public void LoadLayout_LayoutWithMatch_ThrowsAndLeavesBoardUnchanged()
        {
            var board = new Board(Width, Height, new BoardGenerator(Colors, 7));
            int[] before = CopyCells(board);
            int[] layout = new int[Width * Height];
            layout[0] = layout[1] = layout[2] = 3;

            Assert.Throws<ArgumentException>(() => board.LoadLayout(layout));
            CollectionAssert.AreEqual(before, CopyCells(board));
        }

        [Test]
        public void LoadLayout_WrongLength_Throws()
        {
            var board = new Board(Width, Height, new BoardGenerator(Colors, 7));
            Assert.Throws<ArgumentException>(() => board.LoadLayout(new int[Width * Height - 1]));
        }

        private static int[] CopyCells(Board board)
        {
            GlobalBuffer.EnsureCapacity(board.CellCount);
            var cells = new int[board.CellCount];
            for (int i = 0; i < cells.Length; i++) cells[i] = board.GetPiece(i);
            return cells;
        }
    }
}
