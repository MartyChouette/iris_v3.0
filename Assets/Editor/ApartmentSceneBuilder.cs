using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using Unity.Cinemachine;
using UnityEngine.Splines;
using Unity.Mathematics;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the full apartment hub scene with
/// 7 areas, spline dolly camera, station integration, and placement surfaces.
/// Menu: Window > Iris > Build Apartment Scene
/// </summary>
public static class ApartmentSceneBuilder
{
    private const string PlaceableLayerName = "Placeable";
    private const string BooksLayerName = "Books";
    private const string FaceLayerName = "Face";
    private const string StickerPadLayerName = "StickerPad";
    private const string PlantsLayerName = "Plants";
    private const string DrawersLayerName = "Drawers";
    private const string PerfumesLayerName = "Perfumes";
    private const string TrinketsLayerName = "Trinkets";
    private const string CoffeeTableBooksLayerName = "CoffeeTableBooks";

    // ══════════════════════════════════════════════════════════════════
    // Main Build
    // ══════════════════════════════════════════════════════════════════

    [MenuItem("Window/Iris/Build Apartment Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int placeableLayer = EnsureLayer(PlaceableLayerName);
        int booksLayer = EnsureLayer(BooksLayerName);
        int faceLayer = EnsureLayer(FaceLayerName);
        int stickerPadLayer = EnsureLayer(StickerPadLayerName);
        int plantsLayer = EnsureLayer(PlantsLayerName);
        int drawersLayer = EnsureLayer(DrawersLayerName);
        int perfumesLayer = EnsureLayer(PerfumesLayerName);
        int trinketsLayer = EnsureLayer(TrinketsLayerName);
        int coffeeTableBooksLayer = EnsureLayer(CoffeeTableBooksLayerName);

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

        // ── 3. Room geometry ──
        BuildApartment();

        // ── 4. Furniture for all 7 areas ──
        var furnitureRefs = BuildFurniture();

        // ── 5. Placeable objects ──
        BuildPlaceableObjects(placeableLayer);

        // ── 6. Spline path + browse camera with dolly ──
        var splineContainer = BuildSplinePath();
        var cameras = BuildCamerasWithDolly(splineContainer);

        // ── 7. Area SOs (7 areas) ──
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

        // Wire coffee table book targets to apartment furniture
        var coffeeBooks = bookcaseRoot.GetComponentsInChildren<CoffeeTableBook>();
        for (int i = 0; i < coffeeBooks.Length; i++)
        {
            var cbSO = new SerializedObject(coffeeBooks[i]);
            cbSO.FindProperty("coffeeTablePosition").vector3Value =
                new Vector3(-5.0f + i * 0.25f, 0.45f, 3.0f);
            cbSO.FindProperty("coffeeTableRotation").quaternionValue =
                Quaternion.Euler(0f, 15f * (i + 1), 0f);
            cbSO.ApplyModifiedPropertiesWithoutUndo();
        }

        var moodController = BuildBookNookEnvironmentMood(lightGO);
        var itemInspector = BuildBookNookItemInspector(camGO);

        // ── 10. BookInteractionManager ──
        var bookManager = BuildBookInteractionManager(camGO, booksLayer,
            drawersLayer, perfumesLayer, trinketsLayer, coffeeTableBooksLayer,
            itemInspector, moodController);

        // ── 11. Record player station ──
        var recordPlayerData = BuildRecordPlayerStation();

        // ── 11b. Mirror Makeup station ──
        var mirrorMakeupData = BuildMirrorMakeupStation(camGO, faceLayer, stickerPadLayer);

        // ── 11c. Ambient watering (not a station) ──
        BuildAmbientWatering(camGO, plantsLayer);

        // ── 12. Station roots ──
        BuildStationRoots(bookManager, recordPlayerData, mirrorMakeupData);

        // ── 13. ApartmentManager + UI ──
        BuildApartmentManager(cameras.browse, cameras.selected, cameras.dolly,
            grabber, areaDefs);

        // ── 14. Save scene ──
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/apartment.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[ApartmentSceneBuilder] Scene saved to {path}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Apartment geometry (expanded: 14x12 units, 7 areas)
    // ══════════════════════════════════════════════════════════════════

    private static void BuildApartment()
    {
        var parent = new GameObject("Apartment");

        float wallH = 3f;
        float wallT = 0.15f;

        // Floor (14 x 12)
        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(14f, 0.1f, 12f),
            new Color(0.55f, 0.45f, 0.35f));

        // Ceiling
        CreateBox("Ceiling", parent.transform,
            new Vector3(0f, wallH + 0.05f, 0f), new Vector3(14f, 0.1f, 12f),
            new Color(0.90f, 0.88f, 0.85f));

        // ── Outer walls ──
        CreateBox("Wall_South", parent.transform,
            new Vector3(0f, wallH / 2f, -6f), new Vector3(14f + wallT, wallH, wallT),
            new Color(0.82f, 0.78f, 0.72f));
        CreateBox("Wall_North", parent.transform,
            new Vector3(0f, wallH / 2f, 6f), new Vector3(14f + wallT, wallH, wallT),
            new Color(0.82f, 0.78f, 0.72f));
        CreateBox("Wall_West", parent.transform,
            new Vector3(-7f, wallH / 2f, 0f), new Vector3(wallT, wallH, 12f + wallT),
            new Color(0.82f, 0.78f, 0.72f));
        CreateBox("Wall_East", parent.transform,
            new Vector3(7f, wallH / 2f, 0f), new Vector3(wallT, wallH, 12f + wallT),
            new Color(0.82f, 0.78f, 0.72f));

        // ── Interior dividers (with doorway gaps) ──

        // Entrance to Kitchen wall (Z = -2, X = -7 to -1, gap at -1 to 1)
        CreateBox("Divider_Entrance_Left", parent.transform,
            new Vector3(-4f, wallH / 2f, -2f), new Vector3(6f, wallH, wallT),
            new Color(0.78f, 0.75f, 0.70f));

        // Kitchen to Living Room partial wall (Z = 2, X = -7 to -2, gap at -2 to 0)
        CreateBox("Divider_Kitchen_LR", parent.transform,
            new Vector3(-4.5f, wallH / 2f, 2f), new Vector3(5f, wallH, wallT),
            new Color(0.78f, 0.75f, 0.70f));

        // Bathroom wall (X = 4, Z = -6 to -2, gap -2 to 0)
        CreateBox("Divider_Bath", parent.transform,
            new Vector3(4f, wallH / 2f, -4f), new Vector3(wallT, wallH, 4f),
            new Color(0.78f, 0.75f, 0.70f));

        // Cozy Corner partial wall (X = 3, Z = -2 to 2, half height for visual interest)
        CreateBox("Divider_Cozy_Half", parent.transform,
            new Vector3(3f, wallH / 4f, 0f), new Vector3(wallT, wallH / 2f, 4f),
            new Color(0.78f, 0.75f, 0.70f));

        // Flower Room / Watering Nook divider (X = 0, Z = 2 to 6, gap at Z = 3 to 4.5)
        CreateBox("Divider_Flower_Water_Top", parent.transform,
            new Vector3(0f, wallH / 2f, 5.25f), new Vector3(wallT, wallH, 1.5f),
            new Color(0.78f, 0.75f, 0.70f));
        CreateBox("Divider_Flower_Water_Bot", parent.transform,
            new Vector3(0f, wallH / 2f, 2.5f), new Vector3(wallT, wallH, 1f),
            new Color(0.78f, 0.75f, 0.70f));
    }

    // ══════════════════════════════════════════════════════════════════
    // Furniture (returns references needed for wiring)
    // ══════════════════════════════════════════════════════════════════

    private struct FurnitureRefs
    {
        public GameObject coffeeTable;
        public GameObject kitchenTable;
        public GameObject counter;
        public GameObject sideTable;
        public GameObject flowerTable;
        public GameObject wateringShelf;
    }

    private static FurnitureRefs BuildFurniture()
    {
        var parent = new GameObject("Furniture");
        var refs = new FurnitureRefs();

        // ═══ Entrance (center-south: X ~ 0, Z ~ -4.5) ═══
        BuildEntrance(parent.transform);

        // ═══ Kitchen (west-south: X ~ -4, Z ~ -4) ═══
        refs.kitchenTable = BuildKitchen(parent.transform);

        // ═══ Living Room (west: X ~ -4, Z ~ 3) ═══
        refs.coffeeTable = BuildLivingRoom(parent.transform);

        // ═══ Watering Nook (center-north: X ~ -2, Z ~ 4.5) ═══
        refs.wateringShelf = BuildWateringNook(parent.transform);

        // ═══ Flower Room (east-north: X ~ 4, Z ~ 4.5) ═══
        refs.flowerTable = BuildFlowerRoom(parent.transform);

        // ═══ Cozy Corner (east: X ~ 5, Z ~ 0) ═══
        refs.sideTable = BuildCozyCorner(parent.transform);

        // ═══ Bathroom (east-south: X ~ 5, Z ~ -4) ═══
        BuildBathroom(parent.transform);

        return refs;
    }

    private static void BuildEntrance(Transform parent)
    {
        // Coat rack
        CreateBox("CoatRack_Pole", parent,
            new Vector3(0f, 0.9f, -5.3f), new Vector3(0.08f, 1.8f, 0.08f),
            new Color(0.35f, 0.25f, 0.18f));
        CreateBox("CoatRack_Arms", parent,
            new Vector3(0f, 1.75f, -5.3f), new Vector3(0.5f, 0.04f, 0.04f),
            new Color(0.35f, 0.25f, 0.18f));

        // Welcome mat
        CreateBox("WelcomeMat", parent,
            new Vector3(0f, 0.01f, -5.0f), new Vector3(0.8f, 0.02f, 0.5f),
            new Color(0.55f, 0.40f, 0.25f));

        // Key hook (on wall)
        CreateBox("KeyHook", parent,
            new Vector3(-0.8f, 1.3f, -5.85f), new Vector3(0.2f, 0.15f, 0.05f),
            new Color(0.30f, 0.30f, 0.32f));

        // Shoes
        CreateBox("Shoes_L", parent,
            new Vector3(-0.3f, 0.04f, -5.3f), new Vector3(0.12f, 0.08f, 0.25f),
            new Color(0.25f, 0.20f, 0.18f));
        CreateBox("Shoes_R", parent,
            new Vector3(-0.1f, 0.04f, -5.3f), new Vector3(0.12f, 0.08f, 0.25f),
            new Color(0.25f, 0.20f, 0.18f));
    }

    private static GameObject BuildKitchen(Transform parent)
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

        return tableTop;
    }

