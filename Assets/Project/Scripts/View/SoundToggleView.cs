using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>A button that turns all sound on or off and shows the current state.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SoundToggleView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _background;
        [SerializeField] private Color _onColor = new Color(0.18f, 0.80f, 0.44f);
        [SerializeField] private Color _offColor = new Color(0.32f, 0.34f, 0.44f);

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Toggle);
        }

        // OnEnable, so a toggle in one place (e.g. the pause panel) shows correctly wherever it appears next.
        private void OnEnable() => Refresh();

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(Toggle);
        }

        private void Toggle()
        {
            SoundSettings.Toggle();
            Refresh();
        }

        private void Refresh()
        {
            bool on = SoundSettings.IsOn;
            if (_label != null) _label.SetText(on ? "Sound On" : "Sound Off");
            if (_background != null) _background.color = on ? _onColor : _offColor;
        }
    }
}
