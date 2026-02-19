using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using Unity.Cinemachine;
using TMPro;
using UnityEngine.UI;
using Iris.Apartment;

/// <summary>
/// Editor utility that programmatically builds the apartment hub scene with
/// Kitchen and Living Room areas, browse camera, station integration, and placement surfaces.
/// Menu: Window > Iris > Build Apartment Scene
/// </summary>
public static class ApartmentSceneBuilder
{
    private const string PlaceableLayerName = "Placeable";
    private const string BooksLayerName = "Books";
    private const string DrawersLayerName = "Drawers";
    private const string PerfumesLayerName = "Perfumes";
    private const string CoffeeTableBooksLayerName = "CoffeeTableBooks";
    private const string NewspaperLayerName = "Newspaper";
    private const string CleanableLayerName = "Cleanable";
    private const string PhoneLayerName = "Phone";
    private const string FridgeLayerName = "Fridge";
    private const string PlantsLayerName = "Plants";
    private const string GlassLayerName = "Glass";
    private const string SurfacesLayerName = "Surfaces";
    private const string VinylStackLayerName = "VinylStack";
    private const string TurntableLayerName = "Turntable";
    private const string DoorLayerName = "Door";

    // ─── Station Group Positions ─────────────────────────────────
    private static readonly Vector3 BookcaseStationPos  = new Vector3(2.933f, 0.421f, 2.095f);
    private static readonly Quaternion BookcaseStationRot = new Quaternion(0f, 0.9997f, 0f, -0.0227f);
    private static readonly Vector3 RecordPlayerStationPos = new Vector3(-2f, 0f, 5f);
    private static readonly Vector3 DrinkMakingStationPos  = new Vector3(-4f, 0f, -5.2f);

    // ─── Entrance Area Config ────────────────────────────────────
    private static readonly Vector3 EntranceAreaPos     = new Vector3(-1f, 0f, 6.5f);
    private static readonly Vector3 TrashCanPos         = new Vector3(-4.5f, 0f, -4.0f);
    private static readonly Vector3 ShoeRackPos         = new Vector3(-0.5f, 0f, 7.0f);
    private static readonly Vector3 CoatRackPos         = new Vector3(0.5f, 0f, 7.0f);

    // Two-page newspaper spread dimensions
    private const int NewspaperCanvasWidth = 900;
    private const int NewspaperCanvasHeight = 400;

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

    private const string LayoutPath = "Assets/Editor/PlaceableLayout.json";

    [MenuItem("Window/Iris/Save Scene Layout")]
    public static void SavePlaceableLayout()
    {
        SaveLayout();
    }

    [MenuItem("Window/Iris/Build Apartment Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild Apartment Scene",
                "This will CREATE A NEW SCENE from scratch and discard whatever is currently open.\n\n" +
                "Any unsaved changes to the current scene will be lost.\n\n" +
                "Are you sure?",
                "Rebuild", "Cancel"))
            return;

