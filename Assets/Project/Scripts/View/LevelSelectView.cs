using BoardGame.Levels;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Level select screen: one button per catalog level, cloned from a template, showing stars and locks.
    /// Picking a level selects it in <see cref="LevelProgress"/> and loads the game scene.
    /// </summary>
    public sealed class LevelSelectView : MonoBehaviour
    {
        [SerializeField] private LevelCatalog _catalog;
        [Tooltip("Inactive template inside the grid; cloned once per level.")]
        [SerializeField] private LevelButtonView _buttonTemplate;
        [Tooltip("Scene that hosts the board.")]
        [SerializeField] private string _gameScene = "SampleScene";
        [Tooltip("Optional: wipes progress and stars (handy while testing).")]
        [SerializeField] private Button _resetButton;

        private LevelButtonView[] _buttons;

        private void Start()
        {
            if (_catalog == null || _buttonTemplate == null)
            {
                Debug.LogError($"{nameof(LevelSelectView)} needs a catalog and a button template.", this);
                enabled = false;
                return;
            }

            _buttonTemplate.gameObject.SetActive(false);
            Transform grid = _buttonTemplate.transform.parent;
            _buttons = new LevelButtonView[_catalog.Count];
            for (int i = 0; i < _buttons.Length; i++)
            {
                LevelButtonView button = Instantiate(_buttonTemplate, grid);
                button.name = "Level" + (i + 1);
                button.gameObject.SetActive(true);
                int index = i; // captured per button; setup-time only
                button.Button.onClick.AddListener(() => Play(index));
                _buttons[i] = button;
            }

            if (_resetButton != null) _resetButton.onClick.AddListener(ResetProgress);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].Show(i + 1, LevelProgress.GetStars(i), LevelProgress.IsUnlocked(i));
            }
        }

        private void Play(int index)
        {
            LevelProgress.Select(index);
            SceneManager.LoadScene(_gameScene);
        }

        private void ResetProgress()
        {
            LevelProgress.Reset(_catalog.Count);
            Refresh();
        }
    }
}
