using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// One level on the level select grid: its number, the stars earned, a lock when it is not reached yet,
    /// and a tint for its chapter. <see cref="LevelSelectView"/> animates its scale.
    /// </summary>
    public sealed class LevelButtonView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _number;
        [SerializeField] private Image[] _stars = new Image[3];
        [Tooltip("Shown instead of the stars while the level is locked.")]
        [SerializeField] private Image _lockIcon;
        [SerializeField] private Color _starEarned = new Color(1f, 0.82f, 0.2f);
        [SerializeField] private Color _starEmpty = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color _lockedTint = new Color(0.22f, 0.23f, 0.32f);

        public Button Button => _button;

        public void Show(int levelNumber, int stars, bool unlocked, Color chapterColor)
        {
            _number.SetText("{0}", (float)levelNumber);
            _button.interactable = unlocked;
            if (_background != null) _background.color = unlocked ? chapterColor : _lockedTint;

            Color numberColor = _number.color;
            numberColor.a = unlocked ? 1f : 0.45f;
            _number.color = numberColor;

            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].enabled = unlocked;
                _stars[i].color = i < stars ? _starEarned : _starEmpty;
            }
            if (_lockIcon != null) _lockIcon.enabled = !unlocked;
        }
    }
}
