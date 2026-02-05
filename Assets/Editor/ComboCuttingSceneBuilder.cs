using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor utility that programmatically builds the combo_economy scene with
/// 3 flowers, ScissorStation, CuttingPlaneController, ComboManager,
/// ClientQueueManager, ComboHUD, ClientOrderUI, and FlowerGradingUI.
/// Menu: Window > Iris > Build Combo Economy Scene
/// </summary>
public static class ComboCuttingSceneBuilder
{
    [MenuItem("Window/Iris/Build Combo Economy Scene")]
    public static void Build()
    {
        // == 0. New empty scene ================================================
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // == 1. Directional light ==============================================
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.95f, 0.85f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // == 2. Main Camera + AudioListener ====================================
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
        cam.fieldOfView = 65f;
        camGO.transform.position = new Vector3(0f, 0.8f, -2.0f);
        camGO.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
        camGO.AddComponent<AudioListener>();

        // == 3. Room geometry (slightly larger) =================================
        var room = new GameObject("Room");
        CreateBox("Floor", room.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(5f, 0.1f, 5f),
            new Color(0.25f, 0.25f, 0.25f));
        CreateBox("BackWall", room.transform,
            new Vector3(0f, 1.2f, 2.5f), new Vector3(5f, 2.4f, 0.1f),
            new Color(0.3f, 0.3f, 0.32f));
        CreateBox("LeftWall", room.transform,
            new Vector3(-2.5f, 1.2f, 0f), new Vector3(0.1f, 2.4f, 5f),
            new Color(0.32f, 0.30f, 0.30f));
        CreateBox("RightWall", room.transform,
            new Vector3(2.5f, 1.2f, 0f), new Vector3(0.1f, 2.4f, 5f),
            new Color(0.32f, 0.30f, 0.30f));

        // == 4. Three flowers in a row ==========================================
        float stemHeight = 1.0f;
        var flower0 = BuildFlower("Flower_Left", stemHeight, 3, 2,
            new Vector3(-0.8f, 0.5f, 0f));
        var flower1 = BuildFlower("Flower_Center", stemHeight, 3, 2,
            new Vector3(0f, 0.5f, 0f));
        var flower2 = BuildFlower("Flower_Right", stemHeight, 3, 2,
            new Vector3(0.8f, 0.5f, 0f));

        // == 5. ScissorStation + CuttingPlaneController =========================
        var scissorGO = new GameObject("ScissorStation");
        var scissorStation = scissorGO.AddComponent<ScissorStation>();

        var planeControllerGO = new GameObject("CuttingPlaneController");
        planeControllerGO.transform.position = new Vector3(0f, stemHeight * 0.5f, 0f);
        var planeController = planeControllerGO.AddComponent<CuttingPlaneController>();

        // Wire scissor -> planeController
        var ssSO = new SerializedObject(scissorStation);
        ssSO.FindProperty("planeController").objectReferenceValue = planeController;
        ssSO.ApplyModifiedPropertiesWithoutUndo();

        // == 6. Managers object =================================================
        var managersGO = new GameObject("Managers");

        // ComboManager
        var comboManager = managersGO.AddComponent<ComboManager>();
        var comboSO = new SerializedObject(comboManager);
        comboSO.FindProperty("autoDiscoverSessions").boolValue = true;
        comboSO.ApplyModifiedPropertiesWithoutUndo();

        // ClientQueueManager
        var queueManager = managersGO.AddComponent<ClientQueueManager>();

        // ComboHUD
        var comboHUD = managersGO.AddComponent<ComboHUD>();

        // ClientOrderUI
        var clientOrderUI = managersGO.AddComponent<ClientOrderUI>();

        // == 7. Create ClientOrder ScriptableObject assets ======================
        EnsureFolder("Assets/ScriptableObjects", "ClientOrders");

        var orders = new List<ClientOrder>();

        orders.Add(CreateClientOrder(
            "Mrs_Thornberry",
            "Mrs. Thornberry",
            "A classic trim, nothing fancy. Just clean and tidy.",
            30,
            FlowerTypeDefinition.Difficulty.Normal,
            idealStemLength: 0.5f,
            idealCutAngle: 45f));

        orders.Add(CreateClientOrder(
            "Lord_Petalsworth",
            "Lord Petalsworth",
            "Precisely 45 degrees, not a hair more!",
            60,
            FlowerTypeDefinition.Difficulty.Hard,
            idealStemLength: 0.4f,
            idealCutAngle: 45f,
            requireAllPetals: true));

        orders.Add(CreateClientOrder(
            "Daisy_Mae",
            "Daisy Mae",
            "Keep it natural, leaves and all. I like 'em leafy.",
            40,
            FlowerTypeDefinition.Difficulty.Easy,
            minLeavesRequired: 3));

        orders.Add(CreateClientOrder(
            "The_Collector",
            "The Collector",
            "Short stem, pristine petals. Museum quality.",
            80,
            FlowerTypeDefinition.Difficulty.VeryHard,
            idealStemLength: 0.3f,
            requireAllPetals: true));

        orders.Add(CreateClientOrder(
            "Bud",
            "Bud",
            "Anything works! I'm easy to please.",
            20,
            FlowerTypeDefinition.Difficulty.VeryEasy));

        // Wire orders into ClientQueueManager.orderPool
        var queueSO = new SerializedObject(queueManager);
        var poolProp = queueSO.FindProperty("orderPool");
        poolProp.ClearArray();
        for (int i = 0; i < orders.Count; i++)
        {
            poolProp.InsertArrayElementAtIndex(i);
            poolProp.GetArrayElementAtIndex(i).objectReferenceValue = orders[i];
        }
        queueSO.ApplyModifiedPropertiesWithoutUndo();

        // == 8. Screen-space canvas + UI ========================================
        var uiCanvas = CreateScreenCanvas("UI_Canvas");

        // ── ComboHUD area (top-center) ────────────────────────────────────────
        var comboHudRoot = new GameObject("ComboHUD_Root");
        comboHudRoot.transform.SetParent(uiCanvas.transform);

        var comboHudRT = comboHudRoot.AddComponent<RectTransform>();
        comboHudRT.anchorMin = new Vector2(0.5f, 1f);
        comboHudRT.anchorMax = new Vector2(0.5f, 1f);
        comboHudRT.anchoredPosition = new Vector2(0f, -60f);
        comboHudRT.sizeDelta = new Vector2(300f, 100f);
        comboHudRT.localScale = Vector3.one;

        var comboHudCG = comboHudRoot.AddComponent<CanvasGroup>();
        comboHudCG.alpha = 0f;

        var comboLabelGO = CreateLabel("ComboLabel", comboHudRoot.transform,
            new Vector2(0f, 25f), new Vector2(300f, 35f),
            "", 28f, TextAlignmentOptions.Center);

        var multiplierLabelGO = CreateLabel("MultiplierLabel", comboHudRoot.transform,
            new Vector2(0f, -5f), new Vector2(300f, 30f),
            "", 22f, TextAlignmentOptions.Center);

        var timerLabelGO = CreateLabel("TimerLabel", comboHudRoot.transform,
            new Vector2(0f, -35f), new Vector2(300f, 25f),
            "", 18f, TextAlignmentOptions.Center);

        // Wire ComboHUD
        var comboHudSO = new SerializedObject(comboHUD);
        comboHudSO.FindProperty("combo").objectReferenceValue = comboManager;
        comboHudSO.FindProperty("comboLabel").objectReferenceValue = comboLabelGO.GetComponent<TMP_Text>();
        comboHudSO.FindProperty("multiplierLabel").objectReferenceValue = multiplierLabelGO.GetComponent<TMP_Text>();
        comboHudSO.FindProperty("timerLabel").objectReferenceValue = timerLabelGO.GetComponent<TMP_Text>();
        comboHudSO.FindProperty("root").objectReferenceValue = comboHudCG;
        comboHudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── ClientOrderUI area (right panel) ──────────────────────────────────
        var orderPanelGO = new GameObject("OrderPanel");
        orderPanelGO.transform.SetParent(uiCanvas.transform);

        var orderPanelRT = orderPanelGO.AddComponent<RectTransform>();
        orderPanelRT.anchorMin = new Vector2(1f, 0.5f);
        orderPanelRT.anchorMax = new Vector2(1f, 0.5f);
        orderPanelRT.anchoredPosition = new Vector2(-140f, 0f);
        orderPanelRT.sizeDelta = new Vector2(260f, 400f);
        orderPanelRT.localScale = Vector3.one;

        var orderPanelCG = orderPanelGO.AddComponent<CanvasGroup>();

        // Semi-transparent background for order panel
        var orderPanelBG = orderPanelGO.AddComponent<UnityEngine.UI.Image>();
        orderPanelBG.color = new Color(0f, 0f, 0f, 0.4f);

        // Money label (top-right area)
        var moneyLabelGO = CreateLabel("MoneyLabel", orderPanelGO.transform,
            new Vector2(0f, 175f), new Vector2(240f, 30f),
            "$0", 24f, TextAlignmentOptions.Right);

        // Rep label (below money)
        var repLabelGO = CreateLabel("RepLabel", orderPanelGO.transform,
            new Vector2(0f, 145f), new Vector2(240f, 25f),
            "Rep: 50", 18f, TextAlignmentOptions.Right);

        // Active order text
        var activeOrderGO = CreateLabel("ActiveOrderText", orderPanelGO.transform,
            new Vector2(0f, 100f), new Vector2(240f, 40f),
            "Select an order...", 16f, TextAlignmentOptions.Center);

        // 3 order slot texts (clickable)
        var orderSlotTexts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float yOffset = 40f - i * 80f;
            var slotGO = CreateLabel($"OrderSlot_{i}", orderPanelGO.transform,
                new Vector2(0f, yOffset), new Vector2(240f, 70f),
                $"Order Slot {i}", 14f, TextAlignmentOptions.TopLeft);

            // Background image for click target
            var slotBG = slotGO.AddComponent<UnityEngine.UI.Image>();
            slotBG.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);

