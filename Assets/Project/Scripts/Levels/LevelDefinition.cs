using UnityEngine;

namespace BoardGame.Levels
{
    /// <summary>One level's rules: board shape, how many colors, and the move limit and score to beat.</summary>
    [CreateAssetMenu(menuName = "BoardGame/Level Definition", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField, Range(3, 32)] private int _width = 8;
        [SerializeField, Range(3, 32)] private int _height = 8;
        [Tooltip("Piece colors in play. More colors mean fewer matches: harder.")]
        [SerializeField, Range(3, 7)] private int _colorCount = 5;
        [SerializeField, Min(1)] private int _moveLimit = 20;
        [SerializeField, Min(1)] private int _targetScore = 2000;
        [Tooltip("Same seed, same level every time. 0 picks a new layout on each attempt.")]
        [SerializeField] private int _seed;
        [Tooltip("Ice under the board, one line per row from the TOP: '.' none, '1' or '2' layers. " +
                 "Leave empty for no ice. With ice, the level is won only once all of it is broken.")]
        [SerializeField, TextArea(3, 12)] private string _iceLayout = "";
        [Tooltip("Locked pieces, one line per row from the TOP: 'L' locked, '.' free. Leave empty for none. " +
                 "With locks, the level is won only once every lock is broken.")]
        [SerializeField, TextArea(3, 12)] private string _lockLayout = "";

        public int Width => _width;
        public int Height => _height;
        public int ColorCount => _colorCount;
        public int MoveLimit => _moveLimit;
        public int TargetScore => _targetScore;
        public int Seed => _seed;
        public bool HasIce => !string.IsNullOrWhiteSpace(_iceLayout);
        public bool HasLocks => !string.IsNullOrWhiteSpace(_lockLayout);

        /// <summary>Locked cells in board order, parsed like <see cref="BuildIce"/>. Setup only: allocates.</summary>
        public bool[] BuildLocks()
        {
            var locks = new bool[_width * _height];
            if (!HasLocks) return locks;

            string[] rows = _lockLayout.Replace("\r", "").Split('\n');
            for (int row = 0; row < rows.Length && row < _height; row++)
            {
                int y = _height - 1 - row;
                string line = rows[row].Trim();
                for (int x = 0; x < line.Length && x < _width; x++)
                {
                    if (line[x] == 'L' || line[x] == 'l') locks[y * _width + x] = true;
                }
            }
            return locks;
        }

        /// <summary>
        /// Ice layers per cell in board order (index = y * Width + x, y = 0 at the bottom), parsed from the
        /// top-down text layout. Missing rows or columns mean no ice; unknown characters are ignored.
        /// Allocates: call at level setup only.
        /// </summary>
        public int[] BuildIce()
        {
            var ice = new int[_width * _height];
            if (!HasIce) return ice;

            string[] rows = _iceLayout.Replace("\r", "").Split('\n');
            for (int row = 0; row < rows.Length && row < _height; row++)
            {
                int y = _height - 1 - row;
                string line = rows[row].Trim();
                for (int x = 0; x < line.Length && x < _width; x++)
                {
                    char c = line[x];
                    if (c >= '1' && c <= '9') ice[y * _width + x] = c - '0';
                }
            }
            return ice;
        }

        /// <summary>Editor/setup helper for building a catalog from code.</summary>
        public void Configure(int width, int height, int colorCount, int moveLimit, int targetScore, int seed = 0,
                              string iceLayout = "", string lockLayout = "")
        {
            _lockLayout = lockLayout ?? "";
            _width = width;
            _height = height;
            _colorCount = colorCount;
            _moveLimit = moveLimit;
            _targetScore = targetScore;
            _seed = seed;
            _iceLayout = iceLayout ?? "";
        }
    }
}
