using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using Unity.Cinemachine;
using UnityEngine.Splines;
using Unity.Mathematics;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the apartment hub scene with
/// Kitchen + Living Room, spline dolly camera, station integration, and placement surfaces.
/// Menu: Window > Iris > Build Apartment Scene
/// </summary>
public static class ApartmentSceneBuilder
{
    private const string PlaceableLayerName = "Placeable";
    private const string BooksLayerName = "Books";
    private const string DrawersLayerName = "Drawers";
    private const string PerfumesLayerName = "Perfumes";
    private const string TrinketsLayerName = "Trinkets";
    private const string CoffeeTableBooksLayerName = "CoffeeTableBooks";
    private const string NewspaperLayerName = "Newspaper";
    private const string CleanableLayerName = "Cleanable";
    private const string PhoneLayerName = "Phone";

    // Two-page newspaper spread dimensions
    private const int NewspaperCanvasWidth = 1000;
    private const int NewspaperCanvasHeight = 700;

    // ══════════════════════════════════════════════════════════════════
    // Main Build
    // ══════════════════════════════════════════════════════════════════

    [MenuItem("Window/Iris/Build Apartment Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int placeableLayer = EnsureLayer(PlaceableLayerName);
        int booksLayer = EnsureLayer(BooksLayerName);
        int drawersLayer = EnsureLayer(DrawersLayerName);
        int perfumesLayer = EnsureLayer(PerfumesLayerName);
        int trinketsLayer = EnsureLayer(TrinketsLayerName);
        int coffeeTableBooksLayer = EnsureLayer(CoffeeTableBooksLayerName);
        int newspaperLayer = EnsureLayer(NewspaperLayerName);
        int cleanableLayer = EnsureLayer(CleanableLayerName);
        int phoneLayer = EnsureLayer(PhoneLayerName);

        // ── 1. Lighting ──
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.95f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera + CinemachineBrain ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        var brain = camGO.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);

        // ── 2b. EventSystem (required for UI buttons) ──
        var eventSysGO = new GameObject("EventSystem");
        eventSysGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSysGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // ── 3. Room geometry ──
        BuildApartment();

        // ── 4. Furniture (Kitchen + Living Room) ──
        var furnitureRefs = BuildFurniture();

        // ── 5. Placeable objects ──
        BuildPlaceableObjects(placeableLayer);

        // ── 6. Spline path + browse camera with dolly ──
        var splineContainer = BuildSplinePath();
        var cameras = BuildCamerasWithDolly(splineContainer);

        // ── 7. Area SOs (2 areas) ──
        var areaDefs = BuildAreaDefinitions();

        // ── 8. ObjectGrabber ──
        var grabberGO = new GameObject("ObjectGrabber");
        var grabber = grabberGO.AddComponent<ObjectGrabber>();
        var grabberSO = new SerializedObject(grabber);
        grabberSO.FindProperty("placeableLayer").intValue = 1 << placeableLayer;

        // Wire placement surfaces
        var allSurfaces = BuildPlacementSurfaces(furnitureRefs);
        var surfacesProp = grabberSO.FindProperty("surfaces");
        surfacesProp.arraySize = allSurfaces.Length;
        for (int i = 0; i < allSurfaces.Length; i++)
            surfacesProp.GetArrayElementAtIndex(i).objectReferenceValue = allSurfaces[i];

        grabberSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 9. Bookcase unit (shared with standalone bookcase scene) ──
        var bookcaseRoot = BookcaseSceneBuilder.BuildBookcaseUnit(
            booksLayer, drawersLayer, perfumesLayer, trinketsLayer, coffeeTableBooksLayer);
        bookcaseRoot.transform.position = new Vector3(-6.3f, 0f, 3.0f);
        bookcaseRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        // ── 10. BookInteractionManager ──
        var bookManager = BuildBookInteractionManager(camGO, booksLayer,
            drawersLayer, perfumesLayer, trinketsLayer, coffeeTableBooksLayer);

        // ── 11. Newspaper station (Kitchen — auto-activated by DayPhaseManager, not a StationRoot) ──
        var newspaperData = BuildNewspaperStation(camGO, newspaperLayer);

        // ── 12. Station roots ──
        var drinkMakingData = BuildDrinkMakingStation(camGO);
        BuildStationRoots(bookManager, drinkMakingData);

        // ── 13. ApartmentManager + UI ──
        var apartmentUI = BuildApartmentManager(cameras.browse, cameras.selected, cameras.dolly,
            grabber, areaDefs);

        // ── 14. Dating infrastructure (GameClock, DateSessionManager, PhoneController, etc.) ──
        BuildDatingInfrastructure(camGO, furnitureRefs, newspaperData, phoneLayer);

        // ── 15. Ambient cleaning (not a station) ──
        var cleaningData = BuildAmbientCleaning(camGO, cleanableLayer);

        // ── 16. DayPhaseManager (orchestrates daily loop) ──
        BuildDayPhaseManager(newspaperData, cleaningData, apartmentUI);

        // ── 17. Screen fade overlay ──
        BuildScreenFade();

        // ── 18. Save scene ──
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/apartment.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[ApartmentSceneBuilder] Scene saved to {path}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Apartment geometry (Kitchen + Living Room, no walls)
    // ══════════════════════════════════════════════════════════════════

    private static void BuildApartment()
    {
        var parent = new GameObject("Apartment");

        // Floor (10 x 12, covers kitchen + living room)
        CreateBox("Floor", parent.transform,
            new Vector3(-3f, -0.05f, 0f), new Vector3(10f, 0.1f, 12f),
            new Color(0.55f, 0.45f, 0.35f));
    }

    // ══════════════════════════════════════════════════════════════════
    // Furniture (returns references needed for wiring)
    // ══════════════════════════════════════════════════════════════════

    private struct FurnitureRefs
    {
        public GameObject coffeeTable;
        public GameObject kitchenTable;
        // Dating loop references
        public Transform phoneTransform;
        public Transform dateSpawnPoint;
        public Transform couchSeatTarget;
        public Transform coffeeTableDeliveryPoint;
        public Transform tossedNewspaperPosition;
    }

    private static FurnitureRefs BuildFurniture()
    {
        var parent = new GameObject("Furniture");
        var refs = new FurnitureRefs();

        // ═══ Kitchen (west-south: X ~ -4, Z ~ -4) ═══
        refs.kitchenTable = BuildKitchen(parent.transform,
            out refs.tossedNewspaperPosition,
            out refs.phoneTransform,
            out refs.dateSpawnPoint);

        // ═══ Living Room (west: X ~ -4, Z ~ 3) ═══
        refs.coffeeTable = BuildLivingRoom(parent.transform,
            out refs.couchSeatTarget, out refs.coffeeTableDeliveryPoint);

        return refs;
    }

    private static GameObject BuildKitchen(Transform parent,
        out Transform tossedNewspaperPosition,
        out Transform phoneTransform,
        out Transform dateSpawnPoint)
    {
        // Kitchen table (newspaper goes here)
        var tableTop = CreateBox("KitchenTable_Top", parent,
            new Vector3(-4f, 0.72f, -3.5f), new Vector3(1.2f, 0.05f, 0.8f),
            new Color(0.50f, 0.35f, 0.22f));
        CreateBox("KitchenTable_Leg1", parent,
            new Vector3(-4.5f, 0.35f, -3.85f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("KitchenTable_Leg2", parent,
            new Vector3(-3.5f, 0.35f, -3.85f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("KitchenTable_Leg3", parent,
            new Vector3(-4.5f, 0.35f, -3.15f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("KitchenTable_Leg4", parent,
            new Vector3(-3.5f, 0.35f, -3.15f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));

        // Fridge
        CreateBox("Fridge", parent,
            new Vector3(-6.3f, 0.9f, -4.5f), new Vector3(0.7f, 1.8f, 0.7f),
            new Color(0.85f, 0.85f, 0.87f));

        // Counter
        CreateBox("Counter_Top", parent,
            new Vector3(-4f, 0.85f, -5.2f), new Vector3(3f, 0.08f, 0.7f),
            new Color(0.75f, 0.73f, 0.70f));
        CreateBox("Counter_Base", parent,
            new Vector3(-4f, 0.4f, -5.2f), new Vector3(3f, 0.8f, 0.7f),
            new Color(0.50f, 0.40f, 0.30f));

        // Stove
        CreateBox("Stove", parent,
            new Vector3(-2f, 0.45f, -5.2f), new Vector3(0.8f, 0.9f, 0.7f),
            new Color(0.25f, 0.25f, 0.28f));

        // Tossed newspaper position (on coffee table, where newspaper lands after reading)
        var tossedGO = new GameObject("TossedNewspaperPosition");
        tossedGO.transform.SetParent(parent);
        tossedGO.transform.position = new Vector3(-3.5f, 0.42f, 3.0f);
        tossedGO.transform.rotation = Quaternion.Euler(90f, 10f, 0f);
        tossedNewspaperPosition = tossedGO.transform;

        // Phone (wall-mounted near kitchen counter)
        var phoneBody = CreateBox("Phone_Body", parent,
            new Vector3(-2.5f, 1.2f, -5.5f), new Vector3(0.12f, 0.18f, 0.05f),
            new Color(0.18f, 0.18f, 0.20f));
        phoneBody.isStatic = false;
        var ringVisualGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ringVisualGO.name = "RingVisual";
        ringVisualGO.transform.SetParent(phoneBody.transform);
        ringVisualGO.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        ringVisualGO.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
        ringVisualGO.isStatic = false;
        var ringCol = ringVisualGO.GetComponent<Collider>();
        if (ringCol != null) Object.DestroyImmediate(ringCol);
        var ringRend = ringVisualGO.GetComponent<Renderer>();
        if (ringRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.3f, 0.2f);
            mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.2f) * 2f);
            mat.EnableKeyword("_EMISSION");
            ringRend.sharedMaterial = mat;
        }
        ringVisualGO.SetActive(false);
        phoneTransform = phoneBody.transform;

        // Date spawn point (kitchen doorway area)
        var spawnGO = new GameObject("DateSpawnPoint");
        spawnGO.transform.SetParent(parent);
        spawnGO.transform.position = new Vector3(-3f, 0f, -5.5f);
        dateSpawnPoint = spawnGO.transform;

        return tableTop;
    }

    private static GameObject BuildLivingRoom(Transform parent,
        out Transform couchSeatTarget, out Transform coffeeTableDeliveryPoint)
    {
        // Couch
        CreateBox("Couch", parent,
            new Vector3(-5.5f, 0.4f, 3f), new Vector3(1.8f, 0.8f, 0.9f),
            new Color(0.35f, 0.28f, 0.45f));

        // Coffee table
        var tableTop = CreateBox("CoffeeTable_Top", parent,
            new Vector3(-3.5f, 0.35f, 3f), new Vector3(1.0f, 0.05f, 0.6f),
            new Color(0.50f, 0.35f, 0.22f));
        CreateBox("CoffeeTable_Leg1", parent,
            new Vector3(-3.9f, 0.16f, 2.7f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CoffeeTable_Leg2", parent,
            new Vector3(-3.1f, 0.16f, 2.7f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CoffeeTable_Leg3", parent,
            new Vector3(-3.9f, 0.16f, 3.3f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CoffeeTable_Leg4", parent,
            new Vector3(-3.1f, 0.16f, 3.3f), new Vector3(0.06f, 0.32f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));

        // Floor lamp
        CreateBox("FloorLamp_Pole", parent,
            new Vector3(-6.3f, 0.8f, 5.3f), new Vector3(0.06f, 1.6f, 0.06f),
            new Color(0.20f, 0.20f, 0.22f));
        CreateBox("FloorLamp_Shade", parent,
            new Vector3(-6.3f, 1.7f, 5.3f), new Vector3(0.35f, 0.25f, 0.35f),
            new Color(0.92f, 0.85f, 0.65f));

        // Sun ledge with mecha figurine
        CreateBox("SunLedge", parent,
            new Vector3(-3.5f, 0.9f, 5.7f), new Vector3(1.5f, 0.08f, 0.4f),
            new Color(0.50f, 0.45f, 0.38f));
        CreateBox("MechaFigurine", parent,
            new Vector3(-3.5f, 1.05f, 5.7f), new Vector3(0.1f, 0.2f, 0.1f),
            new Color(0.3f, 0.4f, 0.6f));

        // Small plant on sun ledge
        CreateBox("SmallPlant_Pot", parent,
            new Vector3(-4.1f, 0.98f, 5.7f), new Vector3(0.12f, 0.1f, 0.12f),
            new Color(0.6f, 0.35f, 0.25f));
        CreateBox("SmallPlant_Leaves", parent,
            new Vector3(-4.1f, 1.1f, 5.7f), new Vector3(0.15f, 0.12f, 0.15f),
            new Color(0.2f, 0.5f, 0.2f));

        // Couch seat target (where date character sits)
        var seatGO = new GameObject("CouchSeatTarget");
        seatGO.transform.SetParent(parent);
        seatGO.transform.position = new Vector3(-5.5f, 0.5f, 3f);
        couchSeatTarget = seatGO.transform;

        // Coffee table delivery point (where drinks appear)
        var deliveryGO = new GameObject("CoffeeTableDeliveryPoint");
        deliveryGO.transform.SetParent(parent);
        deliveryGO.transform.position = new Vector3(-3.5f, 0.42f, 3f);
        coffeeTableDeliveryPoint = deliveryGO.transform;

        return tableTop;
    }

    // ══════════════════════════════════════════════════════════════════
    // Placeable objects
    // ══════════════════════════════════════════════════════════════════

    private static void BuildPlaceableObjects(int placeableLayer)
    {
        var parent = new GameObject("Placeables");

        // Cup on coffee table
        CreatePlaceable("Cup", parent.transform,
            new Vector3(-3.5f, 0.45f, 3f), new Vector3(0.08f, 0.12f, 0.08f),
            new Color(0.85f, 0.82f, 0.75f), placeableLayer);

        // Vase on kitchen counter
        CreatePlaceable("Vase", parent.transform,
            new Vector3(-4f, 0.98f, -5.2f), new Vector3(0.1f, 0.2f, 0.1f),
            new Color(0.3f, 0.55f, 0.65f), placeableLayer);

        // Magazine on coffee table
        CreatePlaceable("Magazine", parent.transform,
            new Vector3(-3.2f, 0.42f, 3.1f), new Vector3(0.18f, 0.02f, 0.25f),
            new Color(0.7f, 0.3f, 0.3f), placeableLayer);

        // Yoyo on coffee table
        CreatePlaceable("Yoyo", parent.transform,
            new Vector3(-3.7f, 0.42f, 2.8f), new Vector3(0.06f, 0.06f, 0.06f),
            new Color(0.8f, 0.2f, 0.3f), placeableLayer);
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

    // ══════════════════════════════════════════════════════════════════
    // Placement Surfaces
    // ══════════════════════════════════════════════════════════════════

    private static PlacementSurface[] BuildPlacementSurfaces(FurnitureRefs refs)
    {
        var surfaces = new PlacementSurface[2];

        // Coffee table surface
        surfaces[0] = AddSurface(refs.coffeeTable, new Bounds(
            Vector3.zero, new Vector3(1.0f, 0.1f, 0.6f)));

        // Kitchen table surface
        surfaces[1] = AddSurface(refs.kitchenTable, new Bounds(
            Vector3.zero, new Vector3(1.2f, 0.1f, 0.8f)));

        return surfaces;
    }

    private static PlacementSurface AddSurface(GameObject surfaceGO, Bounds localBounds)
    {
        var surface = surfaceGO.AddComponent<PlacementSurface>();

        var so = new SerializedObject(surface);
        so.FindProperty("localBounds").boundsValue = localBounds;
        so.ApplyModifiedPropertiesWithoutUndo();

        return surface;
    }

    // ══════════════════════════════════════════════════════════════════
    // Spline Path (closed loop, 4 knots — Kitchen + Living Room)
    // ══════════════════════════════════════════════════════════════════

    private static SplineContainer BuildSplinePath()
    {
        var splineGO = new GameObject("ApartmentSplinePath");
        var container = splineGO.AddComponent<SplineContainer>();
        var spline = container.Spline;
        spline.Clear();

        // 4 knots orbiting the two-room zone
        var knots = new float3[]
        {
            new float3( -8.0f, 4.0f, -4.0f),  // SW (kitchen side)
            new float3( -8.0f, 4.0f,  5.0f),  // NW (living room side)
            new float3(  2.0f, 4.0f,  5.0f),  // NE
            new float3(  2.0f, 4.0f, -4.0f),  // SE
        };

        foreach (var pos in knots)
            spline.Add(new BezierKnot(pos), TangentMode.AutoSmooth);

        spline.Closed = true;

        Debug.Log($"[ApartmentSceneBuilder] Created spline with {knots.Length} knots (closed loop).");
        return container;
    }

    // ══════════════════════════════════════════════════════════════════
    // Cameras (browse with CinemachineSplineDolly + selected)
    // ══════════════════════════════════════════════════════════════════

    private struct CameraRefs
    {
        public CinemachineCamera browse;
        public CinemachineCamera selected;
        public CinemachineSplineDolly dolly;
    }

    private static CameraRefs BuildCamerasWithDolly(SplineContainer spline)
    {
        var parent = new GameObject("CinemachineCameras");

        // Browse camera with spline dolly
        var browseGO = new GameObject("Cam_Browse");
        browseGO.transform.SetParent(parent.transform);
        var browse = browseGO.AddComponent<CinemachineCamera>();
        var browseLens = LensSettings.Default;
        browseLens.FieldOfView = 55f;
        browseLens.NearClipPlane = 0.1f;
        browseLens.FarClipPlane = 500f;
        browse.Lens = browseLens;
        browse.Priority = 20;

        // Add spline dolly component
        var dolly = browseGO.AddComponent<CinemachineSplineDolly>();
        dolly.Spline = spline;
        dolly.CameraPosition = 0f;
        dolly.CameraRotation = CinemachineSplineDolly.RotationMode.Default;

        // Selected camera (starts inactive — positioned per-area by ApartmentManager)
        var selectedGO = new GameObject("Cam_Selected");
        selectedGO.transform.SetParent(parent.transform);
        selectedGO.transform.position = new Vector3(0f, 3.2f, -7.5f);
        var selected = selectedGO.AddComponent<CinemachineCamera>();
        var selectedLens = LensSettings.Default;
        selectedLens.FieldOfView = 48f;
        selectedLens.NearClipPlane = 0.1f;
        selectedLens.FarClipPlane = 500f;
        selected.Lens = selectedLens;
        selected.Priority = 0;

        return new CameraRefs { browse = browse, selected = selected, dolly = dolly };
    }

    // ══════════════════════════════════════════════════════════════════
    // Area ScriptableObjects (2 areas — Kitchen + Living Room)
    // ══════════════════════════════════════════════════════════════════

    private static ApartmentAreaDefinition[] BuildAreaDefinitions()
    {
        string soDir = "Assets/ScriptableObjects/Apartment";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Apartment");

        var kitchen = CreateAreaDef("Kitchen", soDir,
            "Kitchen", "Table with newspaper, fridge, counter, drink station.",
            StationType.DrinkMaking, 0.0f,
            new Vector3(-6.5f, 3.5f, -4.0f), new Vector3(35f, 45f, 0f), 48f);

        var livingRoom = CreateAreaDef("LivingRoom", soDir,
            "Living Room", "Bookcase, coffee table, couch.",
            StationType.Bookcase, 0.5f,
            new Vector3(-7.0f, 3.5f, 3.0f), new Vector3(30f, 60f, 0f), 48f);

        return new[] { kitchen, livingRoom };
    }

    private static ApartmentAreaDefinition CreateAreaDef(
        string assetName, string directory,
        string areaName, string description,
        StationType stationType, float splinePos,
        Vector3 selectedPos, Vector3 selectedRot, float selectedFOV)
    {
        var def = ScriptableObject.CreateInstance<ApartmentAreaDefinition>();
        def.areaName = areaName;
        def.description = description;
        def.stationType = stationType;
        def.splinePosition = splinePos;
        def.selectedPosition = selectedPos;
        def.selectedRotation = selectedRot;
        def.selectedFOV = selectedFOV;
        def.browseBlendDuration = 0.8f;
        def.selectBlendDuration = 0.5f;

        string path = $"{directory}/Area_{assetName}.asset";
        AssetDatabase.CreateAsset(def, path);

        return def;
    }

    private static TMP_Text CreateHUDText(string name, Transform parent,
        Vector2 anchoredPos, float fontSize, string defaultText)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 40f);
        rt.anchoredPosition = anchoredPos;
        rt.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return tmp;
    }

    // ══════════════════════════════════════════════════════════════════
    // Station Roots
    // ══════════════════════════════════════════════════════════════════

    private static void BuildStationRoots(
        BookInteractionManager bookManager,
        DrinkMakingStationData drinkData)
    {
        var parent = new GameObject("StationRoots");

        // Bookcase station (Living Room)
        CreateStationRoot(parent.transform, "Station_Bookcase",
            StationType.Bookcase, bookManager);

        // Drink Making station (Kitchen) — phase-gated to DateInProgress only
        var drinkRoot = CreateStationRoot(parent.transform, "Station_DrinkMaking",
            StationType.DrinkMaking, drinkData.manager,
            drinkData.hudRoot);
        var drinkRootSO = new SerializedObject(drinkRoot);
        var drinkCamsProp = drinkRootSO.FindProperty("stationCameras");
        drinkCamsProp.arraySize = 1;
        drinkCamsProp.GetArrayElementAtIndex(0).objectReferenceValue = drinkData.stationCamera;
        // Phase gating: only available during DateInProgress
        var phasesProp = drinkRootSO.FindProperty("availableInPhases");
        phasesProp.arraySize = 1;
        phasesProp.GetArrayElementAtIndex(0).intValue = (int)DayPhaseManager.DayPhase.DateInProgress;
        drinkRootSO.ApplyModifiedPropertiesWithoutUndo();

        // Note: Newspaper is DayPhaseManager-driven — no StationRoot needed
    }

    private static StationRoot CreateStationRoot(Transform parent, string name,
        StationType type, MonoBehaviour manager, GameObject hudRoot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var root = go.AddComponent<StationRoot>();

        var so = new SerializedObject(root);
        so.FindProperty("stationType").enumValueIndex = (int)type;
        if (manager != null)
            so.FindProperty("stationManager").objectReferenceValue = manager;
        if (hudRoot != null)
            so.FindProperty("hudRoot").objectReferenceValue = hudRoot;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    // ══════════════════════════════════════════════════════════════════
    // Newspaper Station (Kitchen) — Two-Page Spread
    // ══════════════════════════════════════════════════════════════════

    private struct NewspaperStationData
    {
        public NewspaperManager manager;
        public NewspaperHUD hud;
        public GameObject hudRoot;
        public CinemachineCamera stationCamera;
    }

    private static NewspaperStationData BuildNewspaperStation(GameObject camGO, int newspaperLayer)
    {
        // ── SO folder + assets ────────────────────────────────────
        string baseDir = "Assets/ScriptableObjects";
        if (!AssetDatabase.IsValidFolder(baseDir))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        string datingDir = $"{baseDir}/Dating";
        if (!AssetDatabase.IsValidFolder(datingDir))
            AssetDatabase.CreateFolder(baseDir, "Dating");

        // Date personal definitions
        string[] names = { "Rose", "Thorn", "Lily", "Moss" };
        string[] ads =
        {
            "Romantic soul seeks someone who won't wilt under pressure. Enjoys candlelit dinners and light rain.",
            "Sharp wit, sharper edges. Looking for someone who can handle a little prick. Gardeners welcome.",
            "Gentle spirit with a pure heart. Allergic to drama, loves ponds and moonlight.",
            "Low-maintenance, earthy, always there. Seeking someone who appreciates the ground floor."
        };
        float[] arrivalTimes = { 30f, 45f, 20f, 60f };

        var personalDefs = new DatePersonalDefinition[4];
        for (int i = 0; i < 4; i++)
        {
            string path = $"{datingDir}/Date_{names[i]}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DatePersonalDefinition>(path);
            if (existing != null)
            {
                personalDefs[i] = existing;
            }
            else
            {
                var def = ScriptableObject.CreateInstance<DatePersonalDefinition>();
                def.characterName = names[i];
                def.adText = ads[i];
                def.arrivalTimeSec = arrivalTimes[i];
                AssetDatabase.CreateAsset(def, path);
                personalDefs[i] = def;
            }
        }

        // Commercial definitions
        string[] bizNames = { "Bloom & Co. Fertilizer", "Petal's Pet Grooming", "The Rusty Trowel Pub" };
        string[] bizAds =
        {
            "Your plants deserve the best! Premium organic fertilizer. Now with 20% more nitrogen!",
            "Does your pet look like a weed? Let us trim them into shape! Walk-ins welcome.",
            "Cold pints, warm soil. Live music every Thursday. Happy hour 4-6 PM. No thorns at the bar."
        };

        var commercialDefs = new CommercialAdDefinition[3];
        for (int i = 0; i < 3; i++)
        {
            string path = $"{datingDir}/Commercial_{bizNames[i].Replace(" ", "_").Replace(".", "").Replace("'", "")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CommercialAdDefinition>(path);
            if (existing != null)
            {
                commercialDefs[i] = existing;
                continue;
            }
            var def = ScriptableObject.CreateInstance<CommercialAdDefinition>();
            def.businessName = bizNames[i];
            def.adText = bizAds[i];
            AssetDatabase.CreateAsset(def, path);
            commercialDefs[i] = def;
        }

        // Newspaper pool
        string poolPath = $"{datingDir}/NewspaperPool_Default.asset";
        var existingPool = AssetDatabase.LoadAssetAtPath<NewspaperPoolDefinition>(poolPath);
        NewspaperPoolDefinition pool;
        if (existingPool != null)
        {
            pool = existingPool;
        }
        else
        {
            pool = ScriptableObject.CreateInstance<NewspaperPoolDefinition>();
            pool.newspaperTitle = "The Daily Bloom";
            pool.personalAdsPerDay = 4;
            pool.commercialAdsPerDay = 3;
            pool.allowRepeats = false;
            AssetDatabase.CreateAsset(pool, poolPath);
        }

        var poolSO = new SerializedObject(pool);
        var personalsListProp = poolSO.FindProperty("personalAds");
        personalsListProp.ClearArray();
        for (int i = 0; i < personalDefs.Length; i++)
        {
            personalsListProp.InsertArrayElementAtIndex(i);
            personalsListProp.GetArrayElementAtIndex(i).objectReferenceValue = personalDefs[i];
        }
        var commercialsListProp = poolSO.FindProperty("commercialAds");
        commercialsListProp.ClearArray();
        for (int i = 0; i < commercialDefs.Length; i++)
        {
            commercialsListProp.InsertArrayElementAtIndex(i);
            commercialsListProp.GetArrayElementAtIndex(i).objectReferenceValue = commercialDefs[i];
        }
        poolSO.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        int adCount = Mathf.Clamp(pool.personalAdsPerDay, 1, 8);

        // ── Read camera (couch newspaper view) ──────────────────
        // Camera at couch looking +Z. Canvas at identity rotation faces -Z
        // (toward camera) — text reads correctly with no mirroring.
        var readCamGO = new GameObject("Cam_NewspaperRead");
        readCamGO.transform.position = new Vector3(-5.5f, 1.3f, 3.0f);
        readCamGO.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
        var readCam = readCamGO.AddComponent<CinemachineCamera>();
        var readLens = LensSettings.Default;
        readLens.FieldOfView = 48f;
        readLens.NearClipPlane = 0.1f;
        readLens.FarClipPlane = 100f;
        readCam.Lens = readLens;
        readCam.Priority = 0;

        // ── Newspaper parent ──────────────────────────────────────
        var parent = new GameObject("NewspaperStation");

        // ── Surface quad (invisible — physics only) ────────────────
        // Renderer disabled; canvas provides the paper visual.
        // MeshCollider is required for hit.textureCoord (scissors cutting).
        GameObject surfaceGO;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NewspaperModel.prefab");
        if (prefab != null)
        {
            surfaceGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            surfaceGO.name = "NewspaperSurface";
            surfaceGO.transform.SetParent(parent.transform);
            surfaceGO.transform.position = new Vector3(-5.5f, 1.1f, 5.0f);
            SetNewspaperLayerRecursive(surfaceGO, newspaperLayer);
        }
        else
        {
            surfaceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceGO.name = "NewspaperSurface";
            surfaceGO.transform.SetParent(parent.transform);
            surfaceGO.transform.position = new Vector3(-5.5f, 1.1f, 5.0f);
            surfaceGO.transform.rotation = Quaternion.identity;
            surfaceGO.transform.localScale = new Vector3(2.5f, 1.75f, 1f);
            surfaceGO.layer = newspaperLayer;
            // Ensure MeshCollider with mesh assigned (required for hit.textureCoord)
            var mc = surfaceGO.GetComponent<MeshCollider>();
            if (mc == null) mc = surfaceGO.AddComponent<MeshCollider>();
            mc.sharedMesh = surfaceGO.GetComponent<MeshFilter>().sharedMesh;
        }

        if (surfaceGO.GetComponent<NewspaperSurface>() == null)
            surfaceGO.AddComponent<NewspaperSurface>();

        // Surface quad is invisible — canvas provides the paper visual.
        // Quad only exists for MeshCollider (scissors raycasting).
        var surfRend = surfaceGO.GetComponent<Renderer>();
        if (surfRend == null) surfRend = surfaceGO.GetComponentInChildren<Renderer>();
        if (surfRend != null && prefab == null)
            surfRend.enabled = false;

        // ── WorldSpace canvas (newspaper text) ────────────────────
        // Camera looks +Z, canvas front faces -Z (toward camera) at identity.
        // No mirroring needed — positive scale on all axes.
        float canvasScale = 0.0025f;
        var pivotGO = new GameObject("NewspaperOverlayPivot");
        pivotGO.transform.SetParent(parent.transform);
        pivotGO.transform.position = new Vector3(-5.5f, 1.1f, 5.05f);
        pivotGO.transform.rotation = Quaternion.identity;
        pivotGO.transform.localScale = new Vector3(canvasScale, canvasScale, canvasScale);

        var canvasGO = new GameObject("NewspaperOverlay");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 0;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.SetParent(pivotGO.transform, false);
        canvasRT.anchorMin = new Vector2(0.5f, 0.5f);
        canvasRT.anchorMax = new Vector2(0.5f, 0.5f);
        canvasRT.pivot = new Vector2(0.5f, 0.5f);
        canvasRT.sizeDelta = new Vector2(NewspaperCanvasWidth, NewspaperCanvasHeight);
        canvasRT.localPosition = Vector3.zero;
        canvasRT.localRotation = Quaternion.identity;
        canvasRT.localScale = Vector3.one;

        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Opaque paper background
        var bgGO = new GameObject("PaperBackground");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bgRT.localScale = Vector3.one;
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.92f, 0.90f, 0.85f);
        bgImg.raycastTarget = false;

        // Center fold line
        var foldGO = new GameObject("FoldLine");
        foldGO.transform.SetParent(canvasGO.transform, false);
        var foldRT = foldGO.AddComponent<RectTransform>();
        foldRT.anchorMin = new Vector2(0.5f, 0.5f);
        foldRT.anchorMax = new Vector2(0.5f, 0.5f);
        foldRT.anchoredPosition = new Vector2(0f, 0f);
        foldRT.sizeDelta = new Vector2(2f, NewspaperCanvasHeight);
        foldRT.localScale = Vector3.one;
        foldGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.4f);

        // Left page (decorative articles)
        BuildNewspaperLeftPage(canvasGO.transform);

        // Right page header
        CreateNewspaperText("PersonalsLabel", canvasGO.transform,
            new Vector2(250f, 310f), new Vector2(440f, 35f),
            "PERSONALS", 28f, FontStyles.Bold, TextAlignmentOptions.Center);

        var pRuleGO = new GameObject("PersonalsRule");
        pRuleGO.transform.SetParent(canvasGO.transform, false);
        var pRuleRT = pRuleGO.AddComponent<RectTransform>();
        pRuleRT.anchorMin = new Vector2(0.5f, 0.5f);
        pRuleRT.anchorMax = new Vector2(0.5f, 0.5f);
        pRuleRT.anchoredPosition = new Vector2(250f, 290f);
        pRuleRT.sizeDelta = new Vector2(420f, 2f);
        pRuleRT.localScale = Vector3.one;
        pRuleGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f);

        // ── Flex layout for personal ads (right page) ─────────────
        float contentLeft = 520f;
        float contentRight = 980f;
        float contentBottom = 30f;
        float contentTop = 620f;
        float contentWidth = contentRight - contentLeft;
        float contentHeight = contentTop - contentBottom;
        float spacing = 10f;
        float slotHeight = (contentHeight - spacing * (adCount - 1)) / adCount;

        var personalSlots = new NewspaperAdSlot[adCount];

        for (int i = 0; i < adCount; i++)
        {
            float slotTopY = contentTop - i * (slotHeight + spacing);
            float slotCenterY = slotTopY - slotHeight * 0.5f;
            float anchoredX = (contentLeft + contentWidth * 0.5f) - NewspaperCanvasWidth * 0.5f;
            float anchoredY = slotCenterY - NewspaperCanvasHeight * 0.5f;

            float nameFontSize = Mathf.Clamp(slotHeight * 0.16f, 14f, 28f);
            float bodyFontSize = Mathf.Clamp(slotHeight * 0.11f, 10f, 18f);
            float phoneFontSize = Mathf.Clamp(slotHeight * 0.13f, 12f, 22f);
            float portraitSize = Mathf.Clamp(slotHeight * 0.35f, 32f, 48f);

            string prefix = $"Personal_{i}";

            // Slot background
            var slotBgGO = new GameObject($"{prefix}_BG");
            slotBgGO.transform.SetParent(canvasGO.transform, false);
            var slotBgRT = slotBgGO.AddComponent<RectTransform>();
            slotBgRT.anchorMin = new Vector2(0.5f, 0.5f);
            slotBgRT.anchorMax = new Vector2(0.5f, 0.5f);
            slotBgRT.anchoredPosition = new Vector2(anchoredX, anchoredY);
            slotBgRT.sizeDelta = new Vector2(contentWidth, slotHeight);
            slotBgRT.localScale = Vector3.one;
            slotBgGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.06f);

            // Name
            float nameOffsetY = slotHeight * 0.35f;
            var nameGO = CreateNewspaperText($"{prefix}_Name", canvasGO.transform,
                new Vector2(anchoredX - 20f, anchoredY + nameOffsetY),
                new Vector2(contentWidth - portraitSize - 20f, nameFontSize + 6f),
                "Name", nameFontSize, FontStyles.Bold, TextAlignmentOptions.Left);

            // Portrait
            var portraitGO = CreateNewspaperImage($"{prefix}_Portrait", canvasGO.transform,
                new Vector2(anchoredX + contentWidth * 0.5f - portraitSize * 0.5f - 5f,
                            anchoredY + nameOffsetY),
                new Vector2(portraitSize, portraitSize));

            // Ad body
            var adGO = CreateNewspaperText($"{prefix}_Ad", canvasGO.transform,
                new Vector2(anchoredX, anchoredY),
                new Vector2(contentWidth - 20f, slotHeight * 0.4f),
                "Ad text...", bodyFontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            // Phone number
            float phoneOffsetY = -slotHeight * 0.35f;
            var phoneGO = CreateNewspaperText($"{prefix}_Phone", canvasGO.transform,
                new Vector2(anchoredX, anchoredY + phoneOffsetY),
                new Vector2(contentWidth - 20f, phoneFontSize + 6f),
                "555-0000", phoneFontSize, FontStyles.Italic, TextAlignmentOptions.Left);

            // Logical ad slot with UV bounds
            float slotCenterX_abs = contentLeft + contentWidth * 0.5f;
            personalSlots[i] = CreateNewspaperAdSlot($"PersonalSlot_{i}",
                parent.transform, newspaperLayer,
                new Vector2(slotCenterX_abs, slotCenterY),
                new Vector2(contentWidth, slotHeight),
                new Vector2(NewspaperCanvasWidth, NewspaperCanvasHeight));

            var slotSO = new SerializedObject(personalSlots[i]);
            slotSO.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
            slotSO.FindProperty("adLabel").objectReferenceValue = adGO.GetComponent<TMP_Text>();
            slotSO.FindProperty("phoneNumberLabel").objectReferenceValue = phoneGO.GetComponent<TMP_Text>();
            slotSO.FindProperty("portraitImage").objectReferenceValue =
                portraitGO.GetComponent<UnityEngine.UI.Image>();
            slotSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // Start overlay hidden
        pivotGO.SetActive(false);

        // ── Scissors visual ───────────────────────────────────────
        var scissorsVisual = BuildNewspaperScissors();

        // ── Managers ──────────────────────────────────────────────
        var managersGO = new GameObject("NewspaperManagers");

        // DayManager
        var dayMgr = managersGO.AddComponent<DayManager>();
        var dayMgrSO = new SerializedObject(dayMgr);
        dayMgrSO.FindProperty("pool").objectReferenceValue = pool;
        dayMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // CutPathEvaluator
        var evalComp = managersGO.AddComponent<CutPathEvaluator>();

        // ScissorsCutController
        var cam = camGO.GetComponent<UnityEngine.Camera>();
        var scissorsCtrl = managersGO.AddComponent<ScissorsCutController>();
        var scissorsSO = new SerializedObject(scissorsCtrl);
        scissorsSO.FindProperty("surface").objectReferenceValue =
            surfaceGO.GetComponent<NewspaperSurface>();
        scissorsSO.FindProperty("cam").objectReferenceValue = cam;
        scissorsSO.FindProperty("newspaperLayer").intValue = 1 << newspaperLayer;
        scissorsSO.FindProperty("scissorsVisual").objectReferenceValue = scissorsVisual;
        scissorsSO.FindProperty("surfaceOffset").floatValue = -0.05f;
        scissorsSO.ApplyModifiedPropertiesWithoutUndo();

        // NewspaperManager
        var newsMgr = managersGO.AddComponent<NewspaperManager>();
        var newsMgrSO = new SerializedObject(newsMgr);
        newsMgrSO.FindProperty("dayManager").objectReferenceValue = dayMgr;
        newsMgrSO.FindProperty("scissorsController").objectReferenceValue = scissorsCtrl;
        newsMgrSO.FindProperty("surface").objectReferenceValue =
            surfaceGO.GetComponent<NewspaperSurface>();
        newsMgrSO.FindProperty("evaluator").objectReferenceValue = evalComp;
        newsMgrSO.FindProperty("mainCamera").objectReferenceValue = cam;
        newsMgrSO.FindProperty("newspaperOverlay").objectReferenceValue = pivotGO;
        newsMgrSO.FindProperty("readCamera").objectReferenceValue = readCam;
        newsMgrSO.FindProperty("brain").objectReferenceValue =
            camGO.GetComponent<CinemachineBrain>();
        newsMgrSO.FindProperty("newspaperTransform").objectReferenceValue =
            surfaceGO.transform;

        var personalSlotsProp = newsMgrSO.FindProperty("personalSlots");
        personalSlotsProp.ClearArray();
        for (int i = 0; i < personalSlots.Length; i++)
        {
            personalSlotsProp.InsertArrayElementAtIndex(i);
            personalSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = personalSlots[i];
        }

        var commercialSlotsProp = newsMgrSO.FindProperty("commercialSlots");
        commercialSlotsProp.ClearArray();

        newsMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // NewspaperHUD
        var newsHud = managersGO.AddComponent<NewspaperHUD>();

        // ── HUD Canvas (screen-space overlay) ─────────────────────
        var hudCanvasGO = new GameObject("NewspaperHUD_Canvas");
        hudCanvasGO.transform.SetParent(managersGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        var hudScaler = hudCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        hudScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        hudScaler.referenceResolution = new Vector2(1920f, 1080f);
        hudCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Day label (top-left)
        var dayLabelTMP = CreateHUDText("DayLabel", hudCanvasGO.transform,
            new Vector2(-400f, 200f), 28f, "Day 1");

        // Instruction label (bottom-center)
        var instrTMP = CreateHUDText("InstructionLabel", hudCanvasGO.transform,
            new Vector2(0f, -200f), 18f, "Draw around a phone number to cut it out");

        // Calling UI panel
        var callingPanel = new GameObject("CallingUI");
        callingPanel.transform.SetParent(hudCanvasGO.transform);
        var callingPanelRT = callingPanel.AddComponent<RectTransform>();
        callingPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        callingPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        callingPanelRT.sizeDelta = new Vector2(500f, 100f);
        callingPanelRT.anchoredPosition = Vector2.zero;
        callingPanelRT.localScale = Vector3.one;
        callingPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var callingTextGO = new GameObject("Text");
        callingTextGO.transform.SetParent(callingPanel.transform);
        var callingTextRT = callingTextGO.AddComponent<RectTransform>();
        callingTextRT.anchorMin = Vector2.zero;
        callingTextRT.anchorMax = Vector2.one;
        callingTextRT.offsetMin = new Vector2(10f, 10f);
        callingTextRT.offsetMax = new Vector2(-10f, -10f);
        callingTextRT.localScale = Vector3.one;
        var callingTMP = callingTextGO.AddComponent<TextMeshProUGUI>();
        callingTMP.text = "Calling...";
        callingTMP.fontSize = 32f;
        callingTMP.alignment = TextAlignmentOptions.Center;
        callingTMP.color = Color.white;
        callingPanel.SetActive(false);

        // Wire NewspaperManager UI
        var newsMgrSO2 = new SerializedObject(newsMgr);
        newsMgrSO2.FindProperty("callingUI").objectReferenceValue = callingPanel;
        newsMgrSO2.FindProperty("callingText").objectReferenceValue = callingTMP;
        newsMgrSO2.ApplyModifiedPropertiesWithoutUndo();

        // Wire NewspaperHUD
        var newsHudSO = new SerializedObject(newsHud);
        newsHudSO.FindProperty("dayManager").objectReferenceValue = dayMgr;
        newsHudSO.FindProperty("manager").objectReferenceValue = newsMgr;
        newsHudSO.FindProperty("dayLabel").objectReferenceValue = dayLabelTMP;
        newsHudSO.FindProperty("instructionLabel").objectReferenceValue = instrTMP;
        newsHudSO.ApplyModifiedPropertiesWithoutUndo();

        // Newspaper is DayPhaseManager-driven (not a StationRoot station)
        // Start enabled — DayPhaseManager will control activation via events
        newsMgr.enabled = true;
        hudCanvasGO.SetActive(true);

        Debug.Log("[ApartmentSceneBuilder] Newspaper station built (two-page spread, DayPhaseManager-driven).");

        return new NewspaperStationData
        {
            manager = newsMgr,
            hud = newsHud,
            hudRoot = hudCanvasGO,
            stationCamera = readCam
        };
    }

    // ── Newspaper left page (decorative articles) ─────────────────

    private static void BuildNewspaperLeftPage(Transform canvasTransform)
    {
        float leftCenter = -250f;

        CreateNewspaperText("Title", canvasTransform,
            new Vector2(leftCenter, 300f), new Vector2(460f, 50f),
            "THE DAILY BLOOM", 38f, FontStyles.Bold, TextAlignmentOptions.Center);

        var titleRuleGO = new GameObject("TitleRule");
        titleRuleGO.transform.SetParent(canvasTransform, false);
        var titleRuleRT = titleRuleGO.AddComponent<RectTransform>();
        titleRuleRT.anchorMin = new Vector2(0.5f, 0.5f);
        titleRuleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRuleRT.anchoredPosition = new Vector2(leftCenter, 270f);
        titleRuleRT.sizeDelta = new Vector2(420f, 2f);
        titleRuleRT.localScale = Vector3.one;
        titleRuleGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f);

        CreateNewspaperText("DateLine", canvasTransform,
            new Vector2(leftCenter, 252f), new Vector2(420f, 20f),
            "Vol. XLII  No. 7  |  The Garden District Gazette", 12f,
            FontStyles.Italic, TextAlignmentOptions.Center);

        CreateNewspaperText("Headline1", canvasTransform,
            new Vector2(leftCenter, 215f), new Vector2(420f, 35f),
            "MYSTERIOUS BLOOM SPOTTED IN TOWN SQUARE", 20f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        CreateNewspaperText("Body1", canvasTransform,
            new Vector2(leftCenter, 155f), new Vector2(420f, 80f),
            "Residents were astonished yesterday when a never-before-seen flower appeared overnight in the central fountain. Botanists remain baffled. \"It smells like Tuesday,\" said one local.",
            13f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        var rule2GO = new GameObject("Rule2");
        rule2GO.transform.SetParent(canvasTransform, false);
        var rule2RT = rule2GO.AddComponent<RectTransform>();
        rule2RT.anchorMin = new Vector2(0.5f, 0.5f);
        rule2RT.anchorMax = new Vector2(0.5f, 0.5f);
        rule2RT.anchoredPosition = new Vector2(leftCenter, 108f);
        rule2RT.sizeDelta = new Vector2(420f, 1f);
        rule2RT.localScale = Vector3.one;
        rule2GO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        CreateNewspaperText("Headline2", canvasTransform,
            new Vector2(leftCenter, 85f), new Vector2(420f, 30f),
            "ANNUAL PRUNING FESTIVAL DRAWS RECORD CROWDS", 18f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        CreateNewspaperText("Body2", canvasTransform,
            new Vector2(leftCenter, 25f), new Vector2(420f, 80f),
            "The 47th Annual Pruning Festival exceeded all expectations with over 200 attendees. Highlights included the competitive hedge-sculpting finals and Mrs. Fernsby's award-winning topiary swan.",
            13f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        var rule3GO = new GameObject("Rule3");
        rule3GO.transform.SetParent(canvasTransform, false);
        var rule3RT = rule3GO.AddComponent<RectTransform>();
        rule3RT.anchorMin = new Vector2(0.5f, 0.5f);
        rule3RT.anchorMax = new Vector2(0.5f, 0.5f);
        rule3RT.anchoredPosition = new Vector2(leftCenter, -22f);
        rule3RT.sizeDelta = new Vector2(420f, 1f);
        rule3RT.localScale = Vector3.one;
        rule3GO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        CreateNewspaperText("Headline3", canvasTransform,
            new Vector2(leftCenter, -45f), new Vector2(420f, 30f),
            "WEATHER: PARTLY SUNNY WITH CHANCE OF PETALS", 16f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        CreateNewspaperText("Body3", canvasTransform,
            new Vector2(leftCenter, -100f), new Vector2(420f, 70f),
            "Meteorologists predict a mild week ahead with occasional floral precipitation. Residents advised to carry umbrellas and enjoy the fragrance.",
            12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        CreateNewspaperText("Filler", canvasTransform,
            new Vector2(leftCenter, -185f), new Vector2(420f, 100f),
            "~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~",
            10f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
    }

    // ── Newspaper ad slot (logical, UV bounds for cut evaluation) ──

    private static NewspaperAdSlot CreateNewspaperAdSlot(string name, Transform parent,
        int layer, Vector2 centerPx, Vector2 sizePx, Vector2 virtualSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.layer = layer;

        var rt = go.AddComponent<RectTransform>();
        rt.localPosition = Vector3.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.sizeDelta = sizePx;

        var slot = go.AddComponent<NewspaperAdSlot>();

        // Direct UV mapping — camera looks +Z, no mirroring needed
        float uMin = (centerPx.x - sizePx.x * 0.5f) / virtualSize.x;
        float vMin = (centerPx.y - sizePx.y * 0.5f) / virtualSize.y;
        float uWidth = sizePx.x / virtualSize.x;
        float vHeight = sizePx.y / virtualSize.y;

        var so = new SerializedObject(slot);
        so.FindProperty("slotRect").objectReferenceValue = rt;
        so.FindProperty("normalizedBounds").rectValue = new Rect(uMin, vMin, uWidth, vHeight);
        so.ApplyModifiedPropertiesWithoutUndo();

        return slot;
    }

    // ── Newspaper scissors visual ─────────────────────────────────

    private static Transform BuildNewspaperScissors()
    {
        var parentGO = new GameObject("ScissorsVisual");
        parentGO.transform.position = Vector3.zero;

        var bladeA = CreateBox("Blade_A", parentGO.transform,
            new Vector3(0f, 0.004f, 0.05f), new Vector3(0.012f, 0.006f, 0.10f),
            new Color(0.75f, 0.75f, 0.8f));
        bladeA.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        bladeA.isStatic = false;

        var bladeB = CreateBox("Blade_B", parentGO.transform,
            new Vector3(0f, 0.004f, 0.05f), new Vector3(0.012f, 0.006f, 0.10f),
            new Color(0.75f, 0.75f, 0.8f));
        bladeB.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
        bladeB.isStatic = false;

        var pivot = CreateBox("Pivot", parentGO.transform,
            Vector3.zero, new Vector3(0.018f, 0.008f, 0.018f),
            new Color(0.3f, 0.3f, 0.3f));
        pivot.isStatic = false;

        parentGO.SetActive(false);
        return parentGO.transform;
    }

    // ── Newspaper canvas helpers (WorldSpace) ─────────────────────

    private static TMP_FontAsset s_newspaperFont;

    private static GameObject CreateNewspaperText(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();

        if (s_newspaperFont == null)
            s_newspaperFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (s_newspaperFont != null) tmp.font = s_newspaperFont;

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = new Color(0.1f, 0.1f, 0.1f);
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private static GameObject CreateNewspaperImage(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.white;
        img.preserveAspect = true;

        go.SetActive(false);
        return go;
    }

    private static void SetNewspaperLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetNewspaperLayerRecursive(child.gameObject, layer);
    }

    // ══════════════════════════════════════════════════════════════════
    // ApartmentManager + Screen-Space UI
    // ══════════════════════════════════════════════════════════════════

    private static GameObject BuildApartmentManager(
        CinemachineCamera browseCam, CinemachineCamera selectedCam,
        CinemachineSplineDolly dolly,
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
            "Entrance", 28f, new Color(0f, 0f, 0f, 0.6f));

        // Browse hints panel (bottom center)
        var browseHints = CreateUIPanel("BrowseHintsPanel", uiCanvasGO.transform,
            new Vector2(0f, -200f), new Vector2(500f, 50f),
            "A / D  Cycle Areas    |    Enter  Select", 16f,
            new Color(0f, 0f, 0f, 0.5f));

        // Selected hints panel (bottom center)
        var selectedHints = CreateUIPanel("SelectedHintsPanel", uiCanvasGO.transform,
            new Vector2(0f, -200f), new Vector2(600f, 50f),
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
        so.FindProperty("browseDolly").objectReferenceValue = dolly;

        // Interaction
        so.FindProperty("objectGrabber").objectReferenceValue = grabber;

        // UI
        so.FindProperty("areaNamePanel").objectReferenceValue = areaNamePanel;
        so.FindProperty("areaNameText").objectReferenceValue =
            areaNamePanel.GetComponentInChildren<TMP_Text>();
        so.FindProperty("browseHintsPanel").objectReferenceValue = browseHints;
        so.FindProperty("selectedHintsPanel").objectReferenceValue = selectedHints;

        so.ApplyModifiedPropertiesWithoutUndo();

        // Start hidden — DayPhaseManager shows it when entering Exploration
        uiCanvasGO.SetActive(false);

        return uiCanvasGO;
    }

    // ══════════════════════════════════════════════════════════════════
    // BookInteractionManager
    // ══════════════════════════════════════════════════════════════════

    private static BookInteractionManager BuildBookInteractionManager(
        GameObject camGO, int booksLayer,
        int drawersLayer, int perfumesLayer, int trinketsLayer, int coffeeTableBooksLayer)
    {
        var managerGO = new GameObject("BookInteractionManager");
        var manager = managerGO.AddComponent<BookInteractionManager>();

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

        var cam = camGO.GetComponent<UnityEngine.Camera>();

        var so = new SerializedObject(manager);
        so.FindProperty("readingAnchor").objectReferenceValue = anchorGO.transform;
        so.FindProperty("mainCamera").objectReferenceValue = cam;
        so.FindProperty("titleHintPanel").objectReferenceValue = hintPanel;
        so.FindProperty("titleHintText").objectReferenceValue = tmp;
        so.FindProperty("booksLayerMask").intValue = 1 << booksLayer;
        so.FindProperty("drawersLayerMask").intValue = 1 << drawersLayer;
        so.FindProperty("perfumesLayerMask").intValue = 1 << perfumesLayer;
        so.FindProperty("trinketsLayerMask").intValue = 1 << trinketsLayer;
        so.FindProperty("coffeeTableBooksLayerMask").intValue = 1 << coffeeTableBooksLayer;
        so.FindProperty("maxRayDistance").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

        manager.enabled = false;

        return manager;
    }

    // ══════════════════════════════════════════════════════════════════
    // Drink Making Station (Kitchen)
    // ══════════════════════════════════════════════════════════════════

    private struct DrinkMakingStationData
    {
        public DrinkMakingManager manager;
        public DrinkMakingHUD hud;
        public GameObject hudRoot;
        public CinemachineCamera stationCamera;
    }

    private static DrinkMakingStationData BuildDrinkMakingStation(GameObject camGO)
    {
        // ── SO folder ────────────────────────────────────────────────
        string soDir = "Assets/ScriptableObjects/DrinkMaking";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "DrinkMaking");

        // ── Load or create ingredient SOs ─────────────────────────────
        string[] ingredNames = { "Gin", "Tonic", "Lemon", "Syrup", "Bitters", "Soda", "Grenadine" };
        Color[] ingredColors =
        {
            new Color(0.85f, 0.88f, 0.90f, 0.3f),
            new Color(0.90f, 0.92f, 0.95f, 0.2f),
            new Color(1f, 0.95f, 0.3f, 0.5f),
            new Color(0.85f, 0.70f, 0.30f, 0.4f),
            new Color(0.4f, 0.2f, 0.1f, 0.6f),
            new Color(0.92f, 0.95f, 0.98f, 0.15f),
            new Color(0.9f, 0.15f, 0.2f, 0.5f),
        };

        var ingredients = new DrinkIngredientDefinition[ingredNames.Length];
        for (int i = 0; i < ingredNames.Length; i++)
        {
            string path = $"{soDir}/Ingredient_{ingredNames[i]}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DrinkIngredientDefinition>(path);
            if (existing != null) { ingredients[i] = existing; continue; }

            var def = ScriptableObject.CreateInstance<DrinkIngredientDefinition>();
            def.ingredientName = ingredNames[i];
            def.liquidColor = ingredColors[i];
            def.pourRate = 0.3f;
            AssetDatabase.CreateAsset(def, path);
            ingredients[i] = def;
        }

        // ── Glass definitions ─────────────────────────────────────────
        string[] glassNames = { "Tumbler", "Highball", "Coupe" };
        float[] glassFills = { 0.7f, 0.75f, 0.65f };
        float[] glassTols = { 0.15f, 0.12f, 0.10f };
        float[] glassMaxs = { 1.0f, 1.2f, 0.8f };

        var glasses = new GlassDefinition[glassNames.Length];
        for (int i = 0; i < glassNames.Length; i++)
        {
            string path = $"{soDir}/Glass_{glassNames[i]}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GlassDefinition>(path);
            if (existing != null) { glasses[i] = existing; continue; }

            var def = ScriptableObject.CreateInstance<GlassDefinition>();
            def.glassName = glassNames[i];
            def.fillLineNormalized = glassFills[i];
            def.fillLineTolerance = glassTols[i];
            def.capacity = glassMaxs[i];
            AssetDatabase.CreateAsset(def, path);
            glasses[i] = def;
        }

        // ── Recipe definitions ─────────────────────────────────────────
        string[] recipeNames = { "Gin & Tonic", "Lemon Drop", "Bitter Fizz", "Sunset Sip" };
        int[] recipeGlassIdx = { 1, 2, 0, 1 };
        bool[] recipeStir = { false, true, true, false };
        float[] recipeDur = { 0f, 2f, 1.5f, 0f };
        int[] recipeBase = { 100, 120, 110, 90 };

        var recipes = new DrinkRecipeDefinition[recipeNames.Length];
        for (int i = 0; i < recipeNames.Length; i++)
        {
            string path = $"{soDir}/Recipe_{recipeNames[i].Replace(" ", "_").Replace("&", "and")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DrinkRecipeDefinition>(path);
            if (existing != null) { recipes[i] = existing; continue; }

            var def = ScriptableObject.CreateInstance<DrinkRecipeDefinition>();
            def.drinkName = recipeNames[i];
            def.requiredGlass = glasses[recipeGlassIdx[i]];
            def.requiresStir = recipeStir[i];
            def.stirDuration = recipeDur[i];
            def.baseScore = recipeBase[i];
            AssetDatabase.CreateAsset(def, path);
            recipes[i] = def;
        }

        AssetDatabase.SaveAssets();

        // ── Station camera ────────────────────────────────────────────
        var drinkCamGO = new GameObject("Cam_DrinkMaking");
        drinkCamGO.transform.position = new Vector3(-4f, 1.5f, -4.5f);
        drinkCamGO.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        var drinkCam = drinkCamGO.AddComponent<CinemachineCamera>();
        var drinkLens = LensSettings.Default;
        drinkLens.FieldOfView = 50f;
        drinkLens.NearClipPlane = 0.1f;
        drinkLens.FarClipPlane = 100f;
        drinkCam.Lens = drinkLens;
        drinkCam.Priority = 0;

        // ── Counter geometry (bottles, glass, stirrer) ────────────────
        var stationParent = new GameObject("DrinkMakingStation");

        // Glass on counter
        var glassGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        glassGO.name = "Glass";
        glassGO.transform.SetParent(stationParent.transform);
        glassGO.transform.position = new Vector3(-4f, 0.95f, -5.2f);
        glassGO.transform.localScale = new Vector3(0.08f, 0.10f, 0.08f);
        glassGO.isStatic = false;
        var glassRend = glassGO.GetComponent<Renderer>();
        if (glassRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.85f, 0.9f, 0.95f, 0.5f);
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glassRend.sharedMaterial = mat;
        }
        var glassCtrl = glassGO.AddComponent<GlassController>();

        // Stirrer
        var stirGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stirGO.name = "Stirrer";
        stirGO.transform.SetParent(stationParent.transform);
        stirGO.transform.position = new Vector3(-3.85f, 0.98f, -5.2f);
        stirGO.transform.localScale = new Vector3(0.008f, 0.12f, 0.008f);
        stirGO.isStatic = false;
        var stirRend = stirGO.GetComponent<Renderer>();
        if (stirRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.30f, 0.30f, 0.32f);
            stirRend.sharedMaterial = mat;
        }
        var stirCol = stirGO.GetComponent<Collider>();
        if (stirCol != null) Object.DestroyImmediate(stirCol);
        var stirCtrl = stirGO.AddComponent<StirController>();

        // Bottles (7 bottles along the counter back)
        var bottleCtrls = new BottleController[ingredients.Length];
        for (int i = 0; i < ingredients.Length; i++)
        {
            float x = -5.2f + i * 0.35f;
            var bottleGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottleGO.name = $"Bottle_{ingredNames[i]}";
            bottleGO.transform.SetParent(stationParent.transform);
            bottleGO.transform.position = new Vector3(x, 1.05f, -5.5f);
            bottleGO.transform.localScale = new Vector3(0.04f, 0.12f, 0.04f);
            bottleGO.isStatic = false;

            var bRend = bottleGO.GetComponent<Renderer>();
            if (bRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                       ?? Shader.Find("Standard"));
                Color c = ingredColors[i];
                mat.color = new Color(c.r, c.g, c.b, 1f);
                bRend.sharedMaterial = mat;
            }

            var bc = bottleGO.AddComponent<BottleController>();
            bc.ingredient = ingredients[i];
            bottleCtrls[i] = bc;
        }

        // ── Managers ──────────────────────────────────────────────────
        var managersGO = new GameObject("DrinkMakingManagers");
        var mgr = managersGO.AddComponent<DrinkMakingManager>();
        var hud = managersGO.AddComponent<DrinkMakingHUD>();

        var cam = camGO.GetComponent<UnityEngine.Camera>();

        // Wire manager
        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("glass").objectReferenceValue = glassCtrl;
        mgrSO.FindProperty("stirrer").objectReferenceValue = stirCtrl;
        mgrSO.FindProperty("mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("hud").objectReferenceValue = hud;

        var bottlesProp = mgrSO.FindProperty("bottles");
        bottlesProp.arraySize = bottleCtrls.Length;
        for (int i = 0; i < bottleCtrls.Length; i++)
            bottlesProp.GetArrayElementAtIndex(i).objectReferenceValue = bottleCtrls[i];

        var recipesProp = mgrSO.FindProperty("availableRecipes");
        recipesProp.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
            recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];

        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── HUD Canvas ────────────────────────────────────────────────
        var hudCanvasGO = new GameObject("DrinkMakingHUD_Canvas");
        hudCanvasGO.transform.SetParent(managersGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        hudCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        hudCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var recipeNameLabel = CreateHUDText("RecipeNameLabel", hudCanvasGO.transform,
            new Vector2(0f, 200f), 24f, "Choose a Recipe");
        var instructionLabel = CreateHUDText("InstructionLabel", hudCanvasGO.transform,
            new Vector2(0f, 160f), 18f, "");
        var fillLevelLabel = CreateHUDText("FillLevelLabel", hudCanvasGO.transform,
            new Vector2(250f, 30f), 18f, "");
        var scoreLabel = CreateHUDText("ScoreLabel", hudCanvasGO.transform,
            new Vector2(0f, 0f), 28f, "");

        // Wire HUD (public fields)
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("glass").objectReferenceValue = glassCtrl;
        hudSO.FindProperty("stirrer").objectReferenceValue = stirCtrl;
        hudSO.FindProperty("recipeNameLabel").objectReferenceValue = recipeNameLabel;
        hudSO.FindProperty("instructionLabel").objectReferenceValue = instructionLabel;
        hudSO.FindProperty("fillLevelLabel").objectReferenceValue = fillLevelLabel;
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // Start disabled (StationRoot will enable)
        mgr.enabled = false;
        hudCanvasGO.SetActive(false);

        Debug.Log("[ApartmentSceneBuilder] Drink Making station built.");

        return new DrinkMakingStationData
        {
            manager = mgr,
            hud = hud,
            hudRoot = hudCanvasGO,
            stationCamera = drinkCam
        };
    }

    // ══════════════════════════════════════════════════════════════════
    // Dating Infrastructure
    // ══════════════════════════════════════════════════════════════════

    private static void BuildDatingInfrastructure(
        GameObject camGO, FurnitureRefs furnitureRefs,
        NewspaperStationData newspaperData, int phoneLayer)
    {
        var managersGO = new GameObject("DatingManagers");

        // ── MoodMachine + Profile ─────────────────────────────────────
        string soDir = "Assets/ScriptableObjects/Apartment";
        if (!AssetDatabase.IsValidFolder(soDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Apartment");
        }

        string profilePath = $"{soDir}/MoodProfile_DatingLoop.asset";
        var moodProfile = AssetDatabase.LoadAssetAtPath<MoodMachineProfile>(profilePath);
        if (moodProfile == null)
        {
            moodProfile = ScriptableObject.CreateInstance<MoodMachineProfile>();

            var lightGrad = new Gradient();
            lightGrad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f),
                        new GradientColorKey(new Color(0.9f, 0.6f, 0.4f), 0.5f),
                        new GradientColorKey(new Color(0.3f, 0.3f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            moodProfile.lightColor = lightGrad;

            var ambientGrad = new Gradient();
            ambientGrad.SetKeys(
                new[] { new GradientColorKey(new Color(0.4f, 0.45f, 0.55f), 0f),
                        new GradientColorKey(new Color(0.3f, 0.25f, 0.35f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            moodProfile.ambientColor = ambientGrad;

            var fogGrad = new Gradient();
            fogGrad.SetKeys(
                new[] { new GradientColorKey(new Color(0.5f, 0.55f, 0.6f), 0f),
                        new GradientColorKey(new Color(0.2f, 0.2f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            moodProfile.fogColor = fogGrad;

            moodProfile.lightIntensity = AnimationCurve.Linear(0f, 1.2f, 1f, 0.4f);
            moodProfile.fogDensity = AnimationCurve.Linear(0f, 0f, 1f, 0.02f);
            moodProfile.rainRate = AnimationCurve.Linear(0f, 0f, 1f, 50f);

            AssetDatabase.CreateAsset(moodProfile, profilePath);
        }

        var directionalLight = GameObject.Find("Directional Light");
        var moodMachine = managersGO.AddComponent<MoodMachine>();
        var mmSO = new SerializedObject(moodMachine);
        mmSO.FindProperty("profile").objectReferenceValue = moodProfile;
        if (directionalLight != null)
            mmSO.FindProperty("directionalLight").objectReferenceValue =
                directionalLight.GetComponent<Light>();
        mmSO.ApplyModifiedPropertiesWithoutUndo();

        // ── GameClock ─────────────────────────────────────────────────
        // DayManager was built inside BuildNewspaperStation — find it
        var dayMgr = Object.FindAnyObjectByType<DayManager>();

        var gameClock = managersGO.AddComponent<GameClock>();
        var gcSO = new SerializedObject(gameClock);
        if (dayMgr != null)
            gcSO.FindProperty("dayManager").objectReferenceValue = dayMgr;
        gcSO.ApplyModifiedPropertiesWithoutUndo();

        // ── DateSessionManager ────────────────────────────────────────
        var dateSessionMgr = managersGO.AddComponent<DateSessionManager>();
        var dsmSO = new SerializedObject(dateSessionMgr);
        if (furnitureRefs.dateSpawnPoint != null)
            dsmSO.FindProperty("dateSpawnPoint").objectReferenceValue = furnitureRefs.dateSpawnPoint;
        if (furnitureRefs.couchSeatTarget != null)
            dsmSO.FindProperty("couchSeatTarget").objectReferenceValue = furnitureRefs.couchSeatTarget;
        if (furnitureRefs.coffeeTableDeliveryPoint != null)
            dsmSO.FindProperty("coffeeTableDeliveryPoint").objectReferenceValue =
                furnitureRefs.coffeeTableDeliveryPoint;
        dsmSO.ApplyModifiedPropertiesWithoutUndo();

        // ── PhoneController ───────────────────────────────────────────
        PhoneController phoneCtrl = null;
        if (furnitureRefs.phoneTransform != null)
        {
            var phoneGO = furnitureRefs.phoneTransform.gameObject;
            phoneGO.layer = phoneLayer;
            phoneCtrl = phoneGO.AddComponent<PhoneController>();
            var pcSO = new SerializedObject(phoneCtrl);
            if (phoneGO.transform.childCount > 0)
                pcSO.FindProperty("ringVisual").objectReferenceValue =
                    phoneGO.transform.GetChild(0).gameObject;
            pcSO.FindProperty("phoneLayer").intValue = 1 << phoneLayer;
            pcSO.ApplyModifiedPropertiesWithoutUndo();

            // Phone StationRoot (always available — ambient click)
            var phoneStationGO = new GameObject("Station_Phone");
            phoneStationGO.transform.SetParent(GameObject.Find("StationRoots")?.transform);
            var phoneRoot = phoneStationGO.AddComponent<StationRoot>();
            var phoneRootSO = new SerializedObject(phoneRoot);
            phoneRootSO.FindProperty("stationType").enumValueIndex = (int)StationType.Phone;
            phoneRootSO.FindProperty("stationManager").objectReferenceValue = phoneCtrl;
            phoneRootSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── CoffeeTableDelivery ───────────────────────────────────────
        if (furnitureRefs.coffeeTableDeliveryPoint != null)
        {
            var coffeeDelivery = furnitureRefs.coffeeTableDeliveryPoint.gameObject
                .AddComponent<CoffeeTableDelivery>();
            var cdSO = new SerializedObject(coffeeDelivery);
            cdSO.FindProperty("drinkSpawnPoint").objectReferenceValue =
                furnitureRefs.coffeeTableDeliveryPoint;
            cdSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── DateEndScreen ─────────────────────────────────────────────
        var dateEndScreen = managersGO.AddComponent<DateEndScreen>();
        BuildDateEndScreenUI(managersGO, dateEndScreen, gameClock);

        // ── DateSessionHUD ────────────────────────────────────────────
        BuildDateSessionHUD(managersGO);

        Debug.Log("[ApartmentSceneBuilder] Dating infrastructure built.");
    }

    private static void BuildDateEndScreenUI(GameObject parent,
        DateEndScreen dateEndScreen, GameClock gameClock)
    {
        // End screen canvas
        var canvasGO = new GameObject("DateEndScreen_Canvas");
        canvasGO.transform.SetParent(parent.transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Panel background
        var panelGO = new GameObject("EndPanel");
        panelGO.transform.SetParent(canvasGO.transform);
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.2f, 0.15f);
        panelRT.anchorMax = new Vector2(0.8f, 0.85f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelRT.localScale = Vector3.one;
        panelGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

        var dateNameTMP = CreateHUDText("DateName", panelGO.transform,
            new Vector2(0f, 150f), 28f, "Date Name");
        var affectionTMP = CreateHUDText("AffectionScore", panelGO.transform,
            new Vector2(0f, 80f), 22f, "Affection: 50");
        var gradeTMP = CreateHUDText("Grade", panelGO.transform,
            new Vector2(0f, 20f), 48f, "B");
        var summaryTMP = CreateHUDText("Summary", panelGO.transform,
            new Vector2(0f, -60f), 16f, "A decent evening together.");

        // Continue button
        var btnGO = new GameObject("ContinueButton");
        btnGO.transform.SetParent(panelGO.transform);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0f);
        btnRT.anchorMax = new Vector2(0.5f, 0f);
        btnRT.pivot = new Vector2(0.5f, 0f);
        btnRT.sizeDelta = new Vector2(200f, 50f);
        btnRT.anchoredPosition = new Vector2(0f, 30f);
        btnRT.localScale = Vector3.one;
        btnGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.5f, 0.3f);
        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();

        var btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(btnGO.transform);
        var btnLabelRT = btnLabel.AddComponent<RectTransform>();
        btnLabelRT.anchorMin = Vector2.zero;
        btnLabelRT.anchorMax = Vector2.one;
        btnLabelRT.offsetMin = Vector2.zero;
        btnLabelRT.offsetMax = Vector2.zero;
        btnLabelRT.localScale = Vector3.one;
        var btnTMP = btnLabel.AddComponent<TextMeshProUGUI>();
        btnTMP.text = "Go to Bed";
        btnTMP.fontSize = 20f;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = Color.white;

        // Wire continue button → GameClock.GoToBed
        if (gameClock != null)
        {
            var goToBedAction = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), gameClock,
                typeof(GameClock).GetMethod(nameof(GameClock.GoToBed),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                as UnityEngine.Events.UnityAction;
            if (goToBedAction != null)
                UnityEventTools.AddPersistentListener(btn.onClick, goToBedAction);
        }

        panelGO.SetActive(false);

        // Wire DateEndScreen
        var endSO = new SerializedObject(dateEndScreen);
        endSO.FindProperty("screenRoot").objectReferenceValue = panelGO;
        endSO.FindProperty("dateNameText").objectReferenceValue = dateNameTMP;
        endSO.FindProperty("affectionScoreText").objectReferenceValue = affectionTMP;
        endSO.FindProperty("gradeText").objectReferenceValue = gradeTMP;
        endSO.FindProperty("summaryText").objectReferenceValue = summaryTMP;
        endSO.FindProperty("continueButton").objectReferenceValue = btn;
        endSO.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildDateSessionHUD(GameObject parent)
    {
        var canvasGO = new GameObject("DateSessionHUD_Canvas");
        canvasGO.transform.SetParent(parent.transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // HUD root (hidden when no date)
        var hudRootGO = new GameObject("DateHUDRoot");
        hudRootGO.transform.SetParent(canvasGO.transform);
        var hudRootRT = hudRootGO.AddComponent<RectTransform>();
        hudRootRT.anchorMin = Vector2.zero;
        hudRootRT.anchorMax = Vector2.one;
        hudRootRT.offsetMin = Vector2.zero;
        hudRootRT.offsetMax = Vector2.zero;
        hudRootRT.localScale = Vector3.one;

        var dateNameTMP = CreateHUDText("DateName", hudRootGO.transform,
            new Vector2(-350f, 200f), 22f, "");
        var affectionTMP = CreateHUDText("Affection", hudRootGO.transform,
            new Vector2(-350f, 170f), 18f, "");
        var clockTMP = CreateHUDText("Clock", hudRootGO.transform,
            new Vector2(350f, 200f), 20f, "8:00 AM");
        var dayTMP = CreateHUDText("Day", hudRootGO.transform,
            new Vector2(350f, 170f), 16f, "Day 1");

        // Affection bar
        var barBgGO = new GameObject("AffectionBarBG");
        barBgGO.transform.SetParent(hudRootGO.transform);
        var barBgRT = barBgGO.AddComponent<RectTransform>();
        barBgRT.anchorMin = new Vector2(0.5f, 1f);
        barBgRT.anchorMax = new Vector2(0.5f, 1f);
        barBgRT.pivot = new Vector2(0.5f, 1f);
        barBgRT.sizeDelta = new Vector2(300f, 20f);
        barBgRT.anchoredPosition = new Vector2(0f, -20f);
        barBgRT.localScale = Vector3.one;
        barBgGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.2f);
        var slider = barBgGO.AddComponent<UnityEngine.UI.Slider>();
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 50f;

        var fillAreaGO = new GameObject("FillArea");
        fillAreaGO.transform.SetParent(barBgGO.transform);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = Vector2.zero;
        fillAreaRT.offsetMax = Vector2.zero;
        fillAreaRT.localScale = Vector3.one;
        slider.fillRect = fillAreaRT;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0.5f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillRT.localScale = Vector3.one;
        fillGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.8f, 0.3f, 0.5f);
        slider.fillRect = fillRT;

        hudRootGO.SetActive(false);

        // Wire DateSessionHUD
        var hud = parent.AddComponent<DateSessionHUD>();
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("hudRoot").objectReferenceValue = hudRootGO;
        hudSO.FindProperty("dateNameText").objectReferenceValue = dateNameTMP;
        hudSO.FindProperty("affectionText").objectReferenceValue = affectionTMP;
        hudSO.FindProperty("affectionBar").objectReferenceValue = slider;
        hudSO.FindProperty("clockText").objectReferenceValue = clockTMP;
        hudSO.FindProperty("dayText").objectReferenceValue = dayTMP;
        hudSO.ApplyModifiedPropertiesWithoutUndo();
    }

    // ══════════════════════════════════════════════════════════════════
    // Ambient Cleaning (NOT a station)
    // ══════════════════════════════════════════════════════════════════

    private struct AmbientCleaningData
    {
        public CleaningManager cleaningManager;
        public ApartmentStainSpawner stainSpawner;
    }

    private static AmbientCleaningData BuildAmbientCleaning(GameObject camGO, int cleanableLayer)
    {
        // ── SO folder ────────────────────────────────────────────────
        string soDir = "Assets/ScriptableObjects/Cleaning";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Cleaning");

        // ── SpillDefinition SOs ───────────────────────────────────────
        string[] spillNames = { "Coffee Stain", "Mud Splash", "Juice Spill", "Dust Patch", "Grease Spot" };
        Color[] spillColors =
        {
            new Color(0.35f, 0.2f, 0.1f),
            new Color(0.3f, 0.22f, 0.12f),
            new Color(0.6f, 0.35f, 0.1f),
            new Color(0.5f, 0.48f, 0.45f),
            new Color(0.25f, 0.25f, 0.2f)
        };
        float[] stubborn = { 0.3f, 0.5f, 0.2f, 0.1f, 0.7f };

        var spillDefs = new SpillDefinition[spillNames.Length];
        for (int i = 0; i < spillNames.Length; i++)
        {
            string path = $"{soDir}/Spill_{spillNames[i].Replace(" ", "_")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SpillDefinition>(path);
            if (existing != null) { spillDefs[i] = existing; continue; }

            var def = ScriptableObject.CreateInstance<SpillDefinition>();
            def.displayName = spillNames[i];
            def.spillColor = spillColors[i];
            def.stubbornness = stubborn[i];
            def.coverage = 0.7f;
            def.textureSize = 128;
            def.seed = 42 + i * 7;
            AssetDatabase.CreateAsset(def, path);
            spillDefs[i] = def;
        }
        AssetDatabase.SaveAssets();

        // ── Stain slot quads (pre-placed, start disabled) ─────────────
        var slotsParent = new GameObject("StainSlots");
        Vector3[] slotPositions =
        {
            new Vector3(-4f, 0.01f, -4f),         // Kitchen floor
            new Vector3(-5f, 0.01f, 3f),           // Near couch
            new Vector3(-3.5f, 0.01f, 2.5f),       // Near coffee table
            new Vector3(-3f, 0.01f, -3f),           // Between rooms
        };

        var stainSlots = new CleanableSurface[slotPositions.Length];
        for (int i = 0; i < slotPositions.Length; i++)
        {
            var slotGO = new GameObject($"StainSlot_{i}");
            slotGO.transform.SetParent(slotsParent.transform);
            slotGO.transform.position = slotPositions[i];
            slotGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            slotGO.layer = cleanableLayer;

            // Dirt quad
            var dirtQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            dirtQuad.name = "DirtQuad";
            dirtQuad.transform.SetParent(slotGO.transform);
            dirtQuad.transform.localPosition = Vector3.zero;
            dirtQuad.transform.localRotation = Quaternion.identity;
            dirtQuad.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            dirtQuad.layer = cleanableLayer;
            dirtQuad.isStatic = false;

            // Wet overlay quad (slightly above)
            var wetQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wetQuad.name = "WetQuad";
            wetQuad.transform.SetParent(slotGO.transform);
            wetQuad.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            wetQuad.transform.localRotation = Quaternion.identity;
            wetQuad.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            wetQuad.layer = cleanableLayer;
            wetQuad.isStatic = false;
            var wetCol = wetQuad.GetComponent<Collider>();
            if (wetCol != null) Object.DestroyImmediate(wetCol);

            var surface = slotGO.AddComponent<CleanableSurface>();
            var surfSO = new SerializedObject(surface);
            surfSO.FindProperty("_definition").objectReferenceValue = spillDefs[i % spillDefs.Length];
            surfSO.FindProperty("_dirtRenderer").objectReferenceValue =
                dirtQuad.GetComponent<Renderer>();
            surfSO.FindProperty("_wetRenderer").objectReferenceValue =
                wetQuad.GetComponent<Renderer>();
            surfSO.ApplyModifiedPropertiesWithoutUndo();

            stainSlots[i] = surface;
            slotGO.SetActive(false); // Starts disabled, ApartmentStainSpawner activates subset
        }

        // ── Tool visuals (sponge + spray) ─────────────────────────────
        var spongeGO = CreateBox("SpongeVisual", null,
            Vector3.zero, new Vector3(0.06f, 0.03f, 0.08f),
            new Color(0.9f, 0.85f, 0.3f));
        spongeGO.isStatic = false;
        spongeGO.SetActive(false);

        var sprayGO = CreateBox("SprayVisual", null,
            Vector3.zero, new Vector3(0.04f, 0.12f, 0.04f),
            new Color(0.3f, 0.6f, 0.8f));
        sprayGO.isStatic = false;
        sprayGO.SetActive(false);

        // ── CleaningManager ───────────────────────────────────────────
        var managersGO = new GameObject("AmbientCleaningManagers");
        var cleanMgr = managersGO.AddComponent<CleaningManager>();
        var cleanHud = managersGO.AddComponent<CleaningHUD>();

        // ── Cleaning HUD Canvas ───────────────────────────────────────
        var cleanHudCanvasGO = new GameObject("CleaningHUD_Canvas");
        cleanHudCanvasGO.transform.SetParent(managersGO.transform);
        var cleanHudCanvas = cleanHudCanvasGO.AddComponent<Canvas>();
        cleanHudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cleanHudCanvas.sortingOrder = 13;
        cleanHudCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        cleanHudCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var toolNameLabel = CreateHUDText("ToolNameLabel", cleanHudCanvasGO.transform,
            new Vector2(0f, 200f), 22f, "Sponge");
        var instrLabel = CreateHUDText("InstructionLabel", cleanHudCanvasGO.transform,
            new Vector2(0f, -200f), 16f, "Click stains to clean | Tab to switch tool");
        var progressLabel = CreateHUDText("ProgressLabel", cleanHudCanvasGO.transform,
            new Vector2(350f, 200f), 18f, "Clean: 0%");
        var surfaceDetailLabel = CreateHUDText("SurfaceDetailLabel", cleanHudCanvasGO.transform,
            new Vector2(350f, 160f), 14f, "");

        // Wire CleaningHUD (public fields)
        var cleanHudSO = new SerializedObject(cleanHud);
        cleanHudSO.FindProperty("manager").objectReferenceValue = cleanMgr;
        cleanHudSO.FindProperty("toolNameLabel").objectReferenceValue = toolNameLabel;
        cleanHudSO.FindProperty("instructionLabel").objectReferenceValue = instrLabel;
        cleanHudSO.FindProperty("progressLabel").objectReferenceValue = progressLabel;
        cleanHudSO.FindProperty("surfaceDetailLabel").objectReferenceValue = surfaceDetailLabel;
        cleanHudSO.ApplyModifiedPropertiesWithoutUndo();

        var cam = camGO.GetComponent<UnityEngine.Camera>();

        var cleanSO = new SerializedObject(cleanMgr);
        cleanSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        cleanSO.FindProperty("_hud").objectReferenceValue = cleanHud;
        cleanSO.FindProperty("_spongeVisual").objectReferenceValue = spongeGO.transform;
        cleanSO.FindProperty("_sprayVisual").objectReferenceValue = sprayGO.transform;
        cleanSO.FindProperty("_cleanableLayer").intValue = 1 << cleanableLayer;

        var surfacesProp = cleanSO.FindProperty("_surfaces");
        surfacesProp.arraySize = stainSlots.Length;
        for (int i = 0; i < stainSlots.Length; i++)
            surfacesProp.GetArrayElementAtIndex(i).objectReferenceValue = stainSlots[i];

        cleanSO.ApplyModifiedPropertiesWithoutUndo();

        // ── ApartmentStainSpawner ─────────────────────────────────────
        var spawner = managersGO.AddComponent<ApartmentStainSpawner>();
        var spawnerSO = new SerializedObject(spawner);

        var slotsProp = spawnerSO.FindProperty("_stainSlots");
        slotsProp.arraySize = stainSlots.Length;
        for (int i = 0; i < stainSlots.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = stainSlots[i];

        var spillPoolProp = spawnerSO.FindProperty("_spillPool");
        spillPoolProp.arraySize = spillDefs.Length;
        for (int i = 0; i < spillDefs.Length; i++)
            spillPoolProp.GetArrayElementAtIndex(i).objectReferenceValue = spillDefs[i];

        spawnerSO.FindProperty("_cleaningManager").objectReferenceValue = cleanMgr;
        spawnerSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] Ambient cleaning system built.");

        return new AmbientCleaningData
        {
            cleaningManager = cleanMgr,
            stainSpawner = spawner
        };
    }

    // ══════════════════════════════════════════════════════════════════
    // DayPhaseManager
    // ══════════════════════════════════════════════════════════════════

    private static void BuildDayPhaseManager(
        NewspaperStationData newspaperData, AmbientCleaningData cleaningData,
        GameObject apartmentUI)
    {
        var go = new GameObject("DayPhaseManager");
        var dpm = go.AddComponent<DayPhaseManager>();

        var dpmSO = new SerializedObject(dpm);
        dpmSO.FindProperty("_newspaperManager").objectReferenceValue = newspaperData.manager;
        dpmSO.FindProperty("_readCamera").objectReferenceValue = newspaperData.stationCamera;
        dpmSO.FindProperty("_stainSpawner").objectReferenceValue = cleaningData.stainSpawner;
        dpmSO.FindProperty("_apartmentUI").objectReferenceValue = apartmentUI;
        dpmSO.FindProperty("_newspaperHUD").objectReferenceValue = newspaperData.hudRoot;

        // Find tossed newspaper position
        var tossedPos = GameObject.Find("TossedNewspaperPosition");
        if (tossedPos != null)
            dpmSO.FindProperty("_tossedNewspaperPosition").objectReferenceValue =
                tossedPos.transform;

        dpmSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Wire events via UnityEventTools ────────────────────────────
        // DayManager.OnNewNewspaper → DayPhaseManager.EnterMorning
        var dayMgr = Object.FindAnyObjectByType<DayManager>();
        if (dayMgr != null)
        {
            var enterMorning = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), dpm,
                typeof(DayPhaseManager).GetMethod(nameof(DayPhaseManager.EnterMorning),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                as UnityEngine.Events.UnityAction;
            if (enterMorning != null)
                UnityEventTools.AddPersistentListener(dayMgr.OnNewNewspaper, enterMorning);
        }

        // NewspaperManager.OnNewspaperDone → DayPhaseManager.EnterExploration
        if (newspaperData.manager != null)
        {
            var enterExploration = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), dpm,
                typeof(DayPhaseManager).GetMethod(nameof(DayPhaseManager.EnterExploration),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                as UnityEngine.Events.UnityAction;
            if (enterExploration != null)
                UnityEventTools.AddPersistentListener(
                    newspaperData.manager.OnNewspaperDone, enterExploration);
        }

        // DateSessionManager events (OnDateSessionStarted/OnDateSessionEnded) are
        // subscribed at runtime in DayPhaseManager.Start() to avoid multi-param
        // UnityEvent editor wiring complexity.

        Debug.Log("[ApartmentSceneBuilder] DayPhaseManager wired.");
    }

    // ══════════════════════════════════════════════════════════════════
    // Screen fade overlay
    // ══════════════════════════════════════════════════════════════════

    private static void BuildScreenFade()
    {
        var go = new GameObject("ScreenFade");

        var fadeCanvas = go.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 100;

        var blackPanel = new GameObject("BlackPanel");
        blackPanel.transform.SetParent(go.transform, false);
        var rt = blackPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = blackPanel.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;

        var fade = go.AddComponent<ScreenFade>();
        var fadeSO = new SerializedObject(fade);
        fadeSO.FindProperty("_canvasGroup").objectReferenceValue = cg;
        fadeSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] ScreenFade overlay built.");
    }

    // ══════════════════════════════════════════════════════════════════
    // UI helpers
    // ══════════════════════════════════════════════════════════════════

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

        var panelBg = panel.AddComponent<UnityEngine.UI.Image>();
        panelBg.color = bgColor;

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

    // ══════════════════════════════════════════════════════════════════
    // Shared helpers
    // ══════════════════════════════════════════════════════════════════

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

    private static int EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

        var layersProp = tagManager.FindProperty("layers");

        for (int i = 0; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (element.stringValue == layerName)
                return i;
        }

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