    private static GameObject BuildLivingRoom(Transform parent)
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

        return tableTop;
    }

    private static GameObject BuildWateringNook(Transform parent)
    {
        // Plant shelf
        var shelf = CreateBox("PlantShelf", parent,
            new Vector3(-2f, 0.8f, 5f), new Vector3(2.0f, 0.06f, 0.6f),
            new Color(0.50f, 0.35f, 0.22f));
        // Shelf legs
        CreateBox("PlantShelf_Leg1", parent,
            new Vector3(-2.9f, 0.39f, 4.7f), new Vector3(0.06f, 0.78f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("PlantShelf_Leg2", parent,
            new Vector3(-1.1f, 0.39f, 4.7f), new Vector3(0.06f, 0.78f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("PlantShelf_Leg3", parent,
            new Vector3(-2.9f, 0.39f, 5.3f), new Vector3(0.06f, 0.78f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("PlantShelf_Leg4", parent,
            new Vector3(-1.1f, 0.39f, 5.3f), new Vector3(0.06f, 0.78f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));

        // Decorative pots on shelf
        for (int i = 0; i < 3; i++)
        {
            float x = -2.6f + i * 0.6f;
            CreateBox($"WateringPot_{i}", parent,
                new Vector3(x, 0.92f, 5f), new Vector3(0.15f, 0.18f, 0.15f),
                new Color(0.6f, 0.35f + i * 0.05f, 0.25f));
            CreateBox($"WateringPlant_{i}", parent,
                new Vector3(x, 1.08f, 5f), new Vector3(0.12f, 0.12f, 0.12f),
                new Color(0.15f + i * 0.05f, 0.45f, 0.15f));
        }

        // Watering can
        CreateBox("WateringCan", parent,
            new Vector3(-1.2f, 0.88f, 4.7f), new Vector3(0.2f, 0.15f, 0.12f),
            new Color(0.3f, 0.5f, 0.55f));

        return shelf;
    }

    private static GameObject BuildFlowerRoom(Transform parent)
    {
        // Trimming table
        var tableTop = CreateBox("FlowerTable_Top", parent,
            new Vector3(4f, 0.72f, 4.5f), new Vector3(1.5f, 0.05f, 0.8f),
            new Color(0.50f, 0.35f, 0.22f));
        CreateBox("FlowerTable_Leg1", parent,
            new Vector3(3.3f, 0.35f, 4.1f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("FlowerTable_Leg2", parent,
            new Vector3(4.7f, 0.35f, 4.1f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("FlowerTable_Leg3", parent,
            new Vector3(3.3f, 0.35f, 4.9f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("FlowerTable_Leg4", parent,
            new Vector3(4.7f, 0.35f, 4.9f), new Vector3(0.06f, 0.7f, 0.06f),
            new Color(0.40f, 0.28f, 0.18f));

        // Flower pot placeholder
        CreateBox("FlowerPot", parent,
            new Vector3(4f, 0.88f, 4.5f), new Vector3(0.2f, 0.25f, 0.2f),
            new Color(0.6f, 0.35f, 0.25f));

        // Scissors stand
        CreateBox("ScissorsStand", parent,
            new Vector3(4.5f, 0.82f, 4.5f), new Vector3(0.08f, 0.15f, 0.08f),
            new Color(0.30f, 0.30f, 0.32f));

        return tableTop;
    }

    private static GameObject BuildCozyCorner(Transform parent)
    {
        // Comfy chair
        CreateBox("ComfyChair_Seat", parent,
            new Vector3(5f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.8f),
            new Color(0.55f, 0.35f, 0.28f));
        CreateBox("ComfyChair_Back", parent,
            new Vector3(5f, 0.75f, 0.35f), new Vector3(0.8f, 0.5f, 0.15f),
            new Color(0.55f, 0.35f, 0.28f));

        // Side table
        var sideTop = CreateBox("CozySideTable_Top", parent,
            new Vector3(5.8f, 0.45f, 0f), new Vector3(0.5f, 0.05f, 0.5f),
            new Color(0.50f, 0.35f, 0.22f));
        CreateBox("CozySideTable_Leg1", parent,
            new Vector3(5.6f, 0.21f, -0.2f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CozySideTable_Leg2", parent,
            new Vector3(6.0f, 0.21f, -0.2f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CozySideTable_Leg3", parent,
            new Vector3(5.6f, 0.21f, 0.2f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));
        CreateBox("CozySideTable_Leg4", parent,
            new Vector3(6.0f, 0.21f, 0.2f), new Vector3(0.05f, 0.42f, 0.05f),
            new Color(0.40f, 0.28f, 0.18f));

        // Record player visual (on side table)
        CreateBox("RecordPlayer_Base", parent,
            new Vector3(5.8f, 0.52f, 0f), new Vector3(0.35f, 0.06f, 0.35f),
            new Color(0.25f, 0.22f, 0.20f));
        CreateBox("RecordPlayer_Platter", parent,
            new Vector3(5.8f, 0.56f, 0f), new Vector3(0.28f, 0.02f, 0.28f),
            new Color(0.15f, 0.15f, 0.15f));

        // Record stack next to chair
        for (int i = 0; i < 4; i++)
        {
            CreateBox($"RecordStack_{i}", parent,
                new Vector3(4.2f, 0.01f + i * 0.01f, -0.3f),
                new Vector3(0.3f, 0.01f, 0.3f),
                new Color(0.12f + i * 0.02f, 0.12f, 0.12f));
        }

        // Lamp
        CreateBox("CozyLamp_Pole", parent,
            new Vector3(6.3f, 0.6f, 0.8f), new Vector3(0.05f, 1.2f, 0.05f),
            new Color(0.20f, 0.20f, 0.22f));
        CreateBox("CozyLamp_Shade", parent,
            new Vector3(6.3f, 1.3f, 0.8f), new Vector3(0.3f, 0.22f, 0.3f),
            new Color(0.88f, 0.78f, 0.55f));

        return sideTop;
    }

    private static void BuildBathroom(Transform parent)
    {
        // Sink counter
        CreateBox("Sink_Counter_Base", parent,
            new Vector3(5.5f, 0.4f, -4f), new Vector3(0.7f, 0.8f, 0.5f),
            new Color(0.50f, 0.40f, 0.30f));
        CreateBox("Sink_Counter_Top", parent,
            new Vector3(5.5f, 0.85f, -4f), new Vector3(0.7f, 0.08f, 0.5f),
            new Color(0.75f, 0.73f, 0.70f));
        CreateBox("Sink_Basin", parent,
            new Vector3(5.5f, 0.90f, -4f), new Vector3(0.35f, 0.05f, 0.3f),
            new Color(0.70f, 0.80f, 0.88f));

        // Mirror
        CreateBox("Mirror", parent,
            new Vector3(6.85f, 1.6f, -4f), new Vector3(0.05f, 0.6f, 0.5f),
            new Color(0.80f, 0.85f, 0.90f));

        // Toilet
        CreateBox("Toilet_Bowl", parent,
            new Vector3(5.5f, 0.3f, -5f), new Vector3(0.4f, 0.6f, 0.5f),
            new Color(0.90f, 0.90f, 0.90f));
        CreateBox("Toilet_Tank", parent,
            new Vector3(5.5f, 0.55f, -5.2f), new Vector3(0.35f, 0.3f, 0.15f),
            new Color(0.88f, 0.88f, 0.88f));
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

        // Box on cozy side table
        CreatePlaceable("Box", parent.transform,
            new Vector3(5.8f, 0.55f, 0.1f), new Vector3(0.15f, 0.1f, 0.12f),
            new Color(0.72f, 0.55f, 0.35f), placeableLayer);
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
        var surfaces = new PlacementSurface[4];

        // Coffee table surface
        surfaces[0] = AddSurface(refs.coffeeTable, new Bounds(
            Vector3.zero, new Vector3(1.0f, 0.1f, 0.6f)));

        // Kitchen table surface
        surfaces[1] = AddSurface(refs.kitchenTable, new Bounds(
            Vector3.zero, new Vector3(1.2f, 0.1f, 0.8f)));

        // Cozy side table surface
        surfaces[2] = AddSurface(refs.sideTable, new Bounds(
            Vector3.zero, new Vector3(0.5f, 0.1f, 0.5f)));

        // Flower table surface
        surfaces[3] = AddSurface(refs.flowerTable, new Bounds(
            Vector3.zero, new Vector3(1.5f, 0.1f, 0.8f)));

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
    // Spline Path (closed loop, 7 knots)
    // ══════════════════════════════════════════════════════════════════

    private static SplineContainer BuildSplinePath()
    {
        var splineGO = new GameObject("ApartmentSplinePath");
        var container = splineGO.AddComponent<SplineContainer>();
        var spline = container.Spline;
        spline.Clear();

        // 7 knots in clockwise order — positions are world space
        // (container is at origin, so local = world)
        var knots = new float3[]
        {
            new float3( 0.0f, 2.0f, -6.0f),  // 0: Entrance
            new float3(-4.0f, 2.5f, -2.5f),  // 1: Kitchen
            new float3(-4.0f, 3.0f,  3.0f),  // 2: Living Room
            new float3(-1.0f, 2.5f,  5.5f),  // 3: Watering Nook
            new float3( 4.0f, 2.5f,  4.5f),  // 4: Flower Room
            new float3( 5.0f, 2.5f,  0.0f),  // 5: Cozy Corner
            new float3( 5.0f, 2.5f, -3.5f),  // 6: Bathroom
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
        browseLens.FieldOfView = 60f;
        browseLens.NearClipPlane = 0.1f;
        browseLens.FarClipPlane = 500f;
        browse.Lens = browseLens;
        browse.Priority = 20;

        // Add spline dolly component
        var dolly = browseGO.AddComponent<CinemachineSplineDolly>();
        dolly.Spline = spline;
        dolly.CameraPosition = 0f;
        dolly.CameraRotation = CinemachineSplineDolly.RotationMode.Default;

        // Selected camera (starts inactive)
        var selectedGO = new GameObject("Cam_Selected");
        selectedGO.transform.SetParent(parent.transform);
        selectedGO.transform.position = new Vector3(0f, 2f, -5f);
        var selected = selectedGO.AddComponent<CinemachineCamera>();
        var selectedLens = LensSettings.Default;
        selectedLens.FieldOfView = 50f;
        selectedLens.NearClipPlane = 0.1f;
        selectedLens.FarClipPlane = 500f;
        selected.Lens = selectedLens;
        selected.Priority = 0;

        return new CameraRefs { browse = browse, selected = selected, dolly = dolly };
    }

    // ══════════════════════════════════════════════════════════════════
    // Area ScriptableObjects (7 areas)
    // ══════════════════════════════════════════════════════════════════

    private static ApartmentAreaDefinition[] BuildAreaDefinitions()
    {
        string soDir = "Assets/ScriptableObjects/Apartment";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Apartment");

        var entrance = CreateAreaDef("Entrance", soDir,
            "Entrance", "Coat rack, keys, and welcome mat.",
            StationType.None, 0.000f,
            new Vector3(0f, 2f, -4.5f), new Vector3(40f, 0f, 0f), 55f);

        var kitchen = CreateAreaDef("Kitchen", soDir,
            "Kitchen", "Table with newspaper, fridge, counter.",
            StationType.NewspaperDating, 0.143f,
            new Vector3(-4f, 2f, -2.5f), new Vector3(45f, 0f, 0f), 50f);

        var livingRoom = CreateAreaDef("LivingRoom", soDir,
            "Living Room", "Bookcase, coffee table, couch.",
            StationType.Bookcase, 0.286f,
            new Vector3(-4.5f, 2.5f, 4f), new Vector3(35f, 180f, 0f), 50f);

        var wateringNook = CreateAreaDef("WateringNook", soDir,
            "Watering Nook", "Plant shelf with watering can.",
            StationType.None, 0.429f,
            new Vector3(-2f, 1.8f, 5.5f), new Vector3(30f, 0f, 0f), 48f);

        var flowerRoom = CreateAreaDef("FlowerRoom", soDir,
            "Flower Room", "Trimming table with scissors and flower pot.",
            StationType.FlowerTrimming, 0.571f,
            new Vector3(4f, 2f, 5f), new Vector3(35f, 180f, 0f), 50f);

        var cozyCorner = CreateAreaDef("CozyCorner", soDir,
            "Cozy Corner", "Record player, comfy chair, stack of records.",
            StationType.RecordPlayer, 0.714f,
            new Vector3(5f, 2f, 0.5f), new Vector3(35f, 180f, 0f), 48f);

        var bathroom = CreateAreaDef("Bathroom", soDir,
            "Bathroom", "Mirror, sink, and makeup station.",
            StationType.MirrorMakeup, 0.857f,
            new Vector3(5.2f, 2f, -3.5f), new Vector3(40f, 180f, 0f), 48f);

        return new[] { entrance, kitchen, livingRoom, wateringNook, flowerRoom, cozyCorner, bathroom };
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

    // ══════════════════════════════════════════════════════════════════
    // Record Player Station
    // ══════════════════════════════════════════════════════════════════

    private struct RecordPlayerData
    {
        public RecordPlayerManager manager;
        public RecordPlayerHUD hud;
        public GameObject hudRoot;
    }

    private static RecordPlayerData BuildRecordPlayerStation()
    {
        string soDir = "Assets/ScriptableObjects/RecordPlayer";
        if (!AssetDatabase.IsValidFolder(soDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "RecordPlayer");
        }

        // Create record definition SOs
        var records = new RecordDefinition[]
        {
            CreateRecordDef(soDir, "Petal Rain", "The Pollens", new Color(0.8f, 0.2f, 0.2f)),
            CreateRecordDef(soDir, "Root Beer Blues", "Decomposers", new Color(0.2f, 0.4f, 0.7f)),
            CreateRecordDef(soDir, "Stem & Leaf", "Chlorophyll", new Color(0.2f, 0.6f, 0.3f)),
            CreateRecordDef(soDir, "Wilt Season", "The Gardeners", new Color(0.7f, 0.5f, 0.2f)),
            CreateRecordDef(soDir, "Night Bloom", "Moonflower", new Color(0.5f, 0.2f, 0.6f)),
        };

        // Manager GO
        var managerGO = new GameObject("RecordPlayerManager");
        var mgr = managerGO.AddComponent<RecordPlayerManager>();

        // Record visual (platter on the table)
        var recordVisual = GameObject.Find("RecordPlayer_Platter");

        // HUD canvas
        var hudCanvasGO = new GameObject("RecordPlayerHUD_Canvas");
        hudCanvasGO.transform.SetParent(managerGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        hudCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        hudCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // HUD panels
        var titleText = CreateHUDText("RecordTitle", hudCanvasGO.transform,
            new Vector2(0f, 180f), 24f, "Petal Rain");
        var artistText = CreateHUDText("RecordArtist", hudCanvasGO.transform,
            new Vector2(0f, 150f), 18f, "The Pollens");
        var stateText = CreateHUDText("RecordState", hudCanvasGO.transform,
            new Vector2(0f, 120f), 16f, "Stopped");
        var hintsText = CreateHUDText("RecordHints", hudCanvasGO.transform,
            new Vector2(0f, -200f), 16f, "A / D  Browse    |    Enter  Play");

        // HUD component
        var hud = managerGO.AddComponent<RecordPlayerHUD>();
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("titleText").objectReferenceValue = titleText;
        hudSO.FindProperty("artistText").objectReferenceValue = artistText;
        hudSO.FindProperty("stateText").objectReferenceValue = stateText;
        hudSO.FindProperty("hintsText").objectReferenceValue = hintsText;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire manager
        var mgrSO = new SerializedObject(mgr);
        var recordsProp = mgrSO.FindProperty("records");
        recordsProp.arraySize = records.Length;
        for (int i = 0; i < records.Length; i++)
            recordsProp.GetArrayElementAtIndex(i).objectReferenceValue = records[i];

        if (recordVisual != null)
        {
            mgrSO.FindProperty("recordVisual").objectReferenceValue = recordVisual.transform;
            mgrSO.FindProperty("recordRenderer").objectReferenceValue =
                recordVisual.GetComponent<Renderer>();
        }

        mgrSO.FindProperty("hud").objectReferenceValue = hud;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // Start disabled (StationRoot will enable)
        mgr.enabled = false;
        hudCanvasGO.SetActive(false);

        return new RecordPlayerData { manager = mgr, hud = hud, hudRoot = hudCanvasGO };
    }

    private static RecordDefinition CreateRecordDef(string dir, string title, string artist, Color labelColor)
    {
        var def = ScriptableObject.CreateInstance<RecordDefinition>();
        def.title = title;
        def.artist = artist;
        def.labelColor = labelColor;
        def.volume = 0.7f;

        string path = $"{dir}/Record_{title.Replace(" ", "_")}.asset";
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
        RecordPlayerData recordData,
        MirrorMakeupData mirrorData)
    {
        var parent = new GameObject("StationRoots");

        // Bookcase station (Living Room)
        CreateStationRoot(parent.transform, "Station_Bookcase",
            StationType.Bookcase, bookManager);

        // Record Player station (Cozy Corner)
        CreateStationRoot(parent.transform, "Station_RecordPlayer",
            StationType.RecordPlayer, recordData.manager,
            recordData.hudRoot);

        // Mirror Makeup station (Bathroom) — fully wired
        var mirrorRoot = CreateStationRoot(parent.transform, "Station_MirrorMakeup",
            StationType.MirrorMakeup, mirrorData.manager,
            mirrorData.hudRoot);
        // Wire station camera
        var mirrorRootSO = new SerializedObject(mirrorRoot);
        var mirrorCamsProp = mirrorRootSO.FindProperty("stationCameras");
        mirrorCamsProp.arraySize = 1;
        mirrorCamsProp.GetArrayElementAtIndex(0).objectReferenceValue = mirrorData.stationCamera;
        mirrorRootSO.ApplyModifiedPropertiesWithoutUndo();

        // Placeholder stations for minigames that need their own scenes
        CreateStationRoot(parent.transform, "Station_NewspaperDating",
            StationType.NewspaperDating, null);
        CreateStationRoot(parent.transform, "Station_FlowerTrimming",
            StationType.FlowerTrimming, null);

        // Note: Watering is now ambient (not a station) — no StationRoot needed
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
    // Mirror Makeup Station
    // ══════════════════════════════════════════════════════════════════

    private struct MirrorMakeupData
    {
        public MirrorMakeupManager manager;
        public MirrorMakeupHUD hud;
        public GameObject hudRoot;
        public Unity.Cinemachine.CinemachineCamera stationCamera;
    }

    private static MirrorMakeupData BuildMirrorMakeupStation(
        GameObject camGO, int faceLayer, int stickerPadLayer)
    {
        // ── SO folder ────────────────────────────────────────────────
        string soDir = "Assets/ScriptableObjects/MirrorMakeup";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "MirrorMakeup");

        // ── Tool SOs ─────────────────────────────────────────────────
        var foundation = CreateMakeupToolSO(soDir, "Foundation",
            MakeupToolDefinition.ToolType.Foundation,
            new Color(0.88f, 0.74f, 0.62f), 0.035f, 0.7f, true,
            false, 0f, 0f, 0f, 0f, Color.yellow);

        var lipstick = CreateMakeupToolSO(soDir, "Lipstick",
            MakeupToolDefinition.ToolType.Lipstick,
            new Color(0.85f, 0.15f, 0.2f), 0.02f, 0.9f, false,
            true, 0.002f, 2.5f, 0.4f, 0f, Color.yellow);

        var eyeliner = CreateMakeupToolSO(soDir, "Eyeliner",
            MakeupToolDefinition.ToolType.Eyeliner,
            new Color(0.08f, 0.06f, 0.06f), 0.008f, 1f, false,
            false, 0f, 0f, 0f, 0f, Color.yellow);

        var starSticker = CreateMakeupToolSO(soDir, "Star Sticker",
            MakeupToolDefinition.ToolType.StarSticker,
            Color.yellow, 0.03f, 1f, false,
            false, 0f, 0f, 0f, 0.03f, Color.yellow);

        var allTools = new[] { foundation, lipstick, eyeliner, starSticker };

        // ── Head parent + face spheres ───────────────────────────────
        // Bathroom mirror is at (~6.85, 1.6, -4) — head sits in front
        var headParent = new GameObject("HeadParent");
        headParent.transform.position = new Vector3(5.5f, 1.6f, -4.0f);
        headParent.transform.rotation = Quaternion.Euler(0f, 115f, 0f);

        // Base face sphere
        var faceBase = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        faceBase.name = "FaceBase";
        faceBase.transform.SetParent(headParent.transform);
        faceBase.transform.localPosition = Vector3.zero;
        faceBase.transform.localScale = new Vector3(0.55f, 0.7f, 0.55f);
        faceBase.transform.localRotation = Quaternion.identity;
        faceBase.layer = faceLayer;
        faceBase.isStatic = false;

        // Overlay sphere (slightly larger for Z-fighting avoidance)
        var faceOverlay = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        faceOverlay.name = "FaceOverlay";
        faceOverlay.transform.SetParent(headParent.transform);
        faceOverlay.transform.localPosition = Vector3.zero;
        faceOverlay.transform.localScale = new Vector3(0.56f, 0.71f, 0.56f);
        faceOverlay.transform.localRotation = Quaternion.identity;
        faceOverlay.layer = faceLayer;
        faceOverlay.isStatic = false;

        // Replace default collider with MeshCollider for UV raycasting
        var overlayCol = faceOverlay.GetComponent<Collider>();
        if (overlayCol != null) Object.DestroyImmediate(overlayCol);
        faceOverlay.AddComponent<MeshCollider>();

        Renderer baseRenderer = faceBase.GetComponent<Renderer>();
        Renderer overlayRenderer = faceOverlay.GetComponent<Renderer>();

        // FaceCanvas component
        var faceCanvas = headParent.AddComponent<FaceCanvas>();
        var faceCanvasSO = new SerializedObject(faceCanvas);
        faceCanvasSO.FindProperty("_baseRenderer").objectReferenceValue = baseRenderer;
        faceCanvasSO.FindProperty("_overlayRenderer").objectReferenceValue = overlayRenderer;
        faceCanvasSO.FindProperty("_useExternalBase").boolValue = false;
        faceCanvasSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Sticker pad ──────────────────────────────────────────────
        // On the bathroom shelf (~5.5, 1.0, -4.3)
        var stickerPadGO = CreateBox("StickerPad", null,
            new Vector3(5.2f, 0.98f, -4.3f), new Vector3(0.1f, 0.02f, 0.1f),
            new Color(1f, 0.96f, 0.7f));
        stickerPadGO.layer = stickerPadLayer;
        stickerPadGO.isStatic = false;

        // ── Cursor sticker (visual only, starts inactive) ────────────
        var cursorStickerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cursorStickerGO.name = "CursorSticker";
        cursorStickerGO.transform.localScale = new Vector3(0.04f, 0.04f, 0.005f);
        cursorStickerGO.isStatic = false;
        var cursorCol = cursorStickerGO.GetComponent<Collider>();
        if (cursorCol != null) Object.DestroyImmediate(cursorCol);
        var cursorRend = cursorStickerGO.GetComponent<Renderer>();
        if (cursorRend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.9f, 0.2f);
            cursorRend.sharedMaterial = mat;
        }
        cursorStickerGO.SetActive(false);

        // ── Managers GO ──────────────────────────────────────────────
        var managersGO = new GameObject("MirrorMakeupManagers");
        var headCtrl = managersGO.AddComponent<HeadController>();
        var mgr = managersGO.AddComponent<MirrorMakeupManager>();
        var hud = managersGO.AddComponent<MirrorMakeupHUD>();

        var cam = camGO.GetComponent<UnityEngine.Camera>();

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
        mgrSO.FindProperty("_faceLayer").intValue = 1 << faceLayer;
        mgrSO.FindProperty("_stickerPadLayer").intValue = 1 << stickerPadLayer;
        mgrSO.FindProperty("_cursorSticker").objectReferenceValue = cursorStickerGO.transform;

        var toolsProp = mgrSO.FindProperty("_tools");
        toolsProp.arraySize = allTools.Length;
        for (int i = 0; i < allTools.Length; i++)
            toolsProp.GetArrayElementAtIndex(i).objectReferenceValue = allTools[i];
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── HUD Canvas (sorting 12) ─────────────────────────────────
        var hudCanvasGO = new GameObject("MirrorMakeupHUD_Canvas");
        hudCanvasGO.transform.SetParent(managersGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 12;
        hudCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        hudCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Tool name (top-center)
        var toolNameLabel = CreateHUDText("ToolNameLabel", hudCanvasGO.transform,
            new Vector2(0f, 200f), 24f, "Inspect Mode");

        // Instruction hint (bottom-center)
        var instructionLabel = CreateHUDText("InstructionLabel", hudCanvasGO.transform,
            new Vector2(0f, -200f), 16f, "Move mouse to look around");

        // Pimple counter (top-right)
        var pimpleCountGO = new GameObject("PimpleCountLabel");
        pimpleCountGO.transform.SetParent(hudCanvasGO.transform);
        var pimpleRT = pimpleCountGO.AddComponent<RectTransform>();
        pimpleRT.anchorMin = new Vector2(1f, 1f);
        pimpleRT.anchorMax = new Vector2(1f, 1f);
        pimpleRT.pivot = new Vector2(1f, 1f);
        pimpleRT.sizeDelta = new Vector2(250f, 30f);
        pimpleRT.anchoredPosition = new Vector2(-20f, -30f);
        pimpleRT.localScale = Vector3.one;
        var pimpleTMP = pimpleCountGO.AddComponent<TextMeshProUGUI>();
        pimpleTMP.text = "Pimples: 0/12 covered";
        pimpleTMP.fontSize = 18f;
        pimpleTMP.alignment = TextAlignmentOptions.Right;
        pimpleTMP.color = Color.white;

        // ── Tool button panel (left side) ────────────────────────────
        var toolPanelGO = new GameObject("ToolButtonPanel");
        toolPanelGO.transform.SetParent(hudCanvasGO.transform);
        var toolPanelRT = toolPanelGO.AddComponent<RectTransform>();
        toolPanelRT.anchorMin = new Vector2(0f, 0.5f);
        toolPanelRT.anchorMax = new Vector2(0f, 0.5f);
        toolPanelRT.pivot = new Vector2(0f, 0.5f);
        toolPanelRT.anchoredPosition = new Vector2(20f, 0f);
        toolPanelRT.sizeDelta = new Vector2(140f, 300f);
        toolPanelRT.localScale = Vector3.one;

        string[] toolNames = { "Foundation", "Lipstick", "Eyeliner", "Sticker" };
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
            toolButtonsList[i] = BuildMakeupToolButton(toolPanelGO.transform, toolNames[i],
                i, mgr, new Vector2(0f, yOffset), toolBtnColors[i]);
        }

        // Inspect button (deselect)
        float inspectY = 80f - allTools.Length * 50f;
        var inspectBtn = BuildMakeupToolButton(toolPanelGO.transform, "Inspect",
            -1, mgr, new Vector2(0f, inspectY), new Color(0.4f, 0.4f, 0.5f));

        // ── Wire HUD ─────────────────────────────────────────────────
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("toolNameLabel").objectReferenceValue = toolNameLabel;
        hudSO.FindProperty("instructionLabel").objectReferenceValue = instructionLabel;
        hudSO.FindProperty("pimpleCountLabel").objectReferenceValue = pimpleTMP;
        hudSO.FindProperty("toolButtonPanel").objectReferenceValue = toolPanelGO;
        hudSO.FindProperty("inspectButton").objectReferenceValue = inspectBtn;

        var toolBtnsProp = hudSO.FindProperty("toolButtons");
        toolBtnsProp.arraySize = toolButtonsList.Length;
        for (int i = 0; i < toolButtonsList.Length; i++)
            toolBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = toolButtonsList[i];
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── CinemachineCamera facing mirror area ─────────────────────
        var mirrorCamGO = new GameObject("Cam_MirrorMakeup");
        mirrorCamGO.transform.position = new Vector3(5.0f, 1.7f, -4.6f);
        mirrorCamGO.transform.rotation = Quaternion.Euler(5f, 25f, 0f);
        var mirrorCam = mirrorCamGO.AddComponent<Unity.Cinemachine.CinemachineCamera>();
        var mirrorLens = LensSettings.Default;
        mirrorLens.FieldOfView = 40f;
        mirrorLens.NearClipPlane = 0.01f;
        mirrorLens.FarClipPlane = 100f;
        mirrorCam.Lens = mirrorLens;
        mirrorCam.Priority = 0;

        // Start disabled (StationRoot will enable)
        mgr.enabled = false;
        hudCanvasGO.SetActive(false);

        Debug.Log("[ApartmentSceneBuilder] Mirror Makeup station built.");

        return new MirrorMakeupData
        {
            manager = mgr,
            hud = hud,
            hudRoot = hudCanvasGO,
            stationCamera = mirrorCam
        };
    }

    private static MakeupToolDefinition CreateMakeupToolSO(
        string dir, string name, MakeupToolDefinition.ToolType toolType,
        Color brushColor, float brushRadius, float opacity, bool softEdge,
        bool canSmear, float smearThreshold, float smearWidth, float smearFalloff,
        float starSize, Color starColor)
    {
        string assetPath = $"{dir}/Tool_{name.Replace(" ", "_")}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MakeupToolDefinition>(assetPath);
        if (existing != null) return existing;

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

        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    private static UnityEngine.UI.Button BuildMakeupToolButton(
        Transform parent, string label, int toolIndex,
        MirrorMakeupManager mgr, Vector2 position, Color tintColor)
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

        if (toolIndex >= 0)
        {
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                btn.onClick, mgr.SelectTool, toolIndex);
        }
        else
        {
            var action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), mgr,
                typeof(MirrorMakeupManager).GetMethod(nameof(MirrorMakeupManager.DeselectTool),
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

    // ══════════════════════════════════════════════════════════════════
    // Ambient Watering (NOT a station)
    // ══════════════════════════════════════════════════════════════════

    private static void BuildAmbientWatering(GameObject camGO, int plantsLayer)
    {
        // ── SO folder ────────────────────────────────────────────────
        string soDir = "Assets/ScriptableObjects/Watering";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(soDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Watering");

        // ── Plant definition SOs ─────────────────────────────────────
        string[] plantNames = { "Fern", "Cactus", "Succulent", "Monstera", "Herb Pot" };
        string[] plantDescs =
        {
            "A classic shelf fern \u2014 likes it moist.",
            "Desert dweller \u2014 a little water goes a long way.",
            "Plump leaves store water \u2014 moderate needs.",
            "Tropical giant \u2014 give it a good soak.",
            "Basil and thyme \u2014 keep the soil evenly damp."
        };
        float[] idealLevels = { 0.75f, 0.30f, 0.50f, 0.80f, 0.60f };

        var plantDefs = new PlantDefinition[5];
        for (int i = 0; i < 5; i++)
        {
            string assetPath = $"{soDir}/Plant_{plantNames[i].Replace(" ", "_")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PlantDefinition>(assetPath);
            if (existing != null)
            {
                plantDefs[i] = existing;
                continue;
            }

            var def = ScriptableObject.CreateInstance<PlantDefinition>();
            def.plantName = plantNames[i];
            def.description = plantDescs[i];
            def.idealWaterLevel = idealLevels[i];
            def.baseScore = 100;
            AssetDatabase.CreateAsset(def, assetPath);
            plantDefs[i] = def;
        }

        // ── Add WaterablePlant to existing watering nook pots ────────
        // Pots are named WateringPot_0..2 (built by BuildWateringNook)
        // Also add to SmallPlant_Pot in living room and FlowerPot
        string[] potNames = { "WateringPot_0", "WateringPot_1", "WateringPot_2",
                              "SmallPlant_Pot", "FlowerPot" };
        for (int i = 0; i < potNames.Length; i++)
        {
            var potGO = GameObject.Find(potNames[i]);
            if (potGO != null)
            {
                potGO.layer = plantsLayer;
                potGO.isStatic = false;
                var wp = potGO.AddComponent<WaterablePlant>();
                wp.definition = plantDefs[i < plantDefs.Length ? i : 0];
            }
        }

        // Also set the plant visuals above the pots to the plants layer
        for (int i = 0; i < 3; i++)
        {
            var plantGO = GameObject.Find($"WateringPlant_{i}");
            if (plantGO != null)
            {
                plantGO.layer = plantsLayer;
                plantGO.isStatic = false;
            }
        }

        // ── PotController (hidden — no visuals, HUD shows meters) ────
        var potGOHidden = new GameObject("AmbientPotController");
        var potCtrl = potGOHidden.AddComponent<PotController>();
        // Minimal wiring — no visual transforms needed, simulation only
        var potSO = new SerializedObject(potCtrl);
        potSO.FindProperty("potWorldHeight").floatValue = 0.10f;
        potSO.FindProperty("potWorldRadius").floatValue = 0.04f;
        potSO.ApplyModifiedPropertiesWithoutUndo();

        // ── WateringManager + HUD ────────────────────────────────────
        var managersGO = new GameObject("AmbientWateringManagers");
        var waterMgr = managersGO.AddComponent<WateringManager>();
        var waterHud = managersGO.AddComponent<WateringHUD>();

        var cam = camGO.GetComponent<UnityEngine.Camera>();

        // Wire WateringManager
        var mgrSO = new SerializedObject(waterMgr);
        mgrSO.FindProperty("_plantLayer").intValue = 1 << plantsLayer;
        mgrSO.FindProperty("_pot").objectReferenceValue = potCtrl;
        mgrSO.FindProperty("_hud").objectReferenceValue = waterHud;
        mgrSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("_scoreDisplayTime").floatValue = 2f;
        mgrSO.FindProperty("_overflowPenalty").floatValue = 30f;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── HUD Canvas (sorting 14) ─────────────────────────────────
        var hudCanvasGO = new GameObject("WateringHUD_Canvas");
        hudCanvasGO.transform.SetParent(managersGO.transform);
        var hudCanvas = hudCanvasGO.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 14;
        hudCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        hudCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // HUD panel container (hidden when idle)
        var hudPanelGO = new GameObject("WateringHUDPanel");
        hudPanelGO.transform.SetParent(hudCanvasGO.transform);
        var hudPanelRT = hudPanelGO.AddComponent<RectTransform>();
        hudPanelRT.anchorMin = Vector2.zero;
        hudPanelRT.anchorMax = Vector2.one;
        hudPanelRT.offsetMin = Vector2.zero;
        hudPanelRT.offsetMax = Vector2.zero;
        hudPanelRT.localScale = Vector3.one;

        // Plant name (top center)
        var plantNameLabel = CreateHUDText("PlantNameLabel", hudPanelGO.transform,
            new Vector2(0f, 200f), 24f, "");

        // Water level (right side)
        var waterLabel = CreateHUDText("WaterLevelLabel", hudPanelGO.transform,
            new Vector2(250f, 30f), 18f, "");

        // Foam level (right side)
        var foamLabel = CreateHUDText("FoamLevelLabel", hudPanelGO.transform,
            new Vector2(250f, -10f), 18f, "");

        // Target hint (right side)
        var targetLabel = CreateHUDText("TargetLabel", hudPanelGO.transform,
            new Vector2(250f, -50f), 16f, "");

        // Overflow warning (center, red)
        var overflowGO = new GameObject("OverflowWarning");
        overflowGO.transform.SetParent(hudPanelGO.transform);
        var overflowRT = overflowGO.AddComponent<RectTransform>();
        overflowRT.anchorMin = new Vector2(0.5f, 0.5f);
        overflowRT.anchorMax = new Vector2(0.5f, 0.5f);
        overflowRT.sizeDelta = new Vector2(300f, 40f);
        overflowRT.anchoredPosition = new Vector2(0f, 60f);
        overflowRT.localScale = Vector3.one;
        var overflowTMP = overflowGO.AddComponent<TextMeshProUGUI>();
        overflowTMP.text = "Overflowing!";
        overflowTMP.fontSize = 22f;
        overflowTMP.alignment = TextAlignmentOptions.Center;
        overflowTMP.color = new Color(1f, 0.25f, 0.2f);
        overflowGO.SetActive(false);

        // Score label (center)
        var scoreLabel = CreateHUDText("ScoreLabel", hudPanelGO.transform,
            new Vector2(0f, 0f), 20f, "");

        // ── Wire WateringHUD ─────────────────────────────────────────
        var hudSO = new SerializedObject(waterHud);
        hudSO.FindProperty("manager").objectReferenceValue = waterMgr;
        hudSO.FindProperty("plantNameLabel").objectReferenceValue = plantNameLabel;
        hudSO.FindProperty("waterLevelLabel").objectReferenceValue = waterLabel;
        hudSO.FindProperty("foamLevelLabel").objectReferenceValue = foamLabel;
        hudSO.FindProperty("targetLabel").objectReferenceValue = targetLabel;
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
        hudSO.FindProperty("overflowWarning").objectReferenceValue = overflowGO;
        hudSO.FindProperty("hudPanel").objectReferenceValue = hudPanelGO;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // Always enabled — not managed by StationRoot
        Debug.Log("[ApartmentSceneBuilder] Ambient watering system built.");
    }

    // ══════════════════════════════════════════════════════════════════
    // ApartmentManager + Screen-Space UI
    // ══════════════════════════════════════════════════════════════════

    private static void BuildApartmentManager(
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
    }

    // ══════════════════════════════════════════════════════════════════
    // BookInteractionManager
    // ══════════════════════════════════════════════════════════════════

    private static BookInteractionManager BuildBookInteractionManager(
        GameObject camGO, int booksLayer,
        int drawersLayer, int perfumesLayer, int trinketsLayer, int coffeeTableBooksLayer,
        ItemInspector itemInspector, EnvironmentMoodController moodController)
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
        so.FindProperty("itemInspector").objectReferenceValue = itemInspector;
        so.FindProperty("moodController").objectReferenceValue = moodController;
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
    // Bookcase Extras (mood, inspector — bookcase geometry via BookcaseSceneBuilder)
    // ══════════════════════════════════════════════════════════════════

    private static EnvironmentMoodController BuildBookNookEnvironmentMood(GameObject lightGO)
    {
        var moodGO = new GameObject("EnvironmentMoodController");
        var mood = moodGO.AddComponent<EnvironmentMoodController>();

        var moodSO = new SerializedObject(mood);
        moodSO.FindProperty("directionalLight").objectReferenceValue = lightGO.GetComponent<Light>();
        moodSO.ApplyModifiedPropertiesWithoutUndo();

        return mood;
    }

    private static ItemInspector BuildBookNookItemInspector(GameObject camGO)
    {
        var inspectorGO = new GameObject("BookNookItemInspector");
        var inspector = inspectorGO.AddComponent<ItemInspector>();

        // Inspect anchor child of camera
        var inspectAnchor = new GameObject("BookInspectAnchor");
        inspectAnchor.transform.SetParent(camGO.transform);
        inspectAnchor.transform.localPosition = new Vector3(0f, 0f, 0.4f);
        inspectAnchor.transform.localRotation = Quaternion.identity;

        // Description panel UI
        var uiCanvasGO = new GameObject("BookInspectUI_Canvas");
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 15;
        uiCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        uiCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var descPanel = new GameObject("DescriptionPanel");
        descPanel.transform.SetParent(uiCanvasGO.transform);

        var panelRT = descPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(450f, 100f);
        panelRT.anchoredPosition = new Vector2(0f, 80f);
        panelRT.localScale = Vector3.one;

        var panelBg = descPanel.AddComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.7f);

        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(descPanel.transform);
        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.6f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = new Vector2(10f, 0f);
        nameRT.offsetMax = new Vector2(-10f, -5f);
        nameRT.localScale = Vector3.one;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Item Name";
        nameTMP.fontSize = 22f;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = Color.white;
        nameTMP.fontStyle = FontStyles.Bold;

        var descGO = new GameObject("DescriptionText");
        descGO.transform.SetParent(descPanel.transform);
        var descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.6f);
        descRT.offsetMin = new Vector2(10f, 5f);
        descRT.offsetMax = new Vector2(-10f, 0f);
        descRT.localScale = Vector3.one;
        var descTMP = descGO.AddComponent<TextMeshProUGUI>();
        descTMP.text = "Description";
        descTMP.fontSize = 16f;
        descTMP.alignment = TextAlignmentOptions.Center;
        descTMP.color = new Color(0.8f, 0.8f, 0.8f);
        descTMP.enableWordWrapping = true;

        descPanel.SetActive(false);

        var inspSO = new SerializedObject(inspector);
        inspSO.FindProperty("inspectAnchor").objectReferenceValue = inspectAnchor.transform;
        inspSO.FindProperty("descriptionPanel").objectReferenceValue = descPanel;
        inspSO.FindProperty("nameText").objectReferenceValue = nameTMP;
        inspSO.FindProperty("descriptionText").objectReferenceValue = descTMP;
        inspSO.ApplyModifiedPropertiesWithoutUndo();

        inspector.enabled = false;

        return inspector;
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
