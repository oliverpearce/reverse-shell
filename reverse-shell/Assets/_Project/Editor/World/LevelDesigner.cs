using System.Collections.Generic;
using System.Linq;
using Combat;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    // LEVEL DESIGNER
    // Reverse Shell > Level Designer. Chapters and their areas on the left, the selected area's map as a clickable
    // grid in the middle, the tile palette and the selected cell's settings (warps, fights, NPCs, chests) on the right.
    // Everything edits the area and chapter assets in Resources/Areas and Resources/Chapters, with Undo.
    // Shortcuts (over the window, not while typing): V select, B paint, R rectangle, G fill, [ ] zoom, I eyedropper
    // (or right click). The palette has a search box; the line under the map names the tile under the mouse.
    // The step-by-step guide is LEVEL_EDITOR_GUIDE.html in the repo's top folder.
    public class LevelDesigner : EditorWindow
    {
        enum Tool { Select, Paint, Rectangle, Fill }

        const float LeftWidth = 210f, RightWidth = 320f;
        const string OverworldScene = "Assets/_Project/Scenes/Overworld_Mossline.unity";
        const string PartCatalogPath = "Assets/_Project/Content/Parts/PartCatalog.asset";

        [SerializeField] AreaAsset area;
        [SerializeField] ChapterAsset chapter;
        [SerializeField] Tool tool = Tool.Paint;
        [SerializeField] int brushCode = '.';
        [SerializeField] int cellSize = 18;
        [SerializeField] Vector2Int selected = new Vector2Int(-1, -1);
        [SerializeField] char resizeFill = 'H';
        [SerializeField] string paletteFilter = "";
        Vector2Int hovered = new Vector2Int(-1, -1);
        string[] themeIds;

        Vector2 leftScroll, mapScroll, rightScroll, problemScroll;
        List<AreaAsset> areas;
        List<ChapterAsset> chapters;
        List<LevelValidator.Problem> problems;
        Editor chapterEditor;
        SerializedObject areaSO;

        bool dragging;
        Vector2Int dragStart, dragEnd;
        int paintGroup;

        static GUIStyle cellLabel;

        char Brush { get { return (char)brushCode; } set { brushCode = value; } }

        [MenuItem("Reverse Shell/Level Designer")]
        static void Open()
        {
            GetWindow<LevelDesigner>("Level Designer").minSize = new Vector2(900f, 500f);
        }

        void OnEnable()
        {
            wantsMouseMove = true;   // the line under the map follows the mouse
            Undo.undoRedoPerformed += OnUndoRedo;
            Refresh();
        }

        void OnDisable() { Undo.undoRedoPerformed -= OnUndoRedo; }

        void OnUndoRedo()
        {
            problems = null;
            Repaint();
        }

        void OnProjectChange()
        {
            Refresh();
            Repaint();
        }

        void Refresh()
        {
            areas = Load<AreaAsset>(AreaMigration.AreasFolder);
            chapters = Load<ChapterAsset>(AreaMigration.ChaptersFolder);
            chapters.Sort((a, b) => a.order.CompareTo(b.order));
            WorldAreas.Reload();
            themeIds = null;   // new theme assets show up in the dropdown
        }

        static List<T> Load<T>(string folder) where T : Object
        {
            var list = new List<T>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        void OnGUI()
        {
            if (areas == null || chapters == null || areas.Exists(a => a == null) || chapters.Exists(c => c == null)) Refresh();
            if (cellLabel == null)
                cellLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            Shortcuts();
            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            EditorGUILayout.BeginVertical();
            if (area != null) DrawArea();
            else if (chapter != null) DrawChapter();
            else EditorGUILayout.HelpBox("Pick an area (or a chapter) on the left, or make a new one.", MessageType.Info);
            EditorGUILayout.EndVertical();
            if (area != null) DrawRight();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>One-key tools while the mouse is over the window and no text field has focus.</summary>
        void Shortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0 || area == null) return;
            switch (e.keyCode)
            {
                case KeyCode.V: tool = Tool.Select; break;
                case KeyCode.B: tool = Tool.Paint; break;
                case KeyCode.R: tool = Tool.Rectangle; break;
                case KeyCode.G: tool = Tool.Fill; break;
                case KeyCode.LeftBracket: cellSize = Mathf.Max(8, cellSize - 2); break;
                case KeyCode.RightBracket: cellSize = Mathf.Min(32, cellSize + 2); break;
                case KeyCode.I:
                    var rows = Rows();
                    if (hovered.y >= 0 && hovered.y < rows.Count && hovered.x >= 0 && hovered.x < rows[hovered.y].Length) Brush = rows[hovered.y][hovered.x];
                    break;
                default: return;
            }
            e.Use();
            Repaint();
        }

        /// <summary>The world looks the designer offers: every Resources/Themes/Theme_* asset, plus "(old look)".</summary>
        string[] ThemeIds()
        {
            if (themeIds != null) return themeIds;
            var ids = new List<string> { "" };
            foreach (var t in Resources.LoadAll<WorldTheme>("Themes"))
                if (t.name.StartsWith("Theme_")) ids.Add(t.name.Substring("Theme_".Length));
            ids.Sort((a, b) => string.CompareOrdinal(a, b));
            return themeIds = ids.ToArray();
        }

        // LEVEL DESIGNER > LEFT COLUMN
        // Chapters, each with its areas, then the areas outside every chapter (the Sandbox).

        void DrawLeft()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftWidth));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            var inChapter = new HashSet<AreaAsset>();
            foreach (var c in chapters)
            {
                if (ListButton(c.title + "  (" + c.id + ")", c == chapter && area == null, EditorStyles.boldLabel))
                    Select(null, c);
                foreach (var a in c.areas)
                {
                    if (a == null) continue;
                    inChapter.Add(a);
                    if (ListButton("    " + a.displayName, a == area)) Select(a, c);
                }
                GUILayout.Space(6f);
            }
            var others = areas.Where(a => !inChapter.Contains(a)).ToList();
            if (others.Count > 0)
            {
                GUILayout.Label("Other areas", EditorStyles.boldLabel);
                foreach (var a in others)
                    if (ListButton("    " + a.displayName, a == area)) Select(a, null);
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("New Chapter")) NewChapter();
            if (GUILayout.Button(chapter != null ? "New Area in " + chapter.title : "New Area")) NewArea();
            using (new EditorGUI.DisabledScope(area == null))
            {
                if (GUILayout.Button("Duplicate Area")) DuplicateArea();
                if (GUILayout.Button("Delete Area")) DeleteArea();
            }
            using (new EditorGUI.DisabledScope(chapter == null || area != null))
                if (GUILayout.Button("Delete Chapter")) DeleteChapter();
            EditorGUILayout.EndVertical();
        }

        static bool ListButton(string label, bool on, GUIStyle style = null)
        {
            var s = new GUIStyle(style ?? EditorStyles.label);
            if (on)
            {
                s.normal.textColor = new Color(1f, 0.8f, 0.35f);
                s.fontStyle = FontStyle.Bold;
            }
            return GUILayout.Button(label, s);
        }

        void Select(AreaAsset a, ChapterAsset c)
        {
            area = a;
            chapter = c;
            areaSO = null;
            problems = null;
            selected = new Vector2Int(-1, -1);
            GUI.FocusControl(null);
        }

        // LEVEL DESIGNER > CREATING AND DELETING

        void NewChapter()
        {
            int n = chapters.Count + 1;
            while (chapters.Exists(c => c.id == "chapter" + n)) n++;
            var c2 = CreateInstance<ChapterAsset>();
            c2.id = "chapter" + n;
            c2.title = "Chapter " + n;
            c2.order = chapters.Count > 0 ? chapters.Max(c => c.order) + 1 : 1;
            AssetDatabase.CreateAsset(c2, AssetDatabase.GenerateUniqueAssetPath(AreaMigration.ChaptersFolder + "/Chapter_" + n + ".asset"));
            AssetDatabase.SaveAssets();
            Refresh();
            Select(null, c2);
        }

        void NewArea()
        {
            int n = 1;
            while (areas.Exists(a => a.id == "area" + n)) n++;
            var a2 = CreateInstance<AreaAsset>();
            a2.id = "area" + n;
            a2.displayName = "NEW AREA " + n;
            // A ready room: tall walls at the back (north), low wall at the front, a spawn and an Access Point.
            a2.map = "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" + string.Concat(Enumerable.Repeat("H............................H\n", 4))
                + "H.............S..............H\n" + "H............................H\n" + "H.........................A..H\n"
                + string.Concat(Enumerable.Repeat("H............................H\n", 4)) + "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLL\n";
            a2.theme = "Forest";
            AssetDatabase.CreateAsset(a2, AssetDatabase.GenerateUniqueAssetPath(AreaMigration.AreasFolder + "/Area_" + n + ".asset"));
            if (chapter != null)
            {
                Undo.RecordObject(chapter, "Add Area");
                chapter.areas.Add(a2);
                if (chapter.startArea == null) chapter.startArea = a2;
                EditorUtility.SetDirty(chapter);
            }
            AssetDatabase.SaveAssets();
            Refresh();
            Select(a2, chapter);
        }

        void DuplicateArea()
        {
            var copy = Instantiate(area);
            copy.id = area.id + "_copy";
            copy.displayName = area.displayName + " (COPY)";
            copy.warps.Clear();   // two areas sharing one set of warps would confuse the links
            string path = AssetDatabase.GenerateUniqueAssetPath(AreaMigration.AreasFolder + "/" + area.name + "_Copy.asset");
            AssetDatabase.CreateAsset(copy, path);
            if (chapter != null)
            {
                Undo.RecordObject(chapter, "Duplicate Area");
                chapter.areas.Add(copy);
                EditorUtility.SetDirty(chapter);
            }
            AssetDatabase.SaveAssets();
            Refresh();
            Select(copy, chapter);
        }

        void DeleteArea()
        {
            if (!EditorUtility.DisplayDialog("Delete Area", "Delete " + area.displayName + " (" + area.id + ")?\n\nWarps that lead "
                    + "there will lead nowhere. This can't be undone.", "Delete", "Cancel"))
                return;
            foreach (var c in chapters)
            {
                if (!c.areas.Contains(area) && c.startArea != area) continue;
                c.areas.RemoveAll(a => a == area);
                if (c.startArea == area) c.startArea = null;
                EditorUtility.SetDirty(c);
            }
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(area));
            AssetDatabase.SaveAssets();
            Refresh();
            Select(null, chapter);
        }

        void DeleteChapter()
        {
            if (!EditorUtility.DisplayDialog("Delete Chapter", "Delete " + chapter.title + "?\n\nIts areas stay (they move to "
                    + "Other areas). This can't be undone.", "Delete", "Cancel"))
                return;
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(chapter));
            AssetDatabase.SaveAssets();
            Refresh();
            Select(null, null);
        }

        // LEVEL DESIGNER > CHAPTER
        // The chapter's own fields, in the ordinary inspector.

        void DrawChapter()
        {
            EditorGUILayout.LabelField(chapter.title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The first chapter (lowest Order) is where a new game starts, at its Start Area's spawn. "
                + "Objectives: in story order; the last one whose flag is set shows top-right (its words are a block in Resources/Story).",
                MessageType.None);
            Editor.CreateCachedEditor(chapter, null, ref chapterEditor);
            chapterEditor.OnInspectorGUI();
        }

        // LEVEL DESIGNER > AREA
        // The area's fields, the tools, the map grid and the problems list.

        void DrawArea()
        {
            if (areaSO == null || areaSO.targetObject != area) areaSO = new SerializedObject(area);
            areaSO.Update();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(areaSO.FindProperty("id"), new GUIContent("Id"), GUILayout.MinWidth(120f));
            EditorGUILayout.PropertyField(areaSO.FindProperty("displayName"), new GUIContent("Name"), GUILayout.MinWidth(160f));
            EditorGUILayout.PropertyField(areaSO.FindProperty("subtitle"), new GUIContent("Subtitle"), GUILayout.MinWidth(160f));
            EditorGUILayout.PropertyField(areaSO.FindProperty("dim"), new GUIContent("Dim"), GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            // The world's look (light, haze, pixel size, models, effects): one of the theme assets.
            var themeProp = areaSO.FindProperty("theme");
            var ids = ThemeIds();
            int current = System.Array.IndexOf(ids, themeProp.stringValue);
            var labels = ids.Select(i => i.Length == 0 ? "(old look)" : i).ToArray();
            int picked = EditorGUILayout.Popup("Theme", Mathf.Max(0, current), labels, GUILayout.Width(360f));
            if (picked != current && picked >= 0) themeProp.stringValue = ids[picked];
            areaSO.ApplyModifiedProperties();

            EditorGUILayout.BeginHorizontal();
            tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "Select", "Paint", "Rectangle", "Fill" }, GUILayout.Width(300f));
            GUILayout.Label("Zoom", GUILayout.Width(36f));
            cellSize = (int)GUILayout.HorizontalSlider(cellSize, 8f, 32f, GUILayout.Width(100f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Validate", GUILayout.Width(80f))) problems = LevelValidator.Check(area, areas);
            if (GUILayout.Button("Play This Area", GUILayout.Width(110f))) PlayArea();
            EditorGUILayout.EndHorizontal();

            DrawResize();

            var rows = Rows();
            int width = rows.Count > 0 ? rows[0].Length : 0;
            mapScroll = EditorGUILayout.BeginScrollView(mapScroll, GUILayout.ExpandHeight(true));
            var rect = GUILayoutUtility.GetRect(width * cellSize, rows.Count * cellSize, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            DrawGrid(rect, rows);
            HandleMouse(rect, rows);
            EditorGUILayout.EndScrollView();

            DrawProblems();
            // What's under the mouse, then the controls.
            string under = "";
            if (hovered.y >= 0 && hovered.y < rows.Count && hovered.x >= 0 && hovered.x < width)
            {
                char hc = rows[hovered.y][hovered.x];
                var ht = TileLegend.Find(hc);
                under = "Cell " + hovered.x + ", " + hovered.y + ":  " + hc + "  " + (ht != null ? ht.Name + (ht.Solid ? " (solid)" : " (walkable)") : "(not in the legend)") + "      ";
            }
            GUILayout.Label(under + "Left click: use the tool.  Right click or I: pick up that tile.  V B R G: tools.  [ ]: zoom.  Ctrl+Z: undo.", EditorStyles.miniLabel);
        }

        void DrawResize()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Rows", GUILayout.Width(36f));
            if (GUILayout.Button("+ top", EditorStyles.miniButtonLeft)) Resize(0, 1, 0, 0);
            if (GUILayout.Button("- top", EditorStyles.miniButtonMid)) Resize(0, -1, 0, 0);
            if (GUILayout.Button("+ bottom", EditorStyles.miniButtonMid)) Resize(0, 0, 0, 1);
            if (GUILayout.Button("- bottom", EditorStyles.miniButtonRight)) Resize(0, 0, 0, -1);
            GUILayout.Space(12f);
            GUILayout.Label("Columns", GUILayout.Width(56f));
            if (GUILayout.Button("+ left", EditorStyles.miniButtonLeft)) Resize(1, 0, 0, 0);
            if (GUILayout.Button("- left", EditorStyles.miniButtonMid)) Resize(-1, 0, 0, 0);
            if (GUILayout.Button("+ right", EditorStyles.miniButtonMid)) Resize(0, 0, 1, 0);
            if (GUILayout.Button("- right", EditorStyles.miniButtonRight)) Resize(0, 0, -1, 0);
            GUILayout.Space(12f);
            GUILayout.Label("New cells", GUILayout.Width(62f));
            string fill = EditorGUILayout.TextField(resizeFill.ToString(), GUILayout.Width(24f));
            if (!string.IsNullOrEmpty(fill)) resizeFill = fill[0];
            GUILayout.FlexibleSpace();
            var r = Rows();
            GUILayout.Label((r.Count > 0 ? r[0].Length : 0) + " x " + r.Count, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // LEVEL DESIGNER > MAP TEXT
        // The map is the asset's text: one row per line. Edits go through these two, so every change is undoable.

        List<char[]> Rows()
        {
            var lines = (area.map ?? "").Replace("\r", "").TrimEnd('\n').Split('\n');
            int width = lines.Max(l => l.Length);
            var rows = new List<char[]>();
            foreach (var l in lines)
            {
                if (l.Length == 0 && lines.Length == 1) break;
                rows.Add(l.PadRight(width, resizeFill).ToCharArray());
            }
            return rows;
        }

        void Write(List<char[]> rows, string undoName)
        {
            Undo.RecordObject(area, undoName);
            area.map = string.Join("\n", rows.Select(r => new string(r)).ToArray()) + "\n";
            EditorUtility.SetDirty(area);
            problems = null;
        }

        /// <summary>Adds (positive) or removes (negative) columns on the left / right and rows on the top / bottom.</summary>
        void Resize(int left, int top, int right, int bottom)
        {
            var rows = Rows();
            int width = rows.Count > 0 ? rows[0].Length : 0;
            if (width + left + right < 1 || rows.Count + top + bottom < 1) return;
            var result = new List<char[]>();
            for (int i = 0; i < top; i++) result.Add(Enumerable.Repeat(resizeFill, width).ToArray());
            result.AddRange(rows.Skip(Mathf.Max(0, -top)).Take(rows.Count - Mathf.Max(0, -top) - Mathf.Max(0, -bottom)));
            for (int i = 0; i < bottom; i++) result.Add(Enumerable.Repeat(resizeFill, width).ToArray());
            for (int i = 0; i < result.Count; i++)
            {
                var line = new string(result[i]);
                if (left < 0) line = line.Substring(-left);
                if (right < 0) line = line.Substring(0, line.Length + right);
                line = new string(resizeFill, Mathf.Max(0, left)) + line + new string(resizeFill, Mathf.Max(0, right));
                result[i] = line.ToCharArray();
            }
            Write(result, "Resize Map");
            if (selected.x >= 0) selected += new Vector2Int(left, top);
        }

        // LEVEL DESIGNER > GRID

        void DrawGrid(Rect rect, List<char[]> rows)
        {
            if (Event.current.type != EventType.Repaint) return;
            int cs = cellSize;
            for (int row = 0; row < rows.Count; row++)
                for (int x = 0; x < rows[row].Length; x++)
                {
                    char c = rows[row][x];
                    var tile = TileLegend.Find(c);
                    var color = tile != null ? tile.Color : Color.magenta;
                    var cell = new Rect(rect.x + x * cs, rect.y + row * cs, cs - 1, cs - 1);
                    EditorGUI.DrawRect(cell, color);
                    if (cs >= 12 && !IsPlainGround(c))
                    {
                        cellLabel.normal.textColor = color.grayscale > 0.55f ? Color.black : Color.white;
                        GUI.Label(cell, c.ToString(), cellLabel);
                    }
                }

            if (dragging && tool == Tool.Rectangle)
            {
                var r = CellRect(rect, dragStart, dragEnd);
                EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.25f));
            }
            if (selected.y >= 0 && selected.y < rows.Count && selected.x >= 0 && selected.x < rows[selected.y].Length)
                Outline(new Rect(rect.x + selected.x * cs - 1, rect.y + selected.y * cs - 1, cs + 1, cs + 1), new Color(1f, 0.85f, 0.2f));
        }

        static bool IsPlainGround(char c) { return c == '.' || c == '=' || c == 'P' || c == ','; }

        Rect CellRect(Rect grid, Vector2Int a, Vector2Int b)
        {
            int x0 = Mathf.Min(a.x, b.x), y0 = Mathf.Min(a.y, b.y), x1 = Mathf.Max(a.x, b.x), y1 = Mathf.Max(a.y, b.y);
            return new Rect(grid.x + x0 * cellSize, grid.y + y0 * cellSize, (x1 - x0 + 1) * cellSize, (y1 - y0 + 1) * cellSize);
        }

        static void Outline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 2f, r.y, 2f, r.height), c);
        }

        void HandleMouse(Rect rect, List<char[]> rows)
        {
            var e = Event.current;
            if (rows.Count == 0) return;
            var cell = new Vector2Int(Mathf.FloorToInt((e.mousePosition.x - rect.x) / cellSize), Mathf.FloorToInt((e.mousePosition.y - rect.y) / cellSize));
            bool inside = cell.y >= 0 && cell.y < rows.Count && cell.x >= 0 && cell.x < rows[0].Length;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                var now = inside ? cell : new Vector2Int(-1, -1);
                if (now != hovered) { hovered = now; Repaint(); }
            }

            if (e.type == EventType.MouseDown && inside)
            {
                GUI.FocusControl(null);
                selected = cell;
                if (e.button == 1)
                {
                    Brush = rows[cell.y][cell.x];   // eyedropper
                    if (tool == Tool.Select) tool = Tool.Paint;
                }
                else if (e.button == 0)
                {
                    if (tool == Tool.Paint)
                    {
                        Undo.IncrementCurrentGroup();
                        paintGroup = Undo.GetCurrentGroup();
                        SetCell(rows, cell);
                        dragging = true;
                    }
                    else if (tool == Tool.Rectangle)
                    {
                        dragStart = dragEnd = cell;
                        dragging = true;
                    }
                    else if (tool == Tool.Fill) Flood(rows, cell);
                }
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && dragging && e.button == 0)
            {
                cell.x = Mathf.Clamp(cell.x, 0, rows[0].Length - 1);
                cell.y = Mathf.Clamp(cell.y, 0, rows.Count - 1);
                if (tool == Tool.Paint) SetCell(rows, cell);
                else dragEnd = cell;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && dragging && e.button == 0)
            {
                dragging = false;
                if (tool == Tool.Rectangle)
                {
                    int x0 = Mathf.Min(dragStart.x, dragEnd.x), y0 = Mathf.Min(dragStart.y, dragEnd.y);
                    int x1 = Mathf.Max(dragStart.x, dragEnd.x), y1 = Mathf.Max(dragStart.y, dragEnd.y);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++) rows[y][x] = Brush;
                    Write(rows, "Paint Rectangle");
                }
                else Undo.CollapseUndoOperations(paintGroup);
                e.Use();
                Repaint();
            }
        }

        void SetCell(List<char[]> rows, Vector2Int cell)
        {
            if (rows[cell.y][cell.x] == Brush) return;
            rows[cell.y][cell.x] = Brush;
            Write(rows, "Paint");
        }

        void Flood(List<char[]> rows, Vector2Int start)
        {
            char from = rows[start.y][start.x];
            if (from == Brush) return;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            rows[start.y][start.x] = Brush;
            var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in steps)
                {
                    var n = c + d;
                    if (n.y < 0 || n.y >= rows.Count || n.x < 0 || n.x >= rows[n.y].Length || rows[n.y][n.x] != from) continue;
                    rows[n.y][n.x] = Brush;
                    queue.Enqueue(n);
                }
            }
            Write(rows, "Fill");
        }

        // LEVEL DESIGNER > PROBLEMS

        void DrawProblems()
        {
            if (problems == null)
            {
                GUILayout.Label("Press Validate to check the area for problems.", EditorStyles.miniLabel);
                return;
            }
            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
                return;
            }
            problemScroll = EditorGUILayout.BeginScrollView(problemScroll, GUILayout.Height(120f));
            foreach (var p in problems)
            {
                var content = new GUIContent((p.Error ? "Error: " : "Warning: ") + p.Text + (p.Cell.HasValue ? "  (click to show)" : ""));
                var style = new GUIStyle(EditorStyles.label) { wordWrap = true };
                style.normal.textColor = p.Error ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.8f, 0.35f);
                if (GUILayout.Button(content, style) && p.Cell.HasValue)
                {
                    selected = p.Cell.Value;
                    mapScroll = new Vector2(selected.x * cellSize - 200f, selected.y * cellSize - 120f);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // LEVEL DESIGNER > RIGHT COLUMN
        // The palette (every tile in the legend) and the selected cell's settings.

        void DrawRight()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightWidth));
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
            DrawPalette();
            GUILayout.Space(10f);
            areaSO.Update();
            DrawCell();
            areaSO.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawPalette()
        {
            var brushTile = TileLegend.Find(Brush);
            GUILayout.Label("Brush:  " + Brush + "   " + (brushTile != null ? brushTile.Name : "(not in the legend)")
                + (brushTile != null ? (brushTile.Solid ? "  · solid" : "  · walkable") : ""), EditorStyles.boldLabel);
            paletteFilter = EditorGUILayout.TextField("Search tiles", paletteFilter);
            string filter = (paletteFilter ?? "").Trim().ToLowerInvariant();
            foreach (var group in TileLegend.Groups)
            {
                var tiles = TileLegend.All.Where(t => t.Group == group && (filter.Length == 0
                    || t.Name.ToLowerInvariant().Contains(filter) || t.Char.ToString() == filter || t.Note.ToLowerInvariant().Contains(filter))).ToList();
                if (tiles.Count == 0) continue;
                GUILayout.Label(group, EditorStyles.miniBoldLabel);
                for (int i = 0; i < tiles.Count; i += 6)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + 6, tiles.Count); j++)
                    {
                        var t = tiles[j];
                        var r = GUILayoutUtility.GetRect(46f, 30f, GUILayout.Width(46f));
                        EditorGUI.DrawRect(r, t.Color);
                        if (t.Char == Brush) Outline(r, new Color(1f, 0.85f, 0.2f));
                        cellLabel.normal.textColor = t.Color.grayscale > 0.55f ? Color.black : Color.white;
                        string tip = t.Name + (t.Solid ? " (solid" + (t.Height > 0 ? ", height " + t.Height : "") + ")" : " (walkable)")
                                     + (t.Note.Length > 0 ? "\n" + t.Note : "");
                        if (GUI.Button(r, new GUIContent(t.Char.ToString(), tip), cellLabel))
                        {
                            Brush = t.Char;
                            if (tool == Tool.Select) tool = Tool.Paint;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        void DrawCell()
        {
            var rows = Rows();
            if (selected.y < 0 || selected.y >= rows.Count || selected.x < 0 || selected.x >= rows[selected.y].Length)
            {
                EditorGUILayout.HelpBox("Click a cell to see its settings: warps, fights, NPCs and chests.", MessageType.None);
                DrawAreaLists();
                return;
            }
            char c = rows[selected.y][selected.x];
            var tile = TileLegend.Find(c);
            GUILayout.Label("Cell " + selected.x + ", " + selected.y + ":  " + c + "   " + (tile != null ? tile.Name : "(not in the legend)"), EditorStyles.boldLabel);
            if (tile != null && tile.Note.Length > 0) EditorGUILayout.HelpBox(tile.Note, MessageType.None);

            bool isWarp = c == 'W' || c == 'J' || c == 'w' || c == 'V' || area.warps.Exists(w => w != null && w.tile == c.ToString());
            if (isWarp) DrawWarp(c);
            else if (c == 'E') DrawEncounter(IndexOf(rows, 'E'));
            else if (c == 'n') DrawNpc(IndexOf(rows, 'n'));
            else if (c == 'c') DrawChest(IndexOf(rows, 'c'));
            GUILayout.Space(10f);
            DrawAreaLists();
        }

        /// <summary>The selected cell's place among the cells with this tile, in reading order (left to right, top to bottom).</summary>
        int IndexOf(List<char[]> rows, char c)
        {
            int index = 0;
            for (int row = 0; row < rows.Count; row++)
                for (int x = 0; x < rows[row].Length; x++)
                {
                    if (row == selected.y && x == selected.x) return index;
                    if (rows[row][x] == c) index++;
                }
            return index;
        }

        void DrawAreaLists()
        {
            EditorGUILayout.PropertyField(areaSO.FindProperty("warps"), new GUIContent("All warps"), true);
        }

        // LEVEL DESIGNER > CELL > WARP
        // Every cell with the same warp tile leads to the same area. "Link both ways" adds the warp back.

        void DrawWarp(char c)
        {
            int index = area.warps.FindIndex(w => w != null && w.tile == c.ToString());
            if (c == 'V' || c == 'w')
                EditorGUILayout.HelpBox("The hidden route: Chapter One's director opens it, and it then leads where its 'V' warp says.", MessageType.None);
            if (index < 0)
            {
                if (GUILayout.Button("Make '" + c + "' a warp"))
                {
                    var warps = areaSO.FindProperty("warps");
                    warps.arraySize++;
                    var w = warps.GetArrayElementAtIndex(warps.arraySize - 1);
                    w.FindPropertyRelative("tile").stringValue = c.ToString();
                    w.FindPropertyRelative("to").stringValue = "";
                }
                return;
            }
            var to = areaSO.FindProperty("warps").GetArrayElementAtIndex(index).FindPropertyRelative("to");
            var ids = areas.Where(a => a != area).Select(a => a.id).ToArray();
            to.stringValue = Popup("Leads to", to.stringValue, ids);
            EditorGUILayout.HelpBox("Every '" + c + "' in this area leads there. You arrive beside the warp in that area that leads back here.", MessageType.None);

            var target = areas.Find(a => a.id == to.stringValue);
            if (target == null) return;
            bool linked = target.warps.Exists(w => w != null && w.to == area.id);
            using (new EditorGUI.DisabledScope(linked))
                if (GUILayout.Button(linked ? "Linked both ways" : "Link both ways (add the warp back in " + target.id + ")"))
                    LinkBack(target, c);
        }

        void LinkBack(AreaAsset target, char preferred)
        {
            char tile = preferred == 'V' || preferred == 'w' ? 'W' : preferred;
            foreach (var candidate in new[] { tile, 'W', 'J' })
                if (!target.warps.Exists(w => w != null && w.tile == candidate.ToString())) { tile = candidate; break; }
            Undo.RecordObject(target, "Link Warp");
            target.warps.Add(new AreaAsset.Warp { tile = tile.ToString(), to = area.id });
            EditorUtility.SetDirty(target);
            bool painted = new OverworldMap(target.map).FindAll(tile).Count > 0;
            ShowNotification(new GUIContent(painted
                ? "'" + tile + "' in " + target.id + " now leads back here."
                : "Added. Now paint a '" + tile + "' on " + target.id + "'s map where you arrive."));
        }

        // LEVEL DESIGNER > CELL > FIGHT

        void DrawEncounter(int index)
        {
            var list = areaSO.FindProperty("encounters");
            GUILayout.Label("Fight " + (index + 1) + " (in reading order)", EditorStyles.miniBoldLabel);
            if (index >= list.arraySize)
            {
                EditorGUILayout.HelpBox("No fight set up: this is the shared practice fight.", MessageType.None);
                if (GUILayout.Button("Set up this fight"))
                    while (list.arraySize <= index)
                    {
                        // A new array element copies the one before it, so every field is reset.
                        list.arraySize++;
                        var fresh = list.GetArrayElementAtIndex(list.arraySize - 1);
                        fresh.FindPropertyRelative("Enemies").arraySize = 0;
                        fresh.FindPropertyRelative("Level").intValue = BattleUnit.DefaultEnemyLevel;
                        fresh.FindPropertyRelative("Boss").boolValue = false;
                        fresh.FindPropertyRelative("Requires").arraySize = 0;
                        fresh.FindPropertyRelative("Blocks").arraySize = 0;
                        foreach (var field in new[] { "WinFlag", "WinMessage", "WinKey", "Reward" })
                            fresh.FindPropertyRelative(field).stringValue = "";
                    }
                return;
            }
            var def = list.GetArrayElementAtIndex(index);
            var enemies = def.FindPropertyRelative("Enemies");
            var bestiary = Resources.Load<EnemyCatalog>("Bestiary");
            var names = bestiary != null ? bestiary.enemies.Where(e => e != null).Select(e => e.name).ToArray() : new string[0];
            GUILayout.Label("Enemies (none = the practice fight)");
            for (int i = 0; i < enemies.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var e = enemies.GetArrayElementAtIndex(i);
                e.stringValue = Popup("", e.stringValue, names);
                if (GUILayout.Button("x", GUILayout.Width(22f)))
                {
                    enemies.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Enemy"))
            {
                enemies.arraySize++;
                enemies.GetArrayElementAtIndex(enemies.arraySize - 1).stringValue = names.Length > 0 ? names[0] : "";
            }
            EditorGUILayout.PropertyField(def.FindPropertyRelative("Level"));
            EditorGUILayout.PropertyField(def.FindPropertyRelative("Boss"));
            var reward = def.FindPropertyRelative("Reward");
            reward.stringValue = Popup("Reward Part", reward.stringValue, PartNames());
            EditorGUILayout.PropertyField(def.FindPropertyRelative("WinFlag"), new GUIContent("Win sets flag"));
            EditorGUILayout.PropertyField(def.FindPropertyRelative("Requires"), new GUIContent("Only if flags"), true);
            EditorGUILayout.PropertyField(def.FindPropertyRelative("Blocks"), new GUIContent("Gone once flags"), true);
            EditorGUILayout.PropertyField(def.FindPropertyRelative("WinKey"), new GUIContent("Win story key"));
            EditorGUILayout.PropertyField(def.FindPropertyRelative("WinMessage"), new GUIContent("Win line"));
            EditorGUILayout.HelpBox("A Reward is for one-off story fights (patrols come back at Access Points). Win story key: a "
                + "Resources/Story block said after winning (beats the Win line).", MessageType.None);
        }

        // LEVEL DESIGNER > CELL > NPC AND CHEST

        void DrawNpc(int index)
        {
            var list = areaSO.FindProperty("npcs");
            GUILayout.Label("NPC " + (index + 1) + " (in reading order)", EditorStyles.miniBoldLabel);
            if (index >= list.arraySize)
            {
                EditorGUILayout.HelpBox("Not set up: the old Mossline cast stands here.", MessageType.None);
                if (GUILayout.Button("Set up this NPC"))
                    while (list.arraySize <= index)
                    {
                        list.arraySize++;
                        var fresh = list.GetArrayElementAtIndex(list.arraySize - 1);
                        fresh.FindPropertyRelative("Name").stringValue = "Stranger";
                        fresh.FindPropertyRelative("LinesKey").stringValue = "";
                        fresh.FindPropertyRelative("Lines").arraySize = 0;
                        fresh.FindPropertyRelative("Machine").boolValue = false;
                    }
                return;
            }
            var npc = list.GetArrayElementAtIndex(index);
            EditorGUILayout.PropertyField(npc.FindPropertyRelative("Name"));
            EditorGUILayout.PropertyField(npc.FindPropertyRelative("Machine"), new GUIContent("Machine look"));
            EditorGUILayout.PropertyField(npc.FindPropertyRelative("LinesKey"), new GUIContent("Story key"));
            EditorGUILayout.PropertyField(npc.FindPropertyRelative("Lines"), new GUIContent("Lines (if no key)"), true);
            EditorGUILayout.HelpBox("Story key: a block in Resources/Story (e.g. npc.small_machine), so the words live with the rest of the script.", MessageType.None);
        }

        void DrawChest(int index)
        {
            var list = areaSO.FindProperty("chests");
            GUILayout.Label("Chest " + (index + 1) + " (in reading order)", EditorStyles.miniBoldLabel);
            if (index >= list.arraySize)
            {
                if (!GUILayout.Button("Set up this chest")) return;
                int before = list.arraySize;
                list.arraySize = index + 1;
                for (int i = before; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).stringValue = "";
            }
            var part = list.GetArrayElementAtIndex(index);
            part.stringValue = Popup("Part inside", part.stringValue, PartNames());
        }

        static string[] PartNames()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PartCatalog>(PartCatalogPath);
            return catalog != null ? catalog.parts.Where(p => p != null).Select(p => p.name).ToArray() : new string[0];
        }

        /// <summary>A dropdown of <paramref name="options"/> plus "(none)"; a value not in the list is kept and shown.</summary>
        static string Popup(string label, string value, string[] options)
        {
            var list = new List<string> { "(none)" };
            list.AddRange(options);
            if (!string.IsNullOrEmpty(value) && !list.Contains(value)) list.Add(value);
            int index = string.IsNullOrEmpty(value) ? 0 : list.IndexOf(value);
            index = label.Length > 0 ? EditorGUILayout.Popup(label, index, list.ToArray()) : EditorGUILayout.Popup(index, list.ToArray());
            return index <= 0 ? "" : list[index];
        }

        // LEVEL DESIGNER > PLAY THIS AREA
        // Plays the overworld scene starting at this area's spawn (skipping the title), with the latest save's party.

        void PlayArea()
        {
            if (string.IsNullOrEmpty(area.id)) return;
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(WorldSession.PlayAreaKey, area.id);
            // Back in edit mode, PlayFromTitle puts the usual start scene back.
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(OverworldScene);
            EditorApplication.isPlaying = true;
        }
    }
}
