using System;
using UnityEngine;

namespace BoardGame.Data
{
    /// <summary>
    /// A hand-designed board layout: dimensions plus one piece ID per cell, edited with the Level Editor
    /// window (Window > Match-3 > Level Editor). Cells use the core's layout: row-major,
    /// index = y * Width + x, y = 0 is the bottom row; 0 is empty, 1..<see cref="ColorCount"/> are colors.
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Level Data", fileName = "LevelData")]
    public sealed class LevelData : ScriptableObject
    {
        public const int Empty = 0;
        public const int ColorCount = 6;
        public const int MinSize = 3;
        public const int MaxSize = 32; // 32 x 32 = GlobalBuffer.MaxCellCount
        public const int DefaultSize = 8;

        // Edited through the Level Editor only: changing a dimension in the inspector could not
        // preserve the layout, because the previous width would already be lost.
        [SerializeField, HideInInspector] private int _width = DefaultSize;
        [SerializeField, HideInInspector] private int _height = DefaultSize;
        [SerializeField, HideInInspector] private int[] _cells = new int[DefaultSize * DefaultSize];

        public int Width => _width;
        public int Height => _height;

        /// <summary>The live cell array (length Width * Height). Record an Undo before writing to it.</summary>
        public int[] Cells
        {
            get
            {
                EnsureLayout();
                return _cells;
            }
        }

        public int IndexOf(int x, int y) => y * _width + x;

        public bool Contains(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;

        public int GetCell(int x, int y) => Contains(x, y) ? Cells[IndexOf(x, y)] : Empty;

        public void SetCell(int x, int y, int pieceId)
        {
            if (Contains(x, y)) Cells[IndexOf(x, y)] = pieceId;
        }

        /// <summary>Sets every cell to <paramref name="pieceId"/>.</summary>
        public void Fill(int pieceId)
        {
            Array.Fill(Cells, pieceId);
        }

        /// <summary>
        /// Changes the board size (clamped to <see cref="MinSize"/>..<see cref="MaxSize"/>) and keeps every
        /// piece whose (x, y) still fits. The board is anchored at its bottom-left corner, like the game
        /// board: added columns appear on the right, added rows at the top, and new cells are empty.
        /// </summary>
        public void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, MinSize, MaxSize);
            height = Mathf.Clamp(height, MinSize, MaxSize);
            EnsureLayout();
            if (width == _width && height == _height) return;

            var resized = new int[width * height];
            int keepWidth = Mathf.Min(width, _width);
            int keepHeight = Mathf.Min(height, _height);
            for (int y = 0; y < keepHeight; y++)
            {
                for (int x = 0; x < keepWidth; x++)
                {
                    resized[y * width + x] = _cells[y * _width + x];
                }
            }

            _width = width;
            _height = height;
            _cells = resized;
        }

        /// <summary>
        /// Repairs out-of-range dimensions or a cell array of the wrong length (a hand-edited or older
        /// asset), keeping as many cells as fit. Returns true if anything changed.
        /// </summary>
        public bool EnsureLayout()
        {
            bool changed = false;
            int width = Mathf.Clamp(_width, MinSize, MaxSize);
            int height = Mathf.Clamp(_height, MinSize, MaxSize);
            if (width != _width || height != _height)
            {
                _width = width;
                _height = height;
                changed = true;
            }

            int cellCount = _width * _height;
            if (_cells == null || _cells.Length != cellCount)
            {
                var repaired = new int[cellCount];
                if (_cells != null) Array.Copy(_cells, repaired, Mathf.Min(_cells.Length, cellCount));
                _cells = repaired;
                changed = true;
            }
            return changed;
        }

        private void OnValidate()
        {
            EnsureLayout();
        }
    }
}
