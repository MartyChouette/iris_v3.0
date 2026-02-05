using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that builds a complete watering prototype scene.
/// Creates SO assets, room + shelf + plants, watering station, UI, and wires everything.
/// Menu: Window > Iris > Build Watering Scene
/// </summary>
public static class WateringSceneBuilder
{
    [MenuItem("Window/Iris/Build Watering Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ───────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Ensure SO folders ─────────────────────────────────────
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "Watering");

        // ── 2. Create ScriptableObject assets ────────────────────────
        var fern = CreatePlant("Fern",
            "A classic shelf fern — likes it moist.",
            new Color(0.72f, 0.45f, 0.20f),       // pot
            new Color(0.55f, 0.40f, 0.25f),        // dry
            new Color(0.30f, 0.22f, 0.12f),        // wet
            new Color(0.50f, 0.38f, 0.22f, 0.7f),  // foam
            new Color(0.18f, 0.55f, 0.18f),        // plant
            0.75f, 0.06f, 0.14f, 2.5f, 0.25f, 0.05f, 100);

        var cactus = CreatePlant("Cactus",
            "Desert dweller — a little water goes a long way.",
            new Color(0.65f, 0.40f, 0.22f),
            new Color(0.60f, 0.48f, 0.30f),
            new Color(0.35f, 0.28f, 0.15f),
            new Color(0.55f, 0.42f, 0.25f, 0.6f),
            new Color(0.15f, 0.45f, 0.12f),
            0.30f, 0.10f, 0.10f, 1.2f, 0.30f, 0.03f, 100);

        var succulent = CreatePlant("Succulent",
            "Plump leaves store water — moderate needs.",
            new Color(0.70f, 0.42f, 0.20f),
            new Color(0.52f, 0.38f, 0.25f),
            new Color(0.28f, 0.20f, 0.12f),
            new Color(0.48f, 0.36f, 0.22f, 0.65f),
            new Color(0.30f, 0.60f, 0.28f),
            0.50f, 0.08f, 0.12f, 1.8f, 0.25f, 0.04f, 100);

        var monstera = CreatePlant("Monstera",
            "Tropical giant — give it a good soak.",
            new Color(0.68f, 0.38f, 0.18f),
            new Color(0.50f, 0.35f, 0.22f),
            new Color(0.25f, 0.18f, 0.10f),
            new Color(0.45f, 0.33f, 0.20f, 0.75f),
            new Color(0.10f, 0.50f, 0.15f),
            0.80f, 0.05f, 0.15f, 2.8f, 0.20f, 0.06f, 100);

        var herbPot = CreatePlant("Herb Pot",
            "Basil and thyme — keep the soil evenly damp.",
            new Color(0.75f, 0.48f, 0.22f),
            new Color(0.53f, 0.40f, 0.27f),
            new Color(0.30f, 0.22f, 0.14f),
            new Color(0.50f, 0.40f, 0.24f, 0.68f),
            new Color(0.25f, 0.65f, 0.20f),
            0.60f, 0.07f, 0.13f, 2.0f, 0.25f, 0.05f, 100);

        var allPlants = new[] { fern, cactus, succulent, monstera, herbPot };
        AssetDatabase.SaveAssets();

        // ── 3. Directional light ─────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 4. Main Camera ───────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.10f, 0.10f);
        cam.fieldOfView = 50f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 1.3f, -0.6f);
        camGO.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        // ── 5. Room geometry ─────────────────────────────────────────
        var roomParent = new GameObject("Room");

        CreateBox("Floor", roomParent.transform,
            new Vector3(0f, 0f, 0f), new Vector3(4f, 0.1f, 4f),
            new Color(0.25f, 0.22f, 0.20f));

        CreateBox("BackWall", roomParent.transform,
            new Vector3(0f, 1.5f, 1.5f), new Vector3(4f, 3f, 0.15f),
            new Color(0.35f, 0.33f, 0.30f));

        CreateBox("LeftWall", roomParent.transform,
            new Vector3(-2f, 1.5f, 0f), new Vector3(0.15f, 3f, 4f),
            new Color(0.33f, 0.31f, 0.29f));

