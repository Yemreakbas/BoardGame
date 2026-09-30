using System.Text;
using TMPro;
using UnityEngine;

namespace BoardGame.View
{
    /// <summary>
    /// Game logo: each letter in a piece color, popping in one by one, then riding a gentle wave.
    /// Letters are animated by moving the text mesh's vertices from a cached copy, so frames never allocate.
    /// </summary>
    public sealed class MenuTitleView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private string _title = "MATCH BOARD";
        [SerializeField] private Color[] _letterColors =
        {
            new Color(0.91f, 0.30f, 0.24f), new Color(0.95f, 0.77f, 0.06f), new Color(0.18f, 0.80f, 0.44f),
            new Color(0.20f, 0.60f, 0.86f), new Color(0.61f, 0.35f, 0.71f), new Color(0.98f, 0.55f, 0.16f),
        };
        [SerializeField] private float _waveHeight = 12f;
        [SerializeField] private float _waveSpeed = 3f;
        [SerializeField, Min(0f)] private float _letterDelay = 0.06f;

        private Vector3[][] _baseVertices;  // per material: the laid-out, un-animated vertices
        private float _time;

        private void Start()
        {
            if (_text == null) { enabled = false; return; }

            // Setup only: build the colored rich text once.
            var builder = new StringBuilder();
            int colored = 0;
            foreach (char c in _title)
            {
                if (c == ' ') { builder.Append(' '); continue; }
                Color color = _letterColors[colored++ % _letterColors.Length];
                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>').Append(c).Append("</color>");
            }
            _text.text = builder.ToString();
            CacheMesh();
        }

        // A new layout (e.g. a resolution change) invalidates the cached vertices.
        private void OnRectTransformDimensionsChange()
        {
            if (_baseVertices != null) CacheMesh();
        }

        private void CacheMesh()
        {
            _text.ForceMeshUpdate();
            TMP_TextInfo info = _text.textInfo;
            _baseVertices = new Vector3[info.meshInfo.Length][];
            for (int m = 0; m < info.meshInfo.Length; m++) _baseVertices[m] = (Vector3[])info.meshInfo[m].vertices.Clone();
        }

        private void Update()
        {
            if (_baseVertices == null) return;
            _time += Time.deltaTime;
            TMP_TextInfo info = _text.textInfo;

            for (int c = 0; c < info.characterCount; c++)
            {
                TMP_CharacterInfo character = info.characterInfo[c];
                if (!character.isVisible) continue;
                int m = character.materialReferenceIndex;
                int v = character.vertexIndex;
                if (m >= _baseVertices.Length || v + 3 >= _baseVertices[m].Length) continue;

                // Pop in (ease-out-back), then bob on a wave that travels along the word.
                float t = Mathf.Clamp01((_time - c * _letterDelay) / 0.4f);
                float u = t - 1f;
                float scale = t <= 0f ? 0f : 1f + 3.2f * u * u * u + 2.2f * u * u;
                float lift = Mathf.Sin(_time * _waveSpeed - c * 0.55f) * _waveHeight * t;

                Vector3[] source = _baseVertices[m];
                Vector3[] target = info.meshInfo[m].vertices;
                Vector3 centre = (source[v] + source[v + 2]) * 0.5f;
                for (int k = 0; k < 4; k++)
                {
                    target[v + k] = centre + (source[v + k] - centre) * scale + new Vector3(0f, lift, 0f);
                }
            }
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}
