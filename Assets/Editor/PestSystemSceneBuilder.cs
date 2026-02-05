using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor utility that programmatically builds the pest_system scene with
/// a flower, scissor tools, pest controller, pest HUD, and grading UI.
/// Menu: Window > Iris > Build Pest System Scene
/// </summary>
public static class PestSystemSceneBuilder
{
    // ════════════════════════════════════════════════════════════════════
    // Flower build result
    // ════════════════════════════════════════════════════════════════════

    private struct FlowerBuildResult
    {
        public GameObject root;
        public FlowerGameBrain brain;
        public FlowerSessionController session;
        public FlowerStemRuntime stem;
        public Rigidbody stemRb;
        public List<FlowerPartRuntime> parts;
    }

    [MenuItem("Window/Iris/Build Pest System Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.95f, 0.85f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera + AudioListener ─────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
        cam.fieldOfView = 60f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 0.8f, -1.5f);
        camGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

        // ── 3. Room geometry ───────────────────────────────────────────
        BuildRoom();

        // ── 4. Flower ──────────────────────────────────────────────────
        var flower = BuildFlower();

        // ── 5. ScissorStation + CuttingPlaneController ─────────────────
        BuildScissorTools();

        // ── 6. Managers (PestController + PestHUD) ─────────────────────
        var managersGO = new GameObject("Managers");
        var pestCtrl = managersGO.AddComponent<PestController>();
        var pestHUD = managersGO.AddComponent<PestHUD>();

        // Wire PestController
        var pestCtrlSO = new SerializedObject(pestCtrl);
        pestCtrlSO.FindProperty("brain").objectReferenceValue = flower.brain;
        pestCtrlSO.FindProperty("initialPestCount").intValue = 2;
        pestCtrlSO.FindProperty("spreadInterval").floatValue = 4f;
        pestCtrlSO.FindProperty("spreadChance").floatValue = 0.5f;
        pestCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire PestHUD
        var pestHUDSO = new SerializedObject(pestHUD);
        pestHUDSO.FindProperty("pests").objectReferenceValue = pestCtrl;
        pestHUDSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 7. Screen-space UI canvas ──────────────────────────────────
        var uiCanvas = CreateScreenCanvas("PestUI_Canvas");

        // Status label (top-left area)
        var statusLabel = CreateLabel("StatusLabel", uiCanvas.transform,
            new Vector2(-280f, 220f), new Vector2(300f, 40f),
            "Infested: 0/11 parts", 20f, Color.white,
            TextAlignmentOptions.Left);

        // Warning flash (semi-transparent panel behind warning text)
        var warningFlashGO = new GameObject("WarningFlash");
        warningFlashGO.transform.SetParent(uiCanvas.transform);
        var warningFlashRT = warningFlashGO.AddComponent<RectTransform>();
        warningFlashRT.anchorMin = new Vector2(0.5f, 0.5f);
        warningFlashRT.anchorMax = new Vector2(0.5f, 0.5f);
        warningFlashRT.sizeDelta = new Vector2(400f, 60f);
        warningFlashRT.anchoredPosition = Vector2.zero;
        warningFlashRT.localScale = Vector3.one;
        var warningBg = warningFlashGO.AddComponent<UnityEngine.UI.Image>();
        warningBg.color = new Color(0.6f, 0f, 0f, 0.35f);
        var warningFlashCG = warningFlashGO.AddComponent<CanvasGroup>();
        warningFlashCG.alpha = 0f;

        // Warning label (center, red, starts inactive)
        var warningLabel = CreateLabel("WarningLabel", uiCanvas.transform,
            Vector2.zero, new Vector2(400f, 50f),
            "PEST NEAR CROWN!", 28f, new Color(1f, 0.2f, 0.15f),
            TextAlignmentOptions.Center);
        warningLabel.SetActive(false);

        // Wire PestHUD UI elements
        pestHUDSO = new SerializedObject(pestHUD);
        pestHUDSO.FindProperty("statusLabel").objectReferenceValue =
            statusLabel.GetComponent<TMP_Text>();
        pestHUDSO.FindProperty("warningLabel").objectReferenceValue =
            warningLabel.GetComponent<TMP_Text>();
        pestHUDSO.FindProperty("warningFlash").objectReferenceValue = warningFlashCG;
        pestHUDSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 8. FlowerGradingUI ─────────────────────────────────────────
        BuildGradingUI(uiCanvas.transform, flower.session);

        // ── 9. Save scene ──────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/pest_system.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[PestSystemSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Room geometry
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        // Floor
        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(4f, 0.1f, 4f),
            new Color(0.30f, 0.28f, 0.25f));

