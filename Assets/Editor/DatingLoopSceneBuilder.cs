using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor.Events;
using UnityEngine.Events;

/// <summary>
/// Editor utility that programmatically builds a standalone dating loop test scene.
/// Includes room geometry, NavMesh, newspaper (two-page spread held up in front of player),
/// kitchen, couch, phone, reactable props, all managers, cameras, and UI.
/// Menu: Window > Iris > Build Dating Loop Scene
/// </summary>
public static class DatingLoopSceneBuilder
{
    private const string NewspaperLayerName = "Newspaper";

    // Two-page spread: 1000×700 canvas at 0.001 pivot scale = 1.0×0.7 world units
    private const int CanvasWidth = 1000;
    private const int CanvasHeight = 700;

    [MenuItem("Window/Iris/Archived Builders/Build Dating Loop Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ───────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int newspaperLayer = EnsureLayer(NewspaperLayerName);

        // ── 1. Directional light ─────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Room geometry ─────────────────────────────────────────
        var roomData = BuildRoom();

        // ── 3. Furniture ─────────────────────────────────────────────
        var furnitureData = BuildFurniture();

        // ── 4. Cameras ───────────────────────────────────────────────
        var camData = BuildCameras();

        // ── 5. NavMesh ───────────────────────────────────────────────
        BuildNavMesh(roomData.floor);

        // ── 6. Reactable props ───────────────────────────────────────
        BuildReactableProps();

        // ── 7. Newspaper surface + WorldSpace overlay (two-page spread) ─
        var newspaperData = BuildNewspaper(newspaperLayer);

        // ── 8. Scissors visual ───────────────────────────────────────
        var scissorsVisual = BuildScissorsVisual();

        // ── 9. ScriptableObject assets ───────────────────────────────
        var soData = CreateScriptableObjectAssets();

        // ── 10. Managers ─────────────────────────────────────────────
        var managerData = BuildManagers(camData, furnitureData, soData, light,
            newspaperData, scissorsVisual, newspaperLayer);

        // ── 11. UI ───────────────────────────────────────────────────
        BuildUI(managerData);

        // ── 12. EventSystem (required for UI button clicks) ──────────
        var eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();

        // ── 13. Save scene ───────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/dating_loop.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[DatingLoopSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════
    // Room Geometry
    // ════════════════════════════════════════════════════════════════

    private struct RoomData
    {
        public GameObject floor;
    }

    private static RoomData BuildRoom()
    {
        var parent = new GameObject("Room");

        // Floor (larger for NavMesh walking)
        var floor = CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.01f, 0f), new Vector3(8f, 0.02f, 8f),
            new Color(0.35f, 0.28f, 0.22f));

        // Walls
        CreateBox("WallNorth", parent.transform,
            new Vector3(0f, 1.5f, 4f), new Vector3(8f, 3f, 0.1f),
            new Color(0.6f, 0.55f, 0.5f));
        CreateBox("WallSouth", parent.transform,
            new Vector3(0f, 1.5f, -4f), new Vector3(8f, 3f, 0.1f),
            new Color(0.6f, 0.55f, 0.5f));
        CreateBox("WallEast", parent.transform,
            new Vector3(4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f),
            new Color(0.58f, 0.53f, 0.48f));
        CreateBox("WallWest", parent.transform,
            new Vector3(-4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f),
            new Color(0.58f, 0.53f, 0.48f));

        // Ceiling
        CreateBox("Ceiling", parent.transform,
            new Vector3(0f, 3.01f, 0f), new Vector3(8f, 0.02f, 8f),
            new Color(0.75f, 0.73f, 0.70f));

        return new RoomData { floor = floor };
    }

    // ════════════════════════════════════════════════════════════════
    // Furniture
    // ════════════════════════════════════════════════════════════════

    private struct FurnitureData
    {
        public Transform couchSeatTarget;
        public Transform coffeeTableDeliveryPoint;
        public Transform dateSpawnPoint;
        public Transform phoneTransform;
        public Collider phoneCollider;
        public Transform bedTransform;
    }

