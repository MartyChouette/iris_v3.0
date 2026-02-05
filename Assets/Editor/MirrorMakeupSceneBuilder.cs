using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that builds a complete mirror makeup prototype scene.
/// Creates SO assets, bathroom geometry, face quads, UI, and wires everything.
/// Menu: Window > Iris > Build Mirror Makeup Scene
/// </summary>
public static class MirrorMakeupSceneBuilder
{
    [MenuItem("Window/Iris/Build Mirror Makeup Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Ensure SO folders ───────────────────────────────────────
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "MirrorMakeup");

        // ── 2. Create ScriptableObject assets ──────────────────────────
        var foundation = CreateToolSO("Foundation",
            MakeupToolDefinition.ToolType.Foundation,
            new Color(0.88f, 0.74f, 0.62f), 0.035f, 0.7f, true,
            false, 0f, 0f, 0f,
            0f, Color.yellow);

        var lipstick = CreateToolSO("Lipstick",
            MakeupToolDefinition.ToolType.Lipstick,
            new Color(0.85f, 0.15f, 0.2f), 0.02f, 0.9f, false,
            true, 0.002f, 2.5f, 0.4f,
            0f, Color.yellow);

        var eyeliner = CreateToolSO("Eyeliner",
            MakeupToolDefinition.ToolType.Eyeliner,
            new Color(0.08f, 0.06f, 0.06f), 0.008f, 1f, false,
            false, 0f, 0f, 0f,
            0f, Color.yellow);

        var starSticker = CreateToolSO("Star Sticker",
            MakeupToolDefinition.ToolType.StarSticker,
            Color.yellow, 0.03f, 1f, false,
            false, 0f, 0f, 0f,
            0.03f, Color.yellow);

        var allTools = new[] { foundation, lipstick, eyeliner, starSticker };
        AssetDatabase.SaveAssets();

        // ── 3. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.color = new Color(1f, 0.95f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(40f, -20f, 0f);

        // ── 4. Main Camera ─────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.18f, 0.22f);
        cam.fieldOfView = 40f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 1.5f, -0.8f);
        camGO.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        // ── 5. Bathroom geometry ───────────────────────────────────────
        var roomParent = new GameObject("Room");

        // Back wall (light blue/white tiles)
        CreateBox("BackWall", roomParent.transform,
            new Vector3(0f, 1.5f, 0.6f), new Vector3(3f, 3f, 0.1f),
            new Color(0.78f, 0.85f, 0.88f));

        // Side walls
        CreateBox("LeftWall", roomParent.transform,
            new Vector3(-1.55f, 1.5f, 0f), new Vector3(0.1f, 3f, 2f),
            new Color(0.75f, 0.82f, 0.85f));

        CreateBox("RightWall", roomParent.transform,
            new Vector3(1.55f, 1.5f, 0f), new Vector3(0.1f, 3f, 2f),
            new Color(0.75f, 0.82f, 0.85f));

        // Floor
        CreateBox("Floor", roomParent.transform,
            new Vector3(0f, 0f, 0f), new Vector3(3f, 0.1f, 2f),
            new Color(0.55f, 0.55f, 0.52f));

        // Mirror frame (dark border)
        CreateBox("MirrorFrame", roomParent.transform,
            new Vector3(0f, 1.6f, 0.5f), new Vector3(1.0f, 1.2f, 0.05f),
            new Color(0.25f, 0.15f, 0.1f));

        // Mirror surface (reflective-ish — light grey)
        CreateBox("MirrorSurface", roomParent.transform,
            new Vector3(0f, 1.6f, 0.47f), new Vector3(0.9f, 1.1f, 0.01f),
            new Color(0.75f, 0.8f, 0.82f));

        // Shelf below mirror
        CreateBox("Shelf", roomParent.transform,
            new Vector3(0f, 1.0f, 0.45f), new Vector3(1.0f, 0.04f, 0.2f),
            new Color(0.4f, 0.3f, 0.2f));

