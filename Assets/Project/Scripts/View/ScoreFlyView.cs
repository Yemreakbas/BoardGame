using System;
using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Glowing score orbs that fly from cleared pieces into the score bar. Each orb carries part of a wave's
    /// points; the HUD counts a point only once its orb arrives (<see cref="PendingPoints"/> are still in
    /// flight), so the score visibly fills up as they land. Pooled; launching and flying never allocate.
    /// </summary>
    public sealed class ScoreFlyView : MonoBehaviour
    {
        private const int PoolSize = 48;

        [Tooltip("Where orbs fly to: the score bar (a UI element on a camera-space canvas).")]
        [SerializeField] private RectTransform _target;
        [SerializeField] private Sprite _orbSprite;
        [Tooltip("Additive glow material, so the orbs bloom.")]
        [SerializeField] private Material _orbMaterial;
        [SerializeField, Min(0.05f)] private float _flightTime = 0.6f;
        [SerializeField, Min(0.05f)] private float _orbSize = 0.55f;
        [Tooltip("Optional: sparkles where an orb lands.")]
        [SerializeField] private EffectsView _effects;

        /// <summary>Points carried by orbs that have not landed yet.</summary>
        public int PendingPoints { get; private set; }

        /// <summary>An orb landed in the bar.</summary>
        public event Action Arrived;

        private SpriteRenderer[] _orbs;
        private Vector3[] _from;
        private Vector3[] _bend;       // control point of each orb's curve
        private float[] _time;         // negative while waiting out its launch delay; Unused when free
        private float[] _duration;
        private int[] _points;
        private Color[] _color;

        private const float Unused = -10f;

        private void Awake()
        {
            _orbs = new SpriteRenderer[PoolSize];
            _from = new Vector3[PoolSize];
            _bend = new Vector3[PoolSize];
            _time = new float[PoolSize];
            _duration = new float[PoolSize];
            _points = new int[PoolSize];
            _color = new Color[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Orb" + i);
                go.transform.SetParent(transform, false);
                var orb = go.AddComponent<SpriteRenderer>();
                orb.sprite = _orbSprite;
                if (_orbMaterial != null) orb.sharedMaterial = _orbMaterial;
                orb.sortingOrder = 25;
                orb.enabled = false;
                _orbs[i] = orb;
                _time[i] = Unused;
            }
        }

        /// <summary>Sends an orb worth <paramref name="points"/> from <paramref name="worldFrom"/> to the bar.</summary>
        public void Launch(Vector3 worldFrom, Color color, int points, float delay)
        {
            int slot = -1;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_time[i] <= Unused) { slot = i; break; }
            }
            if (slot < 0 || _target == null) return; // no free orb: the points simply show up without one

            _from[slot] = worldFrom;
            // Bend the path sideways and up, so orbs arc instead of flying straight.
            Vector3 to = TargetPosition();
            Vector3 mid = (worldFrom + to) * 0.5f;
            _bend[slot] = mid + new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), UnityEngine.Random.Range(0.5f, 2f), 0f);
            _time[slot] = -delay;
            _duration[slot] = _flightTime * UnityEngine.Random.Range(0.85f, 1.15f);
            _points[slot] = points;
            _color[slot] = color;
            PendingPoints += points;
        }

        private Vector3 TargetPosition()
        {
            Vector3 p = _target.position;
            p.z = 0f; // same screen spot on the board plane (orthographic camera)
            return p;
        }

        private void Update()
        {
            Vector3 to = _target != null ? TargetPosition() : Vector3.zero;
            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_time[i] <= Unused) continue;
                _time[i] += dt;
                SpriteRenderer orb = _orbs[i];
                if (_time[i] < 0f) continue; // still waiting to launch

                float t = _time[i] / _duration[i];
                if (t >= 1f)
                {
                    orb.enabled = false;
                    _time[i] = Unused;
                    PendingPoints -= _points[i];
                    if (_effects != null) _effects.Sparkle(to, _color[i], 2);
                    Arrived?.Invoke();
                    continue;
                }

                // Quadratic Bezier, accelerating into the bar.
                float e = t * t;
                Vector3 a = Vector3.Lerp(_from[i], _bend[i], e);
                Vector3 b = Vector3.Lerp(_bend[i], to, e);
                orb.transform.position = Vector3.Lerp(a, b, e);
                float pop = t < 0.15f ? t / 0.15f : 1f;
                orb.transform.localScale = Vector3.one * (_orbSize * pop * Mathf.Lerp(1.2f, 0.6f, t));
                orb.color = _color[i];
                orb.enabled = true;
            }
        }
    }
}
