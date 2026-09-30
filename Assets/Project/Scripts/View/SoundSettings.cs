using UnityEngine;

namespace BoardGame.View
{
    /// <summary>Global sound on/off, saved in PlayerPrefs and applied through <see cref="AudioListener.volume"/>.</summary>
    public static class SoundSettings
    {
        private const string Key = "BoardGame.SoundOn";

        public static bool IsOn => PlayerPrefs.GetInt(Key, 1) != 0;

        /// <summary>Applies the saved choice; called once at startup by <see cref="AppSetup"/>.</summary>
        public static void Apply()
        {
            AudioListener.volume = IsOn ? 1f : 0f;
        }

        public static void Toggle()
        {
            PlayerPrefs.SetInt(Key, IsOn ? 0 : 1);
            PlayerPrefs.Save();
            Apply();
        }
    }
}