        // ── 6. Tool visuals on shelf ───────────────────────────────────
        // Foundation (beige box)
        CreateBox("Tool_Foundation", roomParent.transform,
            new Vector3(-0.3f, 1.05f, 0.45f), new Vector3(0.06f, 0.08f, 0.06f),
            new Color(0.88f, 0.74f, 0.62f));

        // Lipstick (red box)
        CreateBox("Tool_Lipstick", roomParent.transform,
            new Vector3(-0.1f, 1.06f, 0.45f), new Vector3(0.025f, 0.10f, 0.025f),
            new Color(0.85f, 0.15f, 0.2f));

        // Eyeliner (thin black box)
        CreateBox("Tool_Eyeliner", roomParent.transform,
            new Vector3(0.1f, 1.04f, 0.45f), new Vector3(0.015f, 0.12f, 0.015f),
            new Color(0.08f, 0.06f, 0.06f));

        // Star stickers (yellow box)
        CreateBox("Tool_StarSticker", roomParent.transform,
            new Vector3(0.3f, 1.04f, 0.45f), new Vector3(0.08f, 0.06f, 0.08f),
            new Color(1f, 0.9f, 0.2f));

        // ── 7. Face layer setup ────────────────────────────────────────
        // Ensure "Face" layer exists (use layer 8 if available)
        int faceLayerIndex = EnsureLayer("Face");

        // ── 8. Head parent + face quads ────────────────────────────────
        var headParent = new GameObject("HeadParent");
        headParent.transform.position = new Vector3(0f, 1.6f, 0.2f);

        // Base face quad (opaque, skin + features + pimples)
        var baseQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        baseQuad.name = "FaceBase";
        baseQuad.transform.SetParent(headParent.transform);
        baseQuad.transform.localPosition = Vector3.zero;
        baseQuad.transform.localScale = new Vector3(0.6f, 0.8f, 1f);
        baseQuad.transform.localRotation = Quaternion.identity;
        baseQuad.layer = faceLayerIndex;
        baseQuad.isStatic = false;

        // Overlay quad (transparent painting surface, slightly in front)
        var overlayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlayQuad.name = "FaceOverlay";
        overlayQuad.transform.SetParent(headParent.transform);
        overlayQuad.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        overlayQuad.transform.localScale = new Vector3(0.6f, 0.8f, 1f);
        overlayQuad.transform.localRotation = Quaternion.identity;
        overlayQuad.layer = faceLayerIndex;
        overlayQuad.isStatic = false;

        // Add collider on overlay for raycasting
        // Quad already has a MeshCollider — replace with BoxCollider for reliability
        var existingOverlayCollider = overlayQuad.GetComponent<Collider>();
        if (existingOverlayCollider != null)
            Object.DestroyImmediate(existingOverlayCollider);
        overlayQuad.AddComponent<BoxCollider>();

        // FaceCanvas component on the head parent
        var faceCanvas = headParent.AddComponent<FaceCanvas>();
        var faceCanvasSO = new SerializedObject(faceCanvas);
        faceCanvasSO.FindProperty("_baseRenderer").objectReferenceValue = baseQuad.GetComponent<Renderer>();
        faceCanvasSO.FindProperty("_overlayRenderer").objectReferenceValue = overlayQuad.GetComponent<Renderer>();
        faceCanvasSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 9. Managers GO ─────────────────────────────────────────────
        var managersGO = new GameObject("Managers");
        var headCtrl = managersGO.AddComponent<HeadController>();
        var mgr = managersGO.AddComponent<MirrorMakeupManager>();
        var hud = managersGO.AddComponent<MirrorMakeupHUD>();

        // Wire HeadController
        var headCtrlSO = new SerializedObject(headCtrl);
        headCtrlSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        headCtrlSO.FindProperty("_headTransform").objectReferenceValue = headParent.transform;
        headCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire Manager
        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("_faceCanvas").objectReferenceValue = faceCanvas;
        mgrSO.FindProperty("_headController").objectReferenceValue = headCtrl;
        mgrSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("_hud").objectReferenceValue = hud;
        mgrSO.FindProperty("_faceLayer").intValue = 1 << faceLayerIndex;