            // Ensure TMP renders on top of the background image
            var slotTMP = slotGO.GetComponent<TMP_Text>();
            // Move text to a child so image + text coexist properly
            // Actually, TMP_Text on the same GO as Image works fine for click targets.
            // The text will render on top of the image.

            // Button component for click
            var btn = slotGO.AddComponent<UnityEngine.UI.Button>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                btn.onClick, clientOrderUI.OnOrderSlotClicked, i);

            orderSlotTexts[i] = slotTMP;
        }

        // Wire ClientOrderUI
        var orderUISO = new SerializedObject(clientOrderUI);
        orderUISO.FindProperty("queue").objectReferenceValue = queueManager;
        orderUISO.FindProperty("combo").objectReferenceValue = comboManager;
        orderUISO.FindProperty("moneyLabel").objectReferenceValue = moneyLabelGO.GetComponent<TMP_Text>();
        orderUISO.FindProperty("repLabel").objectReferenceValue = repLabelGO.GetComponent<TMP_Text>();
        orderUISO.FindProperty("activeOrderText").objectReferenceValue = activeOrderGO.GetComponent<TMP_Text>();
        orderUISO.FindProperty("orderPanel").objectReferenceValue = orderPanelCG;

        // Wire orderSlotTexts array
        var slotsProp = orderUISO.FindProperty("orderSlotTexts");
        slotsProp.ClearArray();
        for (int i = 0; i < orderSlotTexts.Length; i++)
        {
            slotsProp.InsertArrayElementAtIndex(i);
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = orderSlotTexts[i];
        }
        orderUISO.ApplyModifiedPropertiesWithoutUndo();

        // == 9. FlowerGradingUI (wired to first flower's session) ===============
        var gradingGO = new GameObject("FlowerGradingUI");
        gradingGO.transform.SetParent(uiCanvas.transform);
        var gradingUI = gradingGO.AddComponent<FlowerGradingUI>();

        // Grading panel CanvasGroup (starts hidden)
        var gradingPanelGO = new GameObject("GradingPanel");
        gradingPanelGO.transform.SetParent(gradingGO.transform);

        var gradingPanelRT = gradingPanelGO.AddComponent<RectTransform>();
        gradingPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        gradingPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        gradingPanelRT.anchoredPosition = Vector2.zero;
        gradingPanelRT.sizeDelta = new Vector2(400f, 250f);
        gradingPanelRT.localScale = Vector3.one;

        var gradingCG = gradingPanelGO.AddComponent<CanvasGroup>();
        gradingCG.alpha = 0f;

        var gradingBG = gradingPanelGO.AddComponent<UnityEngine.UI.Image>();
        gradingBG.color = new Color(0f, 0f, 0f, 0.8f);

        var titleLabelGO = CreateLabel("TitleLabel", gradingPanelGO.transform,
            new Vector2(0f, 80f), new Vector2(380f, 40f),
            "", 28f, TextAlignmentOptions.Center);

        var scoreLabelGO = CreateLabel("ScoreLabel", gradingPanelGO.transform,
            new Vector2(0f, 30f), new Vector2(380f, 35f),
            "", 22f, TextAlignmentOptions.Center);

        var daysLabelGO = CreateLabel("DaysLabel", gradingPanelGO.transform,
            new Vector2(0f, -10f), new Vector2(380f, 35f),
            "", 22f, TextAlignmentOptions.Center);

        var reasonLabelGO = CreateLabel("ReasonLabel", gradingPanelGO.transform,
            new Vector2(0f, -55f), new Vector2(380f, 30f),
            "", 16f, TextAlignmentOptions.Center);

        var gradingSO = new SerializedObject(gradingUI);
        gradingSO.FindProperty("session").objectReferenceValue = flower0.session;
        gradingSO.FindProperty("root").objectReferenceValue = gradingCG;
        gradingSO.FindProperty("titleLabel").objectReferenceValue = titleLabelGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabelGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("daysLabel").objectReferenceValue = daysLabelGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("reasonLabel").objectReferenceValue = reasonLabelGO.GetComponent<TMP_Text>();
        gradingSO.ApplyModifiedPropertiesWithoutUndo();

        gradingPanelGO.SetActive(false);

        // == 10. Log note about runtime session registration ====================
        Debug.Log("[ComboCuttingSceneBuilder] ComboManager.autoDiscoverSessions is ON. " +
                  "All 3 FlowerSessionControllers will be auto-registered at runtime Start().");

        // == 11. Save scene =====================================================
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/combo_economy.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[ComboCuttingSceneBuilder] Scene saved to {path}");
    }

    // ========================================================================
    // Flower builder (inline, creates cylinder stems + cube parts)
    // ========================================================================

    private struct FlowerBuildResult
    {
        public GameObject root;
        public FlowerStemRuntime stem;
        public FlowerGameBrain brain;
        public FlowerSessionController session;
        public IdealFlowerDefinition ideal;
    }

    private static FlowerBuildResult BuildFlower(string name, float stemHeight,
        int leafCount, int petalCount, Vector3 position)
    {
        var root = new GameObject($"{name}_Flower");
        root.transform.position = position;

        // Stem
        var stemGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stemGO.name = "Stem";
        stemGO.transform.SetParent(root.transform, false);
        stemGO.transform.localPosition = Vector3.zero;
        stemGO.transform.localScale = new Vector3(0.05f, stemHeight * 0.5f, 0.05f);
        SetColor(stemGO, new Color(0.2f, 0.55f, 0.15f));

        var stemRuntime = stemGO.AddComponent<FlowerStemRuntime>();
        var stemRb = stemGO.AddComponent<Rigidbody>();
        stemRb.isKinematic = true;

        // Stem child transforms
        var anchor = new GameObject("StemAnchor");
        anchor.transform.SetParent(stemGO.transform, false);
        anchor.transform.localPosition = Vector3.up * 0.5f;
        stemRuntime.StemAnchor = anchor.transform;

        var tip = new GameObject("StemTip");
        tip.transform.SetParent(stemGO.transform, false);
        tip.transform.localPosition = Vector3.down * 0.5f;
        stemRuntime.StemTip = tip.transform;

        var cutNormal = new GameObject("CutNormalRef");
        cutNormal.transform.SetParent(stemGO.transform, false);
        stemRuntime.cutNormalRef = cutNormal.transform;

        // Crown
        var crownGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crownGO.name = "Crown";
        crownGO.transform.SetParent(root.transform, false);
        crownGO.transform.localPosition = new Vector3(0f, stemHeight * 0.5f, 0f);
        crownGO.transform.localScale = Vector3.one * 0.08f;
        SetColor(crownGO, new Color(0.9f, 0.8f, 0.2f));

        var crownRb = crownGO.AddComponent<Rigidbody>();
        crownRb.isKinematic = false;
        crownRb.interpolation = RigidbodyInterpolation.Interpolate;

        var crownPart = crownGO.AddComponent<FlowerPartRuntime>();
        var crownSO = new SerializedObject(crownPart);
        crownSO.FindProperty("PartId").stringValue = "Crown";
        crownSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Crown;
        crownSO.FindProperty("canCauseGameOver").boolValue = true;
        crownSO.ApplyModifiedPropertiesWithoutUndo();

        var partsList = new List<FlowerPartRuntime> { crownPart };

        // Leaves
        for (int i = 0; i < leafCount; i++)
        {
            var leafGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafGO.name = $"Leaf_{i}";
            leafGO.transform.SetParent(root.transform, false);

            float angle = i * (360f / leafCount) * Mathf.Deg2Rad;
            float height = Mathf.Lerp(0.2f, 0.6f,
                leafCount > 1 ? (float)i / (leafCount - 1) : 0.5f) * stemHeight;
            leafGO.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 0.12f,
                -stemHeight * 0.5f + height,
                Mathf.Sin(angle) * 0.12f);
            leafGO.transform.localScale = new Vector3(0.08f, 0.02f, 0.12f);
            SetColor(leafGO, new Color(0.25f, 0.6f, 0.2f));

            var leafRb = leafGO.AddComponent<Rigidbody>();
            leafRb.isKinematic = false;
            leafRb.interpolation = RigidbodyInterpolation.Interpolate;

            var leafPart = leafGO.AddComponent<FlowerPartRuntime>();
            var leafPartSO = new SerializedObject(leafPart);
            leafPartSO.FindProperty("PartId").stringValue = $"Leaf_{i}";
            leafPartSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Leaf;
            leafPartSO.FindProperty("contributesToScore").boolValue = true;
            leafPartSO.ApplyModifiedPropertiesWithoutUndo();

            var tether = leafGO.AddComponent<XYTetherJoint>();
            var tetherSO = new SerializedObject(tether);
            tetherSO.FindProperty("connectedBody").objectReferenceValue = stemRb;
            tetherSO.FindProperty("breakForce").floatValue = 800f;
            tetherSO.FindProperty("spring").floatValue = 1200f;
            tetherSO.FindProperty("damper").floatValue = 60f;
            tetherSO.ApplyModifiedPropertiesWithoutUndo();

            partsList.Add(leafPart);
        }

        // Petals
        for (int i = 0; i < petalCount; i++)
        {
            var petalGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            petalGO.name = $"Petal_{i}";
            petalGO.transform.SetParent(root.transform, false);

            float angle = i * (360f / petalCount) * Mathf.Deg2Rad;
            float height = Mathf.Lerp(0.7f, 0.9f,
                petalCount > 1 ? (float)i / (petalCount - 1) : 0.5f) * stemHeight;
            petalGO.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * 0.08f,
                -stemHeight * 0.5f + height,
                Mathf.Sin(angle) * 0.08f);
            petalGO.transform.localScale = new Vector3(0.06f, 0.015f, 0.1f);
            SetColor(petalGO, new Color(0.9f, 0.4f, 0.5f));

            var petalRb = petalGO.AddComponent<Rigidbody>();
            petalRb.isKinematic = false;
            petalRb.interpolation = RigidbodyInterpolation.Interpolate;

            var petalPart = petalGO.AddComponent<FlowerPartRuntime>();
            var petalPartSO = new SerializedObject(petalPart);
            petalPartSO.FindProperty("PartId").stringValue = $"Petal_{i}";
            petalPartSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Petal;
            petalPartSO.FindProperty("contributesToScore").boolValue = true;
            petalPartSO.ApplyModifiedPropertiesWithoutUndo();

            var tether = petalGO.AddComponent<XYTetherJoint>();
            var tetherSO = new SerializedObject(tether);
            tetherSO.FindProperty("connectedBody").objectReferenceValue = stemRb;
            tetherSO.FindProperty("breakForce").floatValue = 800f;
            tetherSO.FindProperty("spring").floatValue = 1200f;
            tetherSO.FindProperty("damper").floatValue = 60f;
            tetherSO.ApplyModifiedPropertiesWithoutUndo();

            partsList.Add(petalPart);
        }

        // Brain
        var brain = root.AddComponent<FlowerGameBrain>();
        var brainSO = new SerializedObject(brain);
        brainSO.FindProperty("stem").objectReferenceValue = stemRuntime;

        var partsArrayProp = brainSO.FindProperty("parts");
        partsArrayProp.ClearArray();
        for (int i = 0; i < partsList.Count; i++)
        {
            partsArrayProp.InsertArrayElementAtIndex(i);
            partsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = partsList[i];
        }

        // Create IdealFlowerDefinition SO
        EnsureFolder("Assets/ScriptableObjects", "Flowers");
        string idealPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/Flowers/Ideal_{name}.asset");
        var ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();
        var idealSO = new SerializedObject(ideal);
        idealSO.FindProperty("idealStemLength").floatValue = stemHeight * 0.5f;
        idealSO.FindProperty("idealCutAngleDeg").floatValue = 45f;
        idealSO.FindProperty("stemScoreWeight").floatValue = 0.3f;
        idealSO.FindProperty("cutAngleScoreWeight").floatValue = 0.3f;

        // Part rules
        var rulesProp = idealSO.FindProperty("partRules");
        rulesProp.ClearArray();
        for (int i = 0; i < partsList.Count; i++)
        {
            rulesProp.InsertArrayElementAtIndex(i);
            var elem = rulesProp.GetArrayElementAtIndex(i);
            var partSO2 = new SerializedObject(partsList[i]);
            elem.FindPropertyRelative("partId").stringValue = partSO2.FindProperty("PartId").stringValue;
            elem.FindPropertyRelative("kind").enumValueIndex = partSO2.FindProperty("kind").enumValueIndex;
            elem.FindPropertyRelative("contributesToScore").boolValue = true;
            elem.FindPropertyRelative("canCauseGameOver").boolValue =
                partSO2.FindProperty("kind").enumValueIndex == (int)FlowerPartKind.Crown;
            elem.FindPropertyRelative("scoreWeight").floatValue = 1f;
        }
        idealSO.ApplyModifiedProperties();
        AssetDatabase.CreateAsset(ideal, idealPath);

        brainSO.FindProperty("ideal").objectReferenceValue = ideal;
        brainSO.ApplyModifiedPropertiesWithoutUndo();

        // Session
        var session = root.AddComponent<FlowerSessionController>();
        var sessionSO = new SerializedObject(session);
        sessionSO.FindProperty("brain").objectReferenceValue = brain;

        // FlowerTypeDefinition
        string typePath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/Flowers/Type_{name}.asset");
        var typeDef = ScriptableObject.CreateInstance<FlowerTypeDefinition>();
        var typeSO = new SerializedObject(typeDef);
        typeSO.FindProperty("flowerId").stringValue = name.ToLowerInvariant();
        typeSO.FindProperty("displayName").stringValue = name;
        typeSO.FindProperty("ideal").objectReferenceValue = ideal;
        typeSO.FindProperty("basePerfectScore").floatValue = 100f;
        typeSO.ApplyModifiedProperties();
        AssetDatabase.CreateAsset(typeDef, typePath);

        sessionSO.FindProperty("FlowerType").objectReferenceValue = typeDef;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        return new FlowerBuildResult
        {
            root = root,
            stem = stemRuntime,
            brain = brain,
            session = session,
            ideal = ideal,
        };
    }

    // ========================================================================
    // ClientOrder ScriptableObject creation
    // ========================================================================

    private static ClientOrder CreateClientOrder(
        string assetName, string clientName, string orderText, int payout,
        FlowerTypeDefinition.Difficulty difficulty,
        float idealStemLength = 0.5f, float idealCutAngle = 45f,
        int minLeavesRequired = 0, bool requireAllPetals = false)
    {
        string path = $"Assets/ScriptableObjects/ClientOrders/{assetName}.asset";
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        var order = ScriptableObject.CreateInstance<ClientOrder>();
        order.clientName = clientName;
        order.orderText = orderText;
        order.payout = payout;
        order.difficulty = difficulty;
        order.idealStemLength = idealStemLength;
        order.idealCutAngle = idealCutAngle;
        order.minLeavesRequired = minLeavesRequired;
        order.requireAllPetals = requireAllPetals;

        AssetDatabase.CreateAsset(order, path);
        return order;
    }

    // ========================================================================
    // Shared Helpers
    // ========================================================================

    private static GameObject CreateBox(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.isStatic = true;
        SetColor(go, color);
        return go;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }
    }

    private static GameObject CreateScreenCanvas(string name)
    {
        var canvasGO = new GameObject(name);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return canvasGO;
    }

    private static GameObject CreateLabel(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size, string text, float fontSize,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
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

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        if (!AssetDatabase.IsValidFolder($"{parentFolder}/{newFolder}"))
        {
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                string[] parts = parentFolder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }
    }
}
