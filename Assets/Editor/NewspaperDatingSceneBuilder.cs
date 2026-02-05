using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Unity.Cinemachine;
using UnityEngine.UI;

/// <summary>
/// Editor utility that programmatically builds the newspaper_dating scene with
/// desk, newspaper with procedural ad slots, scissors cutting, Cinemachine cameras,
/// day system, and UI overlays.
/// Menu: Window > Iris > Build Newspaper Dating Scene
/// </summary>
public static class NewspaperDatingSceneBuilder
{
    private const string NewspaperLayerName = "Newspaper";

    [MenuItem("Window/Iris/Build Newspaper Dating Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int newspaperLayer = EnsureLayer(NewspaperLayerName);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Room geometry ───────────────────────────────────────────
        BuildRoom();

        // ── 3. Desk ────────────────────────────────────────────────────
        BuildDesk();

        // ── 4. Cameras (Cinemachine 3) ─────────────────────────────────
        var camData = BuildCameras();

        // ── 5. Newspaper + ad slots ────────────────────────────────────
        var newspaperData = BuildNewspaper(newspaperLayer);

        // ── 6. Scissors visual ─────────────────────────────────────────
        var scissorsVisual = BuildScissorsVisual();

        // ── 7. ScriptableObject assets ─────────────────────────────────
        var poolDef = CreateScriptableObjectAssets();

        // ── 8. Managers ────────────────────────────────────────────────
        var managerData = BuildManagers(
            camData, newspaperData, scissorsVisual, poolDef, newspaperLayer);

        // ── 9. Screen-space UI ─────────────────────────────────────────
        BuildUI(managerData);

        // ── 10. Save scene ─────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/newspaper_dating.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[NewspaperDatingSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Room Geometry
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        // Floor
        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.01f, 0f), new Vector3(4f, 0.02f, 4f),
            new Color(0.3f, 0.25f, 0.2f));