        CreateBox("RightWall", roomParent.transform,
            new Vector3(2f, 1.5f, 0f), new Vector3(0.15f, 3f, 4f),
            new Color(0.33f, 0.31f, 0.29f));

        // ── 6. Shelf ─────────────────────────────────────────────────
        float shelfY = 0.85f;
        CreateBox("Shelf", roomParent.transform,
            new Vector3(0f, shelfY, 0.4f), new Vector3(1.8f, 0.04f, 0.25f),
            new Color(0.50f, 0.35f, 0.20f));

        // ── 7. Plants on shelf ───────────────────────────────────────
        var plantsParent = new GameObject("ShelfPlants");
        plantsParent.transform.SetParent(roomParent.transform);

        var plantVisuals = new Transform[allPlants.Length];
        float plantSpacing = 0.30f;
        float plantStartX = -(allPlants.Length - 1) * plantSpacing * 0.5f;

        for (int i = 0; i < allPlants.Length; i++)
        {
            float px = plantStartX + i * plantSpacing;
            float potTop = shelfY + 0.02f; // just above shelf

            var plantRoot = new GameObject($"Plant_{allPlants[i].plantName}");
            plantRoot.transform.SetParent(plantsParent.transform);
            plantRoot.transform.position = new Vector3(px, potTop, 0.4f);
            plantVisuals[i] = plantRoot.transform;

            // Pot
            CreateBox("Pot", plantRoot.transform,
                new Vector3(0f, 0.03f, 0f), new Vector3(0.06f, 0.06f, 0.06f),
                allPlants[i].potColor);

            // Stem
            CreateBox("Stem", plantRoot.transform,
                new Vector3(0f, 0.10f, 0f), new Vector3(0.012f, 0.08f, 0.012f),
                allPlants[i].plantColor);

            // Leaf left
            CreateBox("LeafL", plantRoot.transform,
                new Vector3(-0.02f, 0.11f, 0f), new Vector3(0.03f, 0.015f, 0.01f),
                allPlants[i].plantColor);

            // Leaf right
            CreateBox("LeafR", plantRoot.transform,
                new Vector3(0.02f, 0.13f, 0f), new Vector3(0.03f, 0.015f, 0.01f),
                allPlants[i].plantColor);
        }

        // ── 8. Highlight ring ────────────────────────────────────────
        var highlightGO = CreateBox("HighlightRing", roomParent.transform,
            new Vector3(plantStartX, shelfY + 0.025f, 0.4f),
            new Vector3(0.08f, 0.005f, 0.08f),
            new Color(1f, 0.9f, 0.2f));
        highlightGO.isStatic = false;

        // ── 9. Watering station (close-up pot) ──────────────────────
        float potWorldHeight = 0.10f;
        float potWorldRadius = 0.04f;

        var stationParent = new GameObject("WateringStation");
        stationParent.transform.position = new Vector3(0f, 0.5f, 0.3f);

        // Pot shell (brown container)
        CreateBox("PotShell", stationParent.transform,
            new Vector3(0f, potWorldHeight * 0.5f, 0f),
            new Vector3(potWorldRadius * 2.5f, potWorldHeight, potWorldRadius * 2.5f),
            new Color(0.65f, 0.40f, 0.20f));

        // Soil box (starts at bottom, scales with water)
        var soilGO = CreateBox("Soil", stationParent.transform,
            new Vector3(0f, 0.001f, 0f),
            new Vector3(potWorldRadius * 2.2f, 0.001f, potWorldRadius * 2.2f),
            fern.dryColor);
        soilGO.isStatic = false;

        // Foam box (bubbly dirt on top of soil)
        var foamGO = CreateBox("Foam", stationParent.transform,
            new Vector3(0f, 0.001f, 0f),
            new Vector3(potWorldRadius * 2.2f, 0.001f, potWorldRadius * 2.2f),
            fern.foamColor);
        foamGO.isStatic = false;

