using BoardGame.Core.Logic;
using UnityEngine;

namespace BoardGame.View
{
    /// <summary>How a move animates: swaps ease in and out, falls accelerate and land with a squash.</summary>
    public enum MoveStyle
    {
        Swap,
        Fall,
    }

    /// <summary>
    /// Visual stand-in for one piece. <see cref="BoardView"/> creates every instance up front and recycles
    /// them (popped when matched, shown again for refills), so gameplay never instantiates or destroys.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("Overlay drawn on specials: a stripe along a rocket's line, a square on a color bomb.")]
        [SerializeField] private SpriteRenderer _marker;
        [Tooltip("Overlay drawn while the piece is locked.")]
        [SerializeField] private SpriteRenderer _lockOverlay;

        [Header("Motion")]
        [Tooltip("Seconds for a one-cell swap; longer trips scale with the square root of the distance.")]
        [SerializeField, Min(0.01f)] private float _swapDuration = 0.14f;
        [Tooltip("Seconds to fall one cell; longer falls scale with the square root of the distance, like gravity.")]
        [SerializeField, Min(0.01f)] private float _fallDuration = 0.12f;
        [Tooltip("Landing squash after a fall: seconds and strength.")]
        [SerializeField, Min(0.01f)] private float _landDuration = 0.12f;
        [SerializeField, Range(0f, 0.5f)] private float _landSquash = 0.18f;

        [Header("Pop")]
        [Tooltip("Seconds a matched piece takes to swell and shrink away.")]
        [SerializeField, Min(0.01f)] private float _popDuration = 0.18f;
        [SerializeField, Range(0f, 0.5f)] private float _popSwell = 0.2f;

        [Header("Hint")]
        [Tooltip("Hint pulse: extra scale at the peak, and pulses per second.")]
        [SerializeField, Range(0f, 0.5f)] private float _hintScale = 0.15f;
        [SerializeField, Min(0.1f)] private float _hintFrequency = 1.5f;

        private enum Phase
        {
            Idle,
            Moving,
            Landing,
            Popping,
            Hinting,
        }

        private Transform _transform;
        private Phase _phase;
        private float _time;           // seconds into the current phase
        private float _duration;       // length of the current move
        private MoveStyle _style;
        private Vector3 _from;
        private Vector3 _target;
        private Vector3 _returnTarget;
        private bool _returnPending;   // MoveToAndBack: head back to _returnTarget on arrival

        /// <summary>The special this view currently shows, so effects can react when it is cleared.</summary>
        public SpecialKind Special { get; private set; }

        /// <summary>Body color, e.g. to tint the particles of its pop.</summary>
        public Color Color => _renderer.color;

        /// <summary>
        /// True while moving, landing or popping. The component is only enabled while animating, so idle pieces
        /// cost no Update call; a hint pulse keeps it enabled but does not count as busy.
        /// </summary>
        public bool IsBusy => enabled && _phase != Phase.Hinting;

        private void Awake()
        {
            _transform = transform;
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            SetIdle();
        }

        /// <summary>Activates the view with the given look and places it instantly.</summary>
        public void Show(Color color, SpecialKind special, Vector3 localPosition)
        {
            gameObject.SetActive(true); // First, so Awake has run even if the prefab was saved inactive.
            SetLook(color, special);
            SetLocked(false); // refills are never locked; level setup locks pieces afterwards
            _transform.localPosition = localPosition;
            _target = localPosition;
            _returnPending = false;
            SetIdle();
        }

        /// <summary>Changes the look without moving the view.</summary>
        public void SetLook(Color color, SpecialKind special)
        {
            Special = special;
            _renderer.color = color;
            if (_marker == null) return;

            _marker.enabled = special != SpecialKind.None;
            switch (special)
            {
                case SpecialKind.RowRocket:
                    _marker.transform.localScale = new Vector3(0.8f, 0.22f, 1f);
                    break;
                case SpecialKind.ColumnRocket:
                    _marker.transform.localScale = new Vector3(0.22f, 0.8f, 1f);
                    break;
                case SpecialKind.ColorBomb:
                    _marker.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                    break;
            }
        }

