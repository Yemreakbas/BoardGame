using BoardGame.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Level select screen: one button per catalog level, cloned from a template, tinted per chapter of five,
    /// showing stars and locks. Buttons pop in one after another; the next level to play breathes.
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
        [Tooltip("Optional: total stars earned out of the possible stars.")]
        [SerializeField] private TMP_Text _starTotalText;

        [Header("Look")]
        [Tooltip("Button colors per chapter of five levels; repeats if there are more chapters.")]
        [SerializeField] private Color[] _chapterColors =
        {
            new Color(0.20f, 0.60f, 0.86f), new Color(0.18f, 0.70f, 0.45f),
            new Color(0.58f, 0.36f, 0.78f), new Color(0.95f, 0.52f, 0.20f),
        };
        [SerializeField, Min(1)] private int _levelsPerChapter = 5;
        [SerializeField, Min(0f)] private float _popInterval = 0.035f;
        [SerializeField, Min(0.05f)] private float _popDuration = 0.35f;

        private LevelButtonView[] _buttons;
        private Transform[] _buttonTransforms;
        private int _currentIndex;
        private float _time;

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
            _buttonTransforms = new Transform[_catalog.Count];
            for (int i = 0; i < _buttons.Length; i++)
            {
                LevelButtonView button = Instantiate(_buttonTemplate, grid);
                button.name = "Level" + (i + 1);
                button.gameObject.SetActive(true);
                int index = i; // captured per button; setup-time only
                button.Button.onClick.AddListener(() => Play(index));
                _buttons[i] = button;
                _buttonTransforms[i] = button.transform;
                _buttonTransforms[i].localScale = Vector3.zero; // popped in by Update
            }

            if (_resetButton != null) _resetButton.onClick.AddListener(ResetProgress);
            Refresh();
        }

        private void Refresh()
        {
            int stars = 0;
            for (int i = 0; i < _buttons.Length; i++)
            {
                int earned = LevelProgress.GetStars(i);
                stars += earned;
                Color chapter = _chapterColors[(i / _levelsPerChapter) % _chapterColors.Length];
                _buttons[i].Show(i + 1, earned, LevelProgress.IsUnlocked(i), chapter);
            }
            _currentIndex = Mathf.Clamp(LevelProgress.HighestUnlocked, 0, _buttons.Length - 1);
            if (_starTotalText != null) _starTotalText.SetText("{0} / {1}", (float)stars, (float)(_buttons.Length * 3));
        }

        // Scale is owned here (pop-in, then the breathing current level), so the level buttons do not carry
        // UiButtonFeel, which would fight over it.
        private void Update()
        {
            if (_buttons == null) return;
            _time += Time.deltaTime;

            for (int i = 0; i < _buttonTransforms.Length; i++)
            {
                float t = (_time - i * _popInterval) / _popDuration;
                float scale;
                if (t <= 0f) scale = 0f;
                else if (t < 1f)
                {
                    // Ease-out-back pop-in.
                    const float back = 2.2f;
                    float u = t - 1f;
                    scale = 1f + (back + 1f) * u * u * u + back * u * u;
                }
                else if (i == _currentIndex) scale = 1f + 0.05f * Mathf.Sin((_time - 1f) * 4f); // breathe
                else scale = 1f;
                _buttonTransforms[i].localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void Play(int index)
        {
            UiSounds.Click();
            LevelProgress.Select(index);
            SceneManager.LoadScene(_gameScene);
        }

        private void ResetProgress()
        {
            LevelProgress.Reset(_catalog.Count);
            Refresh();
            _time = 0f; // replay the pop-in so the reset is visible
        }
    }
}
