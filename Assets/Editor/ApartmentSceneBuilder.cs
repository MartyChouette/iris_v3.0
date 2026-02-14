using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using Unity.Cinemachine;
using UnityEngine.Splines;
using Unity.Mathematics;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Editor utility that programmatically builds the apartment hub scene with
/// Kitchen, Living Room, and Cozy Corner areas, spline dolly camera, station integration, and placement surfaces.
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
    private const string FridgeLayerName = "Fridge";
    private const string PlantsLayerName = "Plants";
    private const string GlassLayerName = "Glass";
    private const string SurfacesLayerName = "Surfaces";

    // ─── Station Group Positions ─────────────────────────────────
    private static readonly Vector3 BookcaseStationPos  = new Vector3(-6.3f, 0f, 3.0f);
    private static readonly Vector3 RecordPlayerStationPos = new Vector3(-2f, 0f, 5f);
    private static readonly Vector3 DrinkMakingStationPos  = new Vector3(-4f, 0f, -5.2f);

    // Two-page newspaper spread dimensions
    private const int NewspaperCanvasWidth = 1000;
    private const int NewspaperCanvasHeight = 700;

    // ─── Newspaper Position Config ──────────────────────────────
    private static readonly Vector3 NewspaperCamPos     = new Vector3(-5.5f, 1.3f, 3.0f);
    private static readonly Vector3 NewspaperCamRot     = new Vector3(5f, 0f, 0f);
    private const float             NewspaperCamFOV     = 48f;
    private static readonly Vector3 NewspaperSurfacePos = new Vector3(-5.5f, 1.1f, 5.0f);
    private static readonly Vector3 NewspaperSurfaceScl = new Vector3(2.5f, 1.75f, 1f);
    private static readonly Vector3 NewspaperCanvasOff  = new Vector3(0f, 0f, 0.05f);
    private const float             NewspaperCanvasScl  = 0.0025f;
    private static readonly Vector3 NewspaperTossPos    = new Vector3(-3.5f, 0.42f, 3.0f);
    private static readonly Vector3 NewspaperTossRot    = new Vector3(90f, 10f, 0f);

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
        int fridgeLayer = EnsureLayer(FridgeLayerName);
        int plantsLayer = EnsureLayer(PlantsLayerName);
        int glassLayer = EnsureLayer(GlassLayerName);
        int surfacesLayer = EnsureLayer(SurfacesLayerName);

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

        // ── 3. Room geometry (import model or procedural fallback) ──
        BuildApartmentModel();

        // ── 4. Modular station groups ──
        BuildBookcaseStationGroup(camGO, booksLayer, drawersLayer,
            perfumesLayer, trinketsLayer, coffeeTableBooksLayer);
        BuildRecordPlayerStationGroup();
        BuildDrinkMakingStationGroup(camGO, fridgeLayer, glassLayer);

        // ── 4d. Newspaper station (DayPhaseManager-driven, not a StationRoot) ──
        var newspaperData = BuildNewspaperStation(camGO, newspaperLayer);

        // ── 5. Furniture (non-station items: couch, coffee table, kitchen table, etc.) ──
        var furnitureRefs = BuildFurniture();

        // ── 6. Placeable objects + ReactableTags ──
        BuildPlaceableObjects(placeableLayer);

        // ── 7. Spline path (7-knot closed loop) ──
        var splineContainer = BuildSplinePath();

        // ── 8. Cameras (browse + selected) ──
        var cameras = BuildCamerasWithDolly(splineContainer);

        // ── 9. Area SOs (Kitchen, Living Room, Cozy Corner) ──
        var areaDefs = BuildAreaDefinitions();

        // ── 10. Placement surfaces (tables + walls) ──
        BuildPlacementSurfaces(furnitureRefs, surfacesLayer);
        BuildWallSurfaces(surfacesLayer);

        // ── 10b. Wall placeables (paintings, diploma) ──
        BuildWallPlaceables(placeableLayer);

        // ── 10c. ObjectGrabber ──
        var grabberGO = new GameObject("ObjectGrabber");
        var grabber = grabberGO.AddComponent<ObjectGrabber>();
        var grabberSO = new SerializedObject(grabber);
        grabberSO.FindProperty("placeableLayer").intValue = 1 << placeableLayer;
        grabberSO.FindProperty("surfaceLayer").intValue = 1 << surfacesLayer;
        grabberSO.FindProperty("gridSize").floatValue = 0.2f;
        grabberSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 11. ApartmentManager + UI ──
        var apartmentUI = BuildApartmentManager(cameras.browse, cameras.dolly,
            grabber, areaDefs);

        // ── 12. Dating infrastructure (GameClock, DateSessionManager, PhoneController, etc.) ──
        BuildDatingInfrastructure(camGO, furnitureRefs, newspaperData, phoneLayer);

        // ── 13. Ambient cleaning (not a station) ──
        var cleaningData = BuildAmbientCleaning(camGO, cleanableLayer);

        // ── 13b. Ambient watering (not a station) ──
        BuildAmbientWatering(camGO, plantsLayer);

        // ── 14. DayPhaseManager (orchestrates daily loop) ──
        BuildDayPhaseManager(newspaperData, cleaningData, apartmentUI);

        // ── 15. Screen fade overlay ──
        BuildScreenFade();

        // ── 16. NavMesh setup ──
        BuildNavMeshSetup();

        // ── 17. Save scene ──
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/apartment.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[ApartmentSceneBuilder] Scene saved to {path}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Apartment geometry (import blockout model or procedural fallback)
    // ══════════════════════════════════════════════════════════════════

    private static void BuildApartmentModel()
    {
        const string modelPath = "Assets/Scenes/Iris_V3/aprtment blockout.obj";
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

        if (modelAsset != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = "ApartmentModel";
            instance.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            // Mark all children static for lighting + NavMesh
            foreach (var tf in instance.GetComponentsInChildren<Transform>(true))
            {
                tf.gameObject.isStatic = true;
                GameObjectUtility.SetStaticEditorFlags(tf.gameObject,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic
                    | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.BatchingStatic);
            }

            // Add MeshColliders to any children with MeshFilter but no Collider
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                    mf.gameObject.AddComponent<MeshCollider>();
            }

            Debug.Log($"[ApartmentSceneBuilder] Imported apartment model from {modelPath}.");
        }
        else
        {
            // Procedural fallback — simple floor
            Debug.LogWarning($"[ApartmentSceneBuilder] Model not found at {modelPath}, using procedural fallback.");
            var parent = new GameObject("Apartment");

            CreateBox("Floor", parent.transform,
                new Vector3(-3f, -0.05f, 0f), new Vector3(16f, 0.1f, 16f),
                new Color(0.55f, 0.45f, 0.35f));
        }
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
        public Transform judgmentStopPoint;
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

        // ═══ Judgment Stop Point (between entrance and couch) ═══
        refs.judgmentStopPoint = BuildJudgmentStopPoint(parent.transform);

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

        // Fridge is now part of the DrinkMaking station group (see BuildDrinkMakingStationGroup)

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
        tossedGO.transform.position = NewspaperTossPos;
        tossedGO.transform.rotation = Quaternion.Euler(NewspaperTossRot);
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

    /// <summary>Build the judgment stop point between entrance and couch.</summary>
    private static Transform BuildJudgmentStopPoint(Transform parent)
    {
        var go = new GameObject("JudgmentStopPoint");
        go.transform.SetParent(parent);
        // Halfway between spawn (-3, 0, -5.5) and couch (-5.5, 0, 3) — near the entrance area
        go.transform.position = new Vector3(-4.2f, 0f, -1.5f);
        return go.transform;
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

        // Small plant on sun ledge (WaterablePlant wired by BuildAmbientWatering)

        // ReactableTag on mecha figurine (decoration)
        var mechaGO = GameObject.Find("MechaFigurine");
        if (mechaGO != null) AddReactableTag(mechaGO, new[] { "figurine", "decoration" }, true);

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

    private static void BuildPlacementSurfaces(FurnitureRefs refs, int surfacesLayer)
    {
        // Coffee table surface (horizontal)
        AddSurface(refs.coffeeTable, new Bounds(
            Vector3.zero, new Vector3(1.0f, 0.1f, 0.6f)),
            PlacementSurface.SurfaceAxis.Up, surfacesLayer);

        // Kitchen table surface (horizontal)
        AddSurface(refs.kitchenTable, new Bounds(
            Vector3.zero, new Vector3(1.2f, 0.1f, 0.8f)),
            PlacementSurface.SurfaceAxis.Up, surfacesLayer);

        // Kitchen counter surface (horizontal)
        // Counter_Top is at (-4, 0.85, -5.2), scale (3, 0.08, 0.7)
        var counterTop = GameObject.Find("Counter_Top");
        if (counterTop != null)
        {
            AddSurface(counterTop, new Bounds(
                Vector3.zero, new Vector3(3.0f, 0.1f, 0.7f)),
                PlacementSurface.SurfaceAxis.Up, surfacesLayer);
        }
    }

    private static PlacementSurface AddSurface(GameObject surfaceGO, Bounds localBounds,
        PlacementSurface.SurfaceAxis axis, int layer)
    {
        var surface = surfaceGO.AddComponent<PlacementSurface>();

        var so = new SerializedObject(surface);
        so.FindProperty("localBounds").boundsValue = localBounds;
        so.FindProperty("normalAxis").enumValueIndex = (int)axis;
        so.FindProperty("surfaceLayerIndex").intValue = layer;
        so.ApplyModifiedPropertiesWithoutUndo();

        return surface;
    }

    private static void BuildWallSurfaces(int surfacesLayer)
    {
        var parent = new GameObject("WallSurfaces");

        // Living room back wall (facing -Z into room)
        var lrWall = new GameObject("WallSurface_LivingRoom");
        lrWall.transform.SetParent(parent.transform);
        lrWall.transform.position = new Vector3(-4.5f, 1.8f, 5.9f);
        lrWall.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // forward faces -Z (into room)
        AddSurface(lrWall, new Bounds(
            Vector3.zero, new Vector3(3.0f, 2.0f, 0.05f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);

        // Kitchen side wall (facing +Z into room)
        var kitchenWall = new GameObject("WallSurface_Kitchen");
        kitchenWall.transform.SetParent(parent.transform);
        kitchenWall.transform.position = new Vector3(-5.5f, 1.5f, -5.5f);
        kitchenWall.transform.rotation = Quaternion.identity; // forward faces +Z (into room)
        AddSurface(kitchenWall, new Bounds(
            Vector3.zero, new Vector3(2.0f, 1.5f, 0.05f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);

        Debug.Log("[ApartmentSceneBuilder] Built 2 wall surfaces.");
    }

    private static void BuildWallPlaceables(int placeableLayer)
    {
        var parent = new GameObject("WallPlaceables");

        // ── Living room paintings ──
        CreateWallPlaceable("Painting_Flowers", parent.transform,
            new Vector3(-5.0f, 2.2f, 5.85f), new Vector3(0.5f, 0.35f, 0.03f),
            new Color(0.6f, 0.4f, 0.5f), placeableLayer,
            Quaternion.Euler(0f, 180f, 0f), // face into room
            "Painting");

        CreateWallPlaceable("Painting_Sunset", parent.transform,
            new Vector3(-3.8f, 2.0f, 5.85f), new Vector3(0.4f, 0.3f, 0.03f),
            new Color(0.8f, 0.5f, 0.3f), placeableLayer,
            Quaternion.Euler(0f, 180f, 0f),
            "Painting");

        // ── Kitchen diploma ──
        CreateWallPlaceable("Diploma_Floristry", parent.transform,
            new Vector3(-5.0f, 1.8f, -5.45f), new Vector3(0.3f, 0.22f, 0.02f),
            new Color(0.9f, 0.88f, 0.8f), placeableLayer,
            Quaternion.identity, // face +Z into room
            "Diploma");

        Debug.Log("[ApartmentSceneBuilder] Built 3 wall placeables (2 paintings, 1 diploma).");
    }

    private static GameObject CreateWallPlaceable(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color, int layer,
        Quaternion wallRotation, string reactableTag)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.transform.rotation = wallRotation;
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
        rb.isKinematic = true;
        rb.useGravity = false;

        var placeable = go.AddComponent<PlaceableObject>();
        var placeableSO = new SerializedObject(placeable);
        placeableSO.FindProperty("canWallMount").boolValue = true;
        placeableSO.FindProperty("crookedAngleRange").floatValue = 12f;
        placeableSO.ApplyModifiedPropertiesWithoutUndo();

        // Apply crooked offset
        Vector3 wallNormal = wallRotation * Vector3.forward;
        placeable.ApplyCrookedOffset(wallNormal);

        // Add ReactableTag for date reactions
        var tag = go.AddComponent<ReactableTag>();
        var tagSO = new SerializedObject(tag);
        var tagsProp = tagSO.FindProperty("tags");
        tagsProp.arraySize = 1;
        tagsProp.GetArrayElementAtIndex(0).stringValue = reactableTag;
        tagSO.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    // ══════════════════════════════════════════════════════════════════
    // Spline Path (closed loop — Kitchen + Living Room for now)
    // ══════════════════════════════════════════════════════════════════

    private static SplineContainer BuildSplinePath()
    {
        var splineGO = new GameObject("ApartmentSplinePath");
        var container = splineGO.AddComponent<SplineContainer>();
        var spline = container.Spline;
        spline.Clear();

        // 4 knots orbiting the Kitchen + Living Room zone
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

        return new CameraRefs { browse = browse, dolly = dolly };
    }

    // ══════════════════════════════════════════════════════════════════
    // Area ScriptableObjects (Kitchen, Living Room, Cozy Corner)
    // ══════════════════════════════════════════════════════════════════

    private static ApartmentAreaDefinition[] BuildAreaDefinitions()
    {
        string soDir = "Assets/ScriptableObjects/Apartment";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Apartment");

        var kitchen = CreateAreaDef("Kitchen", soDir,
            "Kitchen", "Fridge (drink-making), counter, cleaning, watering.",
            StationType.DrinkMaking, 0.0f,
            new Vector3(-4f, 1.0f, -4.5f),
            new Vector3(-6.5f, 3.5f, -4.0f), new Vector3(35f, 45f, 0f), 48f);

        var livingRoom = CreateAreaDef("LivingRoom", soDir,
            "Living Room", "Bookcase, coffee table, couch.",
            StationType.Bookcase, 0.5f,
            new Vector3(-6.3f, 1.2f, 3.0f),
            new Vector3(-7.0f, 3.5f, 3.0f), new Vector3(30f, 60f, 0f), 48f);

        return new[] { kitchen, livingRoom };
    }

    private static ApartmentAreaDefinition CreateAreaDef(
        string assetName, string directory,
        string areaName, string description,
        StationType stationType, float splinePos,
        Vector3 lookAt,
        Vector3 selectedPos, Vector3 selectedRot, float selectedFOV)
    {
        string path = $"{directory}/Area_{assetName}.asset";
        var def = AssetDatabase.LoadAssetAtPath<ApartmentAreaDefinition>(path);
        bool isNew = def == null;
        if (isNew)
            def = ScriptableObject.CreateInstance<ApartmentAreaDefinition>();

        def.areaName = areaName;
        def.description = description;
        def.stationType = stationType;
        def.splinePosition = splinePos;
        def.lookAtPosition = lookAt;
        def.selectedPosition = selectedPos;
        def.selectedRotation = selectedRot;
        def.selectedFOV = selectedFOV;
        def.browseBlendDuration = 0.8f;
        def.selectBlendDuration = 0.5f;

        if (isNew)
            AssetDatabase.CreateAsset(def, path);
        else
            EditorUtility.SetDirty(def);

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
    // Modular Station Groups
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds the bookcase station as a self-contained group:
    /// BookcaseUnit + BookInteractionManager + StationRoot.
    /// </summary>
    private static void BuildBookcaseStationGroup(GameObject camGO,
        int booksLayer, int drawersLayer, int perfumesLayer,
        int trinketsLayer, int coffeeTableBooksLayer)
    {
        var groupGO = new GameObject("Station_Bookcase");

        // Bookcase unit (shared with standalone bookcase scene)
        var bookcaseRoot = BookcaseSceneBuilder.BuildBookcaseUnit(
            booksLayer, drawersLayer, perfumesLayer, trinketsLayer, coffeeTableBooksLayer);
        bookcaseRoot.transform.SetParent(groupGO.transform);
        bookcaseRoot.transform.localPosition = Vector3.zero;
        bookcaseRoot.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

        // BookInteractionManager
        var bookManager = BuildBookInteractionManager(camGO, booksLayer,
            drawersLayer, perfumesLayer, trinketsLayer, coffeeTableBooksLayer);
        bookManager.transform.SetParent(groupGO.transform);

        // ReactableTag on bookcase
        AddReactableTag(groupGO, new[] { "book", "reading" }, true);

        // StationRoot (no station cameras — uses selected cam from ApartmentManager)
        CreateStationRoot(groupGO, StationType.Bookcase, bookManager, null);

        // Position the entire group
        groupGO.transform.position = BookcaseStationPos;

        Debug.Log("[ApartmentSceneBuilder] Bookcase station group built.");
    }

    /// <summary>
    /// Builds the record player station as a self-contained group:
    /// Furniture + RecordPlayerManager + RecordPlayerHUD + AudioSource + StationRoot.
    /// </summary>
    private static void BuildRecordPlayerStationGroup()
    {
        var groupGO = new GameObject("Station_RecordPlayer");
        groupGO.transform.position = RecordPlayerStationPos;

        // ── Furniture ────────────────────────────────────────────────
        // Record table
        var table = CreateBox("RecordTable", groupGO.transform,
            RecordPlayerStationPos + new Vector3(0f, 0.4f, 0f),
            new Vector3(0.6f, 0.8f, 0.4f),
            new Color(0.40f, 0.28f, 0.18f));
        table.isStatic = true;

        // Turntable (flat cylinder on top of table)
        var turntableGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        turntableGO.name = "Turntable";
        turntableGO.transform.SetParent(groupGO.transform);
        turntableGO.transform.position = RecordPlayerStationPos + new Vector3(0f, 0.85f, 0f);
        turntableGO.transform.localScale = new Vector3(0.30f, 0.02f, 0.30f);
        turntableGO.isStatic = true;
        SetMaterial(turntableGO, new Color(0.20f, 0.20f, 0.22f));

        // Record disc (thin cylinder, child of group for rotation)
        var discGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discGO.name = "RecordDisc";
        discGO.transform.SetParent(groupGO.transform);
        discGO.transform.position = RecordPlayerStationPos + new Vector3(0f, 0.88f, 0f);
        discGO.transform.localScale = new Vector3(0.25f, 0.005f, 0.25f);
        discGO.isStatic = false;
        var discCol = discGO.GetComponent<Collider>();
        if (discCol != null) Object.DestroyImmediate(discCol);
        SetMaterial(discGO, new Color(0.05f, 0.05f, 0.08f));

        // Tone arm (thin box at angle)
        var toneArm = CreateBox("ToneArm", groupGO.transform,
            RecordPlayerStationPos + new Vector3(0.18f, 0.90f, 0.05f),
            new Vector3(0.015f, 0.008f, 0.18f),
            new Color(0.60f, 0.60f, 0.65f));
        toneArm.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
        toneArm.isStatic = false;

        // ── Station camera ───────────────────────────────────────────
        var camGO = new GameObject("Cam_RecordPlayer");
        camGO.transform.SetParent(groupGO.transform);
        camGO.transform.position = RecordPlayerStationPos + new Vector3(-1.0f, 1.5f, -0.8f);
        camGO.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
        var stationCam = camGO.AddComponent<CinemachineCamera>();
        var lens = LensSettings.Default;
        lens.FieldOfView = 45f;
        lens.NearClipPlane = 0.1f;
        lens.FarClipPlane = 100f;
        stationCam.Lens = lens;
        stationCam.Priority = 0;

        // ── Managers ─────────────────────────────────────────────────
        var managersGO = new GameObject("RecordPlayerManagers");
        managersGO.transform.SetParent(groupGO.transform);
        var audioSrc = managersGO.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.loop = true;

        var mgr = managersGO.AddComponent<RecordPlayerManager>();
        var hud = managersGO.AddComponent<RecordPlayerHUD>();

        // ReactableTag on managers GO (toggled by RecordPlayerManager during playback)
        AddReactableTag(managersGO, new[] { "vinyl", "music" }, false);

        // ── Load existing RecordDefinition SOs ───────────────────────
        string soDir = "Assets/ScriptableObjects/RecordPlayer";
        var recordPaths = AssetDatabase.FindAssets("t:RecordDefinition", new[] { soDir });
        var records = new RecordDefinition[recordPaths.Length];
        for (int i = 0; i < recordPaths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(recordPaths[i]);
            records[i] = AssetDatabase.LoadAssetAtPath<RecordDefinition>(path);
        }

        // ── Wire RecordPlayerManager ─────────────────────────────────
        var mgrSO = new SerializedObject(mgr);
        var recordsProp = mgrSO.FindProperty("records");
        recordsProp.arraySize = records.Length;
        for (int i = 0; i < records.Length; i++)
            recordsProp.GetArrayElementAtIndex(i).objectReferenceValue = records[i];
        mgrSO.FindProperty("recordVisual").objectReferenceValue = discGO.transform;
        mgrSO.FindProperty("recordRenderer").objectReferenceValue = discGO.GetComponent<Renderer>();
        mgrSO.FindProperty("audioSource").objectReferenceValue = audioSrc;
        mgrSO.FindProperty("hud").objectReferenceValue = hud;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── HUD Canvas ───────────────────────────────────────────────
        var hudCanvasGO = new GameObject("RecordPlayerHUD_Canvas");
        hudCanvasGO.transform.SetParent(groupGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        var hudScaler = hudCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        hudScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        hudScaler.referenceResolution = new Vector2(1920f, 1080f);
        hudCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var titleText = CreateHUDText("TitleText", hudCanvasGO.transform,
            new Vector2(0f, 200f), 24f, "Record Title");
        var artistText = CreateHUDText("ArtistText", hudCanvasGO.transform,
            new Vector2(0f, 160f), 18f, "Artist");
        var stateText = CreateHUDText("StateText", hudCanvasGO.transform,
            new Vector2(0f, 120f), 16f, "Stopped");
        var hintsText = CreateHUDText("HintsText", hudCanvasGO.transform,
            new Vector2(0f, -200f), 16f, "A / D  Browse    |    Enter  Play");

        // Wire RecordPlayerHUD
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("titleText").objectReferenceValue = titleText;
        hudSO.FindProperty("artistText").objectReferenceValue = artistText;
        hudSO.FindProperty("stateText").objectReferenceValue = stateText;
        hudSO.FindProperty("hintsText").objectReferenceValue = hintsText;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // Start disabled (StationRoot.Activate will enable)
        mgr.enabled = false;
        hudCanvasGO.SetActive(false);

        // ── StationRoot ──────────────────────────────────────────────
        var stationRoot = CreateStationRoot(groupGO, StationType.RecordPlayer,
            mgr, hudCanvasGO, stationCam);

        Debug.Log($"[ApartmentSceneBuilder] Record Player station group built ({records.Length} records loaded).");
    }

    // ══════════════════════════════════════════════════════════════════
    // Station Root Helper
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a StationRoot component to the given GO and wires its serialized fields.
    /// </summary>
    private static StationRoot CreateStationRoot(GameObject go,
        StationType type, MonoBehaviour manager,
        GameObject hudRoot, CinemachineCamera stationCamera = null,
        int[] availableInPhases = null)
    {
        var root = go.AddComponent<StationRoot>();

        var so = new SerializedObject(root);
        so.FindProperty("stationType").enumValueIndex = (int)type;
        if (manager != null)
            so.FindProperty("stationManager").objectReferenceValue = manager;
        if (hudRoot != null)
            so.FindProperty("hudRoot").objectReferenceValue = hudRoot;

        if (stationCamera != null)
        {
            var camsProp = so.FindProperty("stationCameras");
            camsProp.arraySize = 1;
            camsProp.GetArrayElementAtIndex(0).objectReferenceValue = stationCamera;
        }

        if (availableInPhases != null && availableInPhases.Length > 0)
        {
            var phasesProp = so.FindProperty("availableInPhases");
            phasesProp.arraySize = availableInPhases.Length;
            for (int i = 0; i < availableInPhases.Length; i++)
                phasesProp.GetArrayElementAtIndex(i).intValue = availableInPhases[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    // ══════════════════════════════════════════════════════════════════
    // ReactableTag Helper
    // ══════════════════════════════════════════════════════════════════

    private static void AddReactableTag(GameObject go, string[] tags, bool isActive)
    {
        var tag = go.AddComponent<ReactableTag>();
        var so = new SerializedObject(tag);
        var tagsProp = so.FindProperty("tags");
        tagsProp.arraySize = tags.Length;
        for (int i = 0; i < tags.Length; i++)
            tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i];
        so.FindProperty("isActive").boolValue = isActive;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ══════════════════════════════════════════════════════════════════
    // Material Helper
    // ══════════════════════════════════════════════════════════════════

    private static void SetMaterial(GameObject go, Color color)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                               ?? Shader.Find("Standard"));
        mat.color = color;
        rend.sharedMaterial = mat;
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
        readCamGO.transform.position = NewspaperCamPos;
        readCamGO.transform.rotation = Quaternion.Euler(NewspaperCamRot);
        var readCam = readCamGO.AddComponent<CinemachineCamera>();
        var readLens = LensSettings.Default;
        readLens.FieldOfView = NewspaperCamFOV;
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
            surfaceGO.transform.position = NewspaperSurfacePos;
            SetNewspaperLayerRecursive(surfaceGO, newspaperLayer);
        }
        else
        {
            surfaceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceGO.name = "NewspaperSurface";
            surfaceGO.transform.SetParent(parent.transform);
            surfaceGO.transform.position = NewspaperSurfacePos;
            surfaceGO.transform.rotation = Quaternion.identity;
            surfaceGO.transform.localScale = NewspaperSurfaceScl;
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
        float canvasScale = NewspaperCanvasScl;
        var pivotGO = new GameObject("NewspaperOverlayPivot");
        pivotGO.transform.SetParent(parent.transform);
        pivotGO.transform.position = NewspaperSurfacePos + NewspaperCanvasOff;
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

        var raycaster = canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // WorldSpace canvas needs a worldCamera for raycasting
        canvas.worldCamera = camGO.GetComponent<UnityEngine.Camera>();

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

            // Clickable ad slot (must be child of canvas for Button raycasting)
            personalSlots[i] = CreateNewspaperAdSlot($"PersonalSlot_{i}",
                canvasGO.transform, newspaperLayer,
                new Vector2(anchoredX, anchoredY),
                new Vector2(contentWidth, slotHeight));

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

        // ── Managers ──────────────────────────────────────────────
        var managersGO = new GameObject("NewspaperManagers");

        // DayManager
        var dayMgr = managersGO.AddComponent<DayManager>();
        var dayMgrSO = new SerializedObject(dayMgr);
        dayMgrSO.FindProperty("pool").objectReferenceValue = pool;
        dayMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // NewspaperManager (button-based ad selection, no scissors)
        var cam = camGO.GetComponent<UnityEngine.Camera>();
        var newsMgr = managersGO.AddComponent<NewspaperManager>();
        var newsMgrSO = new SerializedObject(newsMgr);
        newsMgrSO.FindProperty("dayManager").objectReferenceValue = dayMgr;
        newsMgrSO.FindProperty("surface").objectReferenceValue =
            surfaceGO.GetComponent<NewspaperSurface>();
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

    // ── Newspaper ad slot (clickable button for ad selection) ──

    private static NewspaperAdSlot CreateNewspaperAdSlot(string name, Transform canvasParent,
        int layer, Vector2 anchoredPos, Vector2 sizePx)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvasParent, false);
        go.layer = layer;

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizePx;
        rt.localScale = Vector3.one;

        // Transparent Image for raycast target (required for pointer event detection)
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;

        // Progress fill child — radial fill overlay, starts hidden
        var fillGO = new GameObject("ProgressFill");
        fillGO.transform.SetParent(go.transform, false);
        fillGO.layer = layer;

        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(1f, 1f, 1f, 0.3f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Radial360;
        fillImg.fillOrigin = (int)Image.Origin360.Top;
        fillImg.fillAmount = 0f;
        fillImg.raycastTarget = false;
        fillGO.SetActive(false);

        var slot = go.AddComponent<NewspaperAdSlot>();

        var so = new SerializedObject(slot);
        so.FindProperty("slotRect").objectReferenceValue = rt;
        so.FindProperty("progressFill").objectReferenceValue = fillImg;
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
        CinemachineCamera browseCam,
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
            new Vector2(0f, -200f), new Vector2(600f, 50f),
            "< / >  Cycle  |  Enter  Station  |  Click  Interact", 16f,
            new Color(0f, 0f, 0f, 0.5f));

        // ── Nav buttons (left/right arrows) ──
        var navLeftBtn = CreateNavButton("NavLeft", uiCanvasGO.transform,
            new Vector2(-350f, 0f), "\u25C0");
        var navRightBtn = CreateNavButton("NavRight", uiCanvasGO.transform,
            new Vector2(350f, 0f), "\u25B6");

        // Wire nav button onClick → ApartmentManager.NavigateLeft / NavigateRight
        UnityEventTools.AddPersistentListener(
            navLeftBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(manager.NavigateLeft));
        UnityEventTools.AddPersistentListener(
            navRightBtn.GetComponent<Button>().onClick,
            new UnityEngine.Events.UnityAction(manager.NavigateRight));

        // ── Wire serialized fields ──
        var so = new SerializedObject(manager);

        // Areas array
        var areasProp = so.FindProperty("areas");
        areasProp.arraySize = areaDefs.Length;
        for (int i = 0; i < areaDefs.Length; i++)
            areasProp.GetArrayElementAtIndex(i).objectReferenceValue = areaDefs[i];

        // Cameras
        so.FindProperty("browseCamera").objectReferenceValue = browseCam;
        so.FindProperty("browseDolly").objectReferenceValue = dolly;

        // Interaction
        so.FindProperty("objectGrabber").objectReferenceValue = grabber;

        // UI
        so.FindProperty("areaNamePanel").objectReferenceValue = areaNamePanel;
        so.FindProperty("areaNameText").objectReferenceValue =
            areaNamePanel.GetComponentInChildren<TMP_Text>();
        so.FindProperty("browseHintsPanel").objectReferenceValue = browseHints;

        so.ApplyModifiedPropertiesWithoutUndo();

        // Start hidden — DayPhaseManager shows it when entering Exploration
        uiCanvasGO.SetActive(false);

        return uiCanvasGO;
    }

    private static GameObject CreateNavButton(string name, Transform parent,
        Vector2 anchoredPos, string label)
    {
        var btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(60f, 60f);
        rt.anchoredPosition = anchoredPos;
        rt.localScale = Vector3.one;

        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.4f);

        btnGO.AddComponent<Button>();

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(btnGO.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btnGO;
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
        uiCanvasGO.SetActive(false);

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
    // Drink Making Station Group (Kitchen) — with fridge door
    // ══════════════════════════════════════════════════════════════════

    private static void BuildDrinkMakingStationGroup(GameObject mainCamGO, int fridgeLayer, int glassLayer)
    {
        var groupGO = new GameObject("Station_DrinkMaking");

        // ── SO folder ────────────────────────────────────────────────
        string soDir = "Assets/ScriptableObjects/DrinkMaking";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "DrinkMaking");

        // ── Recipe definitions (with simple-pour fields) ─────────────
        string[] recipeNames = { "Gin & Tonic", "Lemon Drop", "Bitter Fizz", "Sunset Sip" };
        int[] recipeBase = { 100, 120, 110, 90 };
        float[] recipeIdeal = { 0.75f, 0.70f, 0.80f, 0.65f };
        float[] recipeTol = { 0.10f, 0.08f, 0.12f, 0.10f };
        float[] recipePour = { 0.15f, 0.18f, 0.12f, 0.20f };
        float[] recipeFoam = { 2f, 1.5f, 3f, 2.5f };
        float[] recipeSettle = { 0.25f, 0.30f, 0.20f, 0.25f };
        Color[] recipeLiquid =
        {
            new Color(0.85f, 0.90f, 0.70f, 0.6f),
            new Color(1.0f, 0.95f, 0.40f, 0.7f),
            new Color(0.50f, 0.30f, 0.15f, 0.8f),
            new Color(0.95f, 0.45f, 0.25f, 0.7f),
        };

        var recipes = new DrinkRecipeDefinition[recipeNames.Length];
        for (int i = 0; i < recipeNames.Length; i++)
        {
            string path = $"{soDir}/Recipe_{recipeNames[i].Replace(" ", "_").Replace("&", "and")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DrinkRecipeDefinition>(path);
            if (existing != null) { recipes[i] = existing; continue; }

            var def = ScriptableObject.CreateInstance<DrinkRecipeDefinition>();
            def.drinkName = recipeNames[i];
            def.baseScore = recipeBase[i];
            def.idealFillLevel = recipeIdeal[i];
            def.fillTolerance = recipeTol[i];
            def.pourRate = recipePour[i];
            def.foamRateMultiplier = recipeFoam[i];
            def.foamSettleRate = recipeSettle[i];
            def.liquidColor = recipeLiquid[i];
            AssetDatabase.CreateAsset(def, path);
            recipes[i] = def;
        }

        AssetDatabase.SaveAssets();

        // ── Fridge (body + animated door) ─────────────────────────────
        var fridgeBody = CreateBox("FridgeBody", groupGO.transform,
            new Vector3(-6.3f, 0.9f, -4.5f), new Vector3(0.7f, 1.8f, 0.35f),
            new Color(0.85f, 0.85f, 0.87f));
        fridgeBody.isStatic = true;

        var doorPivotGO = new GameObject("FridgeDoorPivot");
        doorPivotGO.transform.SetParent(groupGO.transform);
        doorPivotGO.transform.position = new Vector3(-6.65f, 0.9f, -4.325f);
        doorPivotGO.isStatic = false;

        var fridgeDoor = CreateBox("FridgeDoor", doorPivotGO.transform,
            new Vector3(-6.3f, 0.9f, -4.325f), new Vector3(0.7f, 1.8f, 0.35f),
            new Color(0.87f, 0.87f, 0.89f));
        fridgeDoor.isStatic = false;
        fridgeDoor.layer = fridgeLayer;

        var handle = CreateBox("FridgeHandle", fridgeDoor.transform,
            new Vector3(-5.98f, 1.0f, -4.175f), new Vector3(0.03f, 0.15f, 0.03f),
            new Color(0.5f, 0.5f, 0.55f));
        handle.isStatic = false;
        handle.layer = fridgeLayer;

        // ── Station camera ────────────────────────────────────────────
        var drinkCamGO = new GameObject("Cam_DrinkMaking");
        drinkCamGO.transform.SetParent(groupGO.transform);
        drinkCamGO.transform.position = new Vector3(-4f, 1.5f, -4.5f);
        drinkCamGO.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        var drinkCam = drinkCamGO.AddComponent<CinemachineCamera>();
        var drinkLens = LensSettings.Default;
        drinkLens.FieldOfView = 50f;
        drinkLens.NearClipPlane = 0.1f;
        drinkLens.FarClipPlane = 100f;
        drinkCam.Lens = drinkLens;
        drinkCam.Priority = 0;

        // ── Glass on counter (on Glass layer for raycast) ─────────────
        var glassGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        glassGO.name = "Glass";
        glassGO.transform.SetParent(groupGO.transform);
        glassGO.transform.position = new Vector3(-4f, 0.95f, -5.2f);
        glassGO.transform.localScale = new Vector3(0.08f, 0.10f, 0.08f);
        glassGO.isStatic = false;
        glassGO.layer = glassLayer;
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

        // ── Managers (SimpleDrinkManager + SimpleDrinkHUD) ────────────
        var managersGO = new GameObject("DrinkMakingManagers");
        managersGO.transform.SetParent(groupGO.transform);
        var mgr = managersGO.AddComponent<SimpleDrinkManager>();
        var hud = managersGO.AddComponent<SimpleDrinkHUD>();

        var cam = mainCamGO.GetComponent<UnityEngine.Camera>();

        // Wire manager via SerializedObject
        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("_glassLayer").intValue = 1 << glassLayer;
        mgrSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("_hud").objectReferenceValue = hud;

        var recipesProp = mgrSO.FindProperty("availableRecipes");
        recipesProp.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
            recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];

        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── HUD Canvas ────────────────────────────────────────────────
        var hudCanvasGO = new GameObject("SimpleDrinkHUD_Canvas");
        hudCanvasGO.transform.SetParent(managersGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        hudCanvasGO.AddComponent<CanvasScaler>();
        hudCanvasGO.AddComponent<GraphicRaycaster>();

        // ── Recipe panel (buttons for recipe selection) ───────────────
        var recipePanelGO = new GameObject("RecipePanel");
        recipePanelGO.transform.SetParent(hudCanvasGO.transform);
        var recipePanelRT = recipePanelGO.AddComponent<RectTransform>();
        recipePanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        recipePanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        recipePanelRT.sizeDelta = new Vector2(500f, 300f);
        recipePanelRT.anchoredPosition = Vector2.zero;
        recipePanelRT.localScale = Vector3.one;

        var titleLabel = CreateHUDText("RecipePanelTitle", recipePanelGO.transform,
            new Vector2(0f, 100f), 24f, "Choose a Recipe");

        for (int i = 0; i < recipes.Length; i++)
        {
            float yPos = 40f - i * 50f;
            var btnGO = new GameObject($"Btn_{recipes[i].drinkName}");
            btnGO.transform.SetParent(recipePanelGO.transform);

            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(300f, 40f);
            btnRT.anchoredPosition = new Vector2(0f, yPos);
            btnRT.localScale = Vector3.one;

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.2f, 0.25f, 0.85f);

            var btn = btnGO.AddComponent<Button>();

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform);
            var btnTextRT = btnTextGO.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.sizeDelta = Vector2.zero;
            btnTextRT.anchoredPosition = Vector2.zero;
            btnTextRT.localScale = Vector3.one;
            var btnTMP = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnTMP.text = recipes[i].drinkName;
            btnTMP.fontSize = 18f;
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.color = Color.white;

            // Wire button → SimpleDrinkManager.SelectRecipe(i)
            int recipeIndex = i;
            UnityEventTools.AddIntPersistentListener(btn.onClick,
                mgr.SelectRecipe, recipeIndex);
        }

        // ── HUD panel (fill/foam/score — shown during Pouring + Scoring) ──
        var hudPanelGO = new GameObject("HudPanel");
        hudPanelGO.transform.SetParent(hudCanvasGO.transform);
        var hudPanelRT = hudPanelGO.AddComponent<RectTransform>();
        hudPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        hudPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        hudPanelRT.sizeDelta = new Vector2(500f, 300f);
        hudPanelRT.anchoredPosition = Vector2.zero;
        hudPanelRT.localScale = Vector3.one;

        var drinkNameLabel = CreateHUDText("DrinkNameLabel", hudPanelGO.transform,
            new Vector2(0f, 200f), 24f, "");
        var fillLevelLabel = CreateHUDText("FillLevelLabel", hudPanelGO.transform,
            new Vector2(250f, 30f), 18f, "");
        var foamLevelLabel = CreateHUDText("FoamLevelLabel", hudPanelGO.transform,
            new Vector2(250f, 0f), 18f, "");
        var targetLabel = CreateHUDText("TargetLabel", hudPanelGO.transform,
            new Vector2(250f, -30f), 18f, "");
        var scoreLabel = CreateHUDText("ScoreLabel", hudPanelGO.transform,
            new Vector2(0f, 0f), 28f, "");

        // Overflow warning
        var overflowGO = new GameObject("OverflowWarning");
        overflowGO.transform.SetParent(hudPanelGO.transform);
        var owRT = overflowGO.AddComponent<RectTransform>();
        owRT.anchorMin = new Vector2(0.5f, 0.5f);
        owRT.anchorMax = new Vector2(0.5f, 0.5f);
        owRT.sizeDelta = new Vector2(300f, 40f);
        owRT.anchoredPosition = new Vector2(0f, -80f);
        owRT.localScale = Vector3.one;
        var owTMP = overflowGO.AddComponent<TextMeshProUGUI>();
        owTMP.text = "OVERFLOW!";
        owTMP.fontSize = 22f;
        owTMP.alignment = TextAlignmentOptions.Center;
        owTMP.color = new Color(1f, 0.2f, 0.2f);
        overflowGO.SetActive(false);

        // Wire HUD
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("drinkNameLabel").objectReferenceValue = drinkNameLabel;
        hudSO.FindProperty("fillLevelLabel").objectReferenceValue = fillLevelLabel;
        hudSO.FindProperty("foamLevelLabel").objectReferenceValue = foamLevelLabel;
        hudSO.FindProperty("targetLabel").objectReferenceValue = targetLabel;
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
        hudSO.FindProperty("overflowWarning").objectReferenceValue = overflowGO;
        hudSO.FindProperty("hudPanel").objectReferenceValue = hudPanelGO;
        hudSO.FindProperty("recipePanel").objectReferenceValue = recipePanelGO;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // Start disabled (StationRoot will enable)
        mgr.enabled = false;
        hudCanvasGO.SetActive(false);

        // ── FridgeController ──────────────────────────────────────────
        var fridgeCtrl = groupGO.AddComponent<FridgeController>();
        var fridgeCtrlSO = new SerializedObject(fridgeCtrl);
        fridgeCtrlSO.FindProperty("_doorPivot").objectReferenceValue = doorPivotGO.transform;
        fridgeCtrlSO.FindProperty("_fridgeLayer").intValue = 1 << fridgeLayer;
        fridgeCtrlSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        fridgeCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // ── ReactableTag on drink station (toggled when drink is delivered) ──
        AddReactableTag(groupGO, new[] { "drink", "cocktail" }, false);

        // ── StationRoot (always available — kitchen used for cleaning + watering too) ──
        CreateStationRoot(groupGO, StationType.DrinkMaking, mgr, hudCanvasGO, drinkCam);

        Debug.Log("[ApartmentSceneBuilder] Simple Drink Making station group built (with fridge door).");
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
        if (furnitureRefs.judgmentStopPoint != null)
            dsmSO.FindProperty("judgmentStopPoint").objectReferenceValue = furnitureRefs.judgmentStopPoint;

        // Add EntranceJudgmentSequence and wire it to DateSessionManager
        var entranceJudgments = managersGO.AddComponent<EntranceJudgmentSequence>();
        dsmSO.FindProperty("_entranceJudgments").objectReferenceValue = entranceJudgments;
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
            CreateStationRoot(phoneGO, StationType.Phone, phoneCtrl, null);
        }

        // ── CoffeeTableDelivery ───────────────────────────────────────
        if (furnitureRefs.coffeeTableDeliveryPoint != null)
        {
            var deliveryGO = furnitureRefs.coffeeTableDeliveryPoint.gameObject;
            var coffeeDelivery = deliveryGO.AddComponent<CoffeeTableDelivery>();
            var cdSO = new SerializedObject(coffeeDelivery);
            cdSO.FindProperty("drinkSpawnPoint").objectReferenceValue =
                furnitureRefs.coffeeTableDeliveryPoint;
            cdSO.ApplyModifiedPropertiesWithoutUndo();

            // ReactableTag for date NPC drink reactions (toggled on delivery)
            AddReactableTag(deliveryGO, new[] { "drink" }, false);
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
        cleanHudCanvasGO.SetActive(false);

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
    // Ambient Watering
    // ══════════════════════════════════════════════════════════════════

    private static void BuildAmbientWatering(GameObject camGO, int plantsLayer)
    {
        // ── Load existing PlantDefinition SOs ──────────────────────────
        string soDir = "Assets/ScriptableObjects/Watering";
        string[] plantAssetNames = { "Plant_Fern", "Plant_Cactus", "Plant_Succulent" };
        var plantDefs = new PlantDefinition[plantAssetNames.Length];
        for (int i = 0; i < plantAssetNames.Length; i++)
        {
            string path = $"{soDir}/{plantAssetNames[i]}.asset";
            plantDefs[i] = AssetDatabase.LoadAssetAtPath<PlantDefinition>(path);
            if (plantDefs[i] == null)
                Debug.LogWarning($"[ApartmentSceneBuilder] Missing PlantDefinition: {path}");
        }

        // ── Plant positions in apartment ───────────────────────────────
        Vector3[] plantPositions =
        {
            new Vector3(-4.1f, 0.98f, 5.7f),   // Sun ledge (living room)
            new Vector3(-2.8f, 0.98f, 5.7f),   // Sun ledge (living room, right)
            new Vector3(-5.0f, 0.01f, -3.0f),  // Kitchen floor corner
        };

        var plantsParent = new GameObject("WaterablePlants");

        for (int i = 0; i < plantPositions.Length; i++)
        {
            var def = plantDefs[i % plantDefs.Length];
            string plantName = def != null ? def.plantName : $"Plant{i}";

            var plantRoot = new GameObject($"Plant_{plantName}");
            plantRoot.transform.SetParent(plantsParent.transform);
            plantRoot.transform.position = plantPositions[i];
            plantRoot.layer = plantsLayer;
            plantRoot.isStatic = false;

            // WaterablePlant marker
            var wp = plantRoot.AddComponent<WaterablePlant>();
            wp.definition = def;

            // BoxCollider on root for easy clicking
            var col = plantRoot.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.07f, 0f);
            col.size = new Vector3(0.14f, 0.20f, 0.14f);

            // Pot visual
            Color potColor = def != null ? def.potColor : new Color(0.6f, 0.35f, 0.25f);
            var potGO = CreateBox("Pot", plantRoot.transform,
                new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.1f, 0.12f), potColor);
            potGO.layer = plantsLayer;
            potGO.isStatic = false;

            // Stem
            Color plantColor = def != null ? def.plantColor : new Color(0.2f, 0.5f, 0.2f);
            var stemGO = CreateBox("Stem", plantRoot.transform,
                new Vector3(0f, 0.12f, 0f), new Vector3(0.02f, 0.10f, 0.02f), plantColor);
            stemGO.layer = plantsLayer;

            // Leaves
            var leafL = CreateBox("LeafL", plantRoot.transform,
                new Vector3(-0.04f, 0.14f, 0f), new Vector3(0.06f, 0.03f, 0.02f), plantColor);
            leafL.layer = plantsLayer;
            var leafR = CreateBox("LeafR", plantRoot.transform,
                new Vector3(0.04f, 0.16f, 0f), new Vector3(0.06f, 0.03f, 0.02f), plantColor);
            leafR.layer = plantsLayer;

            // ReactableTag for date reactions
            AddReactableTag(plantRoot, new[] { "plant", "greenery" }, true);
        }

        // ── PotController (hidden simulation) ──────────────────────────
        var potHiddenGO = new GameObject("PotController");
        potHiddenGO.transform.SetParent(plantsParent.transform);
        var potCtrl = potHiddenGO.AddComponent<PotController>();
        var potCtrlSO = new SerializedObject(potCtrl);
        potCtrlSO.FindProperty("potWorldHeight").floatValue = 0.10f;
        potCtrlSO.FindProperty("potWorldRadius").floatValue = 0.04f;
        potCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // ── WateringManager + WateringHUD ──────────────────────────────
        var managersGO = new GameObject("AmbientWateringManagers");
        managersGO.transform.SetParent(plantsParent.transform);
        var mgr = managersGO.AddComponent<WateringManager>();
        var hud = managersGO.AddComponent<WateringHUD>();

        var cam = camGO.GetComponent<UnityEngine.Camera>();

        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("_plantLayer").intValue = 1 << plantsLayer;
        mgrSO.FindProperty("_pot").objectReferenceValue = potCtrl;
        mgrSO.FindProperty("_hud").objectReferenceValue = hud;
        mgrSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("_scoreDisplayTime").floatValue = 2f;
        mgrSO.FindProperty("_overflowPenalty").floatValue = 30f;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Watering HUD Canvas ────────────────────────────────────────
        var canvasGO = new GameObject("WateringHUD_Canvas");
        canvasGO.transform.SetParent(managersGO.transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 14;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // HUD panel (hidden when idle)
        var hudPanelGO = new GameObject("WateringHUDPanel");
        hudPanelGO.transform.SetParent(canvasGO.transform);
        var hudPanelRT = hudPanelGO.AddComponent<RectTransform>();
        hudPanelRT.anchorMin = Vector2.zero;
        hudPanelRT.anchorMax = Vector2.one;
        hudPanelRT.offsetMin = Vector2.zero;
        hudPanelRT.offsetMax = Vector2.zero;
        hudPanelRT.localScale = Vector3.one;

        var plantNameLabel = CreateHUDText("PlantNameLabel", hudPanelGO.transform,
            new Vector2(0f, 280f), 22f, "");
        var waterLevelLabel = CreateHUDText("WaterLevelLabel", hudPanelGO.transform,
            new Vector2(350f, 40f), 18f, "");
        var foamLevelLabel = CreateHUDText("FoamLevelLabel", hudPanelGO.transform,
            new Vector2(350f, 0f), 18f, "");
        var targetLabel = CreateHUDText("TargetLabel", hudPanelGO.transform,
            new Vector2(350f, -40f), 16f, "");

        var overflowWarning = CreateHUDText("OverflowWarning", hudPanelGO.transform,
            new Vector2(0f, 60f), 22f, "Overflowing!");
        overflowWarning.color = new Color(1f, 0.25f, 0.2f);
        overflowWarning.gameObject.SetActive(false);

        var scoreLabel = CreateHUDText("ScoreLabel", hudPanelGO.transform,
            new Vector2(0f, 0f), 20f, "");

        // Wire HUD
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("plantNameLabel").objectReferenceValue = plantNameLabel;
        hudSO.FindProperty("waterLevelLabel").objectReferenceValue = waterLevelLabel;
        hudSO.FindProperty("foamLevelLabel").objectReferenceValue = foamLevelLabel;
        hudSO.FindProperty("targetLabel").objectReferenceValue = targetLabel;
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
        hudSO.FindProperty("overflowWarning").objectReferenceValue = overflowWarning.gameObject;
        hudSO.FindProperty("hudPanel").objectReferenceValue = hudPanelGO;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] Ambient watering system built.");
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
        fadeSO.FindProperty("defaultFadeOutDuration").floatValue = 0.5f;
        fadeSO.FindProperty("defaultFadeInDuration").floatValue = 0.5f;
        // Easing curves use EaseInOut by default (set in ScreenFade field initializers)
        fadeSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] ScreenFade overlay built.");
    }

    // ══════════════════════════════════════════════════════════════════
    // NavMesh Setup
    // ══════════════════════════════════════════════════════════════════

    private static void BuildNavMeshSetup()
    {
        // Mark floor geometry as NavigationStatic for NavMesh baking
        var apartment = GameObject.Find("ApartmentModel") ?? GameObject.Find("Apartment");
        if (apartment != null)
        {
            foreach (var tf in apartment.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(tf.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(tf.gameObject)
                    | StaticEditorFlags.NavigationStatic);
            }
        }

        Debug.Log("[ApartmentSceneBuilder] NavMesh static flags set. " +
                  "Remember to bake NavMesh via Window > AI > Navigation.");
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
