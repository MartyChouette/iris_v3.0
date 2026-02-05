using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that builds the expanded kitchen cleaning prototype scene:
/// counter with multiple spills, potted plant with dusty leaves, sponge + spray
/// bottle tools, and full UI. Creates SpillDefinition SO assets.
/// Menu: Window > Iris > Build Cleaning Scene
/// </summary>
public static class CleaningSceneBuilder
{
    private const string CleanableLayerName = "Cleanable";

    [MenuItem("Window/Iris/Build Cleaning Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int cleanableLayer = EnsureLayer(CleanableLayerName);

        // ── 1. Ensure SO folders ───────────────────────────────────────
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "Cleaning");

        // ── 2. Create SpillDefinition SO assets ────────────────────────
        var coffeeSpill = CreateSpillSO("Coffee Spill",
            SpillDefinition.SpillType.Liquid, new Color(0.35f, 0.20f, 0.08f),
            0.35f, 42, 256, 0.1f);

        var counterGrime = CreateSpillSO("Counter Grime",
            SpillDefinition.SpillType.Dirt, new Color(0.45f, 0.42f, 0.38f),
            0.40f, 77, 256, 0.8f);

        var sauceStain = CreateSpillSO("Sauce Stain",
            SpillDefinition.SpillType.Mixed, new Color(0.65f, 0.18f, 0.12f),
            0.30f, 123, 256, 0.5f);

        var leafDust1 = CreateSpillSO("Leaf Dust 1",
            SpillDefinition.SpillType.Dirt, new Color(0.28f, 0.32f, 0.22f),
            0.60f, 200, 256, 0.9f);

        var leafDust2 = CreateSpillSO("Leaf Dust 2",
            SpillDefinition.SpillType.Dirt, new Color(0.30f, 0.34f, 0.24f),
            0.55f, 215, 256, 0.85f);

        AssetDatabase.SaveAssets();

        // ── 3. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 4. Main Camera ─────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        cam.fieldOfView = 60f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 1.4f, -0.3f);
        camGO.transform.rotation = Quaternion.Euler(70f, 0f, 0f);

        // ── 5. Room geometry ───────────────────────────────────────────
        BuildRoom();

        // ── 6. Counter + spill surfaces ────────────────────────────────
        var counterSurfaces = BuildCounter(cleanableLayer, coffeeSpill, counterGrime, sauceStain);

        // ── 7. Potted plant with dusty leaves ──────────────────────────
        var plantSurfaces = BuildPlant(cleanableLayer, leafDust1, leafDust2);

        // ── 8. Tool visuals ────────────────────────────────────────────
        var (spongeVisual, sprayVisual) = BuildToolVisuals();

        // ── 9. All surfaces array ──────────────────────────────────────
        var allSurfaces = new CleanableSurface[counterSurfaces.Length + plantSurfaces.Length];
        counterSurfaces.CopyTo(allSurfaces, 0);
        plantSurfaces.CopyTo(allSurfaces, counterSurfaces.Length);

        // ── 10. Managers GO ────────────────────────────────────────────
        var managersGO = new GameObject("Managers");
        var mgr = managersGO.AddComponent<CleaningManager>();
        var hud = managersGO.AddComponent<CleaningHUD>();

        // Wire Manager
        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("_hud").objectReferenceValue = hud;
        mgrSO.FindProperty("_spongeVisual").objectReferenceValue = spongeVisual;
        mgrSO.FindProperty("_sprayVisual").objectReferenceValue = sprayVisual;
        mgrSO.FindProperty("_cleanableLayer").intValue = 1 << cleanableLayer;

        var surfacesProp = mgrSO.FindProperty("_surfaces");
        surfacesProp.ClearArray();
        for (int i = 0; i < allSurfaces.Length; i++)
        {
            surfacesProp.InsertArrayElementAtIndex(i);
            surfacesProp.GetArrayElementAtIndex(i).objectReferenceValue = allSurfaces[i];
        }
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 11. UI Canvas ──────────────────────────────────────────────
        var canvasGO = CreateScreenCanvas("CleaningUI_Canvas", managersGO.transform);

        // Tool name (top-center)
        var toolNameLabel = CreateLabel("ToolNameLabel", canvasGO.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -20f), new Vector2(300f, 40f),
            "Sponge", 24f, TextAlignmentOptions.Center);

