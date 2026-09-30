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

        /// <summary>Starts over from level 1 and forgets progress.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(CurrentKey);
            PlayerPrefs.DeleteKey(HighestKey);
            PlayerPrefs.Save();
        }
    }
}