        // Back wall
        CreateBox("BackWall", parent.transform,
            new Vector3(0f, 1f, 2f), new Vector3(4f, 2f, 0.1f),
            new Color(0.6f, 0.55f, 0.5f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Desk
    // ════════════════════════════════════════════════════════════════════

    private static void BuildDesk()
    {
        var parent = new GameObject("Desk");

        CreateBox("DeskTop", parent.transform,
            new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.05f, 0.8f),
            new Color(0.45f, 0.30f, 0.18f));

        float legH = 0.4f;
        float legT = 0.05f;
        float halfW = 0.55f;
        float halfD = 0.35f;
        float legY = legH / 2f;

        CreateBox("Leg_FL", parent.transform,
            new Vector3(-halfW, legY, -halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("Leg_FR", parent.transform,
            new Vector3(halfW, legY, -halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("Leg_BL", parent.transform,
            new Vector3(-halfW, legY, halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("Leg_BR", parent.transform,
            new Vector3(halfW, legY, halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Cameras
    // ════════════════════════════════════════════════════════════════════

    private struct CameraData
    {
        public GameObject cameraGO;
        public Camera camera;
        public CinemachineBrain brain;
        public CinemachineCamera tableCamera;
        public CinemachineCamera paperCamera;
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
        camGO.transform.position = new Vector3(0f, 1.2f, -0.5f);
        camGO.transform.rotation = Quaternion.Euler(50f, 0f, 0f);

        var brain = camGO.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);

        // Table camera — angled down at desk
        var tableGO = new GameObject("TableCamera");
        var tableCam = tableGO.AddComponent<CinemachineCamera>();
        tableCam.Lens = LensSettings.Default;
        tableCam.Lens.FieldOfView = 60f;
        tableCam.Priority = 20;
        tableGO.transform.position = new Vector3(0f, 1.2f, -0.5f);
        tableGO.transform.rotation = Quaternion.Euler(50f, 0f, 0f);

        // Paper camera — close-up straight down at newspaper
        var paperGO = new GameObject("PaperCamera");
        var paperCam = paperGO.AddComponent<CinemachineCamera>();
        paperCam.Lens = LensSettings.Default;
        paperCam.Lens.FieldOfView = 40f;
        paperCam.Priority = 0;
        paperGO.transform.position = new Vector3(0f, 0.9f, 0f);
        paperGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        return new CameraData
        {
            cameraGO = camGO,
            camera = cam,
            brain = brain,
            tableCamera = tableCam,
            paperCamera = paperCam
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // Newspaper + Ad Slots
    // ════════════════════════════════════════════════════════════════════

    private struct NewspaperData
    {
        public GameObject newspaperParent;
        public GameObject surfaceQuad;
        public Collider clickCollider;
        public NewspaperAdSlot[] personalSlots;
        public NewspaperAdSlot[] commercialSlots;
    }

    private static NewspaperData BuildNewspaper(int newspaperLayer)
    {
        var parent = new GameObject("Newspaper");

        // Newspaper surface quad
        var surfaceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surfaceGO.name = "NewspaperSurface";
        surfaceGO.transform.SetParent(parent.transform);
        surfaceGO.transform.position = new Vector3(0f, 0.426f, 0f);
        surfaceGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        surfaceGO.transform.localScale = new Vector3(0.5f, 0.7f, 1f);
        surfaceGO.layer = newspaperLayer;

        // Replace MeshCollider with BoxCollider
        Object.DestroyImmediate(surfaceGO.GetComponent<MeshCollider>());
        var boxCol = surfaceGO.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(1f, 1f, 0.01f);
        boxCol.center = Vector3.zero;

        // Add NewspaperSurface component
        surfaceGO.AddComponent<NewspaperSurface>();

        // Off-white newspaper material (will be overridden by NewspaperSurface at runtime)
        var rend = surfaceGO.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.92f, 0.90f, 0.85f);
            rend.sharedMaterial = mat;
        }

        // World-space canvas on the newspaper
        var canvasGO = new GameObject("NewspaperCanvas");
        canvasGO.transform.SetParent(parent.transform);
        canvasGO.transform.position = new Vector3(0f, 0.427f, 0f);
        canvasGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(500f, 700f);
        canvasRT.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Newspaper title
        CreateTMPText("Header_Text", canvasGO.transform,
            new Vector2(0f, 310f), new Vector2(480f, 50f),
            "THE DAILY BLOOM", 40f, FontStyles.Bold, TextAlignmentOptions.Center);

        // Divider text
        CreateTMPText("Personals_Label", canvasGO.transform,
            new Vector2(-130f, 260f), new Vector2(200f, 30f),
            "PERSONALS", 16f, FontStyles.Bold, TextAlignmentOptions.Center);

        CreateTMPText("Ads_Label", canvasGO.transform,
            new Vector2(130f, 260f), new Vector2(200f, 30f),
            "CLASSIFIEDS", 16f, FontStyles.Bold, TextAlignmentOptions.Center);

        // Create 4 personal ad slots (left column)
        var personalSlots = new NewspaperAdSlot[4];
        float pStartY = 200f;
        float pHeight = 110f;

        for (int i = 0; i < 4; i++)
        {
            float yPos = pStartY - i * pHeight;
            personalSlots[i] = CreateAdSlot(canvasGO.transform, $"PersonalSlot_{i}",
                new Vector2(-130f, yPos), new Vector2(220f, 100f),
                isPersonal: true, newspaperLayer,
                i, canvasRT.sizeDelta);
        }

        // Create 3 commercial ad slots (right column)
        var commercialSlots = new NewspaperAdSlot[3];
        float cStartY = 200f;
        float cHeight = 140f;

        for (int i = 0; i < 3; i++)
        {
            float yPos = cStartY - i * cHeight;
            commercialSlots[i] = CreateAdSlot(canvasGO.transform, $"CommercialSlot_{i}",
                new Vector2(130f, yPos), new Vector2(220f, 120f),
                isPersonal: false, newspaperLayer,
                i, canvasRT.sizeDelta);
        }

        return new NewspaperData
        {
            newspaperParent = parent,
            surfaceQuad = surfaceGO,
            clickCollider = boxCol,
            personalSlots = personalSlots,
            commercialSlots = commercialSlots
        };
    }

    private static NewspaperAdSlot CreateAdSlot(Transform parent, string name,
        Vector2 anchoredPos, Vector2 size, bool isPersonal, int layer,
        int index, Vector2 canvasSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.layer = layer;

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var slot = go.AddComponent<NewspaperAdSlot>();

        // Name label
        var nameGO = CreateTMPText($"Name", go.transform,
            new Vector2(0f, size.y * 0.35f), new Vector2(size.x - 10f, 24f),
            "", 16f, FontStyles.Bold, TextAlignmentOptions.Left);

        // Ad text label
        var adGO = CreateTMPText($"AdText", go.transform,
            new Vector2(0f, -5f), new Vector2(size.x - 10f, size.y * 0.5f),
            "", 11f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // Phone number label (only visible for personal ads)
        GameObject phoneGO = null;
        if (isPersonal)
        {
            phoneGO = CreateTMPText($"Phone", go.transform,
                new Vector2(0f, -size.y * 0.35f), new Vector2(size.x - 10f, 18f),
                "", 12f, FontStyles.Italic, TextAlignmentOptions.Left);
        }

        // Compute normalized bounds on the newspaper surface (0-1 UV space)
        // Canvas coordinates: center is (0,0), ranges from -canvasSize/2 to +canvasSize/2
        float uMin = (anchoredPos.x - size.x * 0.5f + canvasSize.x * 0.5f) / canvasSize.x;
        float vMin = (anchoredPos.y - size.y * 0.5f + canvasSize.y * 0.5f) / canvasSize.y;
        float uWidth = size.x / canvasSize.x;
        float vHeight = size.y / canvasSize.y;

        // Wire serialized fields
        var so = new SerializedObject(slot);
        so.FindProperty("slotRect").objectReferenceValue = rt;
        so.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
        so.FindProperty("adLabel").objectReferenceValue = adGO.GetComponent<TMP_Text>();
        if (phoneGO != null)
            so.FindProperty("phoneNumberLabel").objectReferenceValue = phoneGO.GetComponent<TMP_Text>();
        so.FindProperty("normalizedBounds").rectValue = new Rect(uMin, vMin, uWidth, vHeight);
        so.ApplyModifiedPropertiesWithoutUndo();

        return slot;
    }

    // ════════════════════════════════════════════════════════════════════
    // Scissors Visual
    // ════════════════════════════════════════════════════════════════════

    private static Transform BuildScissorsVisual()
    {
        var parent = new GameObject("ScissorsVisual");
        parent.transform.position = Vector3.zero;

        // Two thin boxes crossing to form X shape
        CreateBox("Blade_A", parent.transform,
            Vector3.zero, new Vector3(0.04f, 0.003f, 0.003f),
            new Color(0.7f, 0.7f, 0.7f));

        var bladeB = CreateBox("Blade_B", parent.transform,
            Vector3.zero, new Vector3(0.003f, 0.003f, 0.04f),
            new Color(0.7f, 0.7f, 0.7f));

        // Handle dots
        CreateBox("Handle_A", parent.transform,
            new Vector3(0.025f, 0f, 0f), new Vector3(0.012f, 0.005f, 0.012f),
            new Color(0.2f, 0.2f, 0.2f));

        CreateBox("Handle_B", parent.transform,
            new Vector3(-0.025f, 0f, 0f), new Vector3(0.012f, 0.005f, 0.012f),
            new Color(0.2f, 0.2f, 0.2f));

        parent.SetActive(false);
        return parent.transform;
    }

    // ════════════════════════════════════════════════════════════════════
    // ScriptableObject Assets
    // ════════════════════════════════════════════════════════════════════

    private static NewspaperPoolDefinition CreateScriptableObjectAssets()
    {
        string baseDir = "Assets/ScriptableObjects";
        if (!AssetDatabase.IsValidFolder(baseDir))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        string datingDir = $"{baseDir}/Dating";
        if (!AssetDatabase.IsValidFolder(datingDir))
            AssetDatabase.CreateFolder(baseDir, "Dating");

        // Create 4 DatePersonalDefinition assets
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

            // Check if already exists to avoid overwriting
            var existing = AssetDatabase.LoadAssetAtPath<DatePersonalDefinition>(path);
            if (existing != null)
            {
                personalDefs[i] = existing;
                continue;
            }

            var def = ScriptableObject.CreateInstance<DatePersonalDefinition>();
            def.characterName = names[i];
            def.adText = ads[i];
            def.arrivalTimeSec = arrivalTimes[i];
            AssetDatabase.CreateAsset(def, path);
            personalDefs[i] = def;
        }

        // Create 3 CommercialAdDefinition assets
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

        // Create NewspaperPoolDefinition
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

        // Populate pool lists
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
        return pool;
    }

    // ════════════════════════════════════════════════════════════════════
    // Managers
    // ════════════════════════════════════════════════════════════════════

    private struct ManagerData
    {
        public GameObject managersGO;
        public DayManager dayManager;
        public NewspaperManager newspaperManager;
        public ScissorsCutController scissorsController;
        public CutPathEvaluator evaluator;
        public NewspaperHUD hud;
    }

    private static ManagerData BuildManagers(
        CameraData camData, NewspaperData newspaperData,
        Transform scissorsVisual, NewspaperPoolDefinition pool, int newspaperLayer)
    {
        var managersGO = new GameObject("Managers");

        // DayManager
        var dayMgr = managersGO.AddComponent<DayManager>();
        var dayMgrSO = new SerializedObject(dayMgr);
        dayMgrSO.FindProperty("pool").objectReferenceValue = pool;
        dayMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // CutPathEvaluator
        var evalComp = managersGO.AddComponent<CutPathEvaluator>();

        // ScissorsCutController
        var scissorsCtrl = managersGO.AddComponent<ScissorsCutController>();
        var scissorsSO = new SerializedObject(scissorsCtrl);
        scissorsSO.FindProperty("surface").objectReferenceValue =
            newspaperData.surfaceQuad.GetComponent<NewspaperSurface>();
        scissorsSO.FindProperty("cam").objectReferenceValue = camData.camera;
        scissorsSO.FindProperty("newspaperLayer").intValue = 1 << newspaperLayer;
        scissorsSO.FindProperty("scissorsVisual").objectReferenceValue = scissorsVisual;
        scissorsSO.ApplyModifiedPropertiesWithoutUndo();

        // NewspaperManager
        var newsMgr = managersGO.AddComponent<NewspaperManager>();
        var newsMgrSO = new SerializedObject(newsMgr);
        newsMgrSO.FindProperty("dayManager").objectReferenceValue = dayMgr;
        newsMgrSO.FindProperty("scissorsController").objectReferenceValue = scissorsCtrl;
        newsMgrSO.FindProperty("surface").objectReferenceValue =
            newspaperData.surfaceQuad.GetComponent<NewspaperSurface>();
        newsMgrSO.FindProperty("evaluator").objectReferenceValue = evalComp;
        newsMgrSO.FindProperty("mainCamera").objectReferenceValue = camData.camera;
        newsMgrSO.FindProperty("tableCamera").objectReferenceValue = camData.tableCamera;
        newsMgrSO.FindProperty("paperCamera").objectReferenceValue = camData.paperCamera;
        newsMgrSO.FindProperty("brain").objectReferenceValue = camData.brain;
        newsMgrSO.FindProperty("newspaperTransform").objectReferenceValue =
            newspaperData.surfaceQuad.transform;
        newsMgrSO.FindProperty("newspaperClickCollider").objectReferenceValue =
            newspaperData.clickCollider;

        // Wire personal slots array
        var personalSlotsProp = newsMgrSO.FindProperty("personalSlots");
        personalSlotsProp.ClearArray();
        for (int i = 0; i < newspaperData.personalSlots.Length; i++)
        {
            personalSlotsProp.InsertArrayElementAtIndex(i);
            personalSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                newspaperData.personalSlots[i];
        }

        // Wire commercial slots array
        var commercialSlotsProp = newsMgrSO.FindProperty("commercialSlots");
        commercialSlotsProp.ClearArray();
        for (int i = 0; i < newspaperData.commercialSlots.Length; i++)
        {
            commercialSlotsProp.InsertArrayElementAtIndex(i);
            commercialSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                newspaperData.commercialSlots[i];
        }

        newsMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // NewspaperHUD (added later when UI is built)
        var hud = managersGO.AddComponent<NewspaperHUD>();

        return new ManagerData
        {
            managersGO = managersGO,
            dayManager = dayMgr,
            newspaperManager = newsMgr,
            scissorsController = scissorsCtrl,
            evaluator = evalComp,
            hud = hud
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // Screen-Space UI
    // ════════════════════════════════════════════════════════════════════

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

        // Day label (top-left)
        var dayLabelGO = CreateUIText("DayLabel", uiCanvasGO.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(100f, -30f), new Vector2(200f, 40f),
            "Day 1", 28f, TextAlignmentOptions.Left);

        // Instruction hint (bottom-center)
        var instructionGO = CreateUIText("InstructionLabel", uiCanvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(600f, 40f),
            "Click the newspaper to pick it up", 20f, TextAlignmentOptions.Center);

        // "Next Day" button (top-right)
        var nextDayBtn = CreateUIButton("NextDayButton", uiCanvasGO.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-100f, -30f), new Vector2(160f, 50f),
            "Next Day", new Color(0.3f, 0.5f, 0.3f));
        nextDayBtn.SetActive(false);

        // Calling UI panel (center)
        var callingPanel = CreateUIPanel("CallingUI", uiCanvasGO.transform,
            "Calling...", 32f, new Color(0f, 0f, 0f, 0.7f));

        // Timer UI panel (center)
        var timerPanel = CreateUIPanel("TimerUI", uiCanvasGO.transform,
            "0:30", 48f, new Color(0f, 0f, 0f, 0.5f));

        // Arrived UI panel (center)
        var arrivedPanel = CreateUIPanel("ArrivedUI", uiCanvasGO.transform,
            "Your date has arrived!", 36f, new Color(0f, 0f, 0f, 0.7f));

        // Wire NewspaperManager UI fields
        var newsMgrSO = new SerializedObject(managerData.newspaperManager);
        newsMgrSO.FindProperty("callingUI").objectReferenceValue = callingPanel;
        newsMgrSO.FindProperty("callingText").objectReferenceValue =
            callingPanel.GetComponentInChildren<TMP_Text>();
        newsMgrSO.FindProperty("timerUI").objectReferenceValue = timerPanel;
        newsMgrSO.FindProperty("timerText").objectReferenceValue =
            timerPanel.GetComponentInChildren<TMP_Text>();
        newsMgrSO.FindProperty("arrivedUI").objectReferenceValue = arrivedPanel;
        newsMgrSO.FindProperty("arrivedText").objectReferenceValue =
            arrivedPanel.GetComponentInChildren<TMP_Text>();
        newsMgrSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire NewspaperHUD
        var hudSO = new SerializedObject(managerData.hud);
        hudSO.FindProperty("dayManager").objectReferenceValue = managerData.dayManager;
        hudSO.FindProperty("manager").objectReferenceValue = managerData.newspaperManager;
        hudSO.FindProperty("dayLabel").objectReferenceValue =
            dayLabelGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("instructionLabel").objectReferenceValue =
            instructionGO.GetComponent<TMP_Text>();
        hudSO.FindProperty("advanceDayButton").objectReferenceValue = nextDayBtn;
        hudSO.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════════
    // UI Helpers
    // ════════════════════════════════════════════════════════════════════

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

        // Button label
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

    // ════════════════════════════════════════════════════════════════════
    // Shared Helpers
    // ════════════════════════════════════════════════════════════════════

    private static GameObject CreateTMPText(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = new Color(0.1f, 0.1f, 0.1f);
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

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
                Debug.Log($"[NewspaperDatingSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[NewspaperDatingSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }
}
