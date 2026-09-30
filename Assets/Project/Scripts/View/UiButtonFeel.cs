using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>Squashes a button while pressed, springs it back on release, and clicks when it fires.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.7f, 1f)] private float _pressedScale = 0.92f;

        private Transform _transform;
        private Button _button;
        private float _target = 1f;

        private void Awake()
        {
            _transform = transform;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(UiSounds.Click);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(UiSounds.Click);
        }

        private void OnDisable()
        {
            _target = 1f;
            _transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button.interactable) _target = _pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData) => _target = 1f;
        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        private void Update()
        {
            float current = _transform.localScale.x;
            if (Mathf.Approximately(current, _target)) return;
            float next = Mathf.MoveTowards(current, _target, Time.unscaledDeltaTime * 2.5f);
            _transform.localScale = new Vector3(next, next, 1f);
        }
    }
}
