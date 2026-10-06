using BoardGame.Data;
using UnityEditor;
using UnityEngine;

namespace BoardGame.EditorTools
{
    /// <summary>
    /// Paints piece IDs onto a <see cref="LevelData"/> asset. Left-click or drag paints the selected brush,
    /// right-click or drag erases, keys 0-6 pick a brush. Every edit is undoable.
    /// The grid is drawn top row first, so it looks like the board in game (y = 0 is the bottom row).
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        // Index = piece ID. Colors 1-6 match BoardView's default palette.
        private static readonly Color[] PieceColors =
        {
            new Color(0.17f, 0.17f, 0.19f), // empty
            new Color(0.91f, 0.30f, 0.24f), // red
            new Color(0.95f, 0.77f, 0.06f), // yellow
            new Color(0.18f, 0.80f, 0.44f), // green
            new Color(0.20f, 0.60f, 0.86f), // blue
            new Color(0.61f, 0.35f, 0.71f), // purple
            new Color(0.98f, 0.55f, 0.16f), // orange
        };

        private static readonly string[] PieceNames = { "Empty", "Red", "Yellow", "Green", "Blue", "Purple", "Orange" };

        private static readonly Color UnknownColor = new Color(1f, 0f, 1f);       // IDs the palette doesn't know
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color SelectedOutline = Color.white;

        private const float SwatchSize = 34f;
        private const float MinCellSize = 18f;
        private const float MaxCellSize = 52f;
        private const float CellGap = 2f;
        private const float Margin = 8f;
        private const float ScrollbarWidth = 16f;

        [SerializeField] private LevelData _level;
        [SerializeField] private int _brush = 1;

        private Vector2 _scroll;
        private int _paintValue;
        private int _undoGroup = -1;
        private GUIStyle _cellLabel;
        private GUIStyle _padding;

        [MenuItem("Window/Match-3/Level Editor")]
        public static void Open()
        {
            var window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("Level Editor");
            window.minSize = new Vector2(340f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += Repaint;
            if (_level == null) _level = Selection.activeObject as LevelData;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        // Selecting a LevelData asset in the Project window opens it here.
        private void OnSelectionChange()
        {
            if (Selection.activeObject is LevelData level && level != _level)
            {
                _level = level;
                Repaint();
            }
        }

        private void OnGUI()
        {
            _padding ??= new GUIStyle { padding = new RectOffset(8, 8, 8, 4) };
            HandleBrushShortcuts();

            using (new EditorGUILayout.VerticalScope(_padding))
            {
                DrawAssetField();
                if (_level == null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("Pick a LevelData asset above, select one in the Project window, " +
                                            "or click New to create one.", MessageType.Info);
                    return;
                }

                if (_level.EnsureLayout()) EditorUtility.SetDirty(_level);

                EditorGUILayout.Space();
                DrawDimensions();
                EditorGUILayout.Space();
                DrawPalette();
                EditorGUILayout.Space();
                DrawGrid();
                DrawFooter();
            }
        }

        // ---------------------------------------------------------------- asset

        private void DrawAssetField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _level = (LevelData)EditorGUILayout.ObjectField("Level", _level, typeof(LevelData), false);
                if (GUILayout.Button("New", GUILayout.Width(56f))) CreateLevel();
            }
        }

