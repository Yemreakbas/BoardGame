using UnityEngine;

namespace BoardGame.Levels
{
    /// <summary>The ordered list of levels. Level numbers shown to the player are index + 1.</summary>
    [CreateAssetMenu(menuName = "BoardGame/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private LevelDefinition[] _levels = new LevelDefinition[0];

        public int Count => _levels.Length;

        /// <summary>The level at <paramref name="index"/>, clamped into range; null only for an empty catalog.</summary>
        public LevelDefinition Get(int index)
        {
            if (_levels.Length == 0) return null;
            return _levels[Mathf.Clamp(index, 0, _levels.Length - 1)];
        }

        /// <summary>Editor/setup helper for building a catalog from code.</summary>
        public void SetLevels(LevelDefinition[] levels) => _levels = levels;
    }
}
