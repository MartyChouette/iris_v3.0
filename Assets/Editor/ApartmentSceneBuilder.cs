using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
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

    // ── Book data ──
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

    // ══════════════════════════════════════════════════════════════════
    // Main Build
    // ══════════════════════════════════════════════════════════════════

    [MenuItem("Window/Iris/Build Apartment Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int placeableLayer = EnsureLayer(PlaceableLayerName);
        int booksLayer = EnsureLayer(BooksLayerName);

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

        // ── 9. Books in book nook ──
        BuildBookNookBooks(booksLayer);

        // ── 10. BookInteractionManager ──
        var bookManager = BuildBookInteractionManager(camGO, booksLayer);

        // ── 11. Record player station ──
        var recordPlayerData = BuildRecordPlayerStation();

        // ── 12. Station roots ──
        BuildStationRoots(bookManager, recordPlayerData);

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

        // Bookcase (against wall, Living Room side)
        CreateBox("Bookshelf", parent,
            new Vector3(-6.3f, 1.0f, 3f), new Vector3(0.5f, 2.0f, 1.5f),
            new Color(0.42f, 0.30f, 0.20f));

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
            StationType.Watering, 0.429f,
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
        RecordPlayerData recordData)
    {
        var parent = new GameObject("StationRoots");

        // Bookcase station (Living Room)
        CreateStationRoot(parent.transform, "Station_Bookcase",
            StationType.Bookcase, bookManager);

        // Record Player station (Cozy Corner)
        CreateStationRoot(parent.transform, "Station_RecordPlayer",
            StationType.RecordPlayer, recordData.manager,
            recordData.hudRoot);

        // Placeholder stations for minigames that need their own scenes
        // (NewspaperDating, MirrorMakeup, Watering, FlowerTrimming)
        // These will be wired when their managers are instantiated in the apartment scene.
        CreateStationRoot(parent.transform, "Station_NewspaperDating",
            StationType.NewspaperDating, null);
        CreateStationRoot(parent.transform, "Station_MirrorMakeup",
            StationType.MirrorMakeup, null);
        CreateStationRoot(parent.transform, "Station_Watering",
            StationType.Watering, null);
        CreateStationRoot(parent.transform, "Station_FlowerTrimming",
            StationType.FlowerTrimming, null);
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
    // Book Nook Books
    // ══════════════════════════════════════════════════════════════════

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

        // Bookshelf is at (-6.3, 1.0, 3.0) with scale (0.5, 2.0, 1.5)
        float bookY = 1.05f;
        float bookX = -6.0f;
        float startZ = 2.35f;
        float spacing = 0.22f;

        for (int i = 0; i < BookTitles.Length; i++)
        {
            float thickness = 0.04f;
            float bookHeight = 0.3f;
            float bookDepth = 0.2f;
            Color color = SpineColors[i % SpineColors.Length];

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

            var volume = bookGO.AddComponent<BookVolume>();
            var pagesRoot = BuildBookPages(bookGO.transform, thickness, bookHeight, bookDepth);

            var bvSO = new SerializedObject(volume);
            bvSO.FindProperty("definition").objectReferenceValue = def;
            bvSO.FindProperty("pagesRoot").objectReferenceValue = pagesRoot;

            var pageLabels = pagesRoot.GetComponentsInChildren<TMP_Text>(true);
            var labelsProperty = bvSO.FindProperty("pageLabels");
            labelsProperty.arraySize = Mathf.Min(pageLabels.Length, 3);
            for (int p = 0; p < labelsProperty.arraySize; p++)
                labelsProperty.GetArrayElementAtIndex(p).objectReferenceValue = pageLabels[p];

            bvSO.ApplyModifiedPropertiesWithoutUndo();
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

    // ══════════════════════════════════════════════════════════════════
    // BookInteractionManager
    // ══════════════════════════════════════════════════════════════════

    private static BookInteractionManager BuildBookInteractionManager(
        GameObject camGO, int booksLayer)
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
        so.FindProperty("maxRayDistance").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

        manager.enabled = false;

        return manager;
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
