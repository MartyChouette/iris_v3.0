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
    private const string BooksLayerName = "Books";

    // ── Book data for the book nook ──
    private static readonly string[] BookTitles =
    {
        "Roots in Darkness", "The Quiet Garden", "Letters to Soil",
        "Pressing Petals", "On Wilting", "Stem Theory",
    };

    private static readonly string[] BookAuthors =
    {
        "Eleanor Moss", "H. Fernwood", "Clara Rootley",
        "Jasper Thorn", "Wren Dewdrop", "P.L. Greenshaw",
    };

    private static readonly string[][] BookPages =
    {
        new[] {
            "The roots do not ask permission. They push through clay and stone, searching for what they need in total darkness.",
            "I have often wondered if the flower knows it is beautiful, or if beauty is simply a side effect of reaching toward light.",
            "When the last petal falls, the stem stands bare — not empty, but unburdened."
        },
        new[] {
            "A garden is never quiet. Listen closely: the earthworms turning, the slow exhale of opening buds, the patient drip.",
            "She planted marigolds along the fence not for their color, but because they reminded her of someone she'd rather not forget.",
            "By September the garden had its own ideas. She learned to stop arguing with it."
        },
        new[] {
            "Dear Soil, I am writing to apologize. I have taken so much from you and returned so little.",
            "The compost heap is a love letter written in eggshells and coffee grounds. Decomposition as devotion.",
            "Perhaps we are all just soil, waiting patiently for something to take root."
        },
        new[] {
            "To press a flower is to stop time — or at least, to press pause. The color fades, but the shape remembers.",
            "Page 47 of her journal: a flattened daisy, brown at the edges, still holding its circular argument.",
            "Some flowers are better preserved in memory than in books. But we press them anyway."
        },
        new[] {
            "Wilting is not failure. It is the flower's way of saying: I have given everything I had to give.",
            "The drooping head of a sunflower in October carries more dignity than any spring bloom.",
            "We fear wilting because we see ourselves in it. But the plant does not fear. It simply returns."
        },
        new[] {
            "Chapter 1: The stem is not merely a support structure. It is a highway, a messenger, a spine.",
            "Consider the hollow stem of the dandelion — empty inside, yet strong enough to hold a wish.",
            "All architecture aspires to the condition of the stem: vertical, purposeful, alive."
        },
    };

    private static readonly Color[] SpineColors =
    {
        new Color(0.55f, 0.15f, 0.15f),
        new Color(0.12f, 0.15f, 0.40f),
        new Color(0.12f, 0.35f, 0.15f),
        new Color(0.70f, 0.62f, 0.48f),
        new Color(0.45f, 0.20f, 0.40f),
        new Color(0.25f, 0.25f, 0.25f),
    };

    [MenuItem("Window/Iris/Build Apartment Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int placeableLayer = EnsureLayer(PlaceableLayerName);
        int booksLayer = EnsureLayer(BooksLayerName);

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

        // ── 9. Books in book nook ────────────────────────────────────
        BuildBookNookBooks(booksLayer);

        // ── 10. BookInteractionManager ───────────────────────────────
        var bookManager = BuildBookInteractionManager(camGO, booksLayer);

        // ── 11. ApartmentManager + UI ─────────────────────────────────
        BuildApartmentManager(cameras.browse, cameras.selected, grabber, areaDefs, bookManager);

        // ── 12. Save scene ─────────────────────────────────────────────
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
        // |      (2m doorway)   |       |
        // |     Kitchen         | Bath  |
        // |   (-6..3, -5..0)    |(3..6) |
        // |                     |(-5..0)|
        // +---------------------+-------+

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

        // Divider between Living Room and Kitchen (Z=0): right segment (1 to 3)
        // 2m doorway gap from X=-1 to X=1, bathroom wall starts at X=3
        CreateBox("Divider_LR_Kitchen_Right", parent.transform,
            new Vector3(2f, wallH / 2f, 0f), new Vector3(2f, wallH, wallT),
            new Color(0.78f, 0.75f, 0.70f));

        // Divider between Living Room and Book Nook (X=0, Z=0..5)
        CreateBox("Divider_LR_BookNook", parent.transform,
            new Vector3(0f, wallH / 2f, 2.5f), new Vector3(wallT, wallH, 5f),
            new Color(0.78f, 0.75f, 0.70f));

        // ── Bathroom divider (X=3, Z=-5..0) with 1.5m doorway gap ──

        // Top segment: Z=-1.75 to Z=0
        CreateBox("Divider_Bath_Top", parent.transform,
            new Vector3(3f, wallH / 2f, -0.875f), new Vector3(wallT, wallH, 1.75f),
            new Color(0.78f, 0.75f, 0.70f));

        // Bottom segment: Z=-5 to Z=-3.25
        CreateBox("Divider_Bath_Bottom", parent.transform,
            new Vector3(3f, wallH / 2f, -4.125f), new Vector3(wallT, wallH, 1.75f),
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

        // ── Bathroom (3..6, -5..0) ──

        // Sink counter (against east wall)
        CreateBox("Sink_Counter_Base", parent.transform,
            new Vector3(5.2f, 0.4f, -2.5f), new Vector3(0.7f, 0.8f, 0.5f),
            new Color(0.50f, 0.40f, 0.30f));
        CreateBox("Sink_Counter_Top", parent.transform,
            new Vector3(5.2f, 0.85f, -2.5f), new Vector3(0.7f, 0.08f, 0.5f),
            new Color(0.75f, 0.73f, 0.70f));

        // Sink basin (inset)
        CreateBox("Sink_Basin", parent.transform,
            new Vector3(5.2f, 0.90f, -2.5f), new Vector3(0.35f, 0.05f, 0.3f),
            new Color(0.70f, 0.80f, 0.88f));

        // Mirror (on east wall above sink)
        CreateBox("Mirror", parent.transform,
            new Vector3(5.85f, 1.6f, -2.5f), new Vector3(0.05f, 0.6f, 0.5f),
            new Color(0.80f, 0.85f, 0.90f));

        // Toilet
        CreateBox("Toilet_Bowl", parent.transform,
            new Vector3(4.5f, 0.3f, -4f), new Vector3(0.4f, 0.6f, 0.5f),
            new Color(0.90f, 0.90f, 0.90f));
        CreateBox("Toilet_Tank", parent.transform,
            new Vector3(4.5f, 0.55f, -4.2f), new Vector3(0.35f, 0.3f, 0.15f),
            new Color(0.88f, 0.88f, 0.88f));
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
            browsePos: new Vector3(-1f, 6f, 8f),
            browseRot: new Vector3(45f, 180f, 0f),
            browseFOV: 60f,
            selectedPos: new Vector3(-3.2f, 3f, 5f),
            selectedRot: new Vector3(35f, 180f, 0f),
            selectedFOV: 50f);

        var kitchen = CreateAreaDefinition("Kitchen", soDir,
            areaName: "Kitchen",
            description: "Counter, stove, and open floor.",
            browsePos: new Vector3(0f, 6f, -3f),
            browseRot: new Vector3(50f, 0f, 0f),
            browseFOV: 65f,
            selectedPos: new Vector3(-1f, 3f, -2.5f),
            selectedRot: new Vector3(35f, -20f, 0f),
            selectedFOV: 50f);

        var bookNook = CreateAreaDefinition("BookNook", soDir,
            areaName: "Book Nook",
            description: "Bookshelf, reading chair, and side table.",
            browsePos: new Vector3(5f, 6f, 8f),
            browseRot: new Vector3(45f, 210f, 0f),
            browseFOV: 55f,
            selectedPos: new Vector3(4f, 3f, 5f),
            selectedRot: new Vector3(35f, 200f, 0f),
            selectedFOV: 48f);

        var bathroom = CreateAreaDefinition("Bathroom", soDir,
            areaName: "Bathroom",
            description: "Sink, mirror, and toilet.",
            browsePos: new Vector3(4.5f, 6f, -1f),
            browseRot: new Vector3(55f, 180f, 0f),
            browseFOV: 55f,
            selectedPos: new Vector3(4.8f, 2.5f, -1.5f),
            selectedRot: new Vector3(35f, 180f, 0f),
            selectedFOV: 48f);

        return new[] { livingRoom, kitchen, bookNook, bathroom };
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
        ObjectGrabber grabber, ApartmentAreaDefinition[] areaDefs,
        BookInteractionManager bookManager)
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

        // Bookshelf
        so.FindProperty("bookInteractionManager").objectReferenceValue = bookManager;
        so.FindProperty("bookNookAreaIndex").intValue = 2;

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
    // Book Nook Books
    // ════════════════════════════════════════════════════════════════════

    private static void BuildBookNookBooks(int booksLayer)
    {
        string defDir = "Assets/ScriptableObjects/Bookcase";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Bookcase");

        var booksParent = new GameObject("BookNookBooks");
        var furniture = GameObject.Find("Furniture");
        if (furniture != null)
            booksParent.transform.SetParent(furniture.transform);

        // Bookshelf is at (5.3, 1.0, 2.5) with scale (0.5, 2.0, 1.5)
        // Shelf front face is at X = 5.3 - 0.25 = 5.05
        // Books sit on the shelf surface at Y = 1.0 (center of shelf)
        // Shelf spans Z = 1.75 to 3.25
        float bookY = 1.05f;        // just above shelf center
        float bookX = 5.0f;         // in front of shelf back
        float startZ = 1.85f;       // start position along shelf
        float spacing = 0.22f;      // space between book centers

        for (int i = 0; i < BookTitles.Length; i++)
        {
            float thickness = 0.04f;
            float bookHeight = 0.3f;
            float bookDepth = 0.2f;
            Color color = SpineColors[i % SpineColors.Length];

            // Create BookDefinition asset
            var def = ScriptableObject.CreateInstance<BookDefinition>();
            def.title = BookTitles[i];
            def.author = BookAuthors[i];
            def.pageTexts = BookPages[i];
            def.spineColor = color;
            def.heightScale = 0.85f;
            def.thicknessScale = thickness;

            string defPath = $"{defDir}/AptBook_{i:D2}_{BookTitles[i].Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(def, defPath);

            float bookZ = startZ + i * spacing;

            var bookGO = CreateBox($"AptBook_{i}", booksParent.transform,
                new Vector3(bookX, bookY + bookHeight / 2f, bookZ),
                new Vector3(thickness, bookHeight, bookDepth),
                color);
            bookGO.isStatic = false;
            bookGO.layer = booksLayer;

            // Add BookVolume component
            var volume = bookGO.AddComponent<BookVolume>();

            // Build pages child hierarchy
            var pagesRoot = BuildBookPages(bookGO.transform, thickness, bookHeight, bookDepth);

            // Wire serialized fields
            var so = new SerializedObject(volume);
            so.FindProperty("definition").objectReferenceValue = def;
            so.FindProperty("pagesRoot").objectReferenceValue = pagesRoot;

            var pageLabels = pagesRoot.GetComponentsInChildren<TMP_Text>(true);
            var labelsProperty = so.FindProperty("pageLabels");
            labelsProperty.arraySize = Mathf.Min(pageLabels.Length, 3);
            for (int p = 0; p < labelsProperty.arraySize; p++)
                labelsProperty.GetArrayElementAtIndex(p).objectReferenceValue = pageLabels[p];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log($"[ApartmentSceneBuilder] Created {BookTitles.Length} books in book nook.");
    }

    private static GameObject BuildBookPages(Transform bookTransform,
        float thickness, float height, float depth)
    {
        var pagesRoot = new GameObject("Pages");
        pagesRoot.transform.SetParent(bookTransform);
        pagesRoot.transform.localPosition = Vector3.zero;
        pagesRoot.transform.localRotation = Quaternion.identity;

        var canvasGO = new GameObject("PageCanvas");
        canvasGO.transform.SetParent(pagesRoot.transform);
        canvasGO.transform.localPosition = new Vector3(0f, 0f, -depth / 2f - 0.001f);
        canvasGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(900f, 400f);
        canvasRT.localScale = new Vector3(0.0005f, 0.0005f, 0.0005f);

        string[] pageNames = { "PageLeft", "PageCenter", "PageRight" };
        float pageWidth = 280f;
        float[] xPositions = { -300f, 0f, 300f };

        for (int i = 0; i < 3; i++)
        {
            var pageGO = new GameObject(pageNames[i]);
            pageGO.transform.SetParent(canvasGO.transform);

            var rt = pageGO.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(xPositions[i], 0f);
            rt.sizeDelta = new Vector2(pageWidth, 380f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            var bg = pageGO.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.95f, 0.93f, 0.88f, 0.95f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(pageGO.transform);

            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8f, 8f);
            textRT.offsetMax = new Vector2(-8f, -8f);
            textRT.localScale = Vector3.one;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 18f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.12f, 0.10f, 0.08f);
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }

        pagesRoot.SetActive(false);
        return pagesRoot;
    }

    // ════════════════════════════════════════════════════════════════════
    // BookInteractionManager (disabled by default, ApartmentManager enables)
    // ════════════════════════════════════════════════════════════════════

    private static BookInteractionManager BuildBookInteractionManager(
        GameObject camGO, int booksLayer)
    {
        var managerGO = new GameObject("BookInteractionManager");
        var manager = managerGO.AddComponent<BookInteractionManager>();

        // Reading anchor — child of main camera
        var anchorGO = new GameObject("ReadingAnchor");
        anchorGO.transform.SetParent(camGO.transform);
        anchorGO.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
        anchorGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // Title hint UI
        var uiCanvasGO = new GameObject("BookUI_Canvas");
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 12;
        uiCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        uiCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var hintPanel = new GameObject("TitleHintPanel");
        hintPanel.transform.SetParent(uiCanvasGO.transform);

        var panelRT = hintPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(400f, 50f);
        panelRT.anchoredPosition = new Vector2(0f, 30f);
        panelRT.localScale = Vector3.one;

        var bg = hintPanel.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        var textGO = new GameObject("TitleText");
        textGO.transform.SetParent(hintPanel.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Book Title";
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Italic;

        hintPanel.SetActive(false);

        // Wire serialized fields
        var cam = camGO.GetComponent<UnityEngine.Camera>();

        var so = new SerializedObject(manager);
        so.FindProperty("readingAnchor").objectReferenceValue = anchorGO.transform;
        so.FindProperty("mainCamera").objectReferenceValue = cam;
        // browseCamera left null — not used in apartment context
        so.FindProperty("titleHintPanel").objectReferenceValue = hintPanel;
        so.FindProperty("titleHintText").objectReferenceValue = tmp;
        so.FindProperty("booksLayerMask").intValue = 1 << booksLayer;
        so.FindProperty("maxRayDistance").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Start disabled — ApartmentManager enables when book nook is selected
        manager.enabled = false;

        return manager;
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