    private static FurnitureData BuildFurniture()
    {
        var parent = new GameObject("Furniture");

        // ── Couch (center-left, facing east) ──────────────────────
        var couchParent = new GameObject("Couch");
        couchParent.transform.SetParent(parent.transform);

        CreateBox("CouchBase", couchParent.transform,
            new Vector3(-1f, 0.25f, 0f), new Vector3(1.2f, 0.5f, 0.6f),
            new Color(0.5f, 0.35f, 0.3f));
        CreateBox("CouchBack", couchParent.transform,
            new Vector3(-1f, 0.55f, 0.25f), new Vector3(1.2f, 0.3f, 0.1f),
            new Color(0.45f, 0.3f, 0.25f));

        var seatTarget = new GameObject("CouchSeatTarget");
        seatTarget.transform.SetParent(couchParent.transform);
        seatTarget.transform.position = new Vector3(-1f, 0.5f, 0f);

        // ── Coffee table (in front of couch) ─────────────────────
        var coffeeTable = new GameObject("CoffeeTable");
        coffeeTable.transform.SetParent(parent.transform);

        CreateBox("CoffeeTableTop", coffeeTable.transform,
            new Vector3(-1f, 0.3f, -0.6f), new Vector3(0.8f, 0.04f, 0.4f),
            new Color(0.4f, 0.28f, 0.18f));
        CreateBox("CoffeeTableLeg1", coffeeTable.transform,
            new Vector3(-1.35f, 0.15f, -0.75f), new Vector3(0.04f, 0.3f, 0.04f),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("CoffeeTableLeg2", coffeeTable.transform,
            new Vector3(-0.65f, 0.15f, -0.75f), new Vector3(0.04f, 0.3f, 0.04f),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("CoffeeTableLeg3", coffeeTable.transform,
            new Vector3(-1.35f, 0.15f, -0.45f), new Vector3(0.04f, 0.3f, 0.04f),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("CoffeeTableLeg4", coffeeTable.transform,
            new Vector3(-0.65f, 0.15f, -0.45f), new Vector3(0.04f, 0.3f, 0.04f),
            new Color(0.35f, 0.22f, 0.12f));

        var deliveryPoint = new GameObject("DrinkDeliveryPoint");
        deliveryPoint.transform.SetParent(coffeeTable.transform);
        deliveryPoint.transform.position = new Vector3(-1f, 0.34f, -0.6f);

        // ── Kitchen counter (back-right) ─────────────────────────
        var kitchen = new GameObject("KitchenCounter");
        kitchen.transform.SetParent(parent.transform);

        CreateBox("CounterTop", kitchen.transform,
            new Vector3(2.5f, 0.5f, -2f), new Vector3(2f, 0.06f, 0.8f),
            new Color(0.55f, 0.55f, 0.55f));
        CreateBox("CounterBase", kitchen.transform,
            new Vector3(2.5f, 0.25f, -2f), new Vector3(2f, 0.5f, 0.8f),
            new Color(0.4f, 0.35f, 0.3f));

        // ── Phone (on wall near kitchen) ─────────────────────────
        var phoneGO = CreateBox("Phone", parent.transform,
            new Vector3(3.2f, 1.0f, 2f), new Vector3(0.12f, 0.18f, 0.06f),
            new Color(0.15f, 0.15f, 0.15f));
        phoneGO.isStatic = false;

        // Ring visual (pulsing glow)
        var ringVisual = CreateBox("RingVisual", phoneGO.transform,
            new Vector3(3.2f, 1.15f, 2f), new Vector3(0.06f, 0.06f, 0.06f),
            new Color(1f, 0.3f, 0.3f));
        ringVisual.isStatic = false;

        // ── Entrance door area ───────────────────────────────────
        CreateBox("EntranceDoor", parent.transform,
            new Vector3(-3.9f, 1f, -2f), new Vector3(0.08f, 2f, 1f),
            new Color(0.5f, 0.35f, 0.2f));

        var dateSpawn = new GameObject("DateSpawnPoint");
        dateSpawn.transform.SetParent(parent.transform);
        dateSpawn.transform.position = new Vector3(-3.5f, 0f, -2f);

        // ── Bed (back wall) ──────────────────────────────────────
        var bed = new GameObject("Bed");
        bed.transform.SetParent(parent.transform);

        var bedBox = CreateBox("BedFrame", bed.transform,
            new Vector3(0f, 0.2f, 3.5f), new Vector3(1.5f, 0.4f, 1f),
            new Color(0.4f, 0.3f, 0.5f));
        bedBox.isStatic = false;

        CreateBox("BedPillow", bed.transform,
            new Vector3(0f, 0.42f, 3.8f), new Vector3(0.8f, 0.08f, 0.3f),
            new Color(0.8f, 0.8f, 0.85f));

        return new FurnitureData
        {
            couchSeatTarget = seatTarget.transform,
            coffeeTableDeliveryPoint = deliveryPoint.transform,
            dateSpawnPoint = dateSpawn.transform,
            phoneTransform = phoneGO.transform,
            phoneCollider = phoneGO.GetComponent<Collider>(),
            bedTransform = bed.transform
        };
    }

    // ════════════════════════════════════════════════════════════════
    // Cameras
    // ════════════════════════════════════════════════════════════════

    private struct CameraData
    {
        public Camera camera;
        public CinemachineBrain brain;
        public CinemachineCamera overviewCamera;
        public CinemachineCamera newspaperReadCamera;
        public CinemachineCamera kitchenCamera;
        public CinemachineCamera couchCamera;
    }

    private static CameraData BuildCameras()
    {
        // Main Camera + CinemachineBrain
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.12f, 0.10f);
        cam.fieldOfView = 60f;
        camGO.transform.position = new Vector3(0f, 2.5f, -3f);
        camGO.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

        var brain = camGO.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);

        // Overview camera (default, sees whole room)
        var overviewGO = new GameObject("OverviewCamera");
        var overviewCam = overviewGO.AddComponent<CinemachineCamera>();
        overviewCam.Lens = LensSettings.Default;
        overviewCam.Lens.FieldOfView = 65f;
        overviewCam.Priority = 10;
        overviewGO.transform.position = new Vector3(0f, 2.5f, -3f);
        overviewGO.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

        // Newspaper read camera — held up in front of player, forward-facing
        var newsReadGO = new GameObject("NewspaperReadCamera");
        var newsReadCam = newsReadGO.AddComponent<CinemachineCamera>();
        newsReadCam.Lens = LensSettings.Default;
        newsReadCam.Lens.FieldOfView = 40f;
        newsReadCam.Priority = 0;
        newsReadGO.transform.position = new Vector3(0f, 1.5f, 0f);
        newsReadGO.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        // Kitchen camera
        var kitchenGO = new GameObject("KitchenCamera");
        var kitchenCam = kitchenGO.AddComponent<CinemachineCamera>();
        kitchenCam.Lens = LensSettings.Default;
        kitchenCam.Lens.FieldOfView = 50f;
        kitchenCam.Priority = 0;
        kitchenGO.transform.position = new Vector3(2.5f, 1.5f, -3.5f);
        kitchenGO.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        // Couch area camera
        var couchGO = new GameObject("CouchCamera");
        var couchCam = couchGO.AddComponent<CinemachineCamera>();
        couchCam.Lens = LensSettings.Default;
        couchCam.Lens.FieldOfView = 55f;
        couchCam.Priority = 0;
        couchGO.transform.position = new Vector3(-1f, 1.8f, -2f);
        couchGO.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        return new CameraData
        {
            camera = cam,
            brain = brain,
            overviewCamera = overviewCam,
            newspaperReadCamera = newsReadCam,
            kitchenCamera = kitchenCam,
            couchCamera = couchCam
        };
    }

    // ════════════════════════════════════════════════════════════════
    // NavMesh
    // ════════════════════════════════════════════════════════════════

    private static void BuildNavMesh(GameObject floor)
    {
        var navSurface = floor.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.All;
        navSurface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        navSurface.BuildNavMesh();
        Debug.Log("[DatingLoopSceneBuilder] NavMesh built.");
    }

    // ════════════════════════════════════════════════════════════════
    // Newspaper Surface + WorldSpace Canvas Overlay (Two-Page Spread)
    // ════════════════════════════════════════════════════════════════

    private struct NewspaperData
    {
        public GameObject surfaceQuad;
        public NewspaperAdSlot[] personalSlots;
        public NewspaperAdSlot[] commercialSlots;
        public GameObject overlayCanvas;
    }

    private const string NewspaperPrefabPath = "Assets/Prefabs/NewspaperModel.prefab";

    private static NewspaperData BuildNewspaper(int newspaperLayer)
    {
        var parent = new GameObject("Newspaper");

        // Held up in front of player — vertical plane facing camera
        // Camera is at (0, 1.5, 0) looking forward (+Z)
        // Newspaper at (0, 1.35, 0.55) — slightly below eye level, in front

        // ── Surface (3D mesh for cut texture) ─────────────────────────
        GameObject surfaceGO;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewspaperPrefabPath);
        if (prefab != null)
        {
            surfaceGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            surfaceGO.name = "NewspaperSurface";
            surfaceGO.transform.SetParent(parent.transform);
            surfaceGO.transform.position = new Vector3(0f, 1.35f, 0.55f);
            SetLayerRecursive(surfaceGO, newspaperLayer);
            Debug.Log($"[DatingLoopSceneBuilder] Using imported newspaper model from {NewspaperPrefabPath}");
        }
        else
        {
            surfaceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceGO.name = "NewspaperSurface";
            surfaceGO.transform.SetParent(parent.transform);
            surfaceGO.transform.position = new Vector3(0f, 1.35f, 0.55f);
            // Face camera (looking -Z), slight tilt toward player
            surfaceGO.transform.rotation = Quaternion.Euler(-5f, 180f, 0f);
            surfaceGO.transform.localScale = new Vector3(1.0f, 0.7f, 1f);
            surfaceGO.layer = newspaperLayer;

            // Remove default MeshCollider from primitive
            Object.DestroyImmediate(surfaceGO.GetComponent<MeshCollider>());
        }

        // Ensure collider exists
        var boxCol = surfaceGO.GetComponent<BoxCollider>();
        if (boxCol == null)
            boxCol = surfaceGO.GetComponentInChildren<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = surfaceGO.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(1f, 1f, 0.01f);
            boxCol.center = Vector3.zero;
        }

