using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Visual stand-in for one piece. <see cref="BoardView"/> creates every instance up front and recycles
    /// them (hidden when matched, shown again for refills), so gameplay never instantiates or destroys.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("Travel speed, in board-local units per second.")]
        [SerializeField, Min(0.01f)] private float _moveSpeed = 10f;

        private Transform _transform;
        private Vector3 _target;

        /// <summary>The component is only enabled while travelling, so idle pieces cost no Update call.</summary>
        public bool IsMoving => enabled;

        private void Awake()
        {
            _transform = transform;
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            enabled = false;
        }

        /// <summary>Activates the view with the given look and places it instantly.</summary>
        public void Show(Color color, Vector3 localPosition)
        {
            gameObject.SetActive(true); // First, so Awake has run even if the prefab was saved inactive.
            _renderer.color = color;
            _transform.localPosition = localPosition;
            _target = localPosition;
            enabled = false;
        }

        /// <summary>Deactivates the view until a refill reuses it.</summary>
        public void Hide()
        {
            enabled = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Movement stub: glides to <paramref name="localTarget"/> at constant speed and can be retargeted
        /// mid-flight. Replace with a tween (easing, squash, sequencing) when polishing.
        /// </summary>
        public void MoveTo(Vector3 localTarget)
        {
            _target = localTarget;
            enabled = true;
        }

        private void Update()
        {
            Vector3 position = Vector3.MoveTowards(_transform.localPosition, _target, _moveSpeed * Time.deltaTime);
            _transform.localPosition = position;
            if (position == _target) enabled = false;
        }
    }
}
