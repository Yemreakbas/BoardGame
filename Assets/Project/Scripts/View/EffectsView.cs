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
        private AudioClip _popClip, _swapClip, _invalidClip, _rocketClip, _bombClip, _winClip, _loseClip;

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
            _sfx.playOnAwake = _popSource.playOnAwake = false;
            BuildClips();
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
                _emit.startSize = Random.Range(0.18f, 0.3f);
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
        }

        private static AudioClip Synth(string name, float seconds, float startHz, float endHz, float noiseAmount,
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
