using UnityEngine;

namespace BoardGame.Levels
{
    /// <summary>
    /// Which level the player is on, kept in PlayerPrefs so it survives restarts. Winning moves to the next
    /// level; after the last one, play wraps back to the first.
    /// </summary>
    public static class LevelProgress
    {
        private const string CurrentKey = "BoardGame.LevelIndex";
        private const string HighestKey = "BoardGame.HighestUnlocked";
        private const string StarsKeyPrefix = "BoardGame.Stars.";

        /// <summary>Zero-based index of the level to play.</summary>
        public static int CurrentIndex => PlayerPrefs.GetInt(CurrentKey, 0);

        /// <summary>Zero-based index of the furthest level reached.</summary>
        public static int HighestUnlocked => PlayerPrefs.GetInt(HighestKey, 0);

        /// <summary>Records a win on the current level and selects the next one (wrapping after the last).</summary>
        /// <returns>True if that was the last level of the catalog.</returns>
        public static bool CompleteCurrent(int levelCount)
        {
            int next = CurrentIndex + 1;
            bool finishedAll = next >= levelCount;
            if (finishedAll) next = 0;

            PlayerPrefs.SetInt(CurrentKey, next);
            if (!finishedAll && next > HighestUnlocked) PlayerPrefs.SetInt(HighestKey, next);
            PlayerPrefs.Save();
            return finishedAll;
        }

        /// <summary>True for every level up to and including the furthest one reached.</summary>
        public static bool IsUnlocked(int index) => index <= HighestUnlocked;

        /// <summary>Makes <paramref name="index"/> the level to play next (e.g. picked on the level select screen).</summary>
        public static void Select(int index)
        {
            PlayerPrefs.SetInt(CurrentKey, Mathf.Max(0, index));
            PlayerPrefs.Save();
        }

        /// <summary>Best star rating earned on a level: 0 (not won yet) to 3.</summary>
        public static int GetStars(int index) => PlayerPrefs.GetInt(StarsKeyPrefix + index, 0);

        /// <summary>Keeps the better of the stored and the new rating.</summary>
        public static void RecordStars(int index, int stars)
        {
            if (stars <= GetStars(index)) return;
            PlayerPrefs.SetInt(StarsKeyPrefix + index, Mathf.Clamp(stars, 0, 3));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Stars for a win, rewarding efficiency: the level ends as soon as the target is reached, so the moves
        /// still left say how well it was played. 40%+ of the moves left: 3 stars, 20%+: 2, otherwise 1.
        /// </summary>
        public static int StarsFor(int movesLeft, int moveLimit)
        {
            if (moveLimit <= 0) return 1;
            float left = (float)movesLeft / moveLimit;
            return left >= 0.4f ? 3 : left >= 0.2f ? 2 : 1;
        }

        /// <summary>Starts over from level 1 and forgets progress and stars.</summary>
        public static void Reset(int levelCount)
        {
            PlayerPrefs.DeleteKey(CurrentKey);
            PlayerPrefs.DeleteKey(HighestKey);
            for (int i = 0; i < levelCount; i++) PlayerPrefs.DeleteKey(StarsKeyPrefix + i);
            PlayerPrefs.Save();
        }
    }
}
