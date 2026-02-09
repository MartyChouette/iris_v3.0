using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that builds a complete watering prototype scene.
/// Creates SO assets, room + shelf + plants, ambient watering system, UI, and wires everything.
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
            new Color(0.72f, 0.45f, 0.20f),
            new Color(0.55f, 0.40f, 0.25f),
            new Color(0.30f, 0.22f, 0.12f),
            new Color(0.50f, 0.38f, 0.22f, 0.7f),
            new Color(0.18f, 0.55f, 0.18f),
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

        // ── 3. Plants layer ────────────────────────────────────────
        int plantsLayer = EnsureLayer("Plants");

        // ── 4. Directional light ─────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 5. Main Camera ───────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.10f, 0.10f);
        cam.fieldOfView = 50f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 1.3f, -0.6f);
        camGO.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        // ── 6. Room geometry ─────────────────────────────────────────
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

        // ── 7. Shelf ─────────────────────────────────────────────────
        float shelfY = 0.85f;
        CreateBox("Shelf", roomParent.transform,
            new Vector3(0f, shelfY, 0.4f), new Vector3(1.8f, 0.04f, 0.25f),
            new Color(0.50f, 0.35f, 0.20f));

        // ── 8. Plants on shelf (with WaterablePlant markers) ─────────
        var plantsParent = new GameObject("ShelfPlants");
        plantsParent.transform.SetParent(roomParent.transform);

        float plantSpacing = 0.30f;
        float plantStartX = -(allPlants.Length - 1) * plantSpacing * 0.5f;

        for (int i = 0; i < allPlants.Length; i++)
        {
            float px = plantStartX + i * plantSpacing;
            float potTop = shelfY + 0.02f;

            var plantRoot = new GameObject($"Plant_{allPlants[i].plantName}");
            plantRoot.transform.SetParent(plantsParent.transform);
            plantRoot.transform.position = new Vector3(px, potTop, 0.4f);
            plantRoot.layer = plantsLayer;
            plantRoot.isStatic = false;

            // WaterablePlant marker
            var wp = plantRoot.AddComponent<WaterablePlant>();
            wp.definition = allPlants[i];

            // Pot (also on Plants layer for raycasting)
            var potGO = CreateBox("Pot", plantRoot.transform,
                new Vector3(0f, 0.03f, 0f), new Vector3(0.06f, 0.06f, 0.06f),
                allPlants[i].potColor);
            potGO.layer = plantsLayer;
            potGO.isStatic = false;

            // Add a BoxCollider to the plant root for easier clicking
            var col = plantRoot.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.07f, 0f);
            col.size = new Vector3(0.08f, 0.14f, 0.08f);

            // Stem
            var stemGO = CreateBox("Stem", plantRoot.transform,
                new Vector3(0f, 0.10f, 0f), new Vector3(0.012f, 0.08f, 0.012f),
                allPlants[i].plantColor);
            stemGO.layer = plantsLayer;

            // Leaf left
            var leafL = CreateBox("LeafL", plantRoot.transform,
                new Vector3(-0.02f, 0.11f, 0f), new Vector3(0.03f, 0.015f, 0.01f),
                allPlants[i].plantColor);
            leafL.layer = plantsLayer;

            // Leaf right
            var leafR = CreateBox("LeafR", plantRoot.transform,
                new Vector3(0.02f, 0.13f, 0f), new Vector3(0.03f, 0.015f, 0.01f),
                allPlants[i].plantColor);
            leafR.layer = plantsLayer;
        }

        // ── 9. PotController (hidden — simulation only) ──────────────
        var potHiddenGO = new GameObject("PotController");
        var potCtrl = potHiddenGO.AddComponent<PotController>();
        var potCtrlSO = new SerializedObject(potCtrl);
        potCtrlSO.FindProperty("potWorldHeight").floatValue = 0.10f;
        potCtrlSO.FindProperty("potWorldRadius").floatValue = 0.04f;
        potCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 10. Managers GO ──────────────────────────────────────────
        var managersGO = new GameObject("Managers");
        var mgr = managersGO.AddComponent<WateringManager>();
        var hud = managersGO.AddComponent<WateringHUD>();

        // Wire manager
        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("_plantLayer").intValue = 1 << plantsLayer;
        mgrSO.FindProperty("_pot").objectReferenceValue = potCtrl;
        mgrSO.FindProperty("_hud").objectReferenceValue = hud;
        mgrSO.FindProperty("_mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("_scoreDisplayTime").floatValue = 2f;
        mgrSO.FindProperty("_overflowPenalty").floatValue = 30f;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 11. UI Canvas ────────────────────────────────────────────
        var canvasGO = CreateScreenCanvas("WateringUI_Canvas", managersGO.transform);

        // HUD panel container (hidden when idle)
        var hudPanelGO = new GameObject("WateringHUDPanel");
        hudPanelGO.transform.SetParent(canvasGO.transform);
        var hudPanelRT = hudPanelGO.AddComponent<RectTransform>();
        hudPanelRT.anchorMin = Vector2.zero;
        hudPanelRT.anchorMax = Vector2.one;
        hudPanelRT.offsetMin = Vector2.zero;
        hudPanelRT.offsetMax = Vector2.zero;
        hudPanelRT.localScale = Vector3.one;

        // Plant name (top-center)
        var plantNameLabel = CreateLabel("PlantNameLabel", hudPanelGO.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(400f, 40f),
            "", 24f, TextAlignmentOptions.Center);

        // Water level (right side)
        var waterLabel = CreateLabel("WaterLevelLabel", hudPanelGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, 30f), new Vector2(150f, 30f),
            "", 18f, TextAlignmentOptions.Right);

        // Foam level (right side, below water)
        var foamLabel = CreateLabel("FoamLevelLabel", hudPanelGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, -10f), new Vector2(150f, 30f),
            "", 18f, TextAlignmentOptions.Right);

        // Target hint (right side, below foam)
        var targetLabel = CreateLabel("TargetLabel", hudPanelGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, -50f), new Vector2(150f, 30f),
            "", 16f, TextAlignmentOptions.Right);

        // Overflow warning
        var overflowGO = CreateLabel("OverflowWarning", hudPanelGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 60f), new Vector2(300f, 40f),
            "Overflowing!", 22f, TextAlignmentOptions.Center);
        overflowGO.GetComponent<TMP_Text>().color = new Color(1f, 0.25f, 0.2f);
        overflowGO.SetActive(false);

        // Score label (center)
        var scoreLabel = CreateLabel("ScoreLabel", hudPanelGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(500f, 120f),
            "", 20f, TextAlignmentOptions.Center);

        // Instruction hint (bottom-center, always visible)
        CreateLabel("InstructionLabel", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(500f, 30f),
            "Click on a plant to water it \u2014 hold to pour!", 16f, TextAlignmentOptions.Center);

        // ── Wire HUD ─────────────────────────────────────────────────
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("plantNameLabel").objectReferenceValue = plantNameLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("waterLevelLabel").objectReferenceValue = waterLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("foamLevelLabel").objectReferenceValue = foamLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("targetLabel").objectReferenceValue = targetLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("overflowWarning").objectReferenceValue = overflowGO;
        hudSO.FindProperty("hudPanel").objectReferenceValue = hudPanelGO;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 12. Save scene ───────────────────────────────────────────
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

    private static int EnsureLayer(string layerName)
    {
        for (int i = 0; i < 32; i++)
        {
            string existing = LayerMask.LayerToName(i);
            if (existing == layerName) return i;
        }

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[WateringSceneBuilder] Created layer '{layerName}' at index {i}");
                return i;
            }
        }

        Debug.LogWarning($"[WateringSceneBuilder] No empty layer slot for '{layerName}', using Default (0)");
        return 0;
    }

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        if (!AssetDatabase.IsValidFolder($"{parentFolder}/{newFolder}"))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }
    }
}
