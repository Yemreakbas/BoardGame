using System;
using BoardGame.Core.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.View
{
    /// <summary>
    /// Big combo words ("Nice!" ... "Incredible!" ... "UNSTOPPABLE!") that slam onto the screen when a move
    /// chains cascades: the word crashes in oversized and tilted, its letters bounce one after another over a
    /// flash of light, then it floats up and fades. Letters are animated through the text mesh's vertices from
    /// a copy made in a buffer allocated once, so a callout never allocates.
    /// </summary>
    public sealed class ComboCalloutView : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TMP_Text _text;
        [Tooltip("Optional: a glow image behind the word that bursts out when it lands.")]
        [SerializeField] private Image _burst;
        [SerializeField, Min(0.3f)] private float _duration = 1.35f;

        private static readonly string[] Words = { "Nice!", "Great!", "Awesome!", "Amazing!", "Incredible!", "UNSTOPPABLE!" };
        private static readonly Color[] Colors =
        {
            new Color(0.55f, 0.95f, 1f), new Color(1f, 0.9f, 0.3f), new Color(0.6f, 1f, 0.45f),
            new Color(1f, 0.62f, 0.2f), new Color(1f, 0.4f, 0.6f), new Color(0.8f, 0.55f, 1f),
        };

        private const int MaxVertices = 1024; // plenty for one short word
        private readonly Vector3[] _baseVertices = new Vector3[MaxVertices];
        private int _baseCount;

        private ScoreKeeper _score;
        private RectTransform _rect;
        private CanvasGroup _fade;   // fades the word without touching the text color, which would rebuild the mesh
        private float _baseFontSize;
        private int _lastCombo;
        private int _tier;
        private float _time = -1f;

        private void Start()
        {
            if (_boardView == null || _boardView.Score == null || _text == null)
            {
                enabled = false;
                return;
            }
            _rect = (RectTransform)_text.transform;
            _baseFontSize = _text.fontSize;
            _fade = _text.GetComponent<CanvasGroup>();
            if (_fade == null) _fade = _text.gameObject.AddComponent<CanvasGroup>();
            _fade.blocksRaycasts = false;
            _fade.interactable = false;
            // Long words shrink to fit the width instead of running off the screen.
            _text.enableAutoSizing = true;
            _text.fontSizeMin = _baseFontSize * 0.4f;
            _text.enabled = false;
            if (_burst != null) _burst.enabled = false;
            _score = _boardView.Score;
            _score.OnChanged += HandleScoreChanged;
        }

        private void OnDestroy()
        {
            if (_score != null) _score.OnChanged -= HandleScoreChanged;
        }

        private void HandleScoreChanged()
        {
            int combo = _score.Combo;
            if (combo > _lastCombo && combo >= 2) Show(Mathf.Min(combo - 2, Words.Length - 1));
            _lastCombo = combo;
        }

        private void Show(int tier)
        {
            _tier = tier;
            _text.fontSizeMax = _baseFontSize * (1f + 0.1f * tier); // bigger words for bigger chains, width permitting
            _text.SetText(Words[tier]);
            _text.color = Colors[tier];
            _fade.alpha = 1f;
            _text.enabled = true;
            _rect.localScale = Vector3.one;
            _rect.localRotation = Quaternion.identity;
            _rect.anchoredPosition = Vector2.zero;

            // Lay the word out once and keep its un-animated vertices.
            _text.ForceMeshUpdate();
            TMP_MeshInfo mesh = _text.textInfo.meshInfo[0];
            _baseCount = Mathf.Min(mesh.vertices.Length, MaxVertices);
            Array.Copy(mesh.vertices, _baseVertices, _baseCount);

            if (_burst != null)
            {
                Color glow = Colors[tier];
                glow.a = 0.9f;
                _burst.color = glow;
                _burst.enabled = true;
            }
            _time = 0f;
        }

        private void Update()
        {
            if (_time < 0f) return;
            _time += Time.deltaTime;
            float t = _time / _duration;
            if (t >= 1f)
            {
                _time = -1f;
                _text.enabled = false;
                if (_burst != null) _burst.enabled = false;
                return;
            }

            // Whole word: slam in from oversized and tilted, hold, then float up and fade.
            float slam = Mathf.Clamp01(_time / 0.22f);
            float ease = 1f - (1f - slam) * (1f - slam) * (1f - slam);
            float settle = slam >= 1f ? 1f + 0.06f * Mathf.Sin((_time - 0.22f) * 18f) * Mathf.Max(0f, 1f - (_time - 0.22f) * 4f) : 1f;
            float scale = Mathf.Lerp(2.5f, 1f, ease) * settle;
            _rect.localScale = new Vector3(scale, scale, 1f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-14f, 0f, ease));
            float rise = t > 0.65f ? (t - 0.65f) / 0.35f : 0f;
            _rect.anchoredPosition = new Vector2(0f, 90f * rise * rise);

            _fade.alpha = 1f - rise;

            // Letters: a bounce that runs along the word, then a gentle wave.
            TMP_TextInfo info = _text.textInfo;
            Vector3[] vertices = info.meshInfo[0].vertices;
            for (int c = 0; c < info.characterCount; c++)
            {
                TMP_CharacterInfo character = info.characterInfo[c];
                if (!character.isVisible || character.materialReferenceIndex != 0) continue;
                int v = character.vertexIndex;
                if (v + 3 >= _baseCount || v + 3 >= vertices.Length) continue;

                float local = Mathf.Clamp01((_time - 0.12f - c * 0.045f) / 0.32f);
                float bounce = Mathf.Sin(local * Mathf.PI) * 38f;
                float wave = Mathf.Sin(_time * 7f - c * 0.6f) * 6f * local;
                float letterScale = 1f + 0.25f * Mathf.Sin(local * Mathf.PI);
                Vector3 centre = (_baseVertices[v] + _baseVertices[v + 2]) * 0.5f;
                Vector3 lift = new Vector3(0f, bounce + wave, 0f);
                for (int k = 0; k < 4; k++)
                {
                    vertices[v + k] = centre + (_baseVertices[v + k] - centre) * letterScale + lift;
                }
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);

            if (_burst != null)
            {
                // A flash of light that bursts out behind the word as it lands and fades.
                float b = Mathf.Clamp01((_time - 0.12f) / 0.5f);
                float burstScale = Mathf.Lerp(0.4f, 2.6f + 0.3f * _tier, 1f - (1f - b) * (1f - b));
                _burst.rectTransform.localScale = new Vector3(burstScale, burstScale * 0.55f, 1f);
                Color glow = _burst.color;
                glow.a = _time < 0.12f ? _time / 0.12f * 0.9f : 0.9f * (1f - b);
                _burst.color = glow;
            }
        }
    }
}
