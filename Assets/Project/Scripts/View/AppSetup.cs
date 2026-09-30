using UnityEngine;

namespace BoardGame.View
{
    /// <summary>App-wide runtime settings, applied once before the first scene loads. Needs no scene object.</summary>
    public static class AppSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // Mobile defaults to 30 fps, which makes swipes and falls look choppy.
            Application.targetFrameRate = 60;
            SoundSettings.Apply();
        }
    }
}
