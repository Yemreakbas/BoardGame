using UnityEngine;

namespace BoardGame.View
{
    /// <summary>Menu sounds shared by every scene. The clip and its player are created on first use and kept.</summary>
    public static class UiSounds
    {
        private static AudioSource _source;
        private static AudioClip _click;

        public static void Click()
        {
            if (_source == null)
            {
                var go = new GameObject("UiSounds");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _click = EffectsView.Synth("Click", 0.06f, 1100f, 700f, 0.05f, 45f, new System.Random(7));
            }
            _source.PlayOneShot(_click, 0.35f);
        }
    }
}