        private void CreateLevel()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Level", "LevelData", "asset",
                                                               "Choose where to save the new level.");
            if (!string.IsNullOrEmpty(path))
            {
                var level = CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, path);
                AssetDatabase.SaveAssetIfDirty(level);
                _level = level;
                EditorGUIUtility.PingObject(level);
            }
            GUIUtility.ExitGUI(); // the modal save panel broke this GUI pass's layout; start a fresh one
        }

        // ---------------------------------------------------------------- dimensions

        private void DrawDimensions()
        {
            EditorGUILayout.LabelField("Board Size", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int width = EditorGUILayout.IntSlider("Width", _level.Width, LevelData.MinSize, LevelData.MaxSize);
            int height = EditorGUILayout.IntSlider("Height", _level.Height, LevelData.MinSize, LevelData.MaxSize);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_level, "Resize Level");
                _level.Resize(width, height);
                EditorUtility.SetDirty(_level);
            }
        }

        // ---------------------------------------------------------------- palette

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int id = 0; id < PieceColors.Length; id++)
                {
                    Rect rect = GUILayoutUtility.GetRect(SwatchSize, SwatchSize,
                                                         GUILayout.Width(SwatchSize), GUILayout.Height(SwatchSize));
                    if (GUI.Button(rect, new GUIContent("", $"{PieceNames[id]} ({id})"), GUIStyle.none))
                    {
                        _brush = id;
                        GUIUtility.keyboardControl = 0;
                    }

                    if (id == _brush) EditorGUI.DrawRect(rect, SelectedOutline);
                    Rect inner = Shrink(rect, id == _brush ? 3f : 1f);
                    EditorGUI.DrawRect(inner, PieceColors[id]);
                    GUI.Label(inner, id.ToString(), CellLabel(PieceColors[id]));
                }
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.LabelField($"Painting: {NameOf(_brush)}   ·   keys 0-{PieceColors.Length - 1} switch brush",
                                       EditorStyles.miniLabel);
        }

        private void HandleBrushShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0) return;

            int id = e.keyCode >= KeyCode.Keypad0 && e.keyCode <= KeyCode.Keypad9
                ? e.keyCode - KeyCode.Keypad0
                : e.keyCode - KeyCode.Alpha0;
            if (id < 0 || id >= PieceColors.Length) return;

            _brush = id;
            e.Use();
            Repaint();
        }

        // ---------------------------------------------------------------- grid

        private void DrawGrid()
        {
            int width = _level.Width;
            int height = _level.Height;

            EditorGUILayout.LabelField("Board   ·   left-click paint, right-click erase", EditorStyles.boldLabel);

            float available = position.width - 2f * Margin - ScrollbarWidth;
            float cell = Mathf.Clamp(Mathf.Floor(available / width), MinCellSize, MaxCellSize);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                Rect area = GUILayoutUtility.GetRect(width * cell, height * cell,
                                                     GUILayout.Width(width * cell), GUILayout.Height(height * cell));
                HandleGridInput(area, cell);
                if (Event.current.type == EventType.Repaint) DrawCells(area, cell);
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawCells(Rect area, float cell)
        {
            int width = _level.Width;
            int height = _level.Height;
            int[] cells = _level.Cells;
            bool showIds = cell >= 24f;
            Vector2 mouse = Event.current.mousePosition;

            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    int id = cells[y * width + x];
                    Color color = ColorOf(id);
                    var rect = new Rect(area.x + x * cell, area.y + row * cell, cell - CellGap, cell - CellGap);

                    EditorGUI.DrawRect(rect, color);
                    if (showIds && id != LevelData.Empty) GUI.Label(rect, id.ToString(), CellLabel(color));
                    if (rect.Contains(mouse)) EditorGUI.DrawRect(rect, HoverColor);
                }
            }
        }

        private void HandleGridInput(Rect area, float cell)
        {
            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if ((e.button != 0 && e.button != 1) || !area.Contains(e.mousePosition)) break;
                    GUIUtility.hotControl = controlId;
                    GUIUtility.keyboardControl = 0; // so brush shortcuts work right after painting
                    _paintValue = e.button == 1 ? LevelData.Empty : _brush;

                    // One drag stroke = one undo step.
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Paint Level");
                    _undoGroup = Undo.GetCurrentGroup();

                    PaintAt(area, cell, e.mousePosition);
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId) break;
                    PaintAt(area, cell, e.mousePosition);
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId) break;
                    GUIUtility.hotControl = 0;
                    Undo.CollapseUndoOperations(_undoGroup);
                    e.Use();
                    break;

                case EventType.MouseMove:
                    if (area.Contains(e.mousePosition)) Repaint();
                    break;
            }
        }

        private void PaintAt(Rect area, float cell, Vector2 mouse)
        {
            int x = Mathf.FloorToInt((mouse.x - area.x) / cell);
            int row = Mathf.FloorToInt((mouse.y - area.y) / cell);
            int y = _level.Height - 1 - row;
            if (!_level.Contains(x, y) || _level.GetCell(x, y) == _paintValue) return;

            Undo.RecordObject(_level, "Paint Level");
            _level.SetCell(x, y, _paintValue);
            EditorUtility.SetDirty(_level);
            Repaint();
        }

        // ---------------------------------------------------------------- footer

        private void DrawFooter()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear")) FillBoard(LevelData.Empty, "Clear Level");
                if (GUILayout.Button($"Fill with {NameOf(_brush)}")) FillBoard(_brush, "Fill Level");
            }

            bool dirty = EditorUtility.IsDirty(_level);
            EditorGUILayout.LabelField(dirty ? "● Unsaved changes" : "All changes saved", EditorStyles.miniLabel);

            if (GUILayout.Button("Save", GUILayout.Height(28f)))
            {
                EditorUtility.SetDirty(_level);
                AssetDatabase.SaveAssetIfDirty(_level);
            }
        }

        private void FillBoard(int pieceId, string undoName)
        {
            Undo.RecordObject(_level, undoName);
            _level.Fill(pieceId);
            EditorUtility.SetDirty(_level);
        }

        // ---------------------------------------------------------------- helpers

        private static Color ColorOf(int id) => id >= 0 && id < PieceColors.Length ? PieceColors[id] : UnknownColor;

        private static string NameOf(int id) => id >= 0 && id < PieceNames.Length ? PieceNames[id] : $"ID {id}";

        private static Rect Shrink(Rect rect, float amount) =>
            new Rect(rect.x + amount, rect.y + amount, rect.width - 2f * amount, rect.height - 2f * amount);

        // Centered bold label, dark on light colors and light on dark ones.
        private GUIStyle CellLabel(Color background)
        {
            _cellLabel ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            float luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            _cellLabel.normal.textColor = luminance > 0.55f ? new Color(0f, 0f, 0f, 0.75f) : new Color(1f, 1f, 1f, 0.9f);
            return _cellLabel;
        }
    }
}