        // Add NewspaperSurface if not already present
        if (surfaceGO.GetComponent<NewspaperSurface>() == null)
            surfaceGO.AddComponent<NewspaperSurface>();

        // Only assign default material if no material is already set (i.e., primitive quad)
        var rend = surfaceGO.GetComponent<Renderer>();
        if (rend == null) rend = surfaceGO.GetComponentInChildren<Renderer>();
        if (rend != null && prefab == null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.92f, 0.90f, 0.85f);
            mat.SetInt("_Cull", 0); // Render both faces
            rend.sharedMaterial = mat;
        }

        // ── WorldSpace Canvas overlay (two-page spread) ─────────────
        // Pivot GO holds 3D transform — matches newspaper quad position/rotation
        var pivotGO = new GameObject("NewspaperOverlayPivot");
        pivotGO.transform.SetParent(parent.transform);
        pivotGO.transform.position = new Vector3(0f, 1.35f, 0.549f);
        pivotGO.transform.rotation = Quaternion.Euler(-5f, 180f, 0f);
        pivotGO.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Canvas child — identity local transform
        var canvasGO = new GameObject("NewspaperOverlay");
        canvasGO.transform.SetParent(pivotGO.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.localPosition = Vector3.zero;
        canvasRT.localRotation = Quaternion.identity;
        canvasRT.localScale = Vector3.one;
        canvasRT.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        canvasRT.pivot = new Vector2(0.5f, 0.5f);

        // CanvasGroup — blocksRaycasts=false so scissors Physics.Raycast passes through
        var cg = canvasGO.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // ── Center fold line ────────────────────────────────────────
        var foldGO = new GameObject("FoldLine");
        foldGO.transform.SetParent(canvasGO.transform, false);
        var foldRT = foldGO.AddComponent<RectTransform>();
        foldRT.anchoredPosition = new Vector2(0f, 0f);
        foldRT.sizeDelta = new Vector2(2f, CanvasHeight);
        foldRT.localScale = Vector3.one;
        var foldImg = foldGO.AddComponent<Image>();
        foldImg.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);

        // ── Left page (decorative fake articles) ────────────────────
        BuildLeftPageSquiggles(canvasGO.transform);

        // ── Right page (personal ads — flex layout) ─────────────────
        // Load pool to determine ad count
        string poolPath = "Assets/ScriptableObjects/Dating/NewspaperPool_Default.asset";
        var pool = AssetDatabase.LoadAssetAtPath<NewspaperPoolDefinition>(poolPath);
        int adCount = pool != null ? pool.personalAdsPerDay : 4;
        adCount = Mathf.Clamp(adCount, 1, 8);

        // Right page header
        CreateCanvasText("PersonalsLabel", canvasGO.transform,
            new Vector2(250f, 310f), new Vector2(440f, 35f),
            "PERSONALS", 28f, FontStyles.Bold, TextAlignmentOptions.Center);

        // Right page header rule
        var pRuleGO = new GameObject("PersonalsRule");
        pRuleGO.transform.SetParent(canvasGO.transform, false);
        var pRuleRT = pRuleGO.AddComponent<RectTransform>();
        pRuleRT.anchoredPosition = new Vector2(250f, 290f);
        pRuleRT.sizeDelta = new Vector2(420f, 2f);
        pRuleRT.localScale = Vector3.one;
        pRuleGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

        // Flex layout for personal ads on right page
        // Content area: x=[520, 980], y=[30, 620] in absolute pixel space (from bottom-left)
        float contentLeft = 520f;
        float contentRight = 980f;
        float contentBottom = 30f;
        float contentTop = 620f;
        float contentWidth = contentRight - contentLeft;   // 460
        float contentHeight = contentTop - contentBottom;   // 590
        float spacing = 10f;
        float slotHeight = (contentHeight - spacing * (adCount - 1)) / adCount;

        var personalSlots = new NewspaperAdSlot[adCount];

        for (int i = 0; i < adCount; i++)
        {
            // Slot position (from top)
            float slotTopY = contentTop - i * (slotHeight + spacing);
            float slotCenterY = slotTopY - slotHeight * 0.5f;

            // Anchored position relative to canvas center (pivot 0.5, 0.5)
            float anchoredX = (contentLeft + contentWidth * 0.5f) - CanvasWidth * 0.5f;  // 250
            float anchoredY = slotCenterY - CanvasHeight * 0.5f;

            // Font sizes clamped by slot height
            float nameFontSize = Mathf.Clamp(slotHeight * 0.16f, 14f, 28f);
            float bodyFontSize = Mathf.Clamp(slotHeight * 0.11f, 10f, 18f);
            float phoneFontSize = Mathf.Clamp(slotHeight * 0.13f, 12f, 22f);
            float portraitSize = Mathf.Clamp(slotHeight * 0.35f, 32f, 48f);

            string prefix = $"Personal_{i}";

            // Slot background (subtle border)
            var slotBgGO = new GameObject($"{prefix}_BG");
            slotBgGO.transform.SetParent(canvasGO.transform, false);
            var slotBgRT = slotBgGO.AddComponent<RectTransform>();
            slotBgRT.anchoredPosition = new Vector2(anchoredX, anchoredY);
            slotBgRT.sizeDelta = new Vector2(contentWidth, slotHeight);
            slotBgRT.localScale = Vector3.one;
            var slotBgImg = slotBgGO.AddComponent<Image>();
            slotBgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.06f);

            // Name text (top of slot)
            float nameOffsetY = slotHeight * 0.35f;
            var nameGO = CreateCanvasText($"{prefix}_Name", canvasGO.transform,
                new Vector2(anchoredX - 20f, anchoredY + nameOffsetY),
                new Vector2(contentWidth - portraitSize - 20f, nameFontSize + 6f),
                "Name", nameFontSize, FontStyles.Bold, TextAlignmentOptions.Left);

            // Portrait image (top-right of slot)
            var portraitGO = CreateCanvasImage($"{prefix}_Portrait", canvasGO.transform,
                new Vector2(anchoredX + contentWidth * 0.5f - portraitSize * 0.5f - 5f,
                            anchoredY + nameOffsetY),
                new Vector2(portraitSize, portraitSize));

            // Ad body text (center of slot)
            var adGO = CreateCanvasText($"{prefix}_Ad", canvasGO.transform,
                new Vector2(anchoredX, anchoredY),
                new Vector2(contentWidth - 20f, slotHeight * 0.4f),
                "Ad text...", bodyFontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            // Phone number (bottom of slot)
            float phoneOffsetY = -slotHeight * 0.35f;
            var phoneGO = CreateCanvasText($"{prefix}_Phone", canvasGO.transform,
                new Vector2(anchoredX, anchoredY + phoneOffsetY),
                new Vector2(contentWidth - 20f, phoneFontSize + 6f),
                "555-0000", phoneFontSize, FontStyles.Italic, TextAlignmentOptions.Left);

            // Create logical ad slot with UV bounds
            float slotCenterX_abs = contentLeft + contentWidth * 0.5f;
            personalSlots[i] = CreateLogicalAdSlot($"PersonalSlot_{i}",
                parent.transform, newspaperLayer,
                new Vector2(slotCenterX_abs, slotCenterY),
                new Vector2(contentWidth, slotHeight),
                true, new Vector2(CanvasWidth, CanvasHeight));

            // Wire TMP_Text and Image refs to ad slot
            var slotSO = new SerializedObject(personalSlots[i]);
            slotSO.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
            slotSO.FindProperty("adLabel").objectReferenceValue = adGO.GetComponent<TMP_Text>();
            slotSO.FindProperty("phoneNumberLabel").objectReferenceValue = phoneGO.GetComponent<TMP_Text>();
            slotSO.FindProperty("portraitImage").objectReferenceValue = portraitGO.GetComponent<Image>();
            slotSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // No commercial slots on the two-page spread (left page is decorative)
        var commercialSlots = new NewspaperAdSlot[0];

        // Start hidden — NewspaperManager shows it when entering ReadingPaper state
        canvasGO.SetActive(false);

        return new NewspaperData
        {
            surfaceQuad = surfaceGO,
            personalSlots = personalSlots,
            commercialSlots = commercialSlots,
            overlayCanvas = canvasGO
        };
    }

    /// <summary>
    /// Build decorative left page content: newspaper title, fake headlines, body squiggles.
    /// Left page occupies x=[0, 500] in canvas space → anchored x=[-250, 0] relative to center.
    /// </summary>
    private static void BuildLeftPageSquiggles(Transform canvasTransform)
    {
        float leftCenter = -250f; // center of left page relative to canvas center

        // Newspaper title
        CreateCanvasText("Title", canvasTransform,
            new Vector2(leftCenter, 300f), new Vector2(460f, 50f),
            "THE DAILY BLOOM", 38f, FontStyles.Bold, TextAlignmentOptions.Center);

        // Title rule
        var titleRuleGO = new GameObject("TitleRule");
        titleRuleGO.transform.SetParent(canvasTransform, false);
        var titleRuleRT = titleRuleGO.AddComponent<RectTransform>();
        titleRuleRT.anchoredPosition = new Vector2(leftCenter, 270f);
        titleRuleRT.sizeDelta = new Vector2(420f, 2f);
        titleRuleRT.localScale = Vector3.one;
        titleRuleGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

        // Date line
        CreateCanvasText("DateLine", canvasTransform,
            new Vector2(leftCenter, 252f), new Vector2(420f, 20f),
            "Vol. XLII  No. 7  |  The Garden District Gazette", 12f,
            FontStyles.Italic, TextAlignmentOptions.Center);

        // Headline 1
        CreateCanvasText("Headline1", canvasTransform,
            new Vector2(leftCenter, 215f), new Vector2(420f, 35f),
            "MYSTERIOUS BLOOM SPOTTED IN TOWN SQUARE", 20f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        // Body 1
        CreateCanvasText("Body1", canvasTransform,
            new Vector2(leftCenter, 155f), new Vector2(420f, 80f),
            "Residents were astonished yesterday when a never-before-seen flower appeared overnight in the central fountain. Botanists remain baffled. \"It smells like Tuesday,\" said one local.",
            13f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // Rule 2
        var rule2GO = new GameObject("Rule2");
        rule2GO.transform.SetParent(canvasTransform, false);
        var rule2RT = rule2GO.AddComponent<RectTransform>();
        rule2RT.anchoredPosition = new Vector2(leftCenter, 108f);
        rule2RT.sizeDelta = new Vector2(420f, 1f);
        rule2RT.localScale = Vector3.one;
        rule2GO.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // Headline 2
        CreateCanvasText("Headline2", canvasTransform,
            new Vector2(leftCenter, 85f), new Vector2(420f, 30f),
            "ANNUAL PRUNING FESTIVAL DRAWS RECORD CROWDS", 18f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        // Body 2
        CreateCanvasText("Body2", canvasTransform,
            new Vector2(leftCenter, 25f), new Vector2(420f, 80f),
            "The 47th Annual Pruning Festival exceeded all expectations with over 200 attendees. Highlights included the competitive hedge-sculpting finals and Mrs. Fernsby's award-winning topiary swan.",
            13f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // Rule 3
        var rule3GO = new GameObject("Rule3");
        rule3GO.transform.SetParent(canvasTransform, false);
        var rule3RT = rule3GO.AddComponent<RectTransform>();
        rule3RT.anchoredPosition = new Vector2(leftCenter, -22f);
        rule3RT.sizeDelta = new Vector2(420f, 1f);
        rule3RT.localScale = Vector3.one;
        rule3GO.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // Headline 3
        CreateCanvasText("Headline3", canvasTransform,
            new Vector2(leftCenter, -45f), new Vector2(420f, 30f),
            "WEATHER: PARTLY SUNNY WITH CHANCE OF PETALS", 16f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        // Body 3
        CreateCanvasText("Body3", canvasTransform,
            new Vector2(leftCenter, -100f), new Vector2(420f, 70f),
            "Meteorologists predict a mild week ahead with occasional floral precipitation. Residents advised to carry umbrellas and enjoy the fragrance. Pollen count: high.",
            12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // Filler squiggles at bottom
        CreateCanvasText("Filler", canvasTransform,
            new Vector2(leftCenter, -185f), new Vector2(420f, 100f),
            "~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~",
            10f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
    }

    /// <summary>
    /// Create a logical ad slot (no visible geometry — text lives on overlay canvas).
    /// Computes UV bounds for cut polygon evaluation.
    /// </summary>
    private static NewspaperAdSlot CreateLogicalAdSlot(string name, Transform parent,
        int layer, Vector2 centerPx, Vector2 sizePx, bool isPersonal, Vector2 virtualSize)
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

        // Compute UV bounds (0-1) from pixel coordinates
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

    // ════════════════════════════════════════════════════════════════
    // Scissors Visual
    // ════════════════════════════════════════════════════════════════

    private static Transform BuildScissorsVisual()
    {
        var parent = new GameObject("ScissorsVisual");
        parent.transform.position = Vector3.zero;

        // Two thin crossed blades (angled apart like open scissors)
        var bladeA = CreateBox("Blade_A", parent.transform,
            new Vector3(0f, 0.001f, 0.012f), new Vector3(0.003f, 0.002f, 0.025f),
            new Color(0.75f, 0.75f, 0.8f));
        bladeA.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        bladeA.isStatic = false;

        var bladeB = CreateBox("Blade_B", parent.transform,
            new Vector3(0f, 0.001f, 0.012f), new Vector3(0.003f, 0.002f, 0.025f),
            new Color(0.75f, 0.75f, 0.8f));
        bladeB.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
        bladeB.isStatic = false;

        // Pivot dot
        var pivot = CreateBox("Pivot", parent.transform,
            Vector3.zero, new Vector3(0.005f, 0.003f, 0.005f),
            new Color(0.3f, 0.3f, 0.3f));
        pivot.isStatic = false;

        parent.SetActive(false);
        return parent.transform;
    }

    // ════════════════════════════════════════════════════════════════
    // Reactable Props
    // ════════════════════════════════════════════════════════════════

    private static void BuildReactableProps()
    {
        var parent = new GameObject("ReactableProps");

        // Bookshelf (left wall)
        var bookshelf = CreateBox("Bookshelf", parent.transform,
            new Vector3(-3.5f, 0.7f, 1f), new Vector3(0.6f, 1.4f, 0.3f),
            new Color(0.5f, 0.35f, 0.2f));
        bookshelf.isStatic = false;
        var bookTag = bookshelf.AddComponent<ReactableTag>();
        WireReactableTag(bookTag, new[] { "books", "cozy" }, true);

        // Plant
        var plant = CreateBox("PottedPlant", parent.transform,
            new Vector3(2f, 0.3f, -0.5f), new Vector3(0.2f, 0.6f, 0.2f),
            new Color(0.3f, 0.6f, 0.3f));
        plant.isStatic = false;
        var plantTag = plant.AddComponent<ReactableTag>();
        WireReactableTag(plantTag, new[] { "plant", "nature" }, true);

        // Trinket on shelf
        var trinket = CreateBox("Trinket", parent.transform,
            new Vector3(-3.5f, 1.5f, 1f), new Vector3(0.1f, 0.1f, 0.1f),
            new Color(0.8f, 0.7f, 0.2f));
        trinket.isStatic = false;
        var trinketTag = trinket.AddComponent<ReactableTag>();
        WireReactableTag(trinketTag, new[] { "trinket", "cute" }, true);

        // Record player (cozy corner)
        var recordPlayer = CreateBox("RecordPlayer", parent.transform,
            new Vector3(-2.5f, 0.5f, 2.5f), new Vector3(0.4f, 0.15f, 0.4f),
            new Color(0.25f, 0.2f, 0.2f));
        recordPlayer.isStatic = false;
        var recordTag = recordPlayer.AddComponent<ReactableTag>();
        WireReactableTag(recordTag, new[] { "vinyl", "music" }, false);

        // Perfume bottle
        var perfume = CreateBox("PerfumeBottle", parent.transform,
            new Vector3(-3.5f, 1.2f, 1.2f), new Vector3(0.05f, 0.15f, 0.05f),
            new Color(0.8f, 0.5f, 0.9f));
        perfume.isStatic = false;
        var perfumeTag = perfume.AddComponent<ReactableTag>();
        WireReactableTag(perfumeTag, new[] { "perfume_floral" }, false);
    }

    private static void WireReactableTag(ReactableTag tag, string[] tags, bool active)
    {
        var so = new SerializedObject(tag);
        var tagsProp = so.FindProperty("tags");
        tagsProp.ClearArray();
        for (int i = 0; i < tags.Length; i++)
        {
            tagsProp.InsertArrayElementAtIndex(i);
            tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i];
        }
        so.FindProperty("isActive").boolValue = active;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════
    // ScriptableObject Assets
    // ════════════════════════════════════════════════════════════════

    private struct SOData
    {
        public NewspaperPoolDefinition pool;
        public MoodMachineProfile moodProfile;
        public DrinkRecipeDefinition[] recipes;
    }

    private static SOData CreateScriptableObjectAssets()
    {
        string baseDir = "Assets/ScriptableObjects";
        if (!AssetDatabase.IsValidFolder(baseDir))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        string datingDir = $"{baseDir}/Dating";
        if (!AssetDatabase.IsValidFolder(datingDir))
            AssetDatabase.CreateFolder(baseDir, "Dating");

        // ── Date personals with preferences ──────────────────────
        string[] names = { "Rose", "Thorn", "Lily", "Moss" };
        string[] ads =
        {
            "Romantic soul seeks someone who won't wilt under pressure. Enjoys candlelit dinners and light rain.",
            "Sharp wit, sharper edges. Looking for someone who can handle a little prick. Gardeners welcome.",
            "Gentle spirit with a pure heart. Allergic to drama, loves ponds and moonlight.",
            "Low-maintenance, earthy, always there. Seeking someone who appreciates the ground floor."
        };
        float[] arrivalTimes = { 30f, 45f, 20f, 60f };

        // Preference data
        string[][] likedTags = {
            new[] { "cozy", "perfume_floral", "music" },
            new[] { "vinyl", "books" },
            new[] { "plant", "nature", "cute" },
            new[] { "plant", "cozy", "books" }
        };
        string[][] dislikedTags = {
            new[] { "vinyl" },
            new[] { "cute", "nature" },
            new[] { "perfume_floral" },
            new[] { "music" }
        };
        float[] moodMins = { 0.1f, 0.4f, 0f, 0.2f };
        float[] moodMaxs = { 0.4f, 0.8f, 0.3f, 0.6f };

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

            // Update preferences via SerializedObject
            var defSO = new SerializedObject(personalDefs[i]);
            var prefsProp = defSO.FindProperty("preferences");

            var likedProp = prefsProp.FindPropertyRelative("likedTags");
            likedProp.ClearArray();
            for (int j = 0; j < likedTags[i].Length; j++)
            {
                likedProp.InsertArrayElementAtIndex(j);
                likedProp.GetArrayElementAtIndex(j).stringValue = likedTags[i][j];
            }

            var dislikedProp = prefsProp.FindPropertyRelative("dislikedTags");
            dislikedProp.ClearArray();
            for (int j = 0; j < dislikedTags[i].Length; j++)
            {
                dislikedProp.InsertArrayElementAtIndex(j);
                dislikedProp.GetArrayElementAtIndex(j).stringValue = dislikedTags[i][j];
            }

            prefsProp.FindPropertyRelative("preferredMoodMin").floatValue = moodMins[i];
            prefsProp.FindPropertyRelative("preferredMoodMax").floatValue = moodMaxs[i];
            prefsProp.FindPropertyRelative("reactionStrength").floatValue = 1f;

            defSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Commercial ads ───────────────────────────────────────
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

        // ── Newspaper pool ───────────────────────────────────────
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

        // ── Mood Machine Profile ─────────────────────────────────
        string moodDir = $"{baseDir}/Apartment";
        if (!AssetDatabase.IsValidFolder(moodDir))
            AssetDatabase.CreateFolder(baseDir, "Apartment");

        string moodPath = $"{moodDir}/MoodProfile_DatingLoop.asset";
        var existingMood = AssetDatabase.LoadAssetAtPath<MoodMachineProfile>(moodPath);
        MoodMachineProfile moodProfile;

        if (existingMood != null)
        {
            moodProfile = existingMood;
        }
        else
        {
            moodProfile = ScriptableObject.CreateInstance<MoodMachineProfile>();
            var lightGrad = new Gradient();
            lightGrad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0f),
                        new GradientColorKey(new Color(0.6f, 0.65f, 0.8f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            moodProfile.lightColor = lightGrad;

            var ambientGrad = new Gradient();
            ambientGrad.SetKeys(
                new[] { new GradientColorKey(new Color(0.5f, 0.6f, 0.8f), 0f),
                        new GradientColorKey(new Color(0.2f, 0.2f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            moodProfile.ambientColor = ambientGrad;

            var fogGrad = new Gradient();
            fogGrad.SetKeys(
                new[] { new GradientColorKey(new Color(0.7f, 0.75f, 0.8f), 0f),
                        new GradientColorKey(new Color(0.3f, 0.3f, 0.35f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            moodProfile.fogColor = fogGrad;

            AssetDatabase.CreateAsset(moodProfile, moodPath);
        }

        // ── Drink recipes (reuse existing or create) ─────────────
        string drinkDir = $"{baseDir}/DrinkMaking";
        if (!AssetDatabase.IsValidFolder(drinkDir))
            AssetDatabase.CreateFolder(baseDir, "DrinkMaking");

        string[] recipeNames = { "Warm Tea", "Cold Brew", "Herbal Mix" };
        var recipes = new DrinkRecipeDefinition[recipeNames.Length];
        for (int i = 0; i < recipeNames.Length; i++)
        {
            string path = $"{drinkDir}/Recipe_{recipeNames[i].Replace(" ", "_")}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DrinkRecipeDefinition>(path);
            if (existing != null)
            {
                recipes[i] = existing;
                continue;
            }

            var def = ScriptableObject.CreateInstance<DrinkRecipeDefinition>();
            def.drinkName = recipeNames[i];
            def.baseScore = 100;
            def.requiresStir = i > 0;
            def.stirDuration = 2f;
            AssetDatabase.CreateAsset(def, path);
            recipes[i] = def;
        }

        AssetDatabase.SaveAssets();

        return new SOData { pool = pool, moodProfile = moodProfile, recipes = recipes };
    }

    // ════════════════════════════════════════════════════════════════
    // Managers
    // ════════════════════════════════════════════════════════════════

    private struct ManagerData
    {
        public GameClock gameClock;
        public DayManager dayManager;
        public DateSessionManager dateSessionManager;
        public DateEndScreen dateEndScreen;
        public DateSessionHUD dateSessionHUD;
        public PhoneController phoneController;
        public CoffeeTableDelivery coffeeTableDelivery;
        public MoodMachine moodMachine;
        public NewspaperManager newspaperManager;
        public NewspaperHUD newspaperHUD;
    }

    private static ManagerData BuildManagers(
        CameraData camData, FurnitureData furnitureData,
        SOData soData, Light directionalLight,
        NewspaperData newspaperData,
        Transform scissorsVisual, int newspaperLayer)
    {
        var managersGO = new GameObject("Managers");

        // ── DayManager ───────────────────────────────────────────
        var dayMgr = managersGO.AddComponent<DayManager>();
        var dayMgrSO = new SerializedObject(dayMgr);
        dayMgrSO.FindProperty("pool").objectReferenceValue = soData.pool;
        dayMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── GameClock ────────────────────────────────────────────
        var gameClock = managersGO.AddComponent<GameClock>();
        var gcSO = new SerializedObject(gameClock);
        gcSO.FindProperty("dayManager").objectReferenceValue = dayMgr;
        gcSO.ApplyModifiedPropertiesWithoutUndo();

        // ── MoodMachine ──────────────────────────────────────────
        var moodMachine = managersGO.AddComponent<MoodMachine>();
        var mmSO = new SerializedObject(moodMachine);
        mmSO.FindProperty("profile").objectReferenceValue = soData.moodProfile;
        mmSO.FindProperty("directionalLight").objectReferenceValue = directionalLight;
        mmSO.ApplyModifiedPropertiesWithoutUndo();

        // ── DateSessionManager ───────────────────────────────────
        var dateSessionMgr = managersGO.AddComponent<DateSessionManager>();
        var dsmSO = new SerializedObject(dateSessionMgr);
        dsmSO.FindProperty("dateSpawnPoint").objectReferenceValue = furnitureData.dateSpawnPoint;
        dsmSO.FindProperty("couchSeatTarget").objectReferenceValue = furnitureData.couchSeatTarget;
        dsmSO.FindProperty("coffeeTableDeliveryPoint").objectReferenceValue = furnitureData.coffeeTableDeliveryPoint;
        dsmSO.ApplyModifiedPropertiesWithoutUndo();

        // ── PhoneController ──────────────────────────────────────
        var phoneCtrl = furnitureData.phoneTransform.gameObject.AddComponent<PhoneController>();
        var pcSO = new SerializedObject(phoneCtrl);
        if (furnitureData.phoneTransform.childCount > 0)
            pcSO.FindProperty("ringVisual").objectReferenceValue = furnitureData.phoneTransform.GetChild(0).gameObject;
        pcSO.FindProperty("phoneLayer").intValue = 1 << furnitureData.phoneTransform.gameObject.layer;
        pcSO.ApplyModifiedPropertiesWithoutUndo();

        // ── CoffeeTableDelivery ──────────────────────────────────
        var coffeeDelivery = furnitureData.coffeeTableDeliveryPoint.gameObject.AddComponent<CoffeeTableDelivery>();
        var cdSO = new SerializedObject(coffeeDelivery);
        cdSO.FindProperty("drinkSpawnPoint").objectReferenceValue = furnitureData.coffeeTableDeliveryPoint;
        cdSO.ApplyModifiedPropertiesWithoutUndo();

        // ── DateEndScreen (added to managers, UI wired later) ────
        var dateEndScreen = managersGO.AddComponent<DateEndScreen>();

        // ── DateSessionHUD (added to managers, UI wired later) ───
        var dateSessionHUD = managersGO.AddComponent<DateSessionHUD>();

        // ── CutPathEvaluator ───────────────────────────────────────
        var evalComp = managersGO.AddComponent<CutPathEvaluator>();

        // ── ScissorsCutController ─────────────────────────────────
        var scissorsCtrl = managersGO.AddComponent<ScissorsCutController>();
        var scissorsSO = new SerializedObject(scissorsCtrl);
        scissorsSO.FindProperty("surface").objectReferenceValue =
            newspaperData.surfaceQuad.GetComponent<NewspaperSurface>();
        scissorsSO.FindProperty("cam").objectReferenceValue = camData.camera;
        scissorsSO.FindProperty("newspaperLayer").intValue = 1 << newspaperLayer;
        scissorsSO.FindProperty("scissorsVisual").objectReferenceValue = scissorsVisual;
        scissorsSO.ApplyModifiedPropertiesWithoutUndo();

        // ── NewspaperManager ──────────────────────────────────────
        var newsMgr = managersGO.AddComponent<NewspaperManager>();
        var newsMgrSO = new SerializedObject(newsMgr);
        newsMgrSO.FindProperty("dayManager").objectReferenceValue = dayMgr;
        newsMgrSO.FindProperty("scissorsController").objectReferenceValue = scissorsCtrl;
        newsMgrSO.FindProperty("surface").objectReferenceValue =
            newspaperData.surfaceQuad.GetComponent<NewspaperSurface>();
        newsMgrSO.FindProperty("evaluator").objectReferenceValue = evalComp;
        newsMgrSO.FindProperty("mainCamera").objectReferenceValue = camData.camera;
        newsMgrSO.FindProperty("newspaperOverlay").objectReferenceValue = newspaperData.overlayCanvas;
        newsMgrSO.FindProperty("readCamera").objectReferenceValue = camData.newspaperReadCamera;
        newsMgrSO.FindProperty("brain").objectReferenceValue = camData.brain;
        newsMgrSO.FindProperty("newspaperTransform").objectReferenceValue =
            newspaperData.surfaceQuad.transform;

        var personalSlotsProp = newsMgrSO.FindProperty("personalSlots");
        personalSlotsProp.ClearArray();
        for (int i = 0; i < newspaperData.personalSlots.Length; i++)
        {
            personalSlotsProp.InsertArrayElementAtIndex(i);
            personalSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                newspaperData.personalSlots[i];
        }

        var commercialSlotsProp = newsMgrSO.FindProperty("commercialSlots");
        commercialSlotsProp.ClearArray();
        for (int i = 0; i < newspaperData.commercialSlots.Length; i++)
        {
            commercialSlotsProp.InsertArrayElementAtIndex(i);
            commercialSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                newspaperData.commercialSlots[i];
        }

        newsMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── NewspaperHUD ──────────────────────────────────────────
        var newspaperHUD = managersGO.AddComponent<NewspaperHUD>();

        // ── Bed click trigger ────────────────────────────────────
        var bedGO = furnitureData.bedTransform.gameObject;
        var bedCollider = bedGO.GetComponentInChildren<Collider>();
        if (bedCollider == null)
        {
            var bedBox = bedGO.GetComponentInChildren<MeshRenderer>();
            if (bedBox != null)
                bedBox.gameObject.AddComponent<BoxCollider>();
        }

        return new ManagerData
        {
            gameClock = gameClock,
            dayManager = dayMgr,
            dateSessionManager = dateSessionMgr,
            dateEndScreen = dateEndScreen,
            dateSessionHUD = dateSessionHUD,
            phoneController = phoneCtrl,
            coffeeTableDelivery = coffeeDelivery,
            moodMachine = moodMachine,
            newspaperManager = newsMgr,
            newspaperHUD = newspaperHUD
        };
    }

    // ════════════════════════════════════════════════════════════════
    // UI
    // ════════════════════════════════════════════════════════════════

    private static void BuildUI(ManagerData managerData)
    {
        var uiCanvasGO = new GameObject("UI_Canvas");
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 10;

        var scaler = uiCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        uiCanvasGO.AddComponent<GraphicRaycaster>();

        // ── Date Session HUD panel ───────────────────────────────
        var hudPanel = new GameObject("DateHUD");
        hudPanel.transform.SetParent(uiCanvasGO.transform);
        var hudRT = hudPanel.AddComponent<RectTransform>();
        hudRT.anchorMin = new Vector2(0f, 1f);
        hudRT.anchorMax = new Vector2(0.4f, 1f);
        hudRT.offsetMin = new Vector2(10f, -80f);
        hudRT.offsetMax = new Vector2(-10f, -10f);
        hudRT.localScale = Vector3.one;

        var hudBG = hudPanel.AddComponent<Image>();
        hudBG.color = new Color(0f, 0f, 0f, 0.5f);

        // Date name
        var dateNameGO = CreateUIText("DateName", hudPanel.transform,
            new Vector2(0f, 1f), new Vector2(0.5f, 1f),
            new Vector2(10f, -5f), new Vector2(200f, 25f),
            "Date Name", 18f, TextAlignmentOptions.Left);

        // Day text
        var dayTextGO = CreateUIText("DayText", hudPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(1f, 1f),
            new Vector2(-10f, -5f), new Vector2(200f, 25f),
            "Day 1", 18f, TextAlignmentOptions.Right);

        // Affection label
        var affTextGO = CreateUIText("AffectionText", hudPanel.transform,
            new Vector2(0f, 0f), new Vector2(0.3f, 0.6f),
            Vector2.zero, Vector2.zero,
            "50%", 22f, TextAlignmentOptions.Center);
        var affTextRT = affTextGO.GetComponent<RectTransform>();
        affTextRT.offsetMin = new Vector2(5f, 5f);
        affTextRT.offsetMax = new Vector2(-5f, -5f);
        affTextRT.localScale = Vector3.one;

        // Affection bar
        var barGO = new GameObject("AffectionBar");
        barGO.transform.SetParent(hudPanel.transform);
        var barRT = barGO.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0.3f, 0.15f);
        barRT.anchorMax = new Vector2(0.75f, 0.55f);
        barRT.offsetMin = Vector2.zero;
        barRT.offsetMax = Vector2.zero;
        barRT.localScale = Vector3.one;

        var barBGImg = barGO.AddComponent<Image>();
        barBGImg.color = new Color(0.2f, 0.2f, 0.2f);

        var slider = barGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 50f;
        slider.interactable = false;

        // Fill area
        var fillAreaGO = new GameObject("FillArea");
        fillAreaGO.transform.SetParent(barGO.transform);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = Vector2.zero;
        fillAreaRT.offsetMax = Vector2.zero;
        fillAreaRT.localScale = Vector3.one;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillRT.localScale = Vector3.one;

        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.9f, 0.4f, 0.5f);

        slider.fillRect = fillRT;

        // Clock text
        var clockTextGO = CreateUIText("ClockText", hudPanel.transform,
            new Vector2(0.75f, 0f), new Vector2(1f, 0.6f),
            Vector2.zero, Vector2.zero,
            "08:00", 22f, TextAlignmentOptions.Center);
        var clockRT = clockTextGO.GetComponent<RectTransform>();
        clockRT.offsetMin = new Vector2(5f, 5f);
        clockRT.offsetMax = new Vector2(-5f, -5f);
        clockRT.localScale = Vector3.one;

        // Wire DateSessionHUD
        var hudSO = new SerializedObject(managerData.dateSessionHUD);
        hudSO.FindProperty("hudRoot").objectReferenceValue = hudPanel;
        hudSO.FindProperty("dateNameText").objectReferenceValue = dateNameGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("affectionText").objectReferenceValue = affTextGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("affectionBar").objectReferenceValue = slider;
        hudSO.FindProperty("clockText").objectReferenceValue = clockTextGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("dayText").objectReferenceValue = dayTextGO.GetComponent<TMP_Text>();
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Date End Screen ──────────────────────────────────────
        var endPanel = new GameObject("DateEndScreen");
        endPanel.transform.SetParent(uiCanvasGO.transform);
        var endPanelRT = endPanel.AddComponent<RectTransform>();
        endPanelRT.anchorMin = new Vector2(0.2f, 0.15f);
        endPanelRT.anchorMax = new Vector2(0.8f, 0.85f);
        endPanelRT.offsetMin = Vector2.zero;
        endPanelRT.offsetMax = Vector2.zero;
        endPanelRT.localScale = Vector3.one;

        var endBG = endPanel.AddComponent<Image>();
        endBG.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

        var endDateNameGO = CreateUIText("EndDateName", endPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(400f, 40f),
            "Date Name", 32f, TextAlignmentOptions.Center);

        var endGradeGO = CreateUIText("EndGrade", endPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(200f, 80f),
            "A", 72f, TextAlignmentOptions.Center);

        var endAffectionGO = CreateUIText("EndAffection", endPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -20f), new Vector2(300f, 40f),
            "75%", 28f, TextAlignmentOptions.Center);

        var endSummaryGO = CreateUIText("EndSummary", endPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -60f), new Vector2(500f, 40f),
            "A wonderful time together.", 20f, TextAlignmentOptions.Center);

        // Continue button
        var continueBtnGO = CreateUIButton("ContinueButton", endPanel.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 50f), new Vector2(200f, 50f),
            "Continue", new Color(0.3f, 0.5f, 0.3f));

        endPanel.SetActive(false);

        // Wire DateEndScreen
        var endSO = new SerializedObject(managerData.dateEndScreen);
        endSO.FindProperty("screenRoot").objectReferenceValue = endPanel;
        endSO.FindProperty("dateNameText").objectReferenceValue = endDateNameGO.GetComponent<TMP_Text>();
        endSO.FindProperty("affectionScoreText").objectReferenceValue = endAffectionGO.GetComponent<TMP_Text>();
        endSO.FindProperty("gradeText").objectReferenceValue = endGradeGO.GetComponent<TMP_Text>();
        endSO.FindProperty("summaryText").objectReferenceValue = endSummaryGO.GetComponent<TMP_Text>();
        endSO.FindProperty("continueButton").objectReferenceValue = continueBtnGO.GetComponent<Button>();
        endSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Calling UI panel ──────────────────────────────────────
        var callingPanel = CreateUIPanel("CallingUI", uiCanvasGO.transform,
            "Calling...", 32f, new Color(0f, 0f, 0f, 0.7f));

        // Wire NewspaperManager UI
        var newsMgrSO = new SerializedObject(managerData.newspaperManager);
        newsMgrSO.FindProperty("callingUI").objectReferenceValue = callingPanel;
        newsMgrSO.FindProperty("callingText").objectReferenceValue =
            callingPanel.GetComponentInChildren<TMP_Text>();
        newsMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── Day label (top-left) ─────────────────────────────────
        var dayLabelGO = CreateUIText("DayLabel", uiCanvasGO.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(100f, -30f), new Vector2(200f, 40f),
            "Day 1", 28f, TextAlignmentOptions.Left);

        // ── Instruction hint (bottom-center) ─────────────────────
        var instructionGO = CreateUIText("InstructionLabel", uiCanvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(600f, 40f),
            "Draw around a phone number to cut it out", 18f, TextAlignmentOptions.Center);

        // Wire NewspaperHUD
        var newsHudSO = new SerializedObject(managerData.newspaperHUD);
        newsHudSO.FindProperty("dayManager").objectReferenceValue = managerData.dayManager;
        newsHudSO.FindProperty("manager").objectReferenceValue = managerData.newspaperManager;
        newsHudSO.FindProperty("dayLabel").objectReferenceValue =
            dayLabelGO.GetComponent<TMP_Text>();
        newsHudSO.FindProperty("instructionLabel").objectReferenceValue =
            instructionGO.GetComponent<TMP_Text>();
        newsHudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── End Date button (bottom-right) ───────────────────────
        var endDateBtnGO = CreateUIButton("EndDateButton", uiCanvasGO.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-100f, 40f), new Vector2(160f, 50f),
            "End Date", new Color(0.6f, 0.3f, 0.3f));
        UnityEventTools.AddVoidPersistentListener(
            endDateBtnGO.GetComponent<Button>().onClick,
            new UnityAction(managerData.dateSessionManager.EndDate));

        // ── Go To Bed button ─────────────────────────────────────
        var bedBtnGO = CreateUIButton("GoToBedButton", uiCanvasGO.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-100f, 100f), new Vector2(160f, 50f),
            "Go To Bed", new Color(0.3f, 0.3f, 0.5f));
        UnityEventTools.AddVoidPersistentListener(
            bedBtnGO.GetComponent<Button>().onClick,
            new UnityAction(managerData.gameClock.GoToBed));
    }

    // ════════════════════════════════════════════════════════════════
    // UI Helpers
    // ════════════════════════════════════════════════════════════════

    private static GameObject CreateUIText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        return go;
    }

    private static GameObject CreateUIButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size,
        string label, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        go.AddComponent<Button>();

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go;
    }

    // ════════════════════════════════════════════════════════════════
    // Shared Helpers
    // ════════════════════════════════════════════════════════════════

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

    private static TMP_FontAsset s_defaultFont;

    private static TMP_FontAsset GetDefaultTMPFont()
    {
        if (s_defaultFont == null)
        {
            s_defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }
        return s_defaultFont;
    }

    private static GameObject CreateCanvasText(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        var font = GetDefaultTMPFont();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = new Color(0.1f, 0.1f, 0.1f);
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private static GameObject CreateCanvasImage(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.color = Color.white;
        img.preserveAspect = true;

        go.SetActive(false); // hidden by default — slot shows it when sprite assigned

        return go;
    }

    private static GameObject CreateUIPanel(string name, Transform parent,
        string defaultText, float fontSize, Color bgColor)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(500f, 100f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.localScale = Vector3.one;

        var bg = panel.AddComponent<Image>();
        bg.color = bgColor;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 10f);
        textRT.offsetMax = new Vector2(-10f, -10f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        panel.SetActive(false);
        return panel;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
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
                Debug.Log($"[DatingLoopSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[DatingLoopSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }
}