        // Back wall
        CreateBox("Wall_Back", parent.transform,
            new Vector3(0f, 1.5f, 2f), new Vector3(4f, 3f, 0.2f),
            new Color(0.40f, 0.38f, 0.35f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Flower builder (inline)
    // ════════════════════════════════════════════════════════════════════

    private static FlowerBuildResult BuildFlower()
    {
        float stemHeight = 1.0f;
        var result = new FlowerBuildResult();
        result.parts = new List<FlowerPartRuntime>();

        // ── Root ──
        var root = new GameObject("PestFlower");
        root.transform.position = new Vector3(0f, 0.5f, 0f);
        result.root = root;

        // ── Stem (Cylinder, green) ──
        var stemGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stemGO.name = "Stem";
        stemGO.transform.SetParent(root.transform);
        stemGO.transform.localPosition = Vector3.zero;
        stemGO.transform.localScale = new Vector3(0.05f, stemHeight * 0.5f, 0.05f);
        SetColor(stemGO, new Color(0.2f, 0.55f, 0.2f));

        var stemRuntime = stemGO.AddComponent<FlowerStemRuntime>();
        var stemRb = stemGO.AddComponent<Rigidbody>();
        stemRb.isKinematic = false;
        stemRb.interpolation = RigidbodyInterpolation.Interpolate;
        result.stem = stemRuntime;
        result.stemRb = stemRb;

        // Stem child transforms
        var anchor = new GameObject("StemAnchor");
        anchor.transform.SetParent(stemGO.transform);
        anchor.transform.localPosition = Vector3.up * 0.5f;
        stemRuntime.StemAnchor = anchor.transform;

        var tip = new GameObject("StemTip");
        tip.transform.SetParent(stemGO.transform);
        tip.transform.localPosition = Vector3.down * 0.5f;
        stemRuntime.StemTip = tip.transform;

        var cutNormal = new GameObject("CutNormalRef");
        cutNormal.transform.SetParent(stemGO.transform);
        cutNormal.transform.localPosition = Vector3.zero;
        cutNormal.transform.localRotation = Quaternion.identity;
        stemRuntime.cutNormalRef = cutNormal.transform;

        // ── Crown (Sphere, yellow) ──
        var crownGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crownGO.name = "Crown";
        crownGO.transform.SetParent(root.transform);
        crownGO.transform.localPosition = new Vector3(0f, stemHeight * 0.5f, 0f);
        crownGO.transform.localScale = Vector3.one * 0.1f;
        SetColor(crownGO, new Color(0.95f, 0.85f, 0.2f));

        var crownRb = crownGO.AddComponent<Rigidbody>();
        crownRb.isKinematic = false;
        crownRb.interpolation = RigidbodyInterpolation.Interpolate;

        var crownPart = crownGO.AddComponent<FlowerPartRuntime>();
        var crownPartSO = new SerializedObject(crownPart);
        crownPartSO.FindProperty("PartId").stringValue = "Crown";
        crownPartSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Crown;
        crownPartSO.FindProperty("canCauseGameOver").boolValue = true;
        crownPartSO.FindProperty("contributesToScore").boolValue = true;
        crownPartSO.FindProperty("scoreWeight").floatValue = 1f;
        crownPartSO.ApplyModifiedPropertiesWithoutUndo();

        result.parts.Add(crownPart);

        // ── Leaves (6, Cube, green) ──
        int leafCount = 6;
        for (int i = 0; i < leafCount; i++)
        {
            float angle = i * (360f / leafCount);
            float rad = angle * Mathf.Deg2Rad;
            float radius = 0.12f;
            float t = leafCount > 1 ? (float)i / (leafCount - 1) : 0.5f;
            float height = Mathf.Lerp(0.1f, 0.4f, t) * stemHeight;
            float yPos = -stemHeight * 0.5f + height;

            float x = Mathf.Cos(rad) * radius;
            float z = Mathf.Sin(rad) * radius;

            var leafGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafGO.name = $"Leaf_{i}";
            leafGO.transform.SetParent(root.transform);
            leafGO.transform.localPosition = new Vector3(x, yPos, z);
            leafGO.transform.localScale = new Vector3(0.08f, 0.02f, 0.12f);

            // Face outward
            Vector3 outward = new Vector3(x, 0f, z).normalized;
            if (outward.sqrMagnitude > 0.001f)
                leafGO.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

            SetColor(leafGO, new Color(0.25f, 0.60f, 0.25f));

            var leafRb = leafGO.AddComponent<Rigidbody>();
            leafRb.isKinematic = false;
            leafRb.interpolation = RigidbodyInterpolation.Interpolate;

            var leafPart = leafGO.AddComponent<FlowerPartRuntime>();
            var leafPartSO = new SerializedObject(leafPart);
            leafPartSO.FindProperty("PartId").stringValue = $"Leaf_{i}";
            leafPartSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Leaf;
            leafPartSO.FindProperty("canCauseGameOver").boolValue = false;
            leafPartSO.FindProperty("contributesToScore").boolValue = true;
            leafPartSO.FindProperty("scoreWeight").floatValue = 1f;
            leafPartSO.ApplyModifiedPropertiesWithoutUndo();

            var leafTether = leafGO.AddComponent<XYTetherJoint>();
            var leafTetherSO = new SerializedObject(leafTether);
            leafTetherSO.FindProperty("connectedBody").objectReferenceValue = stemRb;
            leafTetherSO.FindProperty("breakForce").floatValue = 800f;
            leafTetherSO.FindProperty("spring").floatValue = 1200f;
            leafTetherSO.FindProperty("damper").floatValue = 60f;
            leafTetherSO.ApplyModifiedPropertiesWithoutUndo();

            result.parts.Add(leafPart);
        }

        // ── Petals (5, Cube, pink) ──
        int petalCount = 5;
        for (int i = 0; i < petalCount; i++)
        {
            float angle = i * (360f / petalCount);
            float rad = angle * Mathf.Deg2Rad;
            float radius = 0.08f;
            float t = petalCount > 1 ? (float)i / (petalCount - 1) : 0.5f;
            float height = Mathf.Lerp(0.7f, 0.9f, t) * stemHeight;
            float yPos = -stemHeight * 0.5f + height;

            float x = Mathf.Cos(rad) * radius;
            float z = Mathf.Sin(rad) * radius;

            var petalGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            petalGO.name = $"Petal_{i}";
            petalGO.transform.SetParent(root.transform);
            petalGO.transform.localPosition = new Vector3(x, yPos, z);
            petalGO.transform.localScale = new Vector3(0.06f, 0.015f, 0.10f);

            // Face outward
            Vector3 outward = new Vector3(x, 0f, z).normalized;
            if (outward.sqrMagnitude > 0.001f)
                petalGO.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

            SetColor(petalGO, new Color(0.90f, 0.50f, 0.60f));

            var petalRb = petalGO.AddComponent<Rigidbody>();
            petalRb.isKinematic = false;
            petalRb.interpolation = RigidbodyInterpolation.Interpolate;

            var petalPart = petalGO.AddComponent<FlowerPartRuntime>();
            var petalPartSO = new SerializedObject(petalPart);
            petalPartSO.FindProperty("PartId").stringValue = $"Petal_{i}";
            petalPartSO.FindProperty("kind").enumValueIndex = (int)FlowerPartKind.Petal;
            petalPartSO.FindProperty("canCauseGameOver").boolValue = false;
            petalPartSO.FindProperty("contributesToScore").boolValue = true;
            petalPartSO.FindProperty("scoreWeight").floatValue = 1f;
            petalPartSO.ApplyModifiedPropertiesWithoutUndo();

            var petalTether = petalGO.AddComponent<XYTetherJoint>();
            var petalTetherSO = new SerializedObject(petalTether);
            petalTetherSO.FindProperty("connectedBody").objectReferenceValue = stemRb;
            petalTetherSO.FindProperty("breakForce").floatValue = 800f;
            petalTetherSO.FindProperty("spring").floatValue = 1200f;
            petalTetherSO.FindProperty("damper").floatValue = 60f;
            petalTetherSO.ApplyModifiedPropertiesWithoutUndo();

            result.parts.Add(petalPart);
        }

        // ── ScriptableObjects ──
        EnsureFolder("Assets/ScriptableObjects", "Flowers");

        // IdealFlowerDefinition
        string idealPath = "Assets/ScriptableObjects/Flowers/Ideal_PestFlower.asset";
        idealPath = AssetDatabase.GenerateUniqueAssetPath(idealPath);

        var ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();
        var idealSO = new SerializedObject(ideal);
        idealSO.FindProperty("idealStemLength").floatValue = 0.5f;
        idealSO.FindProperty("idealCutAngleDeg").floatValue = 45f;
        idealSO.FindProperty("stemScoreWeight").floatValue = 0.3f;
        idealSO.FindProperty("cutAngleScoreWeight").floatValue = 0.3f;
        idealSO.FindProperty("stemPerfectDelta").floatValue = 0.05f;
        idealSO.FindProperty("stemHardFailDelta").floatValue = 0.3f;
        idealSO.FindProperty("cutAnglePerfectDelta").floatValue = 5f;
        idealSO.FindProperty("cutAngleHardFailDelta").floatValue = 45f;

        // Part rules
        var rulesProp = idealSO.FindProperty("partRules");
        if (rulesProp != null)
        {
            rulesProp.ClearArray();
            for (int i = 0; i < result.parts.Count; i++)
            {
                var p = result.parts[i];
                var pSO = new SerializedObject(p);
                string partId = pSO.FindProperty("PartId").stringValue;
                int kindIdx = pSO.FindProperty("kind").enumValueIndex;
                bool isCrown = (FlowerPartKind)kindIdx == FlowerPartKind.Crown;

                rulesProp.InsertArrayElementAtIndex(i);
                var elem = rulesProp.GetArrayElementAtIndex(i);

                var pidProp = elem.FindPropertyRelative("partId");
                if (pidProp != null) pidProp.stringValue = partId;

                var kindProp = elem.FindPropertyRelative("kind");
                if (kindProp != null) kindProp.enumValueIndex = kindIdx;

                var condProp = elem.FindPropertyRelative("idealCondition");
                if (condProp != null) condProp.enumValueIndex = (int)FlowerPartCondition.Normal;

                var scoreProp = elem.FindPropertyRelative("contributesToScore");
                if (scoreProp != null) scoreProp.boolValue = true;

                var goProp = elem.FindPropertyRelative("canCauseGameOver");
                if (goProp != null) goProp.boolValue = isCrown;

                var wProp = elem.FindPropertyRelative("scoreWeight");
                if (wProp != null) wProp.floatValue = 1f;
            }
        }
        idealSO.ApplyModifiedProperties();
        AssetDatabase.CreateAsset(ideal, idealPath);
        AssetDatabase.SaveAssets();

        // FlowerTypeDefinition
        string typePath = "Assets/ScriptableObjects/Flowers/Type_PestFlower.asset";
        typePath = AssetDatabase.GenerateUniqueAssetPath(typePath);

        var typeDef = ScriptableObject.CreateInstance<FlowerTypeDefinition>();
        var typeSO = new SerializedObject(typeDef);
        var flowIdProp = typeSO.FindProperty("flowerId");
        if (flowIdProp != null) flowIdProp.stringValue = "pest_flower";
        var dispProp = typeSO.FindProperty("displayName");
        if (dispProp != null) dispProp.stringValue = "Pest Flower";
        var diffProp = typeSO.FindProperty("difficulty");
        if (diffProp != null) diffProp.enumValueIndex = (int)FlowerTypeDefinition.Difficulty.Normal;
        var idealRefProp = typeSO.FindProperty("ideal");
        if (idealRefProp != null) idealRefProp.objectReferenceValue = ideal;
        var bpsProp = typeSO.FindProperty("basePerfectScore");
        if (bpsProp != null) bpsProp.floatValue = 100f;
        var smProp = typeSO.FindProperty("scoreMultiplier");
        if (smProp != null) smProp.floatValue = 1f;
        var minDProp = typeSO.FindProperty("minDays");
        if (minDProp != null) minDProp.intValue = 1;
        var maxDProp = typeSO.FindProperty("maxDays");
        if (maxDProp != null) maxDProp.intValue = 10;
        typeSO.ApplyModifiedProperties();
        AssetDatabase.CreateAsset(typeDef, typePath);
        AssetDatabase.SaveAssets();

        // ── FlowerGameBrain on root ──
        var brain = root.AddComponent<FlowerGameBrain>();
        result.brain = brain;

        var brainSO = new SerializedObject(brain);
        brainSO.FindProperty("stem").objectReferenceValue = stemRuntime;
        brainSO.FindProperty("ideal").objectReferenceValue = ideal;

        var partsArrayProp = brainSO.FindProperty("parts");
        partsArrayProp.ClearArray();
        for (int i = 0; i < result.parts.Count; i++)
        {
            partsArrayProp.InsertArrayElementAtIndex(i);
            partsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = result.parts[i];
        }
        brainSO.ApplyModifiedPropertiesWithoutUndo();

        // ── FlowerSessionController on root ──
        var session = root.AddComponent<FlowerSessionController>();
        result.session = session;

        var sessionSO = new SerializedObject(session);
        sessionSO.FindProperty("brain").objectReferenceValue = brain;
        sessionSO.FindProperty("FlowerType").objectReferenceValue = typeDef;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[PestSystemSceneBuilder] Built flower: 1 stem, 1 crown, 6 leaves, 5 petals " +
                  $"({result.parts.Count} total parts).");

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // Scissor tools
    // ════════════════════════════════════════════════════════════════════

    private static void BuildScissorTools()
    {
        var toolsParent = new GameObject("ScissorTools");

        var stationGO = new GameObject("ScissorStation");
        stationGO.transform.SetParent(toolsParent.transform);
        stationGO.AddComponent<ScissorStation>();

        var cpcGO = new GameObject("CuttingPlaneController");
        cpcGO.transform.SetParent(toolsParent.transform);
        cpcGO.AddComponent<CuttingPlaneController>();
    }

    // ════════════════════════════════════════════════════════════════════
    // Grading UI
    // ════════════════════════════════════════════════════════════════════

    private static void BuildGradingUI(Transform canvasParent, FlowerSessionController session)
    {
        // Grading root panel (hidden by default)
        var gradingGO = new GameObject("FlowerGradingUI");
        gradingGO.transform.SetParent(canvasParent);

        var gradingRT = gradingGO.AddComponent<RectTransform>();
        gradingRT.anchorMin = Vector2.zero;
        gradingRT.anchorMax = Vector2.one;
        gradingRT.offsetMin = Vector2.zero;
        gradingRT.offsetMax = Vector2.zero;
        gradingRT.localScale = Vector3.one;

        // Semi-transparent background
        var bg = gradingGO.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);

        // CanvasGroup for fade
        var cg = gradingGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Title label
        var titleGO = CreateLabel("TitleLabel", gradingGO.transform,
            new Vector2(0f, 80f), new Vector2(400f, 50f),
            "Grading...", 32f, Color.white, TextAlignmentOptions.Center);

        // Score label
        var scoreGO = CreateLabel("ScoreLabel", gradingGO.transform,
            new Vector2(0f, 20f), new Vector2(300f, 40f),
            "Score: 0", 24f, Color.white, TextAlignmentOptions.Center);

        // Days label
        var daysGO = CreateLabel("DaysLabel", gradingGO.transform,
            new Vector2(0f, -30f), new Vector2(300f, 40f),
            "Days: 0", 24f, Color.white, TextAlignmentOptions.Center);

        // Reason label
        var reasonGO = CreateLabel("ReasonLabel", gradingGO.transform,
            new Vector2(0f, -80f), new Vector2(400f, 40f),
            "", 18f, new Color(1f, 0.4f, 0.3f), TextAlignmentOptions.Center);

        // FlowerGradingUI component
        var gradingUI = gradingGO.AddComponent<FlowerGradingUI>();

        var gradingSO = new SerializedObject(gradingUI);
        gradingSO.FindProperty("session").objectReferenceValue = session;
        gradingSO.FindProperty("root").objectReferenceValue = cg;
        gradingSO.FindProperty("titleLabel").objectReferenceValue =
            titleGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("scoreLabel").objectReferenceValue =
            scoreGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("daysLabel").objectReferenceValue =
            daysGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("reasonLabel").objectReferenceValue =
            reasonGO.GetComponent<TMP_Text>();
        gradingSO.ApplyModifiedPropertiesWithoutUndo();

        // Start hidden
        gradingGO.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════
    // Shared helpers
    // ════════════════════════════════════════════════════════════════════

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
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, Color textColor,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        rt.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        if (!AssetDatabase.IsValidFolder(parentFolder))
        {
            // Split and create parent path
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

        string fullPath = $"{parentFolder}/{newFolder}";
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parentFolder, newFolder);
    }
}
