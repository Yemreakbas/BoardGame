using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Visual stand-in for one piece. <see cref="BoardView"/> creates every instance up front and recycles
    /// them (popped when matched, shown again for refills), so gameplay never instantiates or destroys.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("Travel speed, in board-local units per second.")]
        [SerializeField, Min(0.01f)] private float _moveSpeed = 10f;
        [Tooltip("Seconds a matched piece takes to shrink away.")]
        [SerializeField, Min(0.01f)] private float _popDuration = 0.15f;
        [Tooltip("Hint pulse: extra scale at the peak, and pulses per second.")]
        [SerializeField, Range(0f, 0.5f)] private float _hintScale = 0.15f;
        [SerializeField, Min(0.1f)] private float _hintFrequency = 1.5f;

        private Transform _transform;
        private Vector3 _target;
        private Vector3 _returnTarget;
        private bool _returnPending;   // MoveToAndBack: head back to _returnTarget on arrival
        private float _popProgress;    // 0..1 while popping, negative otherwise
        private bool _isHinting;
        private float _hintTime;

        /// <summary>
        /// True while moving or popping. The component is only enabled while animating, so idle pieces cost
        /// no Update call; a hint pulse keeps it enabled but does not count as busy.
        /// </summary>
        public bool IsBusy => enabled && !_isHinting;

        private void Awake()
        {
            _transform = transform;
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            _popProgress = -1f;
            enabled = false;
        }

        /// <summary>Activates the view with the given look and places it instantly.</summary>
        public void Show(Color color, Vector3 localPosition)
        {
            gameObject.SetActive(true); // First, so Awake has run even if the prefab was saved inactive.
            _renderer.color = color;
            _transform.localPosition = localPosition;
            _transform.localScale = Vector3.one;
            _target = localPosition;
            _returnPending = false;
            _popProgress = -1f;
            _isHinting = false;
            enabled = false;
        }

        /// <summary>Changes the look without moving the view.</summary>
        public void SetColor(Color color) => _renderer.color = color;

        /// <summary>Starts pulsing to point out a possible move. Only call on an idle piece.</summary>
        public void StartHint()
        {
            _isHinting = true;
            _hintTime = 0f;
            enabled = true;
        }

        /// <summary>Stops the hint pulse and restores the normal size. Safe to call on any piece.</summary>
        public void StopHint()
        {
            if (!_isHinting) return;
            _isHinting = false;
            _transform.localScale = Vector3.one;
            enabled = false;
        }

        /// <summary>Shrinks the view away, then deactivates it until a refill reuses it.</summary>
        public void Pop()
        {
            StopHint();
            _returnPending = false;
            _target = _transform.localPosition;
            _popProgress = 0f;
            enabled = true;
        }

        /// <summary>
        /// Movement stub: glides to <paramref name="localTarget"/> at constant speed and can be retargeted
        /// mid-flight. Replace with a tween (easing, squash, sequencing) when polishing.
        /// </summary>
        public void MoveTo(Vector3 localTarget)
        {
            StopHint();
            _target = localTarget;
            _returnPending = false;
            enabled = true;
        }

        /// <summary>Glides to <paramref name="localTarget"/> and straight back: the rejected-swap bounce.</summary>
        public void MoveToAndBack(Vector3 localTarget)
        {
            StopHint();
            _returnTarget = _transform.localPosition;
            _target = localTarget;
            _returnPending = true;
            enabled = true;
        }

        private void Update()
        {
            if (_isHinting)
            {
                _hintTime += Time.deltaTime;
                float wave = 0.5f - 0.5f * Mathf.Cos(_hintTime * _hintFrequency * 2f * Mathf.PI); // 0..1, starts at 0
                _transform.localScale = Vector3.one * (1f + _hintScale * wave);
                return;
            }

            if (_popProgress >= 0f)
            {
                _popProgress += Time.deltaTime / _popDuration;
                if (_popProgress < 1f)
                {
                    _transform.localScale = Vector3.one * (1f - _popProgress);
                    return;
                }

                _popProgress = -1f;
                _transform.localScale = Vector3.one;
                enabled = false;
                gameObject.SetActive(false);
                return;
            }

            Vector3 position = Vector3.MoveTowards(_transform.localPosition, _target, _moveSpeed * Time.deltaTime);
            _transform.localPosition = position;
            if (position != _target) return;

            if (_returnPending)
            {
                _returnPending = false;
                _target = _returnTarget;
                return;
            }
            enabled = false;
        }
    }
}