        // Tools array
        var toolsProp = mgrSO.FindProperty("_tools");
        toolsProp.ClearArray();
        for (int i = 0; i < allTools.Length; i++)
        {
            toolsProp.InsertArrayElementAtIndex(i);
            toolsProp.GetArrayElementAtIndex(i).objectReferenceValue = allTools[i];
        }
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 10. UI Canvas ──────────────────────────────────────────────
        var canvasGO = CreateScreenCanvas("MirrorMakeupUI_Canvas", managersGO.transform);

        // Tool name (top-center)
        var toolNameLabel = CreateLabel("ToolNameLabel", canvasGO.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(300f, 40f),
            "Inspect Mode", 24f, TextAlignmentOptions.Center);

        // Instruction hint (bottom-center)
        var instructionLabel = CreateLabel("InstructionLabel", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(500f, 30f),
            "Move mouse to look around — find all your pimples!", 16f, TextAlignmentOptions.Center);

        // Pimple counter (top-right)
        var pimpleCountLabel = CreateLabel("PimpleCountLabel", canvasGO.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-100f, -30f), new Vector2(200f, 30f),
            "Pimples: 0/12 covered", 18f, TextAlignmentOptions.Right);

        // ── Tool selection buttons (left side, vertical stack) ─────────
        var toolPanelGO = new GameObject("ToolButtonPanel");
        toolPanelGO.transform.SetParent(canvasGO.transform);
        var toolPanelRT = toolPanelGO.AddComponent<RectTransform>();
        toolPanelRT.anchorMin = new Vector2(0f, 0.5f);
        toolPanelRT.anchorMax = new Vector2(0f, 0.5f);
        toolPanelRT.pivot = new Vector2(0f, 0.5f);
        toolPanelRT.anchoredPosition = new Vector2(20f, 0f);
        toolPanelRT.sizeDelta = new Vector2(140f, 300f);
        toolPanelRT.localScale = Vector3.one;

        string[] toolNames = { "Foundation", "Lipstick", "Eyeliner", "Star Sticker" };
        Color[] toolBtnColors =
        {
            new Color(0.88f, 0.74f, 0.62f),
            new Color(0.85f, 0.15f, 0.2f),
            new Color(0.25f, 0.25f, 0.25f),
            new Color(1f, 0.9f, 0.2f)
        };

        var toolButtonsList = new UnityEngine.UI.Button[allTools.Length];
        for (int i = 0; i < allTools.Length; i++)
        {
            float yOffset = 80f - i * 50f;
            var btn = BuildToolButton(toolPanelGO.transform, toolNames[i], i, mgr,
                new Vector2(0f, yOffset), toolBtnColors[i]);
            toolButtonsList[i] = btn;
        }

        // Inspect button (deselect)
        float inspectY = 80f - allTools.Length * 50f;
        var inspectBtn = BuildToolButton(toolPanelGO.transform, "Inspect", -1, mgr,
            new Vector2(0f, inspectY), new Color(0.4f, 0.4f, 0.5f));

        // ── Wire HUD ───────────────────────────────────────────────────
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("toolNameLabel").objectReferenceValue = toolNameLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("instructionLabel").objectReferenceValue = instructionLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("pimpleCountLabel").objectReferenceValue = pimpleCountLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("toolButtonPanel").objectReferenceValue = toolPanelGO;
        hudSO.FindProperty("inspectButton").objectReferenceValue = inspectBtn;

        // Wire tool buttons array
        var toolBtnsProp = hudSO.FindProperty("toolButtons");
        toolBtnsProp.ClearArray();
        for (int i = 0; i < toolButtonsList.Length; i++)
        {
            toolBtnsProp.InsertArrayElementAtIndex(i);
            toolBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = toolButtonsList[i];
        }
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 11. Save scene ─────────────────────────────────────────────
        EnsureFolder("Assets", "Scenes");
        string path = "Assets/Scenes/mirror_makeup.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[MirrorMakeupSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // SO creation helper
    // ════════════════════════════════════════════════════════════════════

    private static MakeupToolDefinition CreateToolSO(
        string name, MakeupToolDefinition.ToolType toolType,
        Color brushColor, float brushRadius, float opacity, bool softEdge,
        bool canSmear, float smearThreshold, float smearWidth, float smearFalloff,
        float starSize, Color starColor)
    {
        var so = ScriptableObject.CreateInstance<MakeupToolDefinition>();
        so.toolName = name;
        so.toolType = toolType;
        so.brushColor = brushColor;
        so.brushRadius = brushRadius;
        so.opacity = opacity;
        so.softEdge = softEdge;
        so.canSmear = canSmear;
        so.smearSpeedThreshold = smearThreshold;
        so.smearWidthMultiplier = smearWidth;
        so.smearOpacityFalloff = smearFalloff;
        so.starSize = starSize;
        so.starColor = starColor;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/MirrorMakeup/Tool_{name.Replace(" ", "_")}.asset");
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    // ════════════════════════════════════════════════════════════════════
    // UI builders
    // ════════════════════════════════════════════════════════════════════

    private static UnityEngine.UI.Button BuildToolButton(Transform parent, string label,
        int toolIndex, MirrorMakeupManager mgr, Vector2 position, Color tintColor)
    {
        var btnGO = new GameObject($"Btn_{label.Replace(" ", "")}");
        btnGO.transform.SetParent(parent);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 40f);
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;

        var img = btnGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.25f, 0.25f, 0.35f);

        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();

        // Wire onClick
        if (toolIndex >= 0)
        {
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                btn.onClick, mgr.SelectTool, toolIndex);
        }
        else
        {
            // Inspect = deselect
            var action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), mgr,
                typeof(MirrorMakeupManager).GetMethod(nameof(MirrorMakeupManager.DeselectTool),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // Colour indicator (small square to the left)
        var indicatorGO = new GameObject("ColorIndicator");
        indicatorGO.transform.SetParent(btnGO.transform);
        var indicatorRT = indicatorGO.AddComponent<RectTransform>();
        indicatorRT.anchorMin = new Vector2(0f, 0.5f);
        indicatorRT.anchorMax = new Vector2(0f, 0.5f);
        indicatorRT.pivot = new Vector2(0f, 0.5f);
        indicatorRT.anchoredPosition = new Vector2(4f, 0f);
        indicatorRT.sizeDelta = new Vector2(14f, 14f);
        indicatorRT.localScale = Vector3.one;
        var indicatorImg = indicatorGO.AddComponent<UnityEngine.UI.Image>();
        indicatorImg.color = tintColor;

        // Label text
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(22f, 0f);
        labelRT.offsetMax = Vector2.zero;
        labelRT.localScale = Vector3.one;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;

        return btn;
    }

    // ════════════════════════════════════════════════════════════════════
    // Shared helpers (matching project pattern)
    // ════════════════════════════════════════════════════════════════════

    private static GameObject CreateBox(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.isStatic = true;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }
        return go;
    }

    private static GameObject CreateScreenCanvas(string name, Transform parent)
    {
        var canvasGO = new GameObject(name);
        canvasGO.transform.SetParent(parent);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        return canvasGO;
    }

    private static GameObject CreateLabel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;

        return go;
    }

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        if (!AssetDatabase.IsValidFolder($"{parentFolder}/{newFolder}"))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }
    }

    /// <summary>
    /// Ensures a named layer exists in the TagManager. Returns the layer index.
    /// Uses the first available slot (8-31) if the layer doesn't already exist.
    /// </summary>
    private static int EnsureLayer(string layerName)
    {
        // Check if it already exists
        for (int i = 0; i < 32; i++)
        {
            string existing = LayerMask.LayerToName(i);
            if (existing == layerName) return i;
        }

        // Find first empty user layer (8+) and assign it
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[MirrorMakeupSceneBuilder] Created layer '{layerName}' at index {i}");
                return i;
            }
        }

        Debug.LogWarning($"[MirrorMakeupSceneBuilder] No empty layer slot available for '{layerName}', using Default (0)");
        return 0;
    }
}
