using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>One level on the level select grid: its number, the stars earned, and whether it is locked.</summary>
    public sealed class LevelButtonView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _number;
        [SerializeField] private Image[] _stars = new Image[3];
        [SerializeField] private Color _starEarned = new Color(1f, 0.82f, 0.2f);
        [SerializeField] private Color _starEmpty = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField, Range(0f, 1f)] private float _lockedAlpha = 0.35f;

        public Button Button => _button;

        public void Show(int levelNumber, int stars, bool unlocked)
        {
            _number.SetText("{0}", (float)levelNumber);
            _button.interactable = unlocked;

            Color numberColor = _number.color;
            numberColor.a = unlocked ? 1f : _lockedAlpha;
            _number.color = numberColor;

            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].enabled = unlocked;
                _stars[i].color = i < stars ? _starEarned : _starEmpty;
            }
        }
    }
}
