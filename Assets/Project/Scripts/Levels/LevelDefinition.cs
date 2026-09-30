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

        public int Width => _width;
        public int Height => _height;
        public int ColorCount => _colorCount;
        public int MoveLimit => _moveLimit;
        public int TargetScore => _targetScore;
        public int Seed => _seed;

        /// <summary>Editor/setup helper for building a catalog from code.</summary>
        public void Configure(int width, int height, int colorCount, int moveLimit, int targetScore, int seed = 0)
        {
            _width = width;
            _height = height;
            _colorCount = colorCount;
            _moveLimit = moveLimit;
            _targetScore = targetScore;
            _seed = seed;
        }
    }
}
