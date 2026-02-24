using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MessEditorWindow : EditorWindow
{
    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private List<MessBlueprint> _allBlueprints = new();
    private MessBlueprint _selected;
    private Editor _embeddedEditor;

    private string _searchFilter = "";
    private int _categoryFilterIndex;   // 0=All, 1=DateAftermath, 2=OffScreen, 3=General
    private int _typeFilterIndex;       // 0=All, 1=Stain, 2=Object
    private bool _showGizmos = true;

    private Vector2 _leftScroll;
    private Vector2 _rightScroll;

    private static readonly string[] CategoryFilterLabels = { "All", "DateAftermath", "OffScreen", "General" };
    private static readonly string[] TypeFilterLabels     = { "All", "Stain", "Object" };

    private const float LeftPanelWidth = 360f;
    private const float ToolbarHeight  = 24f;
    private const string BlueprintFolder = "Assets/ScriptableObjects/Messes";

    // ─────────────────────────────────────────────
    //  Menu Item
    // ─────────────────────────────────────────────

    [MenuItem("Window/Iris/Mess Editor")]
    public static void ShowWindow()
    {
        var win = GetWindow<MessEditorWindow>("Mess Editor");
        win.minSize = new Vector2(720, 400);
    }

    // ─────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        RefreshBlueprints();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyEmbeddedEditor();
    }

    private void OnFocus()
    {
        RefreshBlueprints();
    }

    // ─────────────────────────────────────────────
    //  Blueprint Discovery
    // ─────────────────────────────────────────────

    private void RefreshBlueprints()
    {
        _allBlueprints.Clear();

        string[] guids = AssetDatabase.FindAssets("t:MessBlueprint");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var bp = AssetDatabase.LoadAssetAtPath<MessBlueprint>(path);
            if (bp != null) _allBlueprints.Add(bp);
        }

        _allBlueprints.Sort((a, b) =>
        {
            int catCmp = a.category.CompareTo(b.category);
            if (catCmp != 0) return catCmp;
            return string.Compare(a.messName, b.messName, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    // ─────────────────────────────────────────────
    //  Filtering
    // ─────────────────────────────────────────────

    private bool PassesFilter(MessBlueprint bp)
    {
        // Category filter
        if (_categoryFilterIndex > 0)
        {
            var cat = (MessBlueprint.MessCategory)(_categoryFilterIndex - 1);
            if (bp.category != cat) return false;
        }

        // Type filter
        if (_typeFilterIndex > 0)
        {
            var type = (MessBlueprint.MessType)(_typeFilterIndex - 1);
            if (bp.messType != type) return false;
        }

        // Search filter
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            if (bp.messName.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) < 0
                && bp.description.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    // ─────────────────────────────────────────────
    //  GUI
    // ─────────────────────────────────────────────

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ── Toolbar ──

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Search
        GUILayout.Label("Search:", GUILayout.Width(48));
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(140));

        GUILayout.Space(8);

        // Category dropdown
        GUILayout.Label("Category:", GUILayout.Width(56));
        _categoryFilterIndex = EditorGUILayout.Popup(_categoryFilterIndex, CategoryFilterLabels,
            EditorStyles.toolbarPopup, GUILayout.Width(100));

        // Type dropdown
        GUILayout.Label("Type:", GUILayout.Width(34));
        _typeFilterIndex = EditorGUILayout.Popup(_typeFilterIndex, TypeFilterLabels,
            EditorStyles.toolbarPopup, GUILayout.Width(70));

        GUILayout.Space(8);

        // Gizmo toggle
        _showGizmos = GUILayout.Toggle(_showGizmos, "Gizmos", EditorStyles.toolbarButton, GUILayout.Width(56));

        GUILayout.FlexibleSpace();

        // Blueprint count
        int visibleCount = _allBlueprints.Count(PassesFilter);
        GUILayout.Label($"{visibleCount}/{_allBlueprints.Count} blueprints", EditorStyles.miniLabel);

        GUILayout.Space(8);

        // Refresh
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(56)))
            RefreshBlueprints();

        EditorGUILayout.EndHorizontal();
    }

    // ── Left Panel ──

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));

        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

        // Group by category
        DrawGroup("General (Day 1+)", MessBlueprint.MessCategory.General);
        DrawGroup("Date Aftermath", MessBlueprint.MessCategory.DateAftermath);
        DrawGroup("Off-Screen", MessBlueprint.MessCategory.OffScreen);

        EditorGUILayout.EndScrollView();

        // New blueprint button
        DrawNewBlueprintButton();

        EditorGUILayout.EndVertical();
    }

    private void DrawGroup(string header, MessBlueprint.MessCategory category)
    {
        var groupItems = _allBlueprints.Where(bp => bp.category == category && PassesFilter(bp)).ToList();
        if (groupItems.Count == 0) return;

        EditorGUILayout.Space(4);

        // Group header
        Color catColor = MessBlueprintEditor.GetCategoryColor(category);
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = catColor }
        };
        EditorGUILayout.LabelField($"— {header} ({groupItems.Count}) —", headerStyle);

        foreach (var bp in groupItems)
        {
            DrawBlueprintRow(bp, catColor);
        }
    }

    private void DrawBlueprintRow(MessBlueprint bp, Color catColor)
    {
        bool isSelected = _selected == bp;

        // Background highlight for selection
        var rect = EditorGUILayout.BeginHorizontal(
            isSelected ? "selectionRect" : GUIStyle.none,
            GUILayout.Height(22));

        // Category color bar
        var colorBarRect = new Rect(rect.x, rect.y + 2, 4, rect.height - 4);
        EditorGUI.DrawRect(colorBarRect, catColor);

        GUILayout.Space(10);

        // Type indicator
        string typeTag = bp.messType == MessBlueprint.MessType.Stain ? "[S]" : "[O]";
        var typeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            fixedWidth = 24
        };
        GUILayout.Label(typeTag, typeStyle);

        // Name
        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
        };
        GUILayout.Label(bp.messName, nameStyle, GUILayout.MinWidth(120));

        GUILayout.FlexibleSpace();

        // Position status
        bool hasPos = bp.spawnPosition != Vector3.zero;
        var posStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = hasPos ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.9f, 0.6f, 0.3f) }
        };
        GUILayout.Label(hasPos ? "pos" : "no pos", posStyle, GUILayout.Width(36));

        // Weight
        GUILayout.Label($"w{bp.weight:F1}", EditorStyles.miniLabel, GUILayout.Width(30));

        EditorGUILayout.EndHorizontal();

        // Click to select
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            SelectBlueprint(bp);
            Event.current.Use();
        }
    }

    private void SelectBlueprint(MessBlueprint bp)
    {
        _selected = bp;

        // Select in project
        Selection.activeObject = bp;
        EditorGUIUtility.PingObject(bp);

        // Rebuild embedded editor
        DestroyEmbeddedEditor();
        _embeddedEditor = Editor.CreateEditor(bp);

        // Focus scene view
        if (bp.spawnPosition != Vector3.zero)
            MessBlueprintEditor.FocusSceneView(bp.spawnPosition);

        Repaint();
    }

    // ── Right Panel ──

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();

        if (_selected == null)
        {
            EditorGUILayout.Space(40);
            var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 12
            };
            EditorGUILayout.LabelField("Select a blueprint from the list", centeredStyle);
        }
        else
        {
            // Header
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(_selected.messName, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_embeddedEditor != null)
            {
                // Check if target was destroyed (e.g. asset deleted)
                if (_embeddedEditor.target == null)
                {
                    DestroyEmbeddedEditor();
                    _selected = null;
                }
                else
                {
                    _embeddedEditor.OnInspectorGUI();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    // ── New Blueprint ──

    private void DrawNewBlueprintButton()
    {
        EditorGUILayout.Space(4);

        if (GUILayout.Button("+ New Mess Blueprint", GUILayout.Height(28)))
        {
            var menu = new GenericMenu();

            foreach (MessBlueprint.MessCategory cat in System.Enum.GetValues(typeof(MessBlueprint.MessCategory)))
            {
                foreach (MessBlueprint.MessType type in System.Enum.GetValues(typeof(MessBlueprint.MessType)))
                {
                    var catLocal = cat;
                    var typeLocal = type;
                    string label = $"{cat}/{type}";
                    menu.AddItem(new GUIContent(label), false, () => CreateNewBlueprint(catLocal, typeLocal));
                }
            }

            menu.ShowAsContext();
        }

        EditorGUILayout.Space(2);
    }

    private void CreateNewBlueprint(MessBlueprint.MessCategory category, MessBlueprint.MessType type)
    {
        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder(BlueprintFolder))
        {
            string parent = System.IO.Path.GetDirectoryName(BlueprintFolder).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(BlueprintFolder);
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            AssetDatabase.CreateFolder(parent, folderName);
        }

        // Find unique name
        string baseName = $"Mess_New_{type}";
        string path = $"{BlueprintFolder}/{baseName}.asset";
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        var bp = ScriptableObject.CreateInstance<MessBlueprint>();
        bp.category = category;
        bp.messType = type;
        bp.messName = System.IO.Path.GetFileNameWithoutExtension(path).Replace("_", " ");

        AssetDatabase.CreateAsset(bp, path);
        AssetDatabase.SaveAssets();

        RefreshBlueprints();
        SelectBlueprint(bp);

        Debug.Log($"[MessEditorWindow] Created new blueprint: {path}");
    }

    // ─────────────────────────────────────────────
    //  Scene View Gizmos
    // ─────────────────────────────────────────────

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_showGizmos) return;

        foreach (var bp in _allBlueprints)
        {
            if (bp == null) continue;
            if (!PassesFilter(bp)) continue;

            bool isSelected = bp == _selected;
            MessBlueprintEditor.DrawBlueprintGizmo(bp, isSelected);
        }
    }

    // ─────────────────────────────────────────────
    //  Utility
    // ─────────────────────────────────────────────

    private void DestroyEmbeddedEditor()
    {
        if (_embeddedEditor != null)
        {
            DestroyImmediate(_embeddedEditor);
            _embeddedEditor = null;
        }
    }
}
