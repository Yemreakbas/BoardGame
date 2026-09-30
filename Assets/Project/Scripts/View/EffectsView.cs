using TMPro;
using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Juice for board events: particle bursts, rocket beams, a bomb flash and synthesized sounds.
    /// Everything (beam and flash sprites, audio clips) is built in Awake; playing an effect never allocates.
    /// </summary>
    public sealed class EffectsView : MonoBehaviour
    {
        private const int BeamCount = 8;
        private const int SampleRate = 44100;

        [SerializeField] private ParticleSystem _particles;
        [Tooltip("Sprite for rocket beams and the bomb flash (a plain white shape).")]
        [SerializeField] private Sprite _sprite;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.5f;

        [Header("Particles")]
        [SerializeField, Min(0)] private int _burstCount = 6;
        [SerializeField, Min(0f)] private float _burstSpeed = 3.5f;

        [Header("Beams")]
        [SerializeField, Min(0.01f)] private float _beamDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float _beamThickness = 0.55f;
        [SerializeField, Min(0.01f)] private float _flashDuration = 0.35f;

        private SpriteRenderer[] _beams;
        private float[] _beamTime;          // seconds since the beam started; negative when unused
        private bool[] _beamIsRow;
        private float[] _beamLength;
        private SpriteRenderer _flash;
        private float _flashTime = -1f;
        private ParticleSystem.EmitParams _emit;

        private AudioSource _sfx;           // fixed pitch
        private AudioSource _popSource;     // pitched up with the combo
        private AudioSource _starSource;    // pitched up per star
        private AudioClip _popClip, _swapClip, _invalidClip, _rocketClip, _bombClip, _winClip, _loseClip;
        private AudioClip _specialClip, _unlockClip, _iceClip, _starClip;
        private int _iceSoundFrame = -1, _unlockSoundFrame = -1; // one of each per frame, however many cells

        [Header("Glow")]
        [Tooltip("Additive HDR material (BoardGame/AdditiveGlow) for beams, flash and shockwave rings, so Bloom lights them up.")]
        [SerializeField] private Material _glowMaterial;
        [SerializeField] private Sprite _ringSprite;
        [Tooltip("Optional second particle system for star-shaped sparkles (give it a glowing star material).")]
        [SerializeField] private ParticleSystem _sparkles;
        private const int RingCount = 10;
        private SpriteRenderer[] _rings;
        private float[] _ringTime;
        private float[] _ringDuration;
        private float[] _ringSize;
        private Color[] _ringColor;
        private int _nextRing;

        [Header("Screen shake")]
        [Tooltip("Camera to shake; falls back to Camera.main.")]
        [SerializeField] private Camera _camera;
        private Transform _cameraTransform;
        private Vector3 _cameraRest;
        private float _shakeTime = -1f, _shakeDuration, _shakeStrength;

        [Header("Score popups")]
        [SerializeField, Min(0.1f)] private float _popupDuration = 0.85f;
        [SerializeField] private float _popupRise = 1.1f;
        [SerializeField] private float _popupSize = 7f;
        private const int PopupCount = 10;
        private TextMeshPro[] _popups;
        private float[] _popupTime;
        private Vector3[] _popupStart;
        private int _nextPopup;
        private static readonly Color[] ComboColors =
        {
            Color.white, new Color(1f, 0.9f, 0.3f), new Color(1f, 0.62f, 0.2f), new Color(1f, 0.4f, 0.55f), new Color(0.75f, 0.5f, 1f),
        };
        private static readonly Color[] ConfettiColors =
        {
            new Color(0.91f, 0.30f, 0.24f), new Color(0.95f, 0.77f, 0.06f), new Color(0.18f, 0.80f, 0.44f),
            new Color(0.20f, 0.60f, 0.86f), new Color(0.61f, 0.35f, 0.71f), new Color(0.98f, 0.55f, 0.16f),
        };

        private void Awake()
        {
            _beams = new SpriteRenderer[BeamCount];
            _beamTime = new float[BeamCount];
            _beamIsRow = new bool[BeamCount];
            _beamLength = new float[BeamCount];
            for (int i = 0; i < BeamCount; i++)
            {
                _beams[i] = CreateSprite("Beam" + i, 20);
                _beamTime[i] = -1f;
            }
            _flash = CreateSprite("Flash", 19);

            if (_particles != null)
            {
                _particles.Play(); // emits nothing by itself; bursts are pushed through Emit
                _emit.applyShapeToPosition = false;
            }

            _sfx = gameObject.AddComponent<AudioSource>();
            _popSource = gameObject.AddComponent<AudioSource>();
            _starSource = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = _popSource.playOnAwake = _starSource.playOnAwake = false;
            BuildClips();

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _cameraTransform = _camera.transform;

            _rings = new SpriteRenderer[RingCount];
            _ringTime = new float[RingCount];
            _ringDuration = new float[RingCount];
            _ringSize = new float[RingCount];
            _ringColor = new Color[RingCount];
            for (int i = 0; i < RingCount; i++)
            {
                _rings[i] = CreateSprite("Ring" + i, 18);
                _rings[i].sprite = _ringSprite;
                _ringTime[i] = -1f;
            }
            if (_sparkles != null) _sparkles.Play();

            _popups = new TextMeshPro[PopupCount];
            _popupTime = new float[PopupCount];
            _popupStart = new Vector3[PopupCount];
            for (int i = 0; i < PopupCount; i++)
            {
                var go = new GameObject("ScorePopup" + i);
                go.transform.SetParent(transform, false);
                var text = go.AddComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = _popupSize;
                text.fontStyle = FontStyles.Bold;
                text.outlineWidth = 0.22f;
                text.outlineColor = new Color32(20, 18, 34, 255);
                text.sortingOrder = 30;
                text.enabled = false;
                _popups[i] = text;
                _popupTime[i] = -1f;
            }
        }

        // ------------------------------------------------------------------ juice

        /// <summary>Shakes the camera; a stronger shake overrides a weaker one still running.</summary>
        public void Shake(float strength, float duration)
        {
            if (_cameraTransform == null) return;
            if (_shakeTime >= 0f && strength < _shakeStrength * (1f - _shakeTime / _shakeDuration)) return;
            if (_shakeTime < 0f) _cameraRest = _cameraTransform.position;
            _shakeTime = 0f;
            _shakeDuration = duration;
            _shakeStrength = strength;
        }

        /// <summary>A "+points" label that pops up at <paramref name="worldPosition"/>, rises and fades.</summary>
        public void ScorePopup(Vector3 worldPosition, int points, int combo)
        {
            int slot = _nextPopup;
            _nextPopup = (_nextPopup + 1) % PopupCount;

            TextMeshPro text = _popups[slot];
            text.SetText("+{0}", (float)points);
            text.color = ComboColors[Mathf.Clamp(combo - 1, 0, ComboColors.Length - 1)];
            text.fontSize = _popupSize * (1f + 0.12f * Mathf.Clamp(combo - 1, 0, 5));
            text.enabled = true;
            _popupStart[slot] = worldPosition + new Vector3(0f, 0f, -1f);
            _popupTime[slot] = 0f;
            UpdatePopup(slot, 0f);
        }

        /// <summary>A shower of colored confetti from above <paramref name="center"/>.</summary>
        public void Confetti(Vector3 center, float width, int count = 90)
        {
            if (_particles == null) return;
            for (int i = 0; i < count; i++)
            {
                _emit.position = center + new Vector3(Random.Range(-0.5f, 0.5f) * width, Random.Range(0f, 1.5f), 0f);
                _emit.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 8f), 0f);
                _emit.startColor = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                _emit.startSize = Random.Range(0.18f, 0.32f);
                _emit.startLifetime = Random.Range(1.2f, 2f);
                _particles.Emit(_emit, 1);
            }
        }

        /// <summary>A glowing ring that bursts out from <paramref name="worldPosition"/> to <paramref name="size"/> units and fades.</summary>
        public void Shockwave(Vector3 worldPosition, Color color, float size, float duration = 0.4f)
        {
            if (_ringSprite == null) return;
            int slot = _nextRing;
            _nextRing = (_nextRing + 1) % RingCount;
            _rings[slot].transform.position = worldPosition;
            _rings[slot].enabled = true;
            _ringTime[slot] = 0f;
            _ringDuration[slot] = duration;
            _ringSize[slot] = size;
            _ringColor[slot] = color;
            UpdateRing(slot, 0f);
        }

        /// <summary>Star-shaped sparkles flying out of <paramref name="worldPosition"/>.</summary>
        public void Sparkle(Vector3 worldPosition, Color color, int count)
        {
            if (_sparkles == null) return;
            _emit.position = worldPosition;
            _emit.startColor = color;
            for (int i = 0; i < count; i++)
            {
                _emit.velocity = Random.insideUnitCircle.normalized * Random.Range(2f, 6f);
                _emit.startSize = Random.Range(0.35f, 0.65f);
                _emit.startLifetime = Random.Range(0.45f, 0.85f);
                _emit.rotation = Random.Range(0f, 360f);
                _emit.angularVelocity = Random.Range(-360f, 360f);
                _sparkles.Emit(_emit, 1);
            }
            _emit.rotation = 0f;
            _emit.angularVelocity = 0f;
        }

        // Fast ease-out growth; bright at first, fading to nothing.
        private void UpdateRing(int i, float t)
        {
            float grow = 1f - (1f - t) * (1f - t) * (1f - t);
            _rings[i].transform.localScale = Vector3.one * Mathf.Lerp(0.15f, _ringSize[i], grow);
            Color color = _ringColor[i];
            color.a *= 1f - t;
            _rings[i].color = color;
        }

        /// <summary>Confetti across the top of the view, for a win.</summary>
        public void Celebrate()
        {
            if (_camera == null) return;
            float halfHeight = _camera.orthographicSize;
            Vector3 top = _cameraRestOrCurrent() + new Vector3(0f, halfHeight * 0.85f, 0f);
            top.z = 0f;
            Confetti(top, 2f * halfHeight * _camera.aspect, 140);
        }

        private Vector3 _cameraRestOrCurrent() => _shakeTime >= 0f ? _cameraRest : _cameraTransform.position;

        private void UpdatePopup(int i, float t)
        {
            TextMeshPro text = _popups[i];
            float rise = 1f - (1f - t) * (1f - t); // ease out
            text.transform.position = _popupStart[i] + new Vector3(0f, _popupRise * rise, 0f);
            float scale = t < 0.15f ? Mathf.Lerp(0.3f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Min(1f, (t - 0.15f) / 0.15f));
            text.transform.localScale = Vector3.one * scale;
            Color color = text.color;
            color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            text.color = color;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (_shakeTime >= 0f && _cameraTransform != null)
            {
                _shakeTime += dt;
                float t = _shakeTime / _shakeDuration;
                if (t >= 1f)
                {
                    _shakeTime = -1f;
                    _cameraTransform.position = _cameraRest;
                }
                else
                {
                    float falloff = (1f - t) * (1f - t);
                    Vector2 offset = Random.insideUnitCircle * (_shakeStrength * falloff);
                    _cameraTransform.position = _cameraRest + new Vector3(offset.x, offset.y, 0f);
                }
            }

            for (int i = 0; i < RingCount; i++)
            {
                if (_ringTime[i] < 0f) continue;
                _ringTime[i] += Time.deltaTime;
                float t = _ringTime[i] / _ringDuration[i];
                if (t >= 1f)
                {
                    _ringTime[i] = -1f;
                    _rings[i].enabled = false;
                    continue;
                }
                UpdateRing(i, t);
            }

            for (int i = 0; i < PopupCount; i++)
            {
                if (_popupTime[i] < 0f) continue;
                _popupTime[i] += Time.deltaTime;
                float t = _popupTime[i] / _popupDuration;
                if (t >= 1f)
                {
                    _popupTime[i] = -1f;
                    _popups[i].enabled = false;
                    continue;
                }
                UpdatePopup(i, t);
            }
        }

        // ------------------------------------------------------------------ visual effects

        /// <summary>Sprays a few particles of <paramref name="color"/> from a cleared piece.</summary>
        public void Burst(Vector3 worldPosition, Color color, int count = -1)
        {
            if (_particles == null) return;
            if (count < 0) count = _burstCount;

            _emit.position = worldPosition;
            _emit.startColor = color;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                _emit.velocity = direction * (_burstSpeed * Random.Range(0.5f, 1f)) + Vector2.up * 1.5f;
                _emit.startSize = Random.Range(0.3f, 0.5f); // soft glow dots read smaller than their size
                _emit.startLifetime = Random.Range(0.35f, 0.6f);
                _particles.Emit(_emit, 1);
            }
        }

        /// <summary>A bright beam across a whole row or column, centred on <paramref name="worldCenter"/>.</summary>
        public void Beam(Vector3 worldCenter, bool isRow, float worldLength, Color color)
        {
            int slot = 0;
            for (int i = 0; i < BeamCount; i++)
            {
                if (_beamTime[i] < 0f) { slot = i; break; }
                if (_beamTime[i] > _beamTime[slot]) slot = i; // all busy: recycle the oldest
            }

            SpriteRenderer beam = _beams[slot];
            beam.transform.position = worldCenter;
            beam.color = Color.Lerp(color, Color.white, 0.6f);
            beam.enabled = true;
            _beamTime[slot] = 0f;
            _beamIsRow[slot] = isRow;
            _beamLength[slot] = worldLength;
            UpdateBeam(slot, 0f);
            Play(_sfx, _rocketClip, 0.8f);
        }

        /// <summary>A white flash over the board and a big burst at the bomb.</summary>
        public void Bomb(Vector3 worldPosition, Vector3 boardCenter, float boardSize)
        {
            _flash.transform.position = boardCenter;
            _flash.transform.localScale = Vector3.one * boardSize;
            _flash.enabled = true;
            _flashTime = 0f;
            Burst(worldPosition, Color.white, _burstCount * 4);
            Play(_sfx, _bombClip, 1f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < BeamCount; i++)
            {
                if (_beamTime[i] < 0f) continue;
                _beamTime[i] += dt;
                float t = _beamTime[i] / _beamDuration;
                if (t >= 1f)
                {
                    _beamTime[i] = -1f;
                    _beams[i].enabled = false;
                    continue;
                }
                UpdateBeam(i, t);
            }

            if (_flashTime >= 0f)
            {
                _flashTime += dt;
                float t = _flashTime / _flashDuration;
                if (t >= 1f)
                {
                    _flashTime = -1f;
                    _flash.enabled = false;
                }
                else
                {
                    _flash.color = new Color(1f, 1f, 1f, 0.45f * (1f - t));
                }
            }
        }

        // Starts full length and thick, then thins out while fading.
        private void UpdateBeam(int i, float t)
        {
            float thickness = _beamThickness * (1f - t);
            Vector3 scale = _beamIsRow[i] ? new Vector3(_beamLength[i], thickness, 1f) : new Vector3(thickness, _beamLength[i], 1f);
            _beams[i].transform.localScale = scale;
            Color color = _beams[i].color;
            color.a = 1f - t * t;
            _beams[i].color = color;
        }

        private SpriteRenderer CreateSprite(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = sortingOrder;
            if (_glowMaterial != null) renderer.sharedMaterial = _glowMaterial;
            renderer.enabled = false;
            return renderer;
        }

        // ------------------------------------------------------------------ sound

        /// <summary>One pop per cleared wave; the pitch climbs with the cascade combo.</summary>
        public void PlayPop(int combo)
        {
            _popSource.pitch = 1f + 0.08f * Mathf.Clamp(combo - 1, 0, 10);
            Play(_popSource, _popClip, 0.9f);
        }

        public void PlaySpecialCreated() => Play(_sfx, _specialClip, 0.8f);

        public void PlayUnlock()
        {
            if (_unlockSoundFrame == Time.frameCount) return;
            _unlockSoundFrame = Time.frameCount;
            Play(_sfx, _unlockClip, 0.8f);
        }

        public void PlayIce()
        {
            if (_iceSoundFrame == Time.frameCount) return;
            _iceSoundFrame = Time.frameCount;
            Play(_sfx, _iceClip, 0.8f);
        }

        /// <summary>Star reveal on the results card: each earned star rings a step higher.</summary>
        public void PlayStar(int index)
        {
            _starSource.pitch = 1f + 0.26f * index;
            Play(_starSource, _starClip, 0.9f);
        }

        public void PlaySwap() => Play(_sfx, _swapClip, 0.6f);
        public void PlayInvalid() => Play(_sfx, _invalidClip, 0.7f);
        public void PlayOutcome(bool won) => Play(_sfx, won ? _winClip : _loseClip, 1f);

        private void Play(AudioSource source, AudioClip clip, float volume)
        {
            if (source != null && clip != null) source.PlayOneShot(clip, volume * _volume);
        }

        // Tiny synthesizer: every clip is a pitch sweep with an envelope, plus optional noise.
        private void BuildClips()
        {
            var noise = new System.Random(1234);
            _popClip = Synth("Pop", 0.09f, 900f, 480f, 0f, 30f, noise);
            _swapClip = Synth("Swap", 0.05f, 620f, 560f, 0f, 60f, noise);
            _invalidClip = Synth("Invalid", 0.18f, 200f, 150f, 0.1f, 12f, noise, square: true);
            _rocketClip = Synth("Rocket", 0.32f, 1400f, 220f, 0.45f, 8f, noise);
            _bombClip = Synth("Bomb", 0.55f, 130f, 38f, 0.5f, 6f, noise);
            _winClip = Arpeggio("Win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f });
            _loseClip = Arpeggio("Lose", new[] { 392f, 329.63f, 261.63f, 196f });
            _specialClip = Chime("Special", new[] { 1046.5f, 1318.5f, 1568f }, 0.45f);
            _unlockClip = Synth("Unlock", 0.12f, 2400f, 1800f, 0.35f, 24f, noise, square: true);
            _iceClip = Synth("Ice", 0.16f, 3200f, 900f, 0.8f, 20f, noise);
            _starClip = Chime("Star", new[] { 880f, 1760f }, 0.6f);
        }

        // Bell-like: a few sine partials sharing one soft attack and long decay.
        private static AudioClip Chime(string name, float[] partialsHz, float seconds)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 7f) * Mathf.Min(1f, i / (0.003f * SampleRate));
                float sum = 0f;
                for (int p = 0; p < partialsHz.Length; p++) sum += Mathf.Sin(2f * Mathf.PI * partialsHz[p] * t) / (p + 1);
                data[i] = sum * envelope * 0.35f;
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        internal static AudioClip Synth(string name, float seconds, float startHz, float endHz, float noiseAmount,
                                       float decay, System.Random noise, bool square = false)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float hz = Mathf.Lerp(startHz, endHz, t);
                phase += 2f * Mathf.PI * hz / SampleRate;
                float tone = Mathf.Sin(phase);
                if (square) tone = Mathf.Sign(tone) * 0.6f;
                float white = (float)(noise.NextDouble() * 2.0 - 1.0);
                float envelope = Mathf.Exp(-decay * t) * Mathf.Min(1f, i / (0.004f * SampleRate)); // 4 ms attack
                data[i] = (tone * (1f - noiseAmount) + white * noiseAmount) * envelope * 0.8f;
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(string name, float[] notesHz)
        {
            const float noteSeconds = 0.13f;
            int perNote = Mathf.CeilToInt(noteSeconds * SampleRate);
            var data = new float[perNote * notesHz.Length + SampleRate / 5]; // short tail
            for (int n = 0; n < notesHz.Length; n++)
            {
                float length = n == notesHz.Length - 1 ? 0.35f : noteSeconds; // last note rings longer
                int count = Mathf.Min(Mathf.CeilToInt(length * SampleRate), data.Length - n * perNote);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / SampleRate;
                    float envelope = Mathf.Exp(-t * 7f) * Mathf.Min(1f, i / (0.005f * SampleRate));
                    float tone = Mathf.Sin(2f * Mathf.PI * notesHz[n] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notesHz[n] * t);
                    data[n * perNote + i] += tone * envelope * 0.45f;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