        // Fill line marker (thin green line at ideal water level)
        var fillLineGO = CreateBox("FillLine", stationParent.transform,
            new Vector3(0f, fern.idealWaterLevel * potWorldHeight, 0f),
            new Vector3(potWorldRadius * 2.6f, 0.002f, potWorldRadius * 2.6f),
            new Color(0.2f, 0.8f, 0.2f, 0.8f));
        fillLineGO.isStatic = false;

        // Water line marker (thin blue line tracking current level)
        var waterLineGO = CreateBox("WaterLine", stationParent.transform,
            new Vector3(0f, 0f, 0f),
            new Vector3(potWorldRadius * 2.4f, 0.002f, potWorldRadius * 2.4f),
            new Color(0.2f, 0.4f, 0.9f, 0.8f));
        waterLineGO.isStatic = false;

        // PotController component
        var potCtrl = stationParent.AddComponent<PotController>();
        var potCtrlSO = new SerializedObject(potCtrl);
        potCtrlSO.FindProperty("definition").objectReferenceValue = fern;
        potCtrlSO.FindProperty("soilRenderer").objectReferenceValue = soilGO.GetComponent<Renderer>();
        potCtrlSO.FindProperty("soilTransform").objectReferenceValue = soilGO.transform;
        potCtrlSO.FindProperty("foamRenderer").objectReferenceValue = foamGO.GetComponent<Renderer>();
        potCtrlSO.FindProperty("foamTransform").objectReferenceValue = foamGO.transform;
        potCtrlSO.FindProperty("fillLineMarker").objectReferenceValue = fillLineGO.transform;
        potCtrlSO.FindProperty("waterLineMarker").objectReferenceValue = waterLineGO.transform;
        potCtrlSO.FindProperty("potWorldHeight").floatValue = potWorldHeight;
        potCtrlSO.FindProperty("potWorldRadius").floatValue = potWorldRadius;
        potCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 10. Watering can ─────────────────────────────────────────
        var canParent = new GameObject("WateringCan");
        canParent.transform.position = new Vector3(0.15f, 0.75f, 0.3f);