        // Instructions (bottom-center)
        var instructionLabel = CreateLabel("InstructionLabel", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(500f, 30f),
            "Move mouse over a dirty surface", 16f, TextAlignmentOptions.Center);

        // Progress (top-right)
        var progressLabel = CreateLabel("ProgressLabel", canvasGO.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-100f, -20f), new Vector2(200f, 30f),
            "Overall: 0%", 18f, TextAlignmentOptions.Right);

        // Surface details (right side, below progress)
        var surfaceDetailLabel = CreateLabel("SurfaceDetailLabel", canvasGO.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-100f, -100f), new Vector2(200f, 160f),
            "", 14f, TextAlignmentOptions.TopRight);

        // ── Tool selection buttons (bottom-right) ──────────────────────
        var spongeBtn = BuildToolButton(canvasGO.transform, "Sponge", mgr, true,
            new Vector2(1f, 0f), new Vector2(-130f, 50f),
            new Color(0.35f, 0.60f, 0.25f));

        var sprayBtn = BuildToolButton(canvasGO.transform, "Spray Bottle", mgr, false,
            new Vector2(1f, 0f), new Vector2(-130f, 100f),
            new Color(0.25f, 0.45f, 0.70f));

        // ── Completion panel (center, starts inactive) ─────────────────
        var completionPanel = BuildCompletionPanel(canvasGO.transform);

        // ── Wire HUD ───────────────────────────────────────────────────
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("toolNameLabel").objectReferenceValue = toolNameLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("instructionLabel").objectReferenceValue = instructionLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("progressLabel").objectReferenceValue = progressLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("surfaceDetailLabel").objectReferenceValue = surfaceDetailLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("spongeButton").objectReferenceValue = spongeBtn;
        hudSO.FindProperty("sprayButton").objectReferenceValue = sprayBtn;
        hudSO.FindProperty("completionPanel").objectReferenceValue = completionPanel;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 12. Save scene ─────────────────────────────────────────────
        EnsureFolder("Assets", "Scenes");
        string path = "Assets/Scenes/cleaning_scene.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[CleaningSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Room
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        CreateBox("Floor", parent.transform,
            new Vector3(0f, 0f, 0f), new Vector3(3f, 0.1f, 3f),
            new Color(0.25f, 0.25f, 0.25f));

        CreateBox("Wall_Back", parent.transform,
            new Vector3(0f, 1f, 1.5f), new Vector3(3f, 2f, 0.1f),
            new Color(0.75f, 0.73f, 0.70f));

        CreateBox("Wall_Left", parent.transform,
            new Vector3(-1.5f, 1f, 0f), new Vector3(0.1f, 2f, 3f),
            new Color(0.72f, 0.70f, 0.67f));

        CreateBox("Wall_Right", parent.transform,
            new Vector3(1.5f, 1f, 0f), new Vector3(0.1f, 2f, 3f),
            new Color(0.72f, 0.70f, 0.67f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Counter + spill surfaces
    // ════════════════════════════════════════════════════════════════════

    private static CleanableSurface[] BuildCounter(int layer,
        SpillDefinition coffee, SpillDefinition grime, SpillDefinition sauce)
    {
        var parent = new GameObject("Counter");

        // Countertop
        CreateBox("Countertop", parent.transform,
            new Vector3(0f, 0.75f, 0.3f), new Vector3(1.8f, 0.06f, 0.7f),
            new Color(0.82f, 0.80f, 0.76f));

        // 3 spill surfaces on counter
        var s0 = BuildCleanablePair("Coffee", parent.transform, layer, coffee,
            new Vector3(-0.4f, 0.781f, 0.2f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.35f, 0.35f, 1f));

        var s1 = BuildCleanablePair("Grime", parent.transform, layer, grime,
            new Vector3(0f, 0.781f, 0.35f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.35f, 0.35f, 1f));

        var s2 = BuildCleanablePair("Sauce", parent.transform, layer, sauce,
            new Vector3(0.35f, 0.781f, 0.15f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.30f, 0.30f, 1f));

        return new[] { s0, s1, s2 };
    }

    // ════════════════════════════════════════════════════════════════════
    // Potted plant
    // ════════════════════════════════════════════════════════════════════

    private static CleanableSurface[] BuildPlant(int layer,
        SpillDefinition leafDust1, SpillDefinition leafDust2)
    {
        var parent = new GameObject("PottedPlant");

        // Pot (brown box)
        var pot = CreateBox("Pot", parent.transform,
            new Vector3(0.65f, 0.781f, 0.5f), new Vector3(0.12f, 0.08f, 0.12f),
            new Color(0.55f, 0.32f, 0.18f));
        pot.isStatic = false;

        // Stem (thin green box)
        var stem = CreateBox("Stem", parent.transform,
            new Vector3(0.65f, 0.85f, 0.5f), new Vector3(0.015f, 0.10f, 0.015f),
            new Color(0.25f, 0.55f, 0.20f));
        stem.isStatic = false;

        // Leaf 1 with dust
        var leaf1 = BuildCleanablePair("Leaf1", parent.transform, layer, leafDust1,
            new Vector3(0.58f, 0.92f, 0.5f), Quaternion.Euler(30f, 45f, 0f),
            new Vector3(0.12f, 0.12f, 1f));

        // Leaf 2 with dust
        var leaf2 = BuildCleanablePair("Leaf2", parent.transform, layer, leafDust2,
            new Vector3(0.72f, 0.90f, 0.48f), Quaternion.Euler(-20f, -30f, 10f),
            new Vector3(0.10f, 0.10f, 1f));

        return new[] { leaf1, leaf2 };
    }

    // ════════════════════════════════════════════════════════════════════
    // Cleanable surface pair builder (dirt quad + wet quad + component)
    // ════════════════════════════════════════════════════════════════════

    private static CleanableSurface BuildCleanablePair(string name, Transform parent,
        int layer, SpillDefinition definition,
        Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // Parent GO holds the CleanableSurface component + collider
        var surfaceGO = new GameObject($"Surface_{name}");
        surfaceGO.transform.SetParent(parent);
        surfaceGO.transform.position = position;
        surfaceGO.transform.rotation = rotation;
        surfaceGO.transform.localScale = scale;
        surfaceGO.layer = layer;
        surfaceGO.isStatic = false;

        // Dirt quad
        var dirtQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dirtQuad.name = "DirtQuad";
        dirtQuad.transform.SetParent(surfaceGO.transform);
        dirtQuad.transform.localPosition = Vector3.zero;
        dirtQuad.transform.localRotation = Quaternion.identity;
        dirtQuad.transform.localScale = Vector3.one;
        dirtQuad.layer = layer;
        dirtQuad.isStatic = false;

        // KEEP MeshCollider on dirt quad — required for textureCoord
        // (Quad primitive comes with a MeshCollider)

        // Wet overlay quad (slightly above dirt)
        var wetQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wetQuad.name = "WetQuad";
        wetQuad.transform.SetParent(surfaceGO.transform);
        wetQuad.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        wetQuad.transform.localRotation = Quaternion.identity;
        wetQuad.transform.localScale = Vector3.one;
        wetQuad.layer = layer;
        wetQuad.isStatic = false;

        // Remove collider from wet quad (raycast should hit dirt quad)
        var wetCollider = wetQuad.GetComponent<Collider>();
        if (wetCollider != null)
            Object.DestroyImmediate(wetCollider);

        // Add CleanableSurface component
        var surface = surfaceGO.AddComponent<CleanableSurface>();

        // Wire serialized fields
        var so = new SerializedObject(surface);
        so.FindProperty("_definition").objectReferenceValue = definition;
        so.FindProperty("_dirtRenderer").objectReferenceValue = dirtQuad.GetComponent<Renderer>();
        so.FindProperty("_wetRenderer").objectReferenceValue = wetQuad.GetComponent<Renderer>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return surface;
    }

    // ════════════════════════════════════════════════════════════════════
    // Tool visuals
    // ════════════════════════════════════════════════════════════════════

    private static (Transform sponge, Transform spray) BuildToolVisuals()
    {
        var parent = new GameObject("ToolVisuals");

        // Sponge — yellow box
        var sponge = CreateBox("SpongeVisual", parent.transform,
            Vector3.zero, new Vector3(0.06f, 0.02f, 0.04f),
            new Color(0.95f, 0.85f, 0.20f));
        sponge.isStatic = false;
        sponge.SetActive(false);

        // Spray bottle — blue body + grey nozzle
        var sprayParent = new GameObject("SprayVisual");
        sprayParent.transform.SetParent(parent.transform);
        sprayParent.transform.position = Vector3.zero;
        sprayParent.SetActive(false);

        var sprayBody = CreateBox("Body", sprayParent.transform,
            Vector3.zero, new Vector3(0.03f, 0.08f, 0.03f),
            new Color(0.3f, 0.5f, 0.85f));
        sprayBody.isStatic = false;

        var sprayNozzle = CreateBox("Nozzle", sprayParent.transform,
            new Vector3(0.02f, 0.04f, 0f), new Vector3(0.04f, 0.015f, 0.015f),
            new Color(0.5f, 0.5f, 0.5f));
        sprayNozzle.isStatic = false;

        return (sponge.transform, sprayParent.transform);
    }

    // ════════════════════════════════════════════════════════════════════
    // UI builders
    // ════════════════════════════════════════════════════════════════════

    private static UnityEngine.UI.Button BuildToolButton(Transform canvasParent,
        string label, CleaningManager mgr, bool isSponge,
        Vector2 anchor, Vector2 anchoredPos, Color tintColor)
    {
        var btnGO = new GameObject($"Btn_{label.Replace(" ", "")}");
        btnGO.transform.SetParent(canvasParent);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(140f, 40f);
        rt.anchoredPosition = anchoredPos;
        rt.localScale = Vector3.one;

        var img = btnGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.25f, 0.25f, 0.35f);

        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();

        // Wire onClick
        if (isSponge)
        {
            var action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), mgr,
                typeof(CleaningManager).GetMethod(nameof(CleaningManager.SelectSponge),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }
        else
        {
            var action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), mgr,
                typeof(CleaningManager).GetMethod(nameof(CleaningManager.SelectSpray),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // Colour indicator
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

    private static GameObject BuildCompletionPanel(Transform canvasParent)
    {
        var panelGO = new GameObject("CompletionPanel");
        panelGO.transform.SetParent(canvasParent);

        var rt = panelGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 80f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        var bg = panelGO.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.1f, 0.4f, 0.15f, 0.85f);

        var textGO = new GameObject("CompletionText");
        textGO.transform.SetParent(panelGO.transform);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "All Clean!";
        tmp.fontSize = 36f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        panelGO.SetActive(false);
        return panelGO;
    }

    // ════════════════════════════════════════════════════════════════════
    // SO creation helper
    // ════════════════════════════════════════════════════════════════════

    private static SpillDefinition CreateSpillSO(string displayName,
        SpillDefinition.SpillType spillType, Color color,
        float coverage, int seed, int textureSize, float stubbornness)
    {
        var so = ScriptableObject.CreateInstance<SpillDefinition>();
        so.displayName = displayName;
        so.spillType = spillType;
        so.spillColor = color;
        so.coverage = coverage;
        so.seed = seed;
        so.textureSize = textureSize;
        so.stubbornness = stubbornness;

        string safeName = displayName.Replace(" ", "_");
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/Cleaning/Spill_{safeName}.asset");
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
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
                Debug.Log($"[CleaningSceneBuilder] Created layer '{layerName}' at index {i}");
                return i;
            }
        }

        Debug.LogWarning($"[CleaningSceneBuilder] No empty layer slot for '{layerName}', using Default (0)");
        return 0;
    }
}