        /// <summary>Shows or hides the lock overlay.</summary>
        public void SetLocked(bool locked)
        {
            if (_lockOverlay != null) _lockOverlay.enabled = locked;
        }

        /// <summary>Starts pulsing to point out a possible move. Only call on an idle piece.</summary>
        public void StartHint()
        {
            _phase = Phase.Hinting;
            _time = 0f;
            enabled = true;
        }

        /// <summary>Stops the hint pulse and restores the normal size. Safe to call on any piece.</summary>
        public void StopHint()
        {
            if (_phase == Phase.Hinting) SetIdle();
        }

        /// <summary>Swells, shrinks away, then deactivates the view until a refill reuses it.</summary>
        public void Pop()
        {
            _returnPending = false;
            _transform.localScale = Vector3.one;
            _phase = Phase.Popping;
            _time = 0f;
            enabled = true;
        }

        /// <summary>
        /// Glides to <paramref name="localTarget"/>. Can be retargeted mid-flight: the new move starts from
        /// wherever the view is.
        /// </summary>
        public void MoveTo(Vector3 localTarget, MoveStyle style = MoveStyle.Swap)
        {
            _returnPending = false;
            BeginMove(localTarget, style);
        }

        /// <summary>Glides to <paramref name="localTarget"/> and straight back: the rejected-swap bounce.</summary>
        public void MoveToAndBack(Vector3 localTarget)
        {
            _returnTarget = _transform.localPosition;
            _returnPending = true;
            BeginMove(localTarget, MoveStyle.Swap);
        }

        private void BeginMove(Vector3 localTarget, MoveStyle style)
        {
            _transform.localScale = Vector3.one; // cancels a hint pulse or landing squash in progress
            _from = _transform.localPosition;
            _target = localTarget;
            _style = style;

            // Board-local units: one cell is roughly one unit, so sqrt(distance) keeps long trips snappy.
            float distance = Mathf.Max(Vector3.Distance(_from, _target), 0.0001f);
            _duration = (style == MoveStyle.Fall ? _fallDuration : _swapDuration) * Mathf.Sqrt(distance);
            _phase = Phase.Moving;
            _time = 0f;
            enabled = true;
        }

        private void SetIdle()
        {
            _phase = Phase.Idle;
            if (_transform != null) _transform.localScale = Vector3.one;
            enabled = false;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Hinting:
                {
                    float wave = 0.5f - 0.5f * Mathf.Cos(_time * _hintFrequency * 2f * Mathf.PI); // 0..1, starts at 0
                    _transform.localScale = Vector3.one * (1f + _hintScale * wave);
                    break;
                }
                case Phase.Moving:
                {
                    float t = Mathf.Clamp01(_time / _duration);
                    float eased = _style == MoveStyle.Fall ? t * t : t * t * (3f - 2f * t); // gravity / smoothstep
                    _transform.localPosition = Vector3.LerpUnclamped(_from, _target, eased);
                    if (t < 1f) break;

                    if (_returnPending)
                    {
                        _returnPending = false;
                        BeginMove(_returnTarget, MoveStyle.Swap);
                    }
                    else if (_style == MoveStyle.Fall)
                    {
                        _phase = Phase.Landing;
                        _time = 0f;
                    }
                    else
                    {
                        SetIdle();
                    }
                    break;
                }
                case Phase.Landing:
                {
                    float t = _time / _landDuration;
                    if (t >= 1f)
                    {
                        SetIdle();
                        break;
                    }
                    float squash = _landSquash * Mathf.Sin(t * Mathf.PI);
                    _transform.localScale = new Vector3(1f + squash * 0.7f, 1f - squash, 1f);
                    break;
                }
                case Phase.Popping:
                {
                    float t = _time / _popDuration;
                    if (t >= 1f)
                    {
                        SetIdle();
                        gameObject.SetActive(false);
                        break;
                    }
                    // Swell to 1 + _popSwell over the first 30%, then shrink to nothing.
                    float scale = t < 0.3f ? 1f + _popSwell * (t / 0.3f) : (1f + _popSwell) * (1f - (t - 0.3f) / 0.7f);
                    _transform.localScale = Vector3.one * scale;
                    break;
                }
                default:
                    enabled = false;
                    break;
            }
        }
    }
}
