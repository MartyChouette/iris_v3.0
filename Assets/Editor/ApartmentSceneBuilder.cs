using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the apartment scene with
/// room geometry, furniture, placeable objects, cameras, and apartment manager.
/// Menu: Window > Iris > Build Apartment Scene
/// </summary>
public static class ApartmentSceneBuilder
{
    private const string PlaceableLayerName = "Placeable";

    [MenuItem("Window/Iris/Build Apartment Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int placeableLayer = EnsureLayer(PlaceableLayerName);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.95f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera with CinemachineBrain ───────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        camGO.AddComponent<CinemachineBrain>();

        // ── 3. Room geometry ───────────────────────────────────────────
        BuildApartment();

        // ── 4. Furniture ───────────────────────────────────────────────
        BuildFurniture();

        // ── 5. Placeable objects ───────────────────────────────────────
        var placeables = BuildPlaceableObjects(placeableLayer);

        // ── 6. Area ScriptableObjects (created before cameras so we can position them) ─
        var areaDefs = BuildAreaDefinitions();

        // ── 7. Cinemachine cameras (positioned at first area's vantage) ─
        var cameras = BuildCameras(areaDefs[0]);

        // ── 8. ObjectGrabber ───────────────────────────────────────────
        var grabberGO = new GameObject("ObjectGrabber");
        var grabber = grabberGO.AddComponent<ObjectGrabber>();

        // Wire grabber's placeable layer mask
        var grabberSO = new SerializedObject(grabber);
        grabberSO.FindProperty("placeableLayer").intValue = 1 << placeableLayer;
        grabberSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 9. ApartmentManager + UI ─────────────────────────────────
        BuildApartmentManager(cameras.browse, cameras.selected, grabber, areaDefs);

        // ── 10. Save scene ─────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/apartment.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[ApartmentSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Apartment geometry
    // ════════════════════════════════════════════════════════════════════

    private static void BuildApartment()
    {
        // Layout (top-down, Z = north):
        //
        // +------------------+----------+
        // |                  |          |
        // |   Living Room    | Book     |
        // |   (-6..0, 0..5)  | Nook     |
        // |                  | (0..6,   |
        // |                  |  0..5)   |
        // +-------wall-------+--wall----+
        // |          (2m doorway)       |
        // |         Kitchen             |
        // |     (-6..6, -5..0)          |
        // +-----------------------------+

        var parent = new GameObject("Apartment");

        float wallH = 3f;
        float wallT = 0.15f;
        float ceilY = wallH;

        // Floor (12 x 10)
        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 10f),
            new Color(0.55f, 0.45f, 0.35f));

        // Ceiling
        CreateBox("Ceiling", parent.transform,
            new Vector3(0f, ceilY + 0.05f, 0f), new Vector3(12f, 0.1f, 10f),
            new Color(0.90f, 0.88f, 0.85f));

        // ── Outer walls ──

        // South wall (Z = -5)
        CreateBox("Wall_South", parent.transform,
            new Vector3(0f, wallH / 2f, -5f), new Vector3(12f + wallT, wallH, wallT),
            new Color(0.82f, 0.78f, 0.72f));

        // North wall (Z = 5)
        CreateBox("Wall_North", parent.transform,
            new Vector3(0f, wallH / 2f, 5f), new Vector3(12f + wallT, wallH, wallT),
            new Color(0.82f, 0.78f, 0.72f));

        // West wall (X = -6)
        CreateBox("Wall_West", parent.transform,
            new Vector3(-6f, wallH / 2f, 0f), new Vector3(wallT, wallH, 10f + wallT),
            new Color(0.82f, 0.78f, 0.72f));

        // East wall (X = 6)
        CreateBox("Wall_East", parent.transform,
            new Vector3(6f, wallH / 2f, 0f), new Vector3(wallT, wallH, 10f + wallT),
            new Color(0.82f, 0.78f, 0.72f));

        // ── Divider walls (with doorway gaps) ──

        // Divider between Living Room and Kitchen (Z=0): left segment (-6 to -1)
        CreateBox("Divider_LR_Kitchen_Left", parent.transform,
            new Vector3(-3.5f, wallH / 2f, 0f), new Vector3(5f, wallH, wallT),
            new Color(0.78f, 0.75f, 0.70f));

        // Divider between Living Room and Kitchen (Z=0): right segment (1 to 6)
        // 2m doorway gap from X=-1 to X=1
        CreateBox("Divider_LR_Kitchen_Right", parent.transform,
            new Vector3(3.5f, wallH / 2f, 0f), new Vector3(5f, wallH, wallT),
            new Color(0.78f, 0.75f, 0.70f));

        // Divider between Living Room and Book Nook (X=0, Z=0..5)
        CreateBox("Divider_LR_BookNook", parent.transform,
            new Vector3(0f, wallH / 2f, 2.5f), new Vector3(wallT, wallH, 5f),
            new Color(0.78f, 0.75f, 0.70f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Furniture (static boxes)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildFurniture()
    {
        var parent = new GameObject("Furniture");

        // ── Living Room (-6..0, 0..5) ──

        // Couch (against west wall)
        CreateBox("Couch", parent.transform,
            new Vector3(-5f, 0.4f, 2.5f), new Vector3(1.8f, 0.8f, 0.9f),
            new Color(0.35f, 0.28f, 0.45f));

        // Coffee table (in front of couch)
        CreateBox("CoffeeTable_Top", parent.transform,
            new Vector3(-3.2f, 0.35f, 2.5f), new Vector3(1.0f, 0.05f, 0.6f),
            new Color(0.50f, 0.35f, 0.22f));
        CreateBox("CoffeeTable_Leg1", parent.transform,
            new Vector3(-3.6f, 0.16f, 2.2f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CoffeeTable_Leg2", parent.transform,
            new Vector3(-2.8f, 0.16f, 2.2f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CoffeeTable_Leg3", parent.transform,
            new Vector3(-3.6f, 0.16f, 2.8f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CoffeeTable_Leg4", parent.transform,
            new Vector3(-2.8f, 0.16f, 2.8f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));

        // Floor lamp (corner)
        CreateBox("FloorLamp_Pole", parent.transform,
            new Vector3(-5.3f, 0.8f, 4.3f), new Vector3(0.06f, 1.6f, 0.06f),
            new Color(0.20f, 0.20f, 0.22f));
        CreateBox("FloorLamp_Shade", parent.transform,
            new Vector3(-5.3f, 1.7f, 4.3f), new Vector3(0.35f, 0.25f, 0.35f),
            new Color(0.92f, 0.85f, 0.65f));

        // ── Kitchen (-6..6, -5..0) ──

        // Counter (along south wall)
        CreateBox("Counter_Top", parent.transform,
            new Vector3(-2f, 0.85f, -4.2f), new Vector3(3f, 0.08f, 0.7f),
            new Color(0.75f, 0.73f, 0.70f));
        CreateBox("Counter_Base", parent.transform,
            new Vector3(-2f, 0.4f, -4.2f), new Vector3(3f, 0.8f, 0.7f),
            new Color(0.50f, 0.40f, 0.30f));

        // Stove (next to counter)
        CreateBox("Stove", parent.transform,
            new Vector3(1f, 0.45f, -4.2f), new Vector3(0.8f, 0.9f, 0.7f),
            new Color(0.25f, 0.25f, 0.28f));

        // ── Book Nook (0..6, 0..5) ──

        // Bookshelf (against east wall)
        CreateBox("Bookshelf", parent.transform,
            new Vector3(5.3f, 1.0f, 2.5f), new Vector3(0.5f, 2.0f, 1.5f),
            new Color(0.42f, 0.30f, 0.20f));

        // Reading chair
        CreateBox("ReadingChair", parent.transform,
            new Vector3(3f, 0.35f, 3.5f), new Vector3(0.7f, 0.7f, 0.7f),
            new Color(0.60f, 0.35f, 0.25f));

        // Side table (with legs)
        CreateBox("SideTable_Top", parent.transform,
            new Vector3(4.2f, 0.45f, 3.5f), new Vector3(0.5f, 0.05f, 0.5f),
            new Color(0.50f, 0.35f, 0.22f));
        CreateBox("SideTable_Leg1", parent.transform,
            new Vector3(4.0f, 0.21f, 3.3f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("SideTable_Leg2", parent.transform,
            new Vector3(4.4f, 0.21f, 3.3f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("SideTable_Leg3", parent.transform,
            new Vector3(4.0f, 0.21f, 3.7f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("SideTable_Leg4", parent.transform,
            new Vector3(4.4f, 0.21f, 3.7f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Placeable objects (non-static, Rigidbody, on Placeable layer)
    // ════════════════════════════════════════════════════════════════════

    private static GameObject[] BuildPlaceableObjects(int placeableLayer)
    {
        var parent = new GameObject("Placeables");

        // Cup on coffee table
        var cup = CreatePlaceable("Cup", parent.transform,
            new Vector3(-3.2f, 0.45f, 2.5f), new Vector3(0.08f, 0.12f, 0.08f),
            new Color(0.85f, 0.82f, 0.75f), placeableLayer);

        // Vase on kitchen counter
        var vase = CreatePlaceable("Vase", parent.transform,
            new Vector3(-2f, 0.98f, -4.2f), new Vector3(0.1f, 0.2f, 0.1f),
            new Color(0.3f, 0.55f, 0.65f), placeableLayer);

        // Box on book nook side table
        var box = CreatePlaceable("Box", parent.transform,
            new Vector3(4.2f, 0.55f, 3.5f), new Vector3(0.15f, 0.1f, 0.12f),
            new Color(0.72f, 0.55f, 0.35f), placeableLayer);

        return new[] { cup, vase, box };
    }

    private static GameObject CreatePlaceable(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.layer = layer;
        go.isStatic = false;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.5f;

        go.AddComponent<PlaceableObject>();

        return go;
    }

    // ════════════════════════════════════════════════════════════════════
    // Cinemachine cameras (browse + selected)
    // ════════════════════════════════════════════════════════════════════

    private struct CameraPair
    {
        public CinemachineCamera browse;
        public CinemachineCamera selected;
    }

    private static CameraPair BuildCameras(ApartmentAreaDefinition firstArea)
    {
        var parent = new GameObject("CinemachineCameras");

        // Browse camera (starts active, positioned at first area)
        var browseGO = new GameObject("Cam_Browse");
        browseGO.transform.SetParent(parent.transform);
        browseGO.transform.position = firstArea.browsePosition;
        browseGO.transform.rotation = Quaternion.Euler(firstArea.browseRotation);
        var browse = browseGO.AddComponent<CinemachineCamera>();
        var browseLens = LensSettings.Default;
        browseLens.FieldOfView = firstArea.browseFOV;
        browseLens.NearClipPlane = 0.1f;
        browseLens.FarClipPlane = 500f;
        browse.Lens = browseLens;
        browse.Priority = 20;

        // Selected camera (starts inactive, parked at first area's selected view)
        var selectedGO = new GameObject("Cam_Selected");
        selectedGO.transform.SetParent(parent.transform);
        selectedGO.transform.position = firstArea.selectedPosition;
        selectedGO.transform.rotation = Quaternion.Euler(firstArea.selectedRotation);
        var selected = selectedGO.AddComponent<CinemachineCamera>();
        var selectedLens = LensSettings.Default;
        selectedLens.FieldOfView = firstArea.selectedFOV;
        selectedLens.NearClipPlane = 0.1f;
        selectedLens.FarClipPlane = 500f;
        selected.Lens = selectedLens;
        selected.Priority = 0;

        return new CameraPair { browse = browse, selected = selected };
    }

    // ════════════════════════════════════════════════════════════════════
    // Area ScriptableObjects
    // ════════════════════════════════════════════════════════════════════

    private static ApartmentAreaDefinition[] BuildAreaDefinitions()
    {
        string soDir = "Assets/ScriptableObjects/Apartment";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Apartment");

        var livingRoom = CreateAreaDefinition("LivingRoom", soDir,
            areaName: "Living Room",
            description: "Couch, coffee table, and a floor lamp.",
            browsePos: new Vector3(-1f, 4f, 7f),
            browseRot: new Vector3(40f, 180f, 0f),
            browseFOV: 60f,
            selectedPos: new Vector3(-3.2f, 2f, 4.5f),
            selectedRot: new Vector3(35f, 180f, 0f),
            selectedFOV: 50f);

        var kitchen = CreateAreaDefinition("Kitchen", soDir,
            areaName: "Kitchen",
            description: "Counter, stove, and open floor.",
            browsePos: new Vector3(0f, 4f, -2f),
            browseRot: new Vector3(45f, 0f, 0f),
            browseFOV: 65f,
            selectedPos: new Vector3(-1f, 2f, -2f),
            selectedRot: new Vector3(30f, -20f, 0f),
            selectedFOV: 50f);

        var bookNook = CreateAreaDefinition("BookNook", soDir,
            areaName: "Book Nook",
            description: "Bookshelf, reading chair, and side table.",
            browsePos: new Vector3(5f, 4f, 7f),
            browseRot: new Vector3(40f, 210f, 0f),
            browseFOV: 55f,
            selectedPos: new Vector3(4f, 2f, 4.5f),
            selectedRot: new Vector3(30f, 200f, 0f),
            selectedFOV: 48f);

        return new[] { livingRoom, kitchen, bookNook };
    }

    private static ApartmentAreaDefinition CreateAreaDefinition(
        string assetName, string directory,
        string areaName, string description,
        Vector3 browsePos, Vector3 browseRot, float browseFOV,
        Vector3 selectedPos, Vector3 selectedRot, float selectedFOV)
    {
        var def = ScriptableObject.CreateInstance<ApartmentAreaDefinition>();
        def.areaName = areaName;
        def.description = description;
        def.browsePosition = browsePos;
        def.browseRotation = browseRot;
        def.browseFOV = browseFOV;
        def.selectedPosition = selectedPos;
        def.selectedRotation = selectedRot;
        def.selectedFOV = selectedFOV;
        def.browseBlendDuration = 0.8f;
        def.selectBlendDuration = 0.5f;

        string path = $"{directory}/Area_{assetName}.asset";
        AssetDatabase.CreateAsset(def, path);

        return def;
    }

    // ════════════════════════════════════════════════════════════════════
    // ApartmentManager + Screen-Space UI
    // ════════════════════════════════════════════════════════════════════

    private static void BuildApartmentManager(
        CinemachineCamera browseCam, CinemachineCamera selectedCam,
        ObjectGrabber grabber, ApartmentAreaDefinition[] areaDefs)
    {
        var managerGO = new GameObject("ApartmentManager");
        var manager = managerGO.AddComponent<ApartmentManager>();

        // ── Screen-space overlay canvas ──
        var uiCanvasGO = new GameObject("UI_Canvas");
        uiCanvasGO.transform.SetParent(managerGO.transform);
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 10;
        uiCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        uiCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Area name panel (top center)
        var areaNamePanel = CreateUIPanel("AreaNamePanel", uiCanvasGO.transform,
            new Vector2(0f, 200f), new Vector2(400f, 60f),
            "Living Room", 28f, new Color(0f, 0f, 0f, 0.6f));

        // Browse hints panel (bottom center)
        var browseHints = CreateUIPanel("BrowseHintsPanel", uiCanvasGO.transform,
            new Vector2(0f, -200f), new Vector2(500f, 50f),
            "A / D  Cycle Areas    |    Enter  Select", 16f,
            new Color(0f, 0f, 0f, 0.5f));

        // Selected hints panel (bottom center, replaces browse hints)
        var selectedHints = CreateUIPanel("SelectedHintsPanel", uiCanvasGO.transform,
            new Vector2(0f, -200f), new Vector2(500f, 50f),
            "Click  Pick Up / Place    |    G  Grid Snap    |    Esc  Back", 16f,
            new Color(0f, 0f, 0f, 0.5f));

        // ── Wire serialized fields ──
        var so = new SerializedObject(manager);

        // Areas array
        var areasProp = so.FindProperty("areas");
        areasProp.arraySize = areaDefs.Length;
        for (int i = 0; i < areaDefs.Length; i++)
            areasProp.GetArrayElementAtIndex(i).objectReferenceValue = areaDefs[i];

        // Cameras
        so.FindProperty("browseCamera").objectReferenceValue = browseCam;
        so.FindProperty("selectedCamera").objectReferenceValue = selectedCam;

        // Interaction
        so.FindProperty("objectGrabber").objectReferenceValue = grabber;

        // UI
        so.FindProperty("areaNamePanel").objectReferenceValue = areaNamePanel;
        so.FindProperty("areaNameText").objectReferenceValue =
            areaNamePanel.GetComponentInChildren<TMP_Text>();

        so.FindProperty("browseHintsPanel").objectReferenceValue = browseHints;
        so.FindProperty("selectedHintsPanel").objectReferenceValue = selectedHints;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateUIPanel(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size,
        string defaultText, float fontSize, Color bgColor)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = size;
        panelRT.anchoredPosition = anchoredPos;
        panelRT.localScale = Vector3.one;

        // Semi-transparent background
        var bg = panel.AddComponent<UnityEngine.UI.Image>();
        bg.color = bgColor;

        // Text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return panel;
    }

    // ════════════════════════════════════════════════════════════════════
    // Shared helpers
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

    /// <summary>
    /// Ensure a layer exists by name. Returns the layer index.
    /// </summary>
    private static int EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

        var layersProp = tagManager.FindProperty("layers");

        // Check if the layer already exists
        for (int i = 0; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (element.stringValue == layerName)
                return i;
        }

        // Find first empty user layer (8+) and assign
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[ApartmentSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[ApartmentSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }
}