        // Capture positions from current scene before destroying it
        SaveLayout();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int placeableLayer = EnsureLayer(PlaceableLayerName);
        int booksLayer = EnsureLayer(BooksLayerName);
        int drawersLayer = EnsureLayer(DrawersLayerName);
        int perfumesLayer = EnsureLayer(PerfumesLayerName);
        int coffeeTableBooksLayer = EnsureLayer(CoffeeTableBooksLayerName);
        int newspaperLayer = EnsureLayer(NewspaperLayerName);
        int cleanableLayer = EnsureLayer(CleanableLayerName);
        int phoneLayer = EnsureLayer(PhoneLayerName);
        int fridgeLayer = EnsureLayer(FridgeLayerName);
        int plantsLayer = EnsureLayer(PlantsLayerName);
        int glassLayer = EnsureLayer(GlassLayerName);
        int surfacesLayer = EnsureLayer(SurfacesLayerName);
        int vinylStackLayer = EnsureLayer(VinylStackLayerName);
        int turntableLayer = EnsureLayer(TurntableLayerName);
        int doorLayer = EnsureLayer(DoorLayerName);

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
            perfumesLayer, coffeeTableBooksLayer);
        BuildRecordPlayerStationGroup(vinylStackLayer, turntableLayer);
        BuildDrinkMakingStationGroup(camGO, fridgeLayer, glassLayer);

        // ── 4d. Newspaper station (DayPhaseManager-driven, not a StationRoot) ──
        var newspaperData = BuildNewspaperStation(camGO, newspaperLayer);

        // ── 5. Furniture (non-station items: couch, coffee table, kitchen table, etc.) ──
        var furnitureRefs = BuildFurniture();

        // ── 6. Placeable objects + ReactableTags ──
        BuildPlaceableObjects(placeableLayer);

        // ── 6b. Dirty dishes + drop zone ──
        BuildDirtyDishes(placeableLayer, surfacesLayer);

        // ── 6c. Entrance area (shoe rack, coat rack, entrance furniture) ──
        BuildEntranceArea(placeableLayer, surfacesLayer);

        // ── 6d. Trash can (kitchen) ──
        BuildTrashCan(surfacesLayer);

        // ── 7. Browse camera (direct pos/rot/FOV, no spline) ──
        var browseCam = BuildBrowseCamera();

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
        var apartmentUI = BuildApartmentManager(browseCam, grabber, areaDefs);

        // ── 11b. Camera Test Controller (A/B/C preset comparison) ──
        BuildCameraTestController(browseCam, camGO);

        // ── 12. Dating infrastructure (GameClock, DateSessionManager, PhoneController, etc.) ──
        BuildDatingInfrastructure(camGO, furnitureRefs, newspaperData, phoneLayer);

        // ── 12a. Door greeting controller ──
        BuildDoor(camGO, doorLayer);

        // ── 12b. Auto-save controller ──
        var autoSaveGO = new GameObject("AutoSaveController");
        autoSaveGO.AddComponent<AutoSaveController>();

        // ── 13. Ambient cleaning (not a station) ──
        var cleaningData = BuildAmbientCleaning(camGO, cleanableLayer);

        // ── 13b. Ambient watering (not a station) ──
        BuildAmbientWatering(camGO, plantsLayer);

        // ── 13c. TidyScorer (apartment tidiness aggregation) ──
        BuildTidyScorer();

        // ── 13d. DailyMessSpawner (trash + misplaced items each morning) ──
        BuildDailyMessSpawner(placeableLayer);

        // ── 14. DayPhaseManager (orchestrates daily loop) ──
        BuildDayPhaseManager(newspaperData, cleaningData, apartmentUI);

        // ── 15. Screen fade overlay ──
        BuildScreenFade();

        // ── 15b. Name entry screen (shown before newspaper) ──
        BuildNameEntryScreen();

        // ── 15c. Kitchen wall calendar ──
        BuildKitchenCalendar();

        // ── 15d. F1 debug overlay ──
        BuildDateDebugOverlay();

        // ── 16. NavMesh setup ──
        BuildNavMeshSetup();

        // ── 17. Restore saved layout positions ──
        RestoreLayout();

        // ── 18. Save scene ──
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
            instance.transform.position = new Vector3(0.6172299f, 0f, -2.295473f);
            instance.transform.rotation = new Quaternion(0f, -0.70792603f, 0f, 0.70628667f);
            instance.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            // Mark all children static for lighting + NavMesh
            foreach (var tf in instance.GetComponentsInChildren<Transform>(true))
            {
                tf.gameObject.isStatic = true;
                GameObjectUtility.SetStaticEditorFlags(tf.gameObject,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic
                    | StaticEditorFlags.ReflectionProbeStatic);
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
        public Transform kitchenStandPoint;
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

        // ═══ Kitchen Stand Point (where NPC stands during drink phase) ═══
        refs.kitchenStandPoint = BuildKitchenStandPoint(parent.transform);

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

        // Counter (rotated to match scene layout)
        var counterRot = new Quaternion(0f, -0.7219f, 0f, 0.692f);
        var counterTop = CreateBox("Counter_Top", parent,
            new Vector3(1.82f, 0.99f, -3.56f), new Vector3(3f, 0.08f, 0.7f),
            new Color(0.75f, 0.73f, 0.70f));
        counterTop.transform.rotation = counterRot;
        var counterBase = CreateBox("Counter_Base", parent,
            new Vector3(1.82f, 0.54f, -3.56f), new Vector3(3f, 0.8f, 0.7f),
            new Color(0.50f, 0.40f, 0.30f));
        counterBase.transform.rotation = counterRot;

        // Tossed newspaper position (on coffee table, where newspaper lands after reading)
        var tossedGO = new GameObject("TossedNewspaperPosition");
        tossedGO.transform.SetParent(parent);
        tossedGO.transform.position = NewspaperTossPos;
        tossedGO.transform.rotation = Quaternion.Euler(NewspaperTossRot);
        tossedNewspaperPosition = tossedGO.transform;

        // Phone (on fridge shelf)
        var phoneBody = CreateBox("Phone_Body", parent,
            new Vector3(3.209f, 1.2f, -1.15f), new Vector3(0.12f, 0.18f, 0.05f),
            new Color(0.18f, 0.18f, 0.20f));
        phoneBody.transform.rotation = new Quaternion(0f, 0.7112f, 0f, 0.703f);
        phoneBody.isStatic = false;
        phoneBody.AddComponent<InteractableHighlight>();
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
        // Inside the house near the front door — clearly visible in entrance area
        go.transform.position = new Vector3(-3.2f, 0f, -4f);
        return go.transform;
    }

    /// <summary>Build the kitchen stand point where NPC stands during drink phase.</summary>
    private static Transform BuildKitchenStandPoint(Transform parent)
    {
        var go = new GameObject("KitchenStandPoint");
        go.transform.SetParent(parent);
        // Near the drink making station (-4, 0, -5.2), offset so NPC watches the player
        go.transform.position = new Vector3(-3.5f, 0f, -4.8f);
        return go.transform;
    }

    private static GameObject BuildLivingRoom(Transform parent,
        out Transform couchSeatTarget, out Transform coffeeTableDeliveryPoint)
    {
        // Coffee table (legless)
        var tableTop = CreateBox("CoffeeTable_Top", parent,
            new Vector3(-0.571f, 0.35f, 2.007f), new Vector3(1.0f, 0.05f, 0.6f),
            new Color(0.50f, 0.35f, 0.22f));

        // Floor lamp
        CreateBox("FloorLamp_Pole", parent,
            new Vector3(-6.3f, 0.8f, 5.3f), new Vector3(0.06f, 1.6f, 0.06f),
            new Color(0.20f, 0.20f, 0.22f));
        CreateBox("FloorLamp_Shade", parent,
            new Vector3(-6.3f, 1.7f, 5.3f), new Vector3(0.35f, 0.25f, 0.35f),
            new Color(0.92f, 0.85f, 0.65f));

        // Sun ledge with mecha figurine
        CreateBox("SunLedge", parent,
            new Vector3(-1.834f, 1.067f, -2.15f), new Vector3(1.5f, 0.08f, 0.4f),
            new Color(0.50f, 0.45f, 0.38f));
        CreateBox("MechaFigurine", parent,
            new Vector3(-1.834f, 1.217f, -2.15f), new Vector3(0.1f, 0.2f, 0.1f),
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
        deliveryGO.transform.position = new Vector3(-0.571f, 0.42f, 2.007f);
        coffeeTableDeliveryPoint = deliveryGO.transform;

        return tableTop;
    }

    // ══════════════════════════════════════════════════════════════════
    // Placeable layout persistence
    // ══════════════════════════════════════════════════════════════════

    [System.Serializable]
    private class PlaceableEntry
    {
        public string name;
        public float px, py, pz;
        public float rx, ry, rz, rw;
    }

    [System.Serializable]
    private class PlaceableLayoutData
    {
        public PlaceableEntry[] entries;
    }

    /// <summary>Collect all transforms that should be persisted across rebuilds.</summary>
    private static System.Collections.Generic.List<Transform> GatherLayoutTransforms()
    {
        var list = new System.Collections.Generic.List<Transform>();

        // 1. PlaceableObject components (includes wall placeables)
        foreach (var p in Object.FindObjectsByType<PlaceableObject>(FindObjectsSortMode.None))
            list.Add(p.transform);

        // 2. Furniture children
        var furnitureRoot = GameObject.Find("Furniture");
        if (furnitureRoot != null)
        {
            foreach (Transform child in furnitureRoot.transform)
                list.Add(child);
        }

        // 3. Station group roots (Station_Bookcase, Station_RecordPlayer, Station_DrinkMaking)
        string[] stationNames = { "Station_Bookcase", "Station_RecordPlayer", "Station_DrinkMaking" };
        foreach (var sn in stationNames)
        {
            var go = GameObject.Find(sn);
            if (go != null) list.Add(go.transform);
        }

        // 4. WallPlaceables root (so wall-mounted items restore even if PlaceableObject search missed them)
        var wallRoot = GameObject.Find("WallPlaceables");
        if (wallRoot != null)
        {
            foreach (Transform child in wallRoot.transform)
            {
                if (!list.Contains(child)) list.Add(child);
            }
        }

        // 5. WaterablePlant objects
        var plants = Object.FindObjectsByType<WaterablePlant>(FindObjectsSortMode.None);
        foreach (var plant in plants)
        {
            if (!list.Contains(plant.transform)) list.Add(plant.transform);
        }

        // 6. ApartmentCalendar
        var calendars = Object.FindObjectsByType<ApartmentCalendar>(FindObjectsSortMode.None);
        foreach (var cal in calendars)
        {
            if (!list.Contains(cal.transform)) list.Add(cal.transform);
        }

        // 7. FridgeController (fridge body/pivot)
        var fridges = Object.FindObjectsByType<FridgeController>(FindObjectsSortMode.None);
        foreach (var fridge in fridges)
        {
            if (!list.Contains(fridge.transform)) list.Add(fridge.transform);
        }

        return list;
    }

    private static void SaveLayout()
    {
        var transforms = GatherLayoutTransforms();
        if (transforms.Count == 0) return;

        var entries = new PlaceableEntry[transforms.Count];
        for (int i = 0; i < transforms.Count; i++)
        {
            var t = transforms[i];
            entries[i] = new PlaceableEntry
            {
                name = t.gameObject.name,
                px = t.position.x, py = t.position.y, pz = t.position.z,
                rx = t.rotation.x, ry = t.rotation.y, rz = t.rotation.z, rw = t.rotation.w
            };
        }

        var data = new PlaceableLayoutData { entries = entries };
        string json = JsonUtility.ToJson(data, true);
        System.IO.File.WriteAllText(LayoutPath, json);
        Debug.Log($"[ApartmentSceneBuilder] Saved {entries.Length} layout positions to {LayoutPath}");
    }

    private static void RestoreLayout()
    {
        if (!System.IO.File.Exists(LayoutPath)) return;

        string json = System.IO.File.ReadAllText(LayoutPath);
        var data = JsonUtility.FromJson<PlaceableLayoutData>(json);
        if (data == null || data.entries == null) return;

        // Build a lookup of all scene objects by name for fast matching
        var allObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        var lookup = new System.Collections.Generic.Dictionary<string, Transform>();
        foreach (var t in allObjects)
        {
            if (!lookup.ContainsKey(t.gameObject.name))
                lookup[t.gameObject.name] = t;
        }

        int restored = 0;
        foreach (var entry in data.entries)
        {
            if (lookup.TryGetValue(entry.name, out var target))
            {
                target.position = new Vector3(entry.px, entry.py, entry.pz);
                target.rotation = new Quaternion(entry.rx, entry.ry, entry.rz, entry.rw);
                restored++;
            }
        }

        Debug.Log($"[ApartmentSceneBuilder] Restored {restored}/{data.entries.Length} layout positions from {LayoutPath}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Placeable objects
    // ══════════════════════════════════════════════════════════════════

    private static void BuildPlaceableObjects(int placeableLayer)
    {
        var parent = new GameObject("Placeables");

        // Cup on coffee table
        CreatePlaceable("Cup", parent.transform,
            new Vector3(-0.672f, 0.45f, 1.984f), new Vector3(0.08f, 0.12f, 0.08f),
            new Color(0.85f, 0.82f, 0.75f), placeableLayer);

        // Vase on kitchen counter
        var vase = CreatePlaceable("Vase", parent.transform,
            new Vector3(1.89f, 1.11f, -4.26f), new Vector3(0.1f, 0.2f, 0.1f),
            new Color(0.3f, 0.55f, 0.65f), placeableLayer);
        vase.transform.rotation = new Quaternion(0f, -0.7213f, 0f, 0.6927f);

        // Magazine on coffee table
        CreatePlaceable("Magazine", parent.transform,
            new Vector3(-0.372f, 0.42f, 2.084f), new Vector3(0.18f, 0.02f, 0.25f),
            new Color(0.7f, 0.3f, 0.3f), placeableLayer);

        // Yoyo on coffee table
        CreatePlaceable("Yoyo", parent.transform,
            new Vector3(-0.872f, 0.42f, 1.784f), new Vector3(0.06f, 0.06f, 0.06f),
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
        go.AddComponent<InteractableHighlight>();

        return go;
    }

    // ══════════════════════════════════════════════════════════════════
    // Dirty Dishes + Drop Zone
    // ══════════════════════════════════════════════════════════════════

    private static void BuildDirtyDishes(int placeableLayer, int surfacesLayer)
    {
        var parent = new GameObject("DirtyDishes");

        var litShader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
        var plateMat = new Material(litShader);
        plateMat.color = new Color(0.92f, 0.90f, 0.85f); // off-white ceramic

        // ── Plate positions (kitchen table, counter, coffee table, side areas) ──
        Vector3[] positions =
        {
            new Vector3(-3.8f, 0.82f, -3.5f),   // kitchen table
            new Vector3(-4.3f, 0.82f, -3.2f),   // kitchen table 2
            new Vector3(-2.5f, 1.02f, -4.8f),   // kitchen counter
            new Vector3(-0.6f, 0.46f, 1.8f),    // coffee table
            new Vector3(-0.2f, 0.46f, 2.2f),    // coffee table 2
            new Vector3(-5.0f, 0.82f, -3.8f),   // kitchen table 3
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = $"DirtyPlate_{i:D2}";
            plate.transform.SetParent(parent.transform);
            plate.transform.position = positions[i];
            plate.transform.localScale = new Vector3(0.18f, 0.015f, 0.18f);
            plate.layer = placeableLayer;
            plate.isStatic = false;

            // Replace CapsuleCollider with BoxCollider (cylinder primitive adds CapsuleCollider)
            var capsule = plate.GetComponent<CapsuleCollider>();
            if (capsule != null) Object.DestroyImmediate(capsule);
            plate.AddComponent<BoxCollider>();

            var rend = plate.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = plateMat;

            var rb = plate.AddComponent<Rigidbody>();
            rb.mass = 0.3f;

            var placeable = plate.AddComponent<PlaceableObject>();
            var placeableSO = new SerializedObject(placeable);
            placeableSO.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.Dish;
            placeableSO.FindProperty("_homeZoneName").stringValue = "DishDropZone";
            placeableSO.ApplyModifiedPropertiesWithoutUndo();

            plate.AddComponent<InteractableHighlight>();

            var stackable = plate.AddComponent<StackablePlate>();
            var stackSO = new SerializedObject(stackable);
            stackSO.FindProperty("_plateLayer").intValue = 1 << placeableLayer;
            stackSO.ApplyModifiedPropertiesWithoutUndo();

            // ReactableTag for date reactions (dirty dishes have smell)
            AddReactableTag(plate, new[] { "dirty_dish", "mess" }, true, smellAmount: 0.3f);
        }

        // ── Drop zone (near kitchen sink area) ──
        var zoneGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zoneGO.name = "DishDropZone";
        zoneGO.transform.SetParent(parent.transform);
        zoneGO.transform.position = new Vector3(-5.0f, 1.02f, -5.0f);
        zoneGO.transform.localScale = new Vector3(0.5f, 0.05f, 0.4f);
        zoneGO.layer = surfacesLayer;
        zoneGO.isStatic = true;

        // Transparent green emissive material
        var zoneShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
        var zoneMat = new Material(zoneShader);
        zoneMat.color = new Color(0.3f, 0.8f, 0.4f, 0.35f);
        zoneMat.SetFloat("_Surface", 1f);
        zoneMat.SetFloat("_Blend", 0f);
        zoneMat.renderQueue = 3000;

        var zoneRend = zoneGO.GetComponent<Renderer>();
        if (zoneRend != null)
            zoneRend.sharedMaterial = zoneMat;

        // PlacementSurface so plates can be placed on it
        var surface = zoneGO.AddComponent<PlacementSurface>();
        var surfSO = new SerializedObject(surface);
        var axisProp = surfSO.FindProperty("normalAxis");
        if (axisProp != null) axisProp.enumValueIndex = 0; // Up
        surfSO.ApplyModifiedPropertiesWithoutUndo();

        // DishDropZone component (plate stacking)
        var dropZone = zoneGO.AddComponent<DishDropZone>();
        var dzSO = new SerializedObject(dropZone);
        dzSO.FindProperty("_zoneRenderer").objectReferenceValue = zoneRend;
        dzSO.ApplyModifiedPropertiesWithoutUndo();

        // DropZone component (generic home zone for PlaceableObject routing)
        var genericZone = zoneGO.AddComponent<DropZone>();
        var gzSO = new SerializedObject(genericZone);
        gzSO.FindProperty("_zoneName").stringValue = "DishDropZone";
        gzSO.FindProperty("_destroyOnDeposit").boolValue = false;
        gzSO.FindProperty("_zoneRenderer").objectReferenceValue = zoneRend;
        gzSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[ApartmentSceneBuilder] Built {positions.Length} dirty dishes + drop zone.");
    }

    // ══════════════════════════════════════════════════════════════════
    // Placement Surfaces
    // ══════════════════════════════════════════════════════════════════

    private static void BuildPlacementSurfaces(FurnitureRefs refs, int surfacesLayer)
    {
        // Coffee table surface (horizontal)
        // CoffeeTable_Top is a unit cube scaled to (1.0, 0.05, 0.6) — bounds in local (unit) space
        AddSurface(refs.coffeeTable, new Bounds(
            Vector3.zero, new Vector3(1.0f, 0.1f, 1.0f)),
            PlacementSurface.SurfaceAxis.Up, surfacesLayer);

        // Kitchen table surface (horizontal)
        // KitchenTable_Top is a unit cube scaled to (1.2, 0.05, 0.8) — bounds in local (unit) space
        AddSurface(refs.kitchenTable, new Bounds(
            Vector3.zero, new Vector3(1.0f, 0.1f, 1.0f)),
            PlacementSurface.SurfaceAxis.Up, surfacesLayer);

        // Kitchen counter surface (horizontal)
        // Counter_Top is a unit cube scaled to (3, 0.08, 0.7) — bounds must be in local space (unit cube)
        var counterTop = GameObject.Find("Counter_Top");
        if (counterTop != null)
        {
            AddSurface(counterTop, new Bounds(
                Vector3.zero, new Vector3(1.0f, 0.1f, 1.0f)),
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

        // East wall (facing -X into room) — paintings and diploma hang here
        var eastWall = new GameObject("WallSurface_East");
        eastWall.transform.SetParent(parent.transform);
        eastWall.transform.position = new Vector3(3.5f, 1.8f, -1.5f);
        eastWall.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // forward faces -X (into room)
        AddSurface(eastWall, new Bounds(
            Vector3.zero, new Vector3(8.0f, 2.0f, 0.05f)),
            PlacementSurface.SurfaceAxis.Forward, surfacesLayer);

        Debug.Log("[ApartmentSceneBuilder] Built 1 wall surface (east wall).");
    }

    private static void BuildWallPlaceables(int placeableLayer)
    {
        var parent = new GameObject("WallPlaceables");

        // ── Living room paintings (on east wall) ──
        CreateWallPlaceable("Painting_Flowers", parent.transform,
            new Vector3(3.123f, 2.2f, -0.441f), new Vector3(0.5f, 0.35f, 0.03f),
            new Color(0.6f, 0.4f, 0.5f), placeableLayer,
            new Quaternion(0.0353f, -0.7171f, -0.0342f, 0.6952f),
            "Painting");

        CreateWallPlaceable("Painting_Sunset", parent.transform,
            new Vector3(3.198f, 2.0f, 0.461f), new Vector3(0.4f, 0.3f, 0.03f),
            new Color(0.8f, 0.5f, 0.3f), placeableLayer,
            new Quaternion(0.0398f, -0.7169f, -0.0386f, 0.695f),
            "Painting");

        // ── Kitchen diploma (on east wall) ──
        CreateWallPlaceable("Diploma_Floristry", parent.transform,
            new Vector3(3.404f, 1.93f, -5.249f), new Vector3(0.3f, 0.22f, 0.02f),
            new Color(0.9f, 0.88f, 0.8f), placeableLayer,
            new Quaternion(-0.0097f, -0.7212f, 0.0093f, 0.6926f),
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
        placeableSO.FindProperty("wallOnly").boolValue = true;
        placeableSO.FindProperty("crookedAngleRange").floatValue = 12f;
        placeableSO.ApplyModifiedPropertiesWithoutUndo();

        // Apply crooked offset only on first-ever build (layout will overwrite anyway)
        bool hasLayout = System.IO.File.Exists(LayoutPath);
        if (!hasLayout)
        {
            Vector3 wallNormal = wallRotation * Vector3.forward;
            placeable.ApplyCrookedOffset(wallNormal);
        }

        // Add ReactableTag for date reactions
        var tag = go.AddComponent<ReactableTag>();
        var tagSO = new SerializedObject(tag);
        var tagsProp = tagSO.FindProperty("tags");
        tagsProp.arraySize = 1;
        tagsProp.GetArrayElementAtIndex(0).stringValue = reactableTag;
        tagSO.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<InteractableHighlight>();

        return go;
    }

    // ══════════════════════════════════════════════════════════════════
    // Browse Camera (closed loop — Kitchen + Living Room for now)
    // ══════════════════════════════════════════════════════════════════

    private static CinemachineCamera BuildBrowseCamera()
    {
        var parent = new GameObject("CinemachineCameras");

        var browseGO = new GameObject("Cam_Browse");
        browseGO.transform.SetParent(parent.transform);
        var browse = browseGO.AddComponent<CinemachineCamera>();
        var browseLens = LensSettings.Default;
        browseLens.FieldOfView = 50f;
        browseLens.NearClipPlane = 0.1f;
        browseLens.FarClipPlane = 500f;
        browse.Lens = browseLens;
        browse.Priority = 20;

        return browse;
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
            StationType.DrinkMaking,
            new Vector3(-1.0f, 3.5f, -3.5f), new Vector3(35f, 45f, 0f), 48f);

        var livingRoom = CreateAreaDef("LivingRoom", soDir,
            "Living Room", "Bookcase, coffee table.",
            StationType.Bookcase,
            new Vector3(0.5f, 3.5f, 2.0f), new Vector3(30f, 60f, 0f), 48f);

        var entrance = CreateAreaDef("Entrance", soDir,
            "Entrance", "Shoe rack, coat rack, front door.",
            StationType.None,
            new Vector3(-0.5f, 3.0f, 7.0f), new Vector3(25f, 180f, 0f), 48f);

        return new[] { kitchen, livingRoom, entrance };
    }

    private static ApartmentAreaDefinition CreateAreaDef(
        string assetName, string directory,
        string areaName, string description,
        StationType stationType,
        Vector3 camPos, Vector3 camRot, float camFOV)
    {
        string path = $"{directory}/Area_{assetName}.asset";
        var def = AssetDatabase.LoadAssetAtPath<ApartmentAreaDefinition>(path);
        bool isNew = def == null;
        if (isNew)
            def = ScriptableObject.CreateInstance<ApartmentAreaDefinition>();

        def.areaName = areaName;
        def.description = description;
        def.stationType = stationType;

        if (isNew)
        {
            // Only set camera defaults on first creation — preserve user edits on rebuild
            def.cameraPosition = camPos;
            def.cameraRotation = camRot;
            def.cameraFOV = camFOV;
            def.browseBlendDuration = 0.8f;
            AssetDatabase.CreateAsset(def, path);
        }
        else
        {
            EditorUtility.SetDirty(def);
        }

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
    /// BookcaseUnit + BookInteractionManager. No station camera — uses browse cam.
    /// </summary>
    private static void BuildBookcaseStationGroup(GameObject camGO,
        int booksLayer, int drawersLayer, int perfumesLayer,
        int coffeeTableBooksLayer)
    {
        var groupGO = new GameObject("Station_Bookcase");

        // Coffee table target (world-space, baked into each CoffeeTableBook's serialized fields)
        Vector3 ctBase = new Vector3(-0.571f, 0.38f, 2.007f);
        Quaternion ctRot = Quaternion.Euler(0f, 5f, 0f);

        // Bookcase unit (shared with standalone bookcase scene)
        var bookcaseRoot = BookcaseSceneBuilder.BuildBookcaseUnit(
            booksLayer, drawersLayer, perfumesLayer, coffeeTableBooksLayer,
            ctBase, ctRot);
        bookcaseRoot.transform.SetParent(groupGO.transform);
        bookcaseRoot.transform.localPosition = Vector3.zero;
        bookcaseRoot.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

        // BookInteractionManager
        var bookManager = BuildBookInteractionManager(camGO, booksLayer,
            drawersLayer, perfumesLayer, coffeeTableBooksLayer);
        bookManager.transform.SetParent(groupGO.transform);

        // No ReactableTag on bookcase itself — only individual coffee table books
        // have ReactableTags (set by BookcaseSceneBuilder.BuildCoffeeTableBooks)

        // Position and rotate the entire group
        groupGO.transform.position = BookcaseStationPos;
        groupGO.transform.rotation = BookcaseStationRot;

        Debug.Log("[ApartmentSceneBuilder] Bookcase station group built.");
    }

    /// <summary>
    /// Builds the record player station as a self-contained group:
    /// Furniture + RecordPlayerManager + RecordPlayerHUD + AudioSource. No station camera.
    /// </summary>
    private static void BuildRecordPlayerStationGroup(int vinylStackLayer, int turntableLayer)
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
        turntableGO.layer = turntableLayer;
        SetMaterial(turntableGO, new Color(0.20f, 0.20f, 0.22f));
        turntableGO.AddComponent<InteractableHighlight>();

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

        // Vinyl stack on the floor (10 thin cylinders leaning against the table)
        var stackParent = new GameObject("VinylStack");
        stackParent.transform.SetParent(groupGO.transform);
        stackParent.transform.position = RecordPlayerStationPos + new Vector3(-0.35f, 0f, 0f);
        stackParent.isStatic = true;
        stackParent.layer = vinylStackLayer;

        for (int v = 0; v < 10; v++)
        {
            var vinylGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vinylGO.name = $"Vinyl_{v}";
            vinylGO.transform.SetParent(stackParent.transform);
            vinylGO.layer = vinylStackLayer;
            // Lean against table leg, each offset slightly in Z and stacked in X
            float xOff = v * 0.012f;
            float zJitter = (v % 3 - 1) * 0.01f;
            vinylGO.transform.localPosition = new Vector3(xOff, 0.125f, zJitter);
            vinylGO.transform.localRotation = Quaternion.Euler(0f, 0f, 85f + v * 0.5f);
            vinylGO.transform.localScale = new Vector3(0.24f, 0.003f, 0.24f);
            vinylGO.isStatic = true;

            // Destroy individual thin colliders — too small to click
            var col = vinylGO.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            // Alternate dark vinyl colors for variety
            float shade = 0.04f + (v % 3) * 0.02f;
            SetMaterial(vinylGO, new Color(shade, shade, shade + 0.02f));
        }

        // Single box collider on parent for click detection
        var stackBox = stackParent.AddComponent<BoxCollider>();
        stackBox.center = new Vector3(0.05f, 0.125f, 0f);
        stackBox.size = new Vector3(0.20f, 0.26f, 0.26f);

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
        mgrSO.FindProperty("_vinylStackLayer").intValue = 1 << vinylStackLayer;
        mgrSO.FindProperty("_turntableLayer").intValue = 1 << turntableLayer;
        // _hudCanvas wired below after canvas GO is created

        // ── HUD Canvas ───────────────────────────────────────────────
        var hudCanvasGO = new GameObject("RecordPlayerHUD_Canvas");
        hudCanvasGO.transform.SetParent(groupGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        hudCanvasGO.SetActive(false); // Hidden until player interacts
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
            new Vector2(0f, -200f), 16f, "Click Vinyl Stack  Browse    |    Click Turntable  Play / Stop");

        // Wire _hudCanvas on manager and apply
        mgrSO.FindProperty("_hudCanvas").objectReferenceValue = hudCanvas;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire RecordPlayerHUD
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("titleText").objectReferenceValue = titleText;
        hudSO.FindProperty("artistText").objectReferenceValue = artistText;
        hudSO.FindProperty("stateText").objectReferenceValue = stateText;
        hudSO.FindProperty("hintsText").objectReferenceValue = hintsText;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[ApartmentSceneBuilder] Record Player station group built ({records.Length} records loaded).");
    }

    // ══════════════════════════════════════════════════════════════════
    // ReactableTag Helper
    // ══════════════════════════════════════════════════════════════════

    private static void AddReactableTag(GameObject go, string[] tags, bool isActive,
        bool isPrivate = false, float smellAmount = 0f)
    {
        var tag = go.AddComponent<ReactableTag>();
        var so = new SerializedObject(tag);
        var tagsProp = so.FindProperty("tags");
        tagsProp.arraySize = tags.Length;
        for (int i = 0; i < tags.Length; i++)
            tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i];
        so.FindProperty("isActive").boolValue = isActive;
        so.FindProperty("isPrivate").boolValue = isPrivate;
        so.FindProperty("smellAmount").floatValue = smellAmount;
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

        // Date personal definitions (from design doc)
        string[] names = { "Livii", "Sterling", "Sage", "Clover" };
        string[] ads =
        {
            // Livii (she/her) — the Paris dreamer, travel agent by day
            "F, 25, $2,000/mo. Seeking a lover to move to Paris with and co-parent a cat. "
            + "Are we doing the chateau, long dinner, string quartet version, or the "
            + "coastal-cliff elopement with the wind trying to steal my veil?",

            // Sterling (he/him) — the finance bro
            "FINANCE GENT (proudly atypical), unmarried. Seeking wife, or partner to adore "
            + "and spoil (ski trip on the table). Enjoys the classics: watches, gym, and "
            + "whiskey. Confident he's a rare breed. Photo appreciated with reply.",

            // Sage (they/them) — the cold reader / psychic
            "Single. Intuitive. Self-employed. I can sense you are reading this right now. "
            + "You have been through a lot lately (haven't we all), and you sometimes "
            + "overthink at night. There is a name that starts with a letter, and it still "
            + "affects you. I'm feeling an upcoming change - possibly travel, possibly a haircut.",

            // Clover (she/her) — the tradwife
            "I cook from scratch, keep a peaceful home, and take pride in caring for my person. "
            + "Soft, gentle, and a little catlike - seeking someone who'll treat me accordingly.",
        };
        float[] arrivalTimes = { 40f, 45f, 25f, 50f };

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

        // Always update ad text and character name to stay in sync with code
        for (int i = 0; i < personalDefs.Length; i++)
        {
            if (personalDefs[i] == null) continue;
            personalDefs[i].characterName = names[i];
            personalDefs[i].adText = ads[i];
            personalDefs[i].arrivalTimeSec = arrivalTimes[i];
            EditorUtility.SetDirty(personalDefs[i]);
        }

        // Keywords for hoverable tooltips (matched to ad text substrings)
        var defaultKeywords = new DatePersonalDefinition.KeywordEntry[][]
        {
            new[] // Livii — Paris dreamer
            {
                new DatePersonalDefinition.KeywordEntry { keyword = "move to Paris", commentary = "She dresses with deliberate glamour. By day she sells five-star stays and upgrades." },
                new DatePersonalDefinition.KeywordEntry { keyword = "co-parent a cat", commentary = "She holds lovers to novel-worthy standards - and mistakes the ideal for the real." },
                new DatePersonalDefinition.KeywordEntry { keyword = "coastal-cliff elopement", commentary = "Tonic water or champagne. She likes things a certain way." },
            },
            new[] // Sterling — finance bro
            {
                new DatePersonalDefinition.KeywordEntry { keyword = "rare breed", commentary = "Confident. Very confident. He will yap about wine and whiskey all night." },
                new DatePersonalDefinition.KeywordEntry { keyword = "ski trip", commentary = "He'll bring you a gift - that was gifted to him by someone else." },
                new DatePersonalDefinition.KeywordEntry { keyword = "watches, gym, and whiskey", commentary = "Wearing a suit. Just got off work. Will complain about work." },
            },
            new[] // Sage — cold reader
            {
                new DatePersonalDefinition.KeywordEntry { keyword = "overthink at night", commentary = "First date comes with a spontaneous reading (donation suggested)." },
                new DatePersonalDefinition.KeywordEntry { keyword = "upcoming change", commentary = "They'll land one eerie, specific truth about you - and accidentally drop a piece of your lore." },
                new DatePersonalDefinition.KeywordEntry { keyword = "a letter", commentary = "Warm, attentive, and weirdly good at making people feel seen. May offer... substances." },
            },
            new[] // Clover — tradwife
            {
                new DatePersonalDefinition.KeywordEntry { keyword = "cook from scratch", commentary = "She means it. Everything from scratch. No shortcuts." },
                new DatePersonalDefinition.KeywordEntry { keyword = "peaceful home", commentary = "She takes pride in the space. She'll notice what you've done with yours." },
                new DatePersonalDefinition.KeywordEntry { keyword = "catlike", commentary = "Soft and gentle, but on her own terms. Don't push." },
            },
        };
        // Date preferences (liked/disliked tags matched against ReactableTags in apartment)
        var defaultPreferences = new DatePreferences[]
        {
            // Livii — bougie, romantic, champagne over beer
            new DatePreferences
            {
                likedTags = new[] { "vinyl", "perfume", "book", "plant" },
                dislikedTags = new[] { "mecha", "gundam" },
                preferredMoodMin = 0.3f, preferredMoodMax = 0.6f,
                reactionStrength = 1.2f,
            },
            // Sterling — classic taste, expensive, watches + whiskey
            new DatePreferences
            {
                likedTags = new[] { "vinyl", "drink", "cocktail" },
                dislikedTags = new[] { "plant", "greenery", "incense" },
                preferredMoodMin = 0.1f, preferredMoodMax = 0.4f,
                reactionStrength = 0.9f,
            },
            // Sage — mystic, eclectic, vibes over things
            new DatePreferences
            {
                likedTags = new[] { "perfume", "plant", "greenery", "incense", "book" },
                dislikedTags = new[] { "mecha", "gundam" },
                preferredMoodMin = 0.5f, preferredMoodMax = 0.9f,
                reactionStrength = 1.4f,
            },
            // Clover — domestic, clean, organized
            new DatePreferences
            {
                likedTags = new[] { "plant", "greenery", "book" },
                dislikedTags = new[] { "mecha", "gundam", "music" },
                preferredMoodMin = 0.0f, preferredMoodMax = 0.3f,
                reactionStrength = 1.0f,
            },
        };
        for (int i = 0; i < personalDefs.Length; i++)
        {
            if (personalDefs[i] == null) continue;
            personalDefs[i].keywords = defaultKeywords[i];
            personalDefs[i].preferences = defaultPreferences[i];
            EditorUtility.SetDirty(personalDefs[i]);
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
            pool.personalAdsPerDay = 3;
            pool.commercialAdsPerDay = 2;
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

        // Ad slots, header, and tooltip are built at runtime by NewspaperManager.
        // The canvas + background are the only build-time elements.

        // Start overlay hidden
        pivotGO.SetActive(false);

        // ── Managers ──────────────────────────────────────────────
        var managersGO = new GameObject("NewspaperManagers");

        // DayManager
        var dayMgr = managersGO.AddComponent<DayManager>();
        var dayMgrSO = new SerializedObject(dayMgr);
        dayMgrSO.FindProperty("pool").objectReferenceValue = pool;
        dayMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // NewspaperManager (self-constructing layout — builds ad slots at runtime)
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
        newsMgrSO.FindProperty("_contentParent").objectReferenceValue = canvasRT;
        newsMgrSO.FindProperty("_backgroundImage").objectReferenceValue = bgImg;
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
            new Vector2(0f, -30f), new Vector2(400f, 60f),
            "Entrance", 28f, new Color(0f, 0f, 0f, 0.6f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        // Browse hints panel (bottom center)
        var browseHints = CreateUIPanel("BrowseHintsPanel", uiCanvasGO.transform,
            new Vector2(0f, 20f), new Vector2(600f, 50f),
            "Click arrows to switch rooms  |  Click objects to interact", 16f,
            new Color(0f, 0f, 0f, 0.5f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

        // ── Nav buttons (left/right arrows) ──
        var navLeftBtn = CreateNavButton("NavLeft", uiCanvasGO.transform,
            new Vector2(40f, 0f), "\u25C0",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        var navRightBtn = CreateNavButton("NavRight", uiCanvasGO.transform,
            new Vector2(-40f, 0f), "\u25B6",
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

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
        Vector2 anchoredPos, string label,
        Vector2? anchorMin = null, Vector2? anchorMax = null)
    {
        Vector2 aMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        Vector2 aMax = anchorMax ?? new Vector2(0.5f, 0.5f);

        var btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
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
    // Camera Test Controller (A/B/C preset comparison)
    // ══════════════════════════════════════════════════════════════════

    private static void BuildCameraTestController(CinemachineCamera browseCam, GameObject mainCamGO)
    {
        // ── SO folder (already created by BuildAreaDefinitions) ──
        string soDir = "Assets/ScriptableObjects/Apartment";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Apartment");

        // ── Preset SOs (9 presets, keys 1-9) ──
        var v1 = CreateCameraPreset("CameraPreset_V1", soDir, "V1 — High Angle",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-1.0f, 3.5f, -3.5f), rotation = new Vector3(35f, 45f, 0f), lens = MakeLens(48f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(0.5f, 3.5f, 2.0f),   rotation = new Vector3(30f, 60f, 0f), lens = MakeLens(48f) },
            });

        var v2 = CreateCameraPreset("CameraPreset_V2", soDir, "V2 — Low & Wide",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-2.5f, 1.8f, -5.5f), rotation = new Vector3(15f, 30f, 0f), lens = MakeLens(62f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(-0.5f, 1.8f, 1.0f),  rotation = new Vector3(12f, 50f, 0f), lens = MakeLens(62f) },
            });

        var v3 = CreateCameraPreset("CameraPreset_V3", soDir, "V3 — Isometric Ortho",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-2.5f, 6.0f, -4.5f), rotation = new Vector3(55f, 45f, 0f), lens = MakeLensOrtho(3.5f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(1.0f, 6.0f, 2.5f),   rotation = new Vector3(55f, 45f, 0f), lens = MakeLensOrtho(3.5f) },
            });

        var v4 = CreateCameraPreset("CameraPreset_V4", soDir, "V4 — Overhead",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-2.0f, 5.0f, -3.5f), rotation = new Vector3(75f, 0f, 0f), lens = MakeLens(40f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(0.0f, 5.0f, 2.5f),   rotation = new Vector3(75f, 0f, 0f), lens = MakeLens(40f) },
            });

        var v5 = CreateCameraPreset("CameraPreset_V5", soDir, "V5 — Dutch Tilt",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-3.0f, 2.5f, -4.0f), rotation = new Vector3(20f, 40f, 0f), lens = MakeLensDutch(52f, 12f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(-1.0f, 2.5f, 1.5f),  rotation = new Vector3(18f, 55f, 0f), lens = MakeLensDutch(52f, -10f) },
            });

        var v6 = CreateCameraPreset("CameraPreset_V6", soDir, "V6 — Tight Close-Up",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-2.0f, 1.5f, -3.0f), rotation = new Vector3(10f, 35f, 0f), lens = MakeLens(32f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(0.0f, 1.5f, 2.0f),   rotation = new Vector3(8f, 50f, 0f),  lens = MakeLens(32f) },
            });

        var v7 = CreateCameraPreset("CameraPreset_V7", soDir, "V7 — Ultra Wide",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-1.5f, 2.8f, -5.0f), rotation = new Vector3(25f, 35f, 0f), lens = MakeLens(80f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(0.5f, 2.8f, 0.5f),   rotation = new Vector3(22f, 55f, 0f), lens = MakeLens(80f) },
            });

        var v8 = CreateCameraPreset("CameraPreset_V8", soDir, "V8 — Side Profile",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-5.0f, 2.0f, -3.5f), rotation = new Vector3(15f, 90f, 0f), lens = MakeLens(50f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(-4.0f, 2.0f, 2.5f),  rotation = new Vector3(12f, 80f, 0f), lens = MakeLens(50f) },
            });

        var v9 = CreateCameraPreset("CameraPreset_V9", soDir, "V9 — Surveillance",
            new AreaCameraConfig[]
            {
                new AreaCameraConfig { areaLabel = "Kitchen",     position = new Vector3(-0.5f, 4.0f, -6.0f), rotation = new Vector3(40f, 20f, 0f), lens = MakeLens(35f) },
                new AreaCameraConfig { areaLabel = "Living Room", position = new Vector3(2.0f, 4.0f, 0.5f),   rotation = new Vector3(42f, -30f, 0f), lens = MakeLens(35f) },
            });

        var allPresets = new CameraPresetDefinition[] { v1, v2, v3, v4, v5, v6, v7, v8, v9 };

        // ── Controller GO ──
        var controllerGO = new GameObject("CameraTestController");
        var controller = controllerGO.AddComponent<CameraTestController>();

        // ── UI Canvas (bottom-left, ScreenSpace Overlay) ──
        var canvasGO = new GameObject("CameraTestUI_Canvas");
        canvasGO.transform.SetParent(controllerGO.transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        string[] labels = { "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8", "V9" };
        var buttons = new Button[9];
        for (int i = 0; i < 9; i++)
        {
            int col = i % 3;
            int row = 2 - i / 3; // top row = V1-V3, middle = V4-V6, bottom = V7-V9

            var btnGO = new GameObject($"Btn_{labels[i]}");
            btnGO.transform.SetParent(canvasGO.transform);

            var rt = btnGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(60f, 32f);
            rt.anchoredPosition = new Vector2(20f + col * 68f, 20f + row * 38f);
            rt.localScale = Vector3.one;

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.85f);

            var btn = btnGO.AddComponent<Button>();
            buttons[i] = btn;

            // Label text
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(btnGO.transform);

            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            textRT.localScale = Vector3.one;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = labels[i];
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            // onClick wired at runtime in CameraTestController.Start()
        }

        // ── Wire serialized fields ──
        var brain = mainCamGO.GetComponent<CinemachineBrain>();
        var ctrlSO = new SerializedObject(controller);

        var presetsProp = ctrlSO.FindProperty("presets");
        presetsProp.arraySize = allPresets.Length;
        for (int i = 0; i < allPresets.Length; i++)
            presetsProp.GetArrayElementAtIndex(i).objectReferenceValue = allPresets[i];

        ctrlSO.FindProperty("browseCamera").objectReferenceValue = browseCam;
        ctrlSO.FindProperty("brain").objectReferenceValue = brain;

        var btnsProp = ctrlSO.FindProperty("presetButtons");
        btnsProp.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
            btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];

        // Create a Volume for preset post-processing swaps
        var volumeGO = new GameObject("PresetVolume");
        volumeGO.transform.SetParent(controllerGO.transform);
        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.priority = 10; // Above default volume
        ctrlSO.FindProperty("presetVolume").objectReferenceValue = volume;

        // Wire directional light
        var dirLightGO = GameObject.Find("Directional Light");
        if (dirLightGO != null)
            ctrlSO.FindProperty("directionalLight").objectReferenceValue =
                dirLightGO.GetComponent<Light>();

        // Find ApartmentManager and wire bidirectional references
        var aptManager = Object.FindAnyObjectByType<ApartmentManager>();
        if (aptManager != null)
        {
            ctrlSO.FindProperty("apartmentManager").objectReferenceValue = aptManager;
            ctrlSO.ApplyModifiedPropertiesWithoutUndo();

            var aptSO = new SerializedObject(aptManager);
            aptSO.FindProperty("cameraTestController").objectReferenceValue = controller;

            // Wire V1 preset as the default browse angles
            string v1Path = $"{soDir}/CameraPreset_V1.asset";
            var v1Preset = AssetDatabase.LoadAssetAtPath<CameraPresetDefinition>(v1Path);
            if (v1Preset != null)
                aptSO.FindProperty("defaultPreset").objectReferenceValue = v1Preset;

            aptSO.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            ctrlSO.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("[ApartmentSceneBuilder] CameraTestController wired with 3 presets + Volume + directional light.");
    }

    private static CameraPresetDefinition CreateCameraPreset(
        string assetName, string directory, string label,
        AreaCameraConfig[] configs)
    {
        string path = $"{directory}/{assetName}.asset";
        var def = AssetDatabase.LoadAssetAtPath<CameraPresetDefinition>(path);
        bool isNew = def == null;
        if (isNew)
            def = ScriptableObject.CreateInstance<CameraPresetDefinition>();

        if (isNew)
        {
            // Only set defaults on first creation — preserve user edits on rebuild
            def.label = label;
            def.areaConfigs = configs;
            AssetDatabase.CreateAsset(def, path);
        }
        else
        {
            EditorUtility.SetDirty(def);
        }

        return def;
    }

    private static LensSettings MakeLens(float fov, float near = 0.3f, float far = 1000f)
    {
        var lens = LensSettings.Default;
        lens.FieldOfView = fov;
        lens.NearClipPlane = near;
        lens.FarClipPlane = far;
        lens.ModeOverride = LensSettings.OverrideModes.None;
        return lens;
    }

    private static LensSettings MakeLensOrtho(float orthoSize, float near = 0.3f, float far = 1000f)
    {
        var lens = LensSettings.Default;
        lens.OrthographicSize = orthoSize;
        lens.NearClipPlane = near;
        lens.FarClipPlane = far;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        return lens;
    }

    private static LensSettings MakeLensDutch(float fov, float dutch, float near = 0.3f, float far = 1000f)
    {
        var lens = MakeLens(fov, near, far);
        lens.Dutch = dutch;
        return lens;
    }

    // ══════════════════════════════════════════════════════════════════
    // BookInteractionManager
    // ══════════════════════════════════════════════════════════════════

    private static BookInteractionManager BuildBookInteractionManager(
        GameObject camGO, int booksLayer,
        int drawersLayer, int perfumesLayer, int coffeeTableBooksLayer)
    {
        var managerGO = new GameObject("BookInteractionManager");
        var manager = managerGO.AddComponent<BookInteractionManager>();

        var anchorGO = new GameObject("ReadingAnchor");
        anchorGO.transform.SetParent(camGO.transform);
        anchorGO.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
        anchorGO.transform.localRotation = Quaternion.identity;

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
        so.FindProperty("coffeeTableBooksLayerMask").intValue = 1 << coffeeTableBooksLayer;
        so.FindProperty("maxRayDistance").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

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
            new Vector3(3.063f, 0.9f, -1.653f), new Vector3(0.7f, 1.8f, 0.35f),
            new Color(0.85f, 0.85f, 0.87f));
        fridgeBody.isStatic = true;

        var doorPivotGO = new GameObject("FridgeDoorPivot");
        doorPivotGO.transform.SetParent(groupGO.transform);
        doorPivotGO.transform.position = new Vector3(2.713f, 0.9f, -1.478f);
        doorPivotGO.isStatic = false;

        var fridgeDoor = CreateBox("FridgeDoor", doorPivotGO.transform,
            new Vector3(3.063f, 0.9f, -1.478f), new Vector3(0.7f, 1.8f, 0.35f),
            new Color(0.87f, 0.87f, 0.89f));
        fridgeDoor.isStatic = false;
        fridgeDoor.layer = fridgeLayer;
        fridgeDoor.AddComponent<InteractableHighlight>();

        var handle = CreateBox("FridgeHandle", fridgeDoor.transform,
            new Vector3(3.383f, 1.0f, -1.328f), new Vector3(0.03f, 0.15f, 0.03f),
            new Color(0.5f, 0.5f, 0.55f));
        handle.isStatic = false;
        handle.layer = fridgeLayer;

        // ── Fridge interior light ────────────────────────────────────
        var fridgeLightGO = new GameObject("FridgeLight");
        fridgeLightGO.transform.SetParent(groupGO.transform);
        fridgeLightGO.transform.position = new Vector3(3.063f, 1.5f, -1.55f);
        var fridgeLight = fridgeLightGO.AddComponent<Light>();
        fridgeLight.type = LightType.Point;
        fridgeLight.color = new Color(0.95f, 0.97f, 1f);
        fridgeLight.intensity = 1.5f;
        fridgeLight.range = 1.2f;
        fridgeLight.enabled = false;

        // ── Glass on counter (on Glass layer for raycast) ─────────────
        var glassGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        glassGO.name = "Glass";
        glassGO.transform.SetParent(groupGO.transform);
        glassGO.transform.position = new Vector3(1.82f, 1.09f, -3.56f);
        glassGO.transform.rotation = new Quaternion(0f, -0.7219f, 0f, 0.692f);
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
        glassGO.AddComponent<InteractableHighlight>();

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

        // _hudCanvas wired below after canvas GO is created

        // ── HUD Canvas ────────────────────────────────────────────────
        var hudCanvasGO = new GameObject("SimpleDrinkHUD_Canvas");
        hudCanvasGO.transform.SetParent(managersGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        hudCanvasGO.SetActive(false); // Hidden until player picks a recipe
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
        var scoreLabel = CreateHUDText("ScoreLabel", hudPanelGO.transform,
            new Vector2(0f, -60f), 20f, "");

        // PourBarUI — positioned on right side of HUD panel
        var pourBarGO = new GameObject("PourBar");
        pourBarGO.transform.SetParent(hudPanelGO.transform, false);
        var pourBarRT = pourBarGO.AddComponent<RectTransform>();
        pourBarRT.anchorMin = new Vector2(0.5f, 0.5f);
        pourBarRT.anchorMax = new Vector2(0.5f, 0.5f);
        pourBarRT.sizeDelta = new Vector2(60f, 220f);
        pourBarRT.anchoredPosition = new Vector2(250f, 0f);
        pourBarRT.localScale = Vector3.one;
        var pourBar = pourBarGO.AddComponent<PourBarUI>();
        pourBar.barHeight = 200f;
        pourBar.barWidth = 40f;

        // Wire HUD
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("drinkNameLabel").objectReferenceValue = drinkNameLabel;
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
        hudSO.FindProperty("pourBar").objectReferenceValue = pourBar;
        hudSO.FindProperty("hudPanel").objectReferenceValue = hudPanelGO;
        hudSO.FindProperty("recipePanel").objectReferenceValue = recipePanelGO;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire _hudCanvas on manager and apply
        mgrSO.FindProperty("_hudCanvas").objectReferenceValue = hudCanvas;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── FridgeController ──────────────────────────────────────────
        var fridgeCtrl = groupGO.AddComponent<FridgeController>();
        var fridgeCtrlSO = new SerializedObject(fridgeCtrl);
        fridgeCtrlSO.FindProperty("_doorPivot").objectReferenceValue = doorPivotGO.transform;
        fridgeCtrlSO.FindProperty("_fridgeLayer").intValue = 1 << fridgeLayer;
        fridgeCtrlSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        fridgeCtrlSO.FindProperty("_interiorLight").objectReferenceValue = fridgeLight;
        fridgeCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // ── ReactableTag on drink station (toggled when drink is delivered) ──
        AddReactableTag(groupGO, new[] { "drink", "cocktail" }, false);

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
        if (furnitureRefs.kitchenStandPoint != null)
            dsmSO.FindProperty("kitchenStandPoint").objectReferenceValue = furnitureRefs.kitchenStandPoint;

        // Add EntranceJudgmentSequence and wire it to DateSessionManager
        var entranceJudgments = managersGO.AddComponent<EntranceJudgmentSequence>();
        dsmSO.FindProperty("_entranceJudgments").objectReferenceValue = entranceJudgments;
        dsmSO.ApplyModifiedPropertiesWithoutUndo();

        // ── MidDateActionWatcher ──────────────────────────────────────
        managersGO.AddComponent<MidDateActionWatcher>();

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

            // Phone is always active — no station gating needed
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

    // ══════════════════════════════════════════════════════════════════
    // Door Greeting Controller
    // ══════════════════════════════════════════════════════════════════

    private static void BuildDoor(GameObject camGO, int doorLayer)
    {
        var doorGO = CreateBox("Door", null,
            new Vector3(-3f, 1.1f, -5.5f), new Vector3(0.8f, 2.2f, 0.1f),
            new Color(0.4f, 0.25f, 0.15f));
        doorGO.layer = doorLayer;
        doorGO.isStatic = false;

        // World-space canvas for "knock knock" text
        var canvasGO = new GameObject("KnockCanvas");
        canvasGO.transform.SetParent(doorGO.transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(400f, 80f);
        canvasRT.localPosition = new Vector3(0f, 1.4f, -0.1f);
        canvasRT.localScale = Vector3.one * 0.008f;

        var knockGO = new GameObject("KnockText");
        knockGO.transform.SetParent(canvasGO.transform, false);
        var knockRT = knockGO.AddComponent<RectTransform>();
        knockRT.anchorMin = Vector2.zero;
        knockRT.anchorMax = Vector2.one;
        knockRT.offsetMin = Vector2.zero;
        knockRT.offsetMax = Vector2.zero;
        knockRT.localScale = Vector3.one;
        var knockTMP = knockGO.AddComponent<TextMeshProUGUI>();
        knockTMP.text = "knock knock";
        knockTMP.fontSize = 42f;
        knockTMP.alignment = TextAlignmentOptions.Center;
        knockTMP.color = new Color(1f, 0.9f, 0.3f);
        knockTMP.outlineWidth = 0.2f;
        knockTMP.outlineColor = Color.black;
        knockGO.SetActive(false);

        // DoorGreetingController component
        var doorCtrl = doorGO.AddComponent<DoorGreetingController>();
        var doorSO = new SerializedObject(doorCtrl);
        doorSO.FindProperty("_doorLayer").intValue = 1 << doorLayer;
        var cam = camGO.GetComponent<Camera>();
        if (cam != null)
            doorSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        doorSO.FindProperty("_knockText").objectReferenceValue = knockTMP;
        doorSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] Door greeting controller built.");
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

        // Continue button — dismisses end screen only (no scene load, no GoToBed)
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
        btnTMP.text = "Continue";
        btnTMP.fontSize = 20f;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = Color.white;

        // OnContinue is wired via Awake listener in DateEndScreen — no persistent listener needed

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
            // Assign area based on Z position: Z < 0 = Kitchen, Z >= 0 = LivingRoom
            surfSO.FindProperty("_area").enumValueIndex = slotPositions[i].z < 0f ? 0 : 1;
            surfSO.ApplyModifiedPropertiesWithoutUndo();

            stainSlots[i] = surface;
            slotGO.SetActive(false); // Starts disabled, ApartmentStainSpawner activates subset
        }

        // ── Tool visual (sponge) ────────────────────────────────────────
        var spongeGO = CreateBox("SpongeVisual", null,
            Vector3.zero, new Vector3(0.06f, 0.03f, 0.08f),
            new Color(0.9f, 0.85f, 0.3f));
        spongeGO.isStatic = false;
        spongeGO.SetActive(false);

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

        var instrLabel = CreateHUDText("InstructionLabel", cleanHudCanvasGO.transform,
            new Vector2(0f, -200f), 16f, "Click stains to scrub them clean");
        var progressLabel = CreateHUDText("ProgressLabel", cleanHudCanvasGO.transform,
            new Vector2(350f, 200f), 18f, "Clean: 0%");
        var surfaceDetailLabel = CreateHUDText("SurfaceDetailLabel", cleanHudCanvasGO.transform,
            new Vector2(350f, 160f), 14f, "");

        // Wire CleaningHUD (public fields)
        var cleanHudSO = new SerializedObject(cleanHud);
        cleanHudSO.FindProperty("manager").objectReferenceValue = cleanMgr;
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
        string[] plantAssetNames = { "Plant_Fern", "Plant_Cactus", "Plant_Succulent", "Plant_Monstera", "Plant_Herb_Pot" };
        var plantDefs = new PlantDefinition[plantAssetNames.Length];
        for (int i = 0; i < plantAssetNames.Length; i++)
        {
            string path = $"{soDir}/{plantAssetNames[i]}.asset";
            plantDefs[i] = AssetDatabase.LoadAssetAtPath<PlantDefinition>(path);
            if (plantDefs[i] == null)
                Debug.LogWarning($"[ApartmentSceneBuilder] Missing PlantDefinition: {path}");
        }

        // ── Plant positions in apartment ───────────────────────────────
        // Distributed across the 7 apartment areas for visual variety.
        // Y=0.01 = floor level, Y=0.98 = on ledge/counter.
        Vector3[] plantPositions =
        {
            new Vector3(-4.1f, 0.98f,  5.7f),  // Living room ledge left
            new Vector3(-2.8f, 0.98f,  5.7f),  // Living room ledge right
            new Vector3(-5.0f, 0.01f, -3.0f),  // Kitchen floor corner
            new Vector3(-7.0f, 0.01f,  4.0f),  // Near bookcase (living room)
            new Vector3(-1.5f, 0.98f,  5.7f),  // Living room ledge far right
            new Vector3(-3.0f, 0.01f, -4.5f),  // Kitchen floor near counter
            new Vector3(-6.5f, 0.01f,  1.0f),  // Hallway area
            new Vector3( 0.0f, 0.98f,  5.7f),  // Living room ledge center
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

            // CreateBox uses WORLD position, so offset from plantPositions[i]
            Vector3 p = plantPositions[i];

            // Pot visual
            Color potColor = def != null ? def.potColor : new Color(0.6f, 0.35f, 0.25f);
            var potGO = CreateBox("Pot", plantRoot.transform,
                p + new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.1f, 0.12f), potColor);
            potGO.layer = plantsLayer;
            potGO.isStatic = false;
            potGO.AddComponent<InteractableHighlight>();

            // Stem
            Color plantColor = def != null ? def.plantColor : new Color(0.2f, 0.5f, 0.2f);
            var stemGO = CreateBox("Stem", plantRoot.transform,
                p + new Vector3(0f, 0.12f, 0f), new Vector3(0.02f, 0.10f, 0.02f), plantColor);
            stemGO.layer = plantsLayer;

            // Leaves
            var leafL = CreateBox("LeafL", plantRoot.transform,
                p + new Vector3(-0.04f, 0.14f, 0f), new Vector3(0.06f, 0.03f, 0.02f), plantColor);
            leafL.layer = plantsLayer;
            var leafR = CreateBox("LeafR", plantRoot.transform,
                p + new Vector3(0.04f, 0.16f, 0f), new Vector3(0.06f, 0.03f, 0.02f), plantColor);
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
        canvasGO.SetActive(false); // Hidden until player interacts with a plant
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

        // PourBarUI — positioned on right side of screen
        var pourBarGO = new GameObject("PourBar");
        pourBarGO.transform.SetParent(hudPanelGO.transform, false);
        var pourBarRT = pourBarGO.AddComponent<RectTransform>();
        pourBarRT.anchorMin = new Vector2(0.5f, 0.5f);
        pourBarRT.anchorMax = new Vector2(0.5f, 0.5f);
        pourBarRT.sizeDelta = new Vector2(60f, 220f);
        pourBarRT.anchoredPosition = new Vector2(300f, 0f);
        pourBarRT.localScale = Vector3.one;
        var pourBar = pourBarGO.AddComponent<PourBarUI>();
        pourBar.barHeight = 200f;
        pourBar.barWidth = 40f;
        pourBar.liquidColor = new Color(0.3f, 0.55f, 0.85f, 0.9f);
        pourBar.foamColor = new Color(0.5f, 0.38f, 0.22f, 0.7f);

        // Wire HUD
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("plantNameLabel").objectReferenceValue = plantNameLabel;
        hudSO.FindProperty("pourBar").objectReferenceValue = pourBar;
        hudSO.FindProperty("hudPanel").objectReferenceValue = hudPanelGO;
        hudSO.FindProperty("hudCanvas").objectReferenceValue = canvas;
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

        // Wire DailyMessSpawner
        var messSpawner = Object.FindAnyObjectByType<DailyMessSpawner>();
        if (messSpawner != null)
            dpmSO.FindProperty("_messSpawner").objectReferenceValue = messSpawner;
        dpmSO.FindProperty("_newspaperHUD").objectReferenceValue = newspaperData.hudRoot;

        // ── Prep Timer UI (top-right corner) ──
        var timerCanvas = new GameObject("PrepTimerCanvas");
        timerCanvas.transform.SetParent(go.transform);
        var timerC = timerCanvas.AddComponent<Canvas>();
        timerC.renderMode = RenderMode.ScreenSpaceOverlay;
        timerC.sortingOrder = 15;
        timerCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        timerCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var timerPanel = new GameObject("PrepTimerPanel");
        timerPanel.transform.SetParent(timerCanvas.transform);
        var timerRT = timerPanel.AddComponent<RectTransform>();
        timerRT.anchorMin = new Vector2(1f, 1f);
        timerRT.anchorMax = new Vector2(1f, 1f);
        timerRT.pivot = new Vector2(1f, 1f);
        timerRT.sizeDelta = new Vector2(180f, 50f);
        timerRT.anchoredPosition = new Vector2(-20f, -20f);
        timerRT.localScale = Vector3.one;

        var timerBg = timerPanel.AddComponent<UnityEngine.UI.Image>();
        timerBg.color = new Color(0f, 0f, 0f, 0.7f);

        var timerTextGO = new GameObject("TimerText");
        timerTextGO.transform.SetParent(timerPanel.transform);
        var timerTextRT = timerTextGO.AddComponent<RectTransform>();
        timerTextRT.anchorMin = Vector2.zero;
        timerTextRT.anchorMax = Vector2.one;
        timerTextRT.offsetMin = new Vector2(10f, 5f);
        timerTextRT.offsetMax = new Vector2(-10f, -5f);
        timerTextRT.localScale = Vector3.one;

        var timerTMP = timerTextGO.AddComponent<TextMeshProUGUI>();
        timerTMP.text = "\u23F0 2:00";
        timerTMP.fontSize = 24f;
        timerTMP.alignment = TextAlignmentOptions.Center;
        timerTMP.color = Color.white;

        timerPanel.SetActive(false); // hidden until prep starts

        dpmSO.FindProperty("_prepTimerPanel").objectReferenceValue = timerPanel;
        dpmSO.FindProperty("_prepTimerText").objectReferenceValue = timerTMP;

        // Find tossed newspaper position
        var tossedPos = GameObject.Find("TossedNewspaperPosition");
        if (tossedPos != null)
            dpmSO.FindProperty("_tossedNewspaperPosition").objectReferenceValue =
                tossedPos.transform;

        // ── Go to Bed panel (bottom-center, visible during Evening only) ──
        var bedCanvas = new GameObject("GoToBedCanvas");
        bedCanvas.transform.SetParent(go.transform);
        var bedC = bedCanvas.AddComponent<Canvas>();
        bedC.renderMode = RenderMode.ScreenSpaceOverlay;
        bedC.sortingOrder = 16;
        bedCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        bedCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var bedPanel = new GameObject("GoToBedPanel");
        bedPanel.transform.SetParent(bedCanvas.transform);
        var bedRT = bedPanel.AddComponent<RectTransform>();
        bedRT.anchorMin = new Vector2(0.5f, 0f);
        bedRT.anchorMax = new Vector2(0.5f, 0f);
        bedRT.pivot = new Vector2(0.5f, 0f);
        bedRT.sizeDelta = new Vector2(220f, 60f);
        bedRT.anchoredPosition = new Vector2(0f, 30f);
        bedRT.localScale = Vector3.one;

        var bedBg = bedPanel.AddComponent<UnityEngine.UI.Image>();
        bedBg.color = new Color(0.1f, 0.05f, 0.2f, 0.85f);

        var bedBtn = bedPanel.AddComponent<UnityEngine.UI.Button>();

        var bedLabelGO = new GameObject("Label");
        bedLabelGO.transform.SetParent(bedPanel.transform);
        var bedLabelRT = bedLabelGO.AddComponent<RectTransform>();
        bedLabelRT.anchorMin = Vector2.zero;
        bedLabelRT.anchorMax = Vector2.one;
        bedLabelRT.offsetMin = Vector2.zero;
        bedLabelRT.offsetMax = Vector2.zero;
        bedLabelRT.localScale = Vector3.one;
        var bedLabelTMP = bedLabelGO.AddComponent<TextMeshProUGUI>();
        bedLabelTMP.text = "Go to Bed";
        bedLabelTMP.fontSize = 22f;
        bedLabelTMP.alignment = TextAlignmentOptions.Center;
        bedLabelTMP.color = Color.white;

        // Wire button → GameClock.GoToBed
        var gameClock = Object.FindAnyObjectByType<GameClock>();
        if (gameClock != null)
        {
            var goToBedAction = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), gameClock,
                typeof(GameClock).GetMethod(nameof(GameClock.GoToBed),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                as UnityEngine.Events.UnityAction;
            if (goToBedAction != null)
                UnityEventTools.AddPersistentListener(bedBtn.onClick, goToBedAction);
        }

        bedPanel.SetActive(false); // DayPhaseManager shows it in Evening

        dpmSO.FindProperty("_goToBedPanel").objectReferenceValue = bedPanel;
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
        img.color = Color.white;
        img.raycastTarget = true;

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f; // Start fully opaque — MorningTransition fades in
        cg.blocksRaycasts = true;

        var fade = go.AddComponent<ScreenFade>();
        var fadeSO = new SerializedObject(fade);
        fadeSO.FindProperty("_canvasGroup").objectReferenceValue = cg;
        fadeSO.FindProperty("defaultFadeOutDuration").floatValue = 0.5f;
        fadeSO.FindProperty("defaultFadeInDuration").floatValue = 0.5f;
        // Easing curves use EaseInOut by default (set in ScreenFade field initializers)
        fadeSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Phase title text (centered, shown during phase transitions, starts hidden) ──
        var phaseGO = new GameObject("PhaseText");
        phaseGO.transform.SetParent(go.transform, false);
        var phaseRT = phaseGO.AddComponent<RectTransform>();
        phaseRT.anchorMin = new Vector2(0.5f, 0.5f);
        phaseRT.anchorMax = new Vector2(0.5f, 0.5f);
        phaseRT.pivot = new Vector2(0.5f, 0.5f);
        phaseRT.sizeDelta = new Vector2(800f, 200f);
        phaseRT.anchoredPosition = Vector2.zero;
        phaseRT.localScale = Vector3.one;
        var phaseTMP = phaseGO.AddComponent<TextMeshProUGUI>();
        phaseTMP.text = "";
        phaseTMP.fontSize = 90f;
        phaseTMP.alignment = TextAlignmentOptions.Center;
        phaseTMP.color = new Color(0.8f, 0.1f, 0.1f, 1f);
        phaseGO.SetActive(false);

        // Wire phase text to ScreenFade
        fadeSO.FindProperty("_phaseText").objectReferenceValue = phaseTMP;
        fadeSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Dream interstitial text (centered, large, italic, starts hidden) ──
        var dreamGO = new GameObject("DreamText");
        dreamGO.transform.SetParent(go.transform, false);
        var dreamRT = dreamGO.AddComponent<RectTransform>();
        dreamRT.anchorMin = new Vector2(0.5f, 0.5f);
        dreamRT.anchorMax = new Vector2(0.5f, 0.5f);
        dreamRT.pivot = new Vector2(0.5f, 0.5f);
        dreamRT.sizeDelta = new Vector2(800f, 100f);
        dreamRT.anchoredPosition = Vector2.zero;
        dreamRT.localScale = Vector3.one;
        var dreamTMP = dreamGO.AddComponent<TextMeshProUGUI>();
        dreamTMP.text = "";
        dreamTMP.fontSize = 32f;
        dreamTMP.fontStyle = FontStyles.Italic;
        dreamTMP.alignment = TextAlignmentOptions.Center;
        dreamTMP.color = new Color(0.8f, 0.8f, 1f, 0.9f);
        dreamGO.SetActive(false);

        // Wire dream text to GameClock
        var gameClock = Object.FindAnyObjectByType<GameClock>();
        if (gameClock != null)
        {
            var gcSO = new SerializedObject(gameClock);
            gcSO.FindProperty("_dreamText").objectReferenceValue = dreamTMP;
            gcSO.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("[ApartmentSceneBuilder] ScreenFade overlay built (with dream text).");
    }

    // ══════════════════════════════════════════════════════════════════
    // F1 Debug Overlay
    // ══════════════════════════════════════════════════════════════════

    private static void BuildDateDebugOverlay()
    {
        var go = new GameObject("DateDebugOverlay");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        // Semi-transparent background panel (left-aligned)
        var panelGO = new GameObject("DebugPanel");
        panelGO.transform.SetParent(go.transform, false);
        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(0.45f, 1f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        panelRT.localScale = Vector3.one;
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.7f);
        panelImg.raycastTarget = false;

        // Debug text (monospace, left-aligned)
        var textGO = new GameObject("DebugText");
        textGO.transform.SetParent(panelGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8f, 8f);
        textRT.offsetMax = new Vector2(-8f, -8f);
        textRT.localScale = Vector3.one;
        var debugTMP = textGO.AddComponent<TextMeshProUGUI>();
        debugTMP.fontSize = 11f;
        debugTMP.alignment = TextAlignmentOptions.TopLeft;
        debugTMP.color = new Color(0.8f, 1f, 0.8f, 1f);
        debugTMP.richText = true;
        debugTMP.enableWordWrapping = true;
        debugTMP.overflowMode = TextOverflowModes.Truncate;
        debugTMP.raycastTarget = false;

        // DateDebugOverlay component
        var overlay = go.AddComponent<DateDebugOverlay>();
        var overlaySO = new SerializedObject(overlay);
        overlaySO.FindProperty("_overlayRoot").objectReferenceValue = panelGO;
        overlaySO.FindProperty("_debugText").objectReferenceValue = debugTMP;
        overlaySO.ApplyModifiedPropertiesWithoutUndo();

        // Start hidden
        panelGO.SetActive(false);

        Debug.Log("[ApartmentSceneBuilder] F1 debug overlay built.");
    }

    // ══════════════════════════════════════════════════════════════════
    // Name Entry Screen
    // ══════════════════════════════════════════════════════════════════

    private static void BuildNameEntryScreen()
    {
        var go = new GameObject("NameEntryScreen");
        var entryCanvas = go.AddComponent<Canvas>();
        entryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        entryCanvas.sortingOrder = 110; // Above ScreenFade (100) so it's visible on start

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>();

        // Dark background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
        bgImg.raycastTarget = true;

        // Title — "What is your name?"
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(go.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.5f);
        titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = new Vector2(600f, 60f);
        titleRT.anchoredPosition = new Vector2(0f, 260f);
        titleRT.localScale = Vector3.one;
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "What is your name?";
        titleTMP.fontSize = 36f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.9f, 0.88f, 0.82f);
        titleTMP.richText = true;

        // Name display (shows entered characters with cursor)
        var nameGO = new GameObject("NameDisplay");
        nameGO.transform.SetParent(go.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.5f, 0.5f);
        nameRT.anchorMax = new Vector2(0.5f, 0.5f);
        nameRT.sizeDelta = new Vector2(500f, 50f);
        nameRT.anchoredPosition = new Vector2(0f, 190f);
        nameRT.localScale = Vector3.one;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "N e m a _ . . .";
        nameTMP.fontSize = 32f;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = Color.white;
        nameTMP.richText = true;

        // Letter grid (monospace-style rich text, updated by script)
        var gridGO = new GameObject("LetterGrid");
        gridGO.transform.SetParent(go.transform, false);
        var gridRT = gridGO.AddComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0.5f, 0.5f);
        gridRT.anchorMax = new Vector2(0.5f, 0.5f);
        gridRT.sizeDelta = new Vector2(700f, 350f);
        gridRT.anchoredPosition = new Vector2(0f, -40f);
        gridRT.localScale = Vector3.one;
        var gridTMP = gridGO.AddComponent<TextMeshProUGUI>();
        gridTMP.text = "";
        gridTMP.fontSize = 24f;
        gridTMP.alignment = TextAlignmentOptions.Center;
        gridTMP.color = new Color(0.85f, 0.85f, 0.85f);
        gridTMP.richText = true;
        gridTMP.enableWordWrapping = false;

        // Hint text at bottom
        var hintGO = new GameObject("HintText");
        hintGO.transform.SetParent(go.transform, false);
        var hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0.5f, 0.5f);
        hintRT.anchorMax = new Vector2(0.5f, 0.5f);
        hintRT.sizeDelta = new Vector2(600f, 40f);
        hintRT.anchoredPosition = new Vector2(0f, -250f);
        hintRT.localScale = Vector3.one;
        var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
        hintTMP.text = "Arrow Keys  Navigate    |    Enter  Select";
        hintTMP.fontSize = 16f;
        hintTMP.alignment = TextAlignmentOptions.Center;
        hintTMP.color = new Color(0.5f, 0.5f, 0.5f);

        // NameEntryScreen component
        var screen = go.AddComponent<NameEntryScreen>();
        var screenSO = new SerializedObject(screen);
        screenSO.FindProperty("_canvas").objectReferenceValue = entryCanvas;
        screenSO.FindProperty("_nameDisplay").objectReferenceValue = nameTMP;
        screenSO.FindProperty("_gridText").objectReferenceValue = gridTMP;
        screenSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] Name entry screen built (Earthbound-style grid).");
    }

    // ══════════════════════════════════════════════════════════════════
    // Kitchen Wall Calendar
    // ══════════════════════════════════════════════════════════════════

    private static void BuildKitchenCalendar()
    {
        // Small wall-mounted box on the kitchen wall (near the phone/fridge area)
        var calGO = CreateBox("WallCalendar", null,
            new Vector3(3.2f, 1.6f, -2.5f), new Vector3(0.02f, 0.35f, 0.25f),
            new Color(0.95f, 0.92f, 0.85f));
        // Face into the room (east wall)
        calGO.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        calGO.isStatic = false;

        // Needs its own layer for raycast — use Default (0) and the calendar does
        // self-check via transform identity, so we just need the collider present.
        // The BoxCollider is already added by CreateBox (primitive).

        calGO.AddComponent<InteractableHighlight>();
        var cal = calGO.AddComponent<ApartmentCalendar>();
        var calSO = new SerializedObject(cal);
        // Calendar layer — use Default (everything). The calendar checks hit.transform == self.
        calSO.FindProperty("_calendarLayer").intValue = ~0;
        calSO.FindProperty("_maxRayDistance").floatValue = 8f;
        calSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] Kitchen wall calendar placed.");
    }

    // ══════════════════════════════════════════════════════════════════
    // Entrance Area
    // ══════════════════════════════════════════════════════════════════

    private static void BuildEntranceArea(int placeableLayer, int surfacesLayer)
    {
        var parent = new GameObject("EntranceArea");
        parent.transform.position = EntranceAreaPos;

        var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // ── Shoe Rack (PlacementSurface + DropZone) ──
        var shoeRack = CreateBox("ShoeRack", parent.transform,
            ShoeRackPos, new Vector3(0.6f, 0.3f, 0.3f),
            new Color(0.45f, 0.3f, 0.2f));
        shoeRack.isStatic = false;
        shoeRack.layer = surfacesLayer;

        var shoeRackSurf = shoeRack.AddComponent<PlacementSurface>();
        var srSurfSO = new SerializedObject(shoeRackSurf);
        var srAxisProp = srSurfSO.FindProperty("normalAxis");
        if (srAxisProp != null) srAxisProp.enumValueIndex = 0; // Up
        srSurfSO.ApplyModifiedPropertiesWithoutUndo();

        var shoeRackZone = shoeRack.AddComponent<DropZone>();
        var srZoneSO = new SerializedObject(shoeRackZone);
        srZoneSO.FindProperty("_zoneName").stringValue = "ShoeRack";
        srZoneSO.FindProperty("_destroyOnDeposit").boolValue = false;
        srZoneSO.FindProperty("_zoneRenderer").objectReferenceValue = shoeRack.GetComponent<Renderer>();
        srZoneSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Coat Rack (vertical PlacementSurface + DropZone) ──
        var coatRackPole = CreateBox("CoatRack_Pole", parent.transform,
            CoatRackPos, new Vector3(0.08f, 1.5f, 0.08f),
            new Color(0.35f, 0.25f, 0.15f));
        coatRackPole.isStatic = true;

        // Flat surface on top for placing coat/hat
        var coatRackTop = CreateBox("CoatRack_Surface", parent.transform,
            CoatRackPos + new Vector3(0f, 1.5f, 0f),
            new Vector3(0.4f, 0.05f, 0.4f),
            new Color(0.35f, 0.25f, 0.15f));
        coatRackTop.isStatic = false;
        coatRackTop.layer = surfacesLayer;

        var coatRackSurf = coatRackTop.AddComponent<PlacementSurface>();
        var crSurfSO = new SerializedObject(coatRackSurf);
        var crAxisProp = crSurfSO.FindProperty("normalAxis");
        if (crAxisProp != null) crAxisProp.enumValueIndex = 0; // Up
        crSurfSO.ApplyModifiedPropertiesWithoutUndo();

        var coatRackZone = coatRackTop.AddComponent<DropZone>();
        var crZoneSO = new SerializedObject(coatRackZone);
        crZoneSO.FindProperty("_zoneName").stringValue = "CoatRack";
        crZoneSO.FindProperty("_destroyOnDeposit").boolValue = false;
        crZoneSO.FindProperty("_zoneRenderer").objectReferenceValue = coatRackTop.GetComponent<Renderer>();
        crZoneSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Entrance items (shoes, coat, hat) — start at home, DailyMessSpawner misplaces them ──

        // Shoes
        var shoes = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shoes.name = "Shoes";
        shoes.transform.SetParent(parent.transform);
        shoes.transform.position = ShoeRackPos + new Vector3(0f, 0.2f, 0f);
        shoes.transform.localScale = new Vector3(0.2f, 0.1f, 0.12f);
        shoes.layer = placeableLayer;
        shoes.isStatic = false;
        SetMaterial(shoes, new Color(0.2f, 0.15f, 0.1f));

        var shoesRB = shoes.AddComponent<Rigidbody>();
        shoesRB.mass = 0.5f;
        var shoesPO = shoes.AddComponent<PlaceableObject>();
        var shoesPOSO = new SerializedObject(shoesPO);
        shoesPOSO.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.Shoe;
        shoesPOSO.FindProperty("_homeZoneName").stringValue = "ShoeRack";
        shoesPOSO.ApplyModifiedPropertiesWithoutUndo();
        shoes.AddComponent<InteractableHighlight>();
        AddReactableTag(shoes, new[] { "shoes", "mess" }, true);

        // Coat
        var coat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        coat.name = "Coat";
        coat.transform.SetParent(parent.transform);
        coat.transform.position = CoatRackPos + new Vector3(0f, 1.6f, 0f);
        coat.transform.localScale = new Vector3(0.3f, 0.4f, 0.08f);
        coat.layer = placeableLayer;
        coat.isStatic = false;
        SetMaterial(coat, new Color(0.15f, 0.2f, 0.3f));

        var coatRB = coat.AddComponent<Rigidbody>();
        coatRB.mass = 0.4f;
        var coatPO = coat.AddComponent<PlaceableObject>();
        var coatPOSO = new SerializedObject(coatPO);
        coatPOSO.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.Coat;
        coatPOSO.FindProperty("_homeZoneName").stringValue = "CoatRack";
        coatPOSO.ApplyModifiedPropertiesWithoutUndo();
        coat.AddComponent<InteractableHighlight>();
        AddReactableTag(coat, new[] { "coat", "mess" }, true);

        // Hat
        var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hat.name = "Hat";
        hat.transform.SetParent(parent.transform);
        hat.transform.position = CoatRackPos + new Vector3(0.15f, 1.6f, 0f);
        hat.transform.localScale = new Vector3(0.2f, 0.05f, 0.2f);
        hat.layer = placeableLayer;
        hat.isStatic = false;
        SetMaterial(hat, new Color(0.6f, 0.5f, 0.35f));

        // Replace CapsuleCollider with BoxCollider
        var hatCapsule = hat.GetComponent<CapsuleCollider>();
        if (hatCapsule != null) Object.DestroyImmediate(hatCapsule);
        hat.AddComponent<BoxCollider>();

        var hatRB = hat.AddComponent<Rigidbody>();
        hatRB.mass = 0.15f;
        var hatPO = hat.AddComponent<PlaceableObject>();
        var hatPOSO = new SerializedObject(hatPO);
        hatPOSO.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.Hat;
        hatPOSO.FindProperty("_homeZoneName").stringValue = "CoatRack";
        hatPOSO.ApplyModifiedPropertiesWithoutUndo();
        hat.AddComponent<InteractableHighlight>();
        AddReactableTag(hat, new[] { "hat", "mess" }, true);

        // ── Entrance bench / mat for decoration ──
        CreateBox("EntranceMat", parent.transform,
            EntranceAreaPos + new Vector3(0f, 0.01f, 0f),
            new Vector3(1.0f, 0.02f, 0.5f),
            new Color(0.4f, 0.35f, 0.3f));

        Debug.Log("[ApartmentSceneBuilder] Entrance area built (shoe rack, coat rack, shoes, coat, hat).");
    }

    // ══════════════════════════════════════════════════════════════════
    // Trash Can
    // ══════════════════════════════════════════════════════════════════

    private static void BuildTrashCan(int surfacesLayer)
    {
        var parent = new GameObject("TrashCan");
        parent.transform.position = TrashCanPos;

        // Can body
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "TrashCan_Body";
        body.transform.SetParent(parent.transform);
        body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        body.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        body.isStatic = true;
        SetMaterial(body, new Color(0.35f, 0.35f, 0.38f));

        // Top surface (PlacementSurface + DropZone with destroyOnDeposit)
        var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        top.name = "TrashCan_Top";
        top.transform.SetParent(parent.transform);
        top.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        top.transform.localScale = new Vector3(0.28f, 0.02f, 0.28f);
        top.layer = surfacesLayer;
        top.isStatic = false;

        // Transparent green-ish material for zone indicator
        var zoneShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
        var zoneMat = new Material(zoneShader);
        zoneMat.color = new Color(0.3f, 0.8f, 0.4f, 0.35f);
        zoneMat.SetFloat("_Surface", 1f);
        zoneMat.SetFloat("_Blend", 0f);
        zoneMat.renderQueue = 3000;
        var topRend = top.GetComponent<Renderer>();
        if (topRend != null) topRend.sharedMaterial = zoneMat;

        // Replace CapsuleCollider with BoxCollider
        var capsule = top.GetComponent<CapsuleCollider>();
        if (capsule != null) Object.DestroyImmediate(capsule);
        top.AddComponent<BoxCollider>();

        var surface = top.AddComponent<PlacementSurface>();
        var surfSO = new SerializedObject(surface);
        var axisProp = surfSO.FindProperty("normalAxis");
        if (axisProp != null) axisProp.enumValueIndex = 0; // Up
        surfSO.ApplyModifiedPropertiesWithoutUndo();

        var dropZone = top.AddComponent<DropZone>();
        var dzSO = new SerializedObject(dropZone);
        dzSO.FindProperty("_zoneName").stringValue = "TrashCan";
        dzSO.FindProperty("_destroyOnDeposit").boolValue = true;
        dzSO.FindProperty("_zoneRenderer").objectReferenceValue = topRend;
        dzSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ApartmentSceneBuilder] Trash can built in kitchen.");
    }

    // ══════════════════════════════════════════════════════════════════
    // TidyScorer
    // ══════════════════════════════════════════════════════════════════

    private static void BuildTidyScorer()
    {
        var go = new GameObject("TidyScorer");
        go.AddComponent<TidyScorer>();
        Debug.Log("[ApartmentSceneBuilder] TidyScorer built.");
    }

    // ══════════════════════════════════════════════════════════════════
    // DailyMessSpawner
    // ══════════════════════════════════════════════════════════════════

    private static void BuildDailyMessSpawner(int placeableLayer)
    {
        var go = new GameObject("DailyMessSpawner");
        var spawner = go.AddComponent<DailyMessSpawner>();

        // ── Trash spawn slots (disabled by default, spawner activates subset each day) ──
        var trashParent = new GameObject("TrashSlots");
        trashParent.transform.SetParent(go.transform);

        Vector3[] trashPositions =
        {
            new Vector3(-3.0f, 0.5f, -3.0f),   // kitchen floor
            new Vector3(-4.5f, 0.82f, -3.5f),   // kitchen table
            new Vector3(-1.0f, 0.46f, 1.5f),    // near coffee table
            new Vector3(-0.5f, 0.1f, 3.0f),     // living room floor
            new Vector3(0.5f, 0.1f, 2.0f),      // living room floor 2
            new Vector3(-2.0f, 0.1f, -4.0f),    // kitchen floor 2
        };

        var trashSlots = new GameObject[trashPositions.Length];

        for (int i = 0; i < trashPositions.Length; i++)
        {
            var trash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trash.name = $"TrashItem_{i:D2}";
            trash.transform.SetParent(trashParent.transform);
            trash.transform.position = trashPositions[i];
            trash.transform.localScale = new Vector3(0.1f, 0.08f, 0.08f);
            trash.layer = placeableLayer;
            trash.isStatic = false;
            SetMaterial(trash, new Color(0.6f, 0.55f, 0.45f)); // crumpled paper look

            var rb = trash.AddComponent<Rigidbody>();
            rb.mass = 0.05f;

            var trashPO = trash.AddComponent<PlaceableObject>();
            var trashSO = new SerializedObject(trashPO);
            trashSO.FindProperty("_itemCategory").enumValueIndex = (int)ItemCategory.Trash;
            trashSO.FindProperty("_homeZoneName").stringValue = "TrashCan";
            trashSO.ApplyModifiedPropertiesWithoutUndo();

            trash.AddComponent<InteractableHighlight>();
            AddReactableTag(trash, new[] { "trash", "mess" }, true, smellAmount: 0.2f);

            trash.SetActive(false); // spawner activates subset
            trashSlots[i] = trash;
        }

        // ── Wrong-position markers (for misplacing entrance items) ──
        var wrongParent = new GameObject("WrongPositions");
        wrongParent.transform.SetParent(go.transform);

        Vector3[] wrongPositions =
        {
            new Vector3(-3.5f, 0.82f, -3.2f),   // kitchen table
            new Vector3(-0.4f, 0.46f, 2.0f),    // coffee table
            new Vector3(-1.5f, 0.1f, 0.5f),     // hallway floor
            new Vector3(1.0f, 0.1f, 3.0f),      // living room floor
            new Vector3(-5.0f, 0.1f, -4.0f),    // kitchen floor
            new Vector3(-2.0f, 0.1f, 5.0f),     // near entrance
        };

        var wrongTransforms = new Transform[wrongPositions.Length];
        for (int i = 0; i < wrongPositions.Length; i++)
        {
            var marker = new GameObject($"WrongPos_{i:D2}");
            marker.transform.SetParent(wrongParent.transform);
            marker.transform.position = wrongPositions[i];
            wrongTransforms[i] = marker.transform;
        }

        // ── Wire spawner fields ──
        var spawnerSO = new SerializedObject(spawner);

        // Trash slots array
        var trashSlotsProp = spawnerSO.FindProperty("_trashSlots");
        trashSlotsProp.arraySize = trashSlots.Length;
        for (int i = 0; i < trashSlots.Length; i++)
            trashSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = trashSlots[i];

        // Wrong positions array
        var wrongPosProp = spawnerSO.FindProperty("_wrongPositions");
        wrongPosProp.arraySize = wrongTransforms.Length;
        for (int i = 0; i < wrongTransforms.Length; i++)
            wrongPosProp.GetArrayElementAtIndex(i).objectReferenceValue = wrongTransforms[i];

        // Find entrance items (created by BuildEntranceArea)
        var shoesGO = GameObject.Find("Shoes");
        var coatGO = GameObject.Find("Coat");
        var hatGO = GameObject.Find("Hat");

        if (shoesGO != null)
            spawnerSO.FindProperty("_shoes").objectReferenceValue = shoesGO.GetComponent<PlaceableObject>();
        if (coatGO != null)
            spawnerSO.FindProperty("_coat").objectReferenceValue = coatGO.GetComponent<PlaceableObject>();
        if (hatGO != null)
            spawnerSO.FindProperty("_hat").objectReferenceValue = hatGO.GetComponent<PlaceableObject>();

        spawnerSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[ApartmentSceneBuilder] DailyMessSpawner built ({trashSlots.Length} trash slots, {wrongPositions.Length} wrong positions).");
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
        string defaultText, float fontSize, Color bgColor,
        Vector2? anchorMin = null, Vector2? anchorMax = null)
    {
        Vector2 aMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        Vector2 aMax = anchorMax ?? new Vector2(0.5f, 0.5f);

        var panel = new GameObject(name);
        panel.transform.SetParent(parent);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = aMin;
        panelRT.anchorMax = aMax;
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
