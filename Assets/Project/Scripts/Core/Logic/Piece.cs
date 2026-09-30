namespace BoardGame.Core.Logic
{
    public enum SpecialKind
    {
        None = 0,

        /// <summary>Clears its whole row when it is cleared. Made from a horizontal run of 4.</summary>
        RowRocket = 1,

        /// <summary>Clears its whole column when it is cleared. Made from a vertical run of 4.</summary>
        ColumnRocket = 2,

        /// <summary>
        /// Colorless: never part of a run. Swapped with a neighbour it clears every piece of that neighbour's
        /// color (the whole board if both are bombs). Made from a straight run of 5 or more.
        /// </summary>
        ColorBomb = 3,
    }

    /// <summary>
    /// Piece ID encoding. A board cell holds one int: bits 0-7 are the color (1..255, 0 for
    /// <see cref="Board.Empty"/> and color bombs), bits 8-11 the <see cref="SpecialKind"/>, bit 12 the lock.
    /// Plain pieces are therefore just their color, which keeps the generator and match code unchanged.
    /// </summary>
    public static class Piece
    {
        private const int ColorBits = 8;
        private const int ColorMask = (1 << ColorBits) - 1;
        private const int SpecialMask = 0xF;
        private const int LockBit = 1 << 12;

        public static int Make(int color, SpecialKind special) => ((int)special << ColorBits) | (color & ColorMask);

        public static int ColorOf(int pieceId) => pieceId & ColorMask;

        public static SpecialKind SpecialOf(int pieceId) => (SpecialKind)((pieceId >> ColorBits) & SpecialMask);

        public static bool IsColorBomb(int pieceId) => SpecialOf(pieceId) == SpecialKind.ColorBomb;

        /// <summary>
        /// A locked piece cannot be swapped. It still matches by color and falls with its column; the first
        /// clear that hits it only breaks the lock and leaves the piece in place.
        /// </summary>
        public static bool IsLocked(int pieceId) => (pieceId & LockBit) != 0;

        public static int WithLock(int pieceId, bool locked) => locked ? pieceId | LockBit : pieceId & ~LockBit;
    }
}