        // Body
        CreateBox("CanBody", canParent.transform,
            new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.04f, 0.04f),
            new Color(0.25f, 0.55f, 0.55f));

        // Spout
        CreateBox("CanSpout", canParent.transform,
            new Vector3(-0.05f, 0.01f, 0f), new Vector3(0.04f, 0.012f, 0.012f),
            new Color(0.25f, 0.55f, 0.55f));

        canParent.SetActive(false); // hidden until watering state

        // ── 11. Managers GO ──────────────────────────────────────────
        var managersGO = new GameObject("Managers");
        var mgr = managersGO.AddComponent<WateringManager>();
        var hud = managersGO.AddComponent<WateringHUD>();

        // Wire manager via SerializedObject
        var mgrSO = new SerializedObject(mgr);

        // Plant definitions array
        var plantDefsProp = mgrSO.FindProperty("_plantDefinitions");
        plantDefsProp.ClearArray();
        for (int i = 0; i < allPlants.Length; i++)
        {
            plantDefsProp.InsertArrayElementAtIndex(i);
            plantDefsProp.GetArrayElementAtIndex(i).objectReferenceValue = allPlants[i];
        }

        // Plant visuals array
        var plantVisProp = mgrSO.FindProperty("_plantVisuals");
        plantVisProp.ClearArray();
        for (int i = 0; i < plantVisuals.Length; i++)
        {
            plantVisProp.InsertArrayElementAtIndex(i);
            plantVisProp.GetArrayElementAtIndex(i).objectReferenceValue = plantVisuals[i];
        }

        mgrSO.FindProperty("_highlightRing").objectReferenceValue = highlightGO.transform;
        mgrSO.FindProperty("_highlightColor").colorValue = new Color(1f, 0.9f, 0.2f);
        mgrSO.FindProperty("_pot").objectReferenceValue = potCtrl;
        mgrSO.FindProperty("_wateringCanVisual").objectReferenceValue = canParent.transform;
        mgrSO.FindProperty("_mainCamera").objectReferenceValue = cam;

        mgrSO.FindProperty("_browsePosition").vector3Value = new Vector3(0f, 1.3f, -0.6f);
        mgrSO.FindProperty("_browseRotation").vector3Value = new Vector3(20f, 0f, 0f);
        mgrSO.FindProperty("_wateringPosition").vector3Value = new Vector3(0f, 0.8f, -0.1f);
        mgrSO.FindProperty("_wateringRotation").vector3Value = new Vector3(40f, 0f, 0f);
        mgrSO.FindProperty("_cameraBlendSpeed").floatValue = 3f;
        mgrSO.FindProperty("_canIdleAngle").floatValue = 0f;
        mgrSO.FindProperty("_canPourAngle").floatValue = -45f;
        mgrSO.FindProperty("_overflowPenalty").floatValue = 30f;
        mgrSO.FindProperty("_hud").objectReferenceValue = hud;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 12. UI Canvas ────────────────────────────────────────────
        var canvasGO = CreateScreenCanvas("WateringUI_Canvas", managersGO.transform);

        // Plant name (top-center)
        var plantNameLabel = CreateLabel("PlantNameLabel", canvasGO.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(400f, 40f),
            "Select a plant", 24f, TextAlignmentOptions.Center);

        // Description (top-center, below name)
        var descLabel = CreateLabel("DescriptionLabel", canvasGO.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -65f), new Vector2(450f, 30f),
            "", 14f, TextAlignmentOptions.Center);

        // Instruction hint (bottom-center)
        var instructionLabel = CreateLabel("InstructionLabel", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(500f, 30f),
            "A/D: Browse plants   Enter: Select", 16f, TextAlignmentOptions.Center);

        // Water level (right side)
        var waterLabel = CreateLabel("WaterLevelLabel", canvasGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, 30f), new Vector2(150f, 30f),
            "", 18f, TextAlignmentOptions.Right);

        // Foam level (right side, below water)
        var foamLabel = CreateLabel("FoamLevelLabel", canvasGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, -10f), new Vector2(150f, 30f),
            "", 18f, TextAlignmentOptions.Right);

        // Score label (center, used in scoring state)
        var scoreLabel = CreateLabel("ScoreLabel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(500f, 120f),
            "", 20f, TextAlignmentOptions.Center);

        // ── Browse panel ─────────────────────────────────────────────
        var browsePanelGO = new GameObject("BrowsePanel");
        browsePanelGO.transform.SetParent(canvasGO.transform);
        var browsePanelRT = browsePanelGO.AddComponent<RectTransform>();
        browsePanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        browsePanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        browsePanelRT.sizeDelta = new Vector2(10f, 10f);
        browsePanelRT.anchoredPosition = Vector2.zero;
        browsePanelRT.localScale = Vector3.one;

        // ── Overflow warning ─────────────────────────────────────────
        var overflowGO = CreateLabel("OverflowWarning", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 60f), new Vector2(300f, 40f),
            "Overflowing!", 22f, TextAlignmentOptions.Center);
        overflowGO.GetComponent<TMP_Text>().color = new Color(1f, 0.25f, 0.2f);
        overflowGO.SetActive(false);

        // ── Watering panel (Done Watering button) ────────────────────
        var wateringPanelGO = new GameObject("WateringPanel");
        wateringPanelGO.transform.SetParent(canvasGO.transform);
        var wateringRT = wateringPanelGO.AddComponent<RectTransform>();
        wateringRT.anchorMin = new Vector2(1f, 0f);
        wateringRT.anchorMax = new Vector2(1f, 0f);
        wateringRT.pivot = new Vector2(1f, 0f);
        wateringRT.anchoredPosition = new Vector2(-20f, 20f);
        wateringRT.sizeDelta = new Vector2(180f, 40f);
        wateringRT.localScale = Vector3.one;

        var doneBtnGO = BuildActionButton(wateringPanelGO.transform, "Done Watering",
            mgr, nameof(WateringManager.FinishWatering), Vector2.zero,
            new Color(0.3f, 0.55f, 0.3f));

        // ── Scoring panel (Retry + Next Plant) ──────────────────────
        var scoringPanelGO = new GameObject("ScoringPanel");
        scoringPanelGO.transform.SetParent(canvasGO.transform);
        var scoringPanelRT = scoringPanelGO.AddComponent<RectTransform>();
        scoringPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        scoringPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        scoringPanelRT.sizeDelta = new Vector2(350f, 50f);
        scoringPanelRT.anchoredPosition = new Vector2(0f, -80f);
        scoringPanelRT.localScale = Vector3.one;

        var retryBtnGO = BuildActionButton(scoringPanelGO.transform, "Retry",
            mgr, nameof(WateringManager.RetryPlant), new Vector2(-80f, 0f),
            new Color(0.6f, 0.45f, 0.3f));

        var nextBtnGO = BuildActionButton(scoringPanelGO.transform, "Next Plant",
            mgr, nameof(WateringManager.NextPlant), new Vector2(80f, 0f),
            new Color(0.3f, 0.55f, 0.3f));

        // ── Wire HUD ─────────────────────────────────────────────────
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("plantNameLabel").objectReferenceValue = plantNameLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("descriptionLabel").objectReferenceValue = descLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("instructionLabel").objectReferenceValue = instructionLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("waterLevelLabel").objectReferenceValue = waterLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("foamLevelLabel").objectReferenceValue = foamLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("browsePanel").objectReferenceValue = browsePanelGO;
        hudSO.FindProperty("wateringPanel").objectReferenceValue = wateringPanelGO;
        hudSO.FindProperty("scoringPanel").objectReferenceValue = scoringPanelGO;
        hudSO.FindProperty("overflowWarning").objectReferenceValue = overflowGO;
        hudSO.FindProperty("doneButton").objectReferenceValue = doneBtnGO.GetComponent<UnityEngine.UI.Button>();
        hudSO.FindProperty("retryButton").objectReferenceValue = retryBtnGO.GetComponent<UnityEngine.UI.Button>();
        hudSO.FindProperty("nextPlantButton").objectReferenceValue = nextBtnGO.GetComponent<UnityEngine.UI.Button>();
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 13. Save scene ───────────────────────────────────────────
        EnsureFolder("Assets", "Scenes");
        string path = "Assets/Scenes/watering.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[WateringSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // SO creation helper
    // ════════════════════════════════════════════════════════════════════

    private static PlantDefinition CreatePlant(
        string name, string desc,
        Color potColor, Color dryColor, Color wetColor, Color foamColor, Color plantColor,
        float idealWater, float tolerance, float pourRate, float foamMul,
        float foamSettle, float absorption, int baseScore)
    {
        var so = ScriptableObject.CreateInstance<PlantDefinition>();
        so.plantName = name;
        so.description = desc;
        so.potColor = potColor;
        so.dryColor = dryColor;
        so.wetColor = wetColor;
        so.foamColor = foamColor;
        so.plantColor = plantColor;
        so.idealWaterLevel = idealWater;
        so.waterTolerance = tolerance;
        so.pourRate = pourRate;
        so.foamRateMultiplier = foamMul;
        so.foamSettleRate = foamSettle;
        so.absorptionRate = absorption;
        so.baseScore = baseScore;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/Watering/Plant_{name.Replace(" ", "_")}.asset");
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    // ════════════════════════════════════════════════════════════════════
    // UI button builder
    // ════════════════════════════════════════════════════════════════════

    private static GameObject BuildActionButton(Transform parent, string label,
        WateringManager mgr, string methodName, Vector2 position, Color bgColor)
    {
        var btnGO = new GameObject($"Btn_{label.Replace(" ", "")}");
        btnGO.transform.SetParent(parent);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(140f, 36f);
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;

        var img = btnGO.AddComponent<UnityEngine.UI.Image>();
        img.color = bgColor;

        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();

        // Wire onClick to manager method by name
        var method = typeof(WateringManager).GetMethod(methodName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            var action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), mgr, method)
                as UnityEngine.Events.UnityAction;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // Label text
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform);

        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelRT.localScale = Vector3.one;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btnGO;
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
        go.transform.localPosition = position;
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

        // EventSystem for button clicks
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

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
}
