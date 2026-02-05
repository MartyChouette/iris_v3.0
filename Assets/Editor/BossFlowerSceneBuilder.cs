using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor utility that programmatically builds the boss_flowers scene with
/// an oversized flower, snap jaw mechanic, boss HUD, and cutting tools.
/// Menu: Window > Iris > Build Boss Flower Scene
/// </summary>
public static class BossFlowerSceneBuilder
{
    // ── Flower build result (same pattern as other builders) ──
    private struct FlowerBuildResult
    {
        public GameObject root;
        public FlowerStemRuntime stemRuntime;
        public FlowerSessionController session;
        public FlowerGameBrain brain;
        public GameObject crown;
        public List<FlowerPartRuntime> allParts;
        public IdealFlowerDefinition ideal;
        public FlowerTypeDefinition flowerType;
    }

    [MenuItem("Window/Iris/Build Boss Flower Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Directional light (warm tone) ─────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.92f, 0.78f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera + AudioListener ───────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.06f, 0.05f);
        cam.fieldOfView = 60f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 1.5f, -2.5f);
        camGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

        // ── 3. Room geometry (floor + back wall) ─────────────────────
        BuildRoom();

        // ── 4. Large flower ──────────────────────────────────────────
        var flower = BuildFlower();

        // ── 5. Snap jaw on the crown ─────────────────────────────────
        var jawUpper = BuildSnapJaw(flower.crown.transform);

        // ── 6. ScissorStation + CuttingPlaneController ───────────────
        var cuttingGO = BuildCuttingTools();
        var planeController = cuttingGO.GetComponent<CuttingPlaneController>();

        // ── 7. BossFlowerController on the flower root ───────────────
        var bossCtrl = flower.root.AddComponent<BossFlowerController>();
        WireBossFlowerController(bossCtrl, flower, planeController, jawUpper);

        // ── 8. Managers object with BossFlowerHUD ────────────────────
        var managersGO = new GameObject("Managers");
        var bossHud = managersGO.AddComponent<BossFlowerHUD>();
        WireBossFlowerHUD(bossHud, bossCtrl);

        // ── 9. Screen-space canvas with boss UI ──────────────────────
        BuildBossUI(managersGO.transform, bossCtrl);

        // ── 10. FlowerGradingUI wired to the session ─────────────────
        BuildGradingUI(managersGO.transform, flower.session);

        // ── 11. Save scene ───────────────────────────────────────────
        EnsureFolder("Assets", "Scenes");

        string path = "Assets/Scenes/boss_flowers.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[BossFlowerSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Room geometry
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        // Floor
        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(6f, 0.1f, 6f),
            new Color(0.25f, 0.22f, 0.20f));

        // Back wall
        CreateBox("Wall_Back", parent.transform,
            new Vector3(0f, 2f, 3f), new Vector3(6f, 4f, 0.2f),
            new Color(0.35f, 0.30f, 0.28f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Flower (inline BuildFlower)
    // ════════════════════════════════════════════════════════════════════

    private static FlowerBuildResult BuildFlower()
    {
        var result = new FlowerBuildResult();
        result.allParts = new List<FlowerPartRuntime>();

        float stemHeight = 2.0f;
        Vector3 flowerOrigin = new Vector3(0f, 1.0f, 0f);

        // ── Root ──
        var root = new GameObject("BossFlower_VenusMaw");
        root.transform.position = flowerOrigin;
        result.root = root;

        // ── Stem (thick cylinder) ──
        var stemGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stemGO.name = "Stem";
        stemGO.transform.SetParent(root.transform);
        stemGO.transform.localPosition = Vector3.zero;
        stemGO.transform.localScale = new Vector3(0.08f, stemHeight / 2f, 0.08f);
        stemGO.isStatic = false;
        SetColor(stemGO, new Color(0.18f, 0.55f, 0.15f));

        var stemRuntime = stemGO.AddComponent<FlowerStemRuntime>();
        var stemRb = stemGO.AddComponent<Rigidbody>();
        stemRb.isKinematic = false;
        stemRb.interpolation = RigidbodyInterpolation.Interpolate;
        result.stemRuntime = stemRuntime;

        // StemAnchor (top of stem, near crown)
        var anchor = new GameObject("StemAnchor");
        anchor.transform.SetParent(stemGO.transform);
        anchor.transform.localPosition = Vector3.up * (stemHeight * 0.5f);
        stemRuntime.StemAnchor = anchor.transform;

        // StemTip (bottom of stem)
        var tip = new GameObject("StemTip");
        tip.transform.SetParent(stemGO.transform);
        tip.transform.localPosition = Vector3.down * (stemHeight * 0.5f);
        stemRuntime.StemTip = tip.transform;

        // CutNormalRef
        var cutNormal = new GameObject("CutNormalRef");
        cutNormal.transform.SetParent(stemGO.transform);
        cutNormal.transform.localPosition = Vector3.zero;
        cutNormal.transform.localRotation = Quaternion.identity;
        stemRuntime.cutNormalRef = cutNormal.transform;

        // ── Crown (sphere, yellow) ──
        var crownGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crownGO.name = "Crown";
        crownGO.transform.SetParent(root.transform);
        crownGO.transform.localPosition = new Vector3(0f, stemHeight * 0.5f, 0f);
        crownGO.transform.localScale = Vector3.one * 0.15f;
        crownGO.isStatic = false;
        SetColor(crownGO, new Color(0.95f, 0.85f, 0.2f));
        result.crown = crownGO;

        var crownRb = crownGO.AddComponent<Rigidbody>();
        crownRb.isKinematic = false;
        crownRb.interpolation = RigidbodyInterpolation.Interpolate;

        var crownPart = crownGO.AddComponent<FlowerPartRuntime>();
        var crownPartSO = new SerializedObject(crownPart);
        SetProp(crownPartSO, "PartId", "Crown_VenusMaw");
        SetEnumProp(crownPartSO, "kind", (int)FlowerPartKind.Crown);
        SetBoolProp(crownPartSO, "canCauseGameOver", true);
        SetBoolProp(crownPartSO, "contributesToScore", true);
        SetFloatProp(crownPartSO, "scoreWeight", 1f);
        crownPartSO.ApplyModifiedPropertiesWithoutUndo();
        result.allParts.Add(crownPart);

        // ── Leaves (4, cubes, green) ──
        int leafCount = 4;
        for (int i = 0; i < leafCount; i++)
        {
            float angle = i * (360f / leafCount);
            float rad = angle * Mathf.Deg2Rad;
            float leafRadius = 0.2f;
            float height = Mathf.Lerp(0.1f, 0.6f, (float)i / (leafCount - 1)) * stemHeight - stemHeight * 0.5f;

            float x = Mathf.Cos(rad) * leafRadius;
            float z = Mathf.Sin(rad) * leafRadius;

            var leafGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafGO.name = $"Leaf_{i}";
            leafGO.transform.SetParent(root.transform);
            leafGO.transform.localPosition = new Vector3(x, height, z);
            leafGO.transform.localScale = new Vector3(0.12f, 0.02f, 0.06f);
            leafGO.isStatic = false;
            SetColor(leafGO, new Color(0.2f, 0.6f, 0.18f));

            // Look outward from stem
            Vector3 outward = new Vector3(x, 0f, z).normalized;
            if (outward.sqrMagnitude > 0.001f)
                leafGO.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

            var leafRb = leafGO.AddComponent<Rigidbody>();
            leafRb.isKinematic = false;
            leafRb.interpolation = RigidbodyInterpolation.Interpolate;

            var leafPart = leafGO.AddComponent<FlowerPartRuntime>();
            var leafPartSO = new SerializedObject(leafPart);
            SetProp(leafPartSO, "PartId", $"Leaf_Boss_{i}");
            SetEnumProp(leafPartSO, "kind", (int)FlowerPartKind.Leaf);
            SetBoolProp(leafPartSO, "canCauseGameOver", false);
            SetBoolProp(leafPartSO, "contributesToScore", true);
            SetFloatProp(leafPartSO, "scoreWeight", 1f);
            leafPartSO.ApplyModifiedPropertiesWithoutUndo();

            var leafTether = leafGO.AddComponent<XYTetherJoint>();
            var leafTetherSO = new SerializedObject(leafTether);
            var connProp = leafTetherSO.FindProperty("connectedBody");
            if (connProp != null) connProp.objectReferenceValue = stemRb;
            leafTetherSO.ApplyModifiedPropertiesWithoutUndo();

            result.allParts.Add(leafPart);
        }

        // ── Petals (3, cubes, pink) ──
        int petalCount = 3;
        for (int i = 0; i < petalCount; i++)
        {
            float angle = i * (360f / petalCount);
            float rad = angle * Mathf.Deg2Rad;
            float petalRadius = 0.12f;
            float height = Mathf.Lerp(0.7f, 0.9f, petalCount > 1 ? (float)i / (petalCount - 1) : 0.5f) * stemHeight - stemHeight * 0.5f;

            float x = Mathf.Cos(rad) * petalRadius;
            float z = Mathf.Sin(rad) * petalRadius;

            var petalGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            petalGO.name = $"Petal_{i}";
            petalGO.transform.SetParent(root.transform);
            petalGO.transform.localPosition = new Vector3(x, height, z);
            petalGO.transform.localScale = new Vector3(0.08f, 0.015f, 0.05f);
            petalGO.isStatic = false;
            SetColor(petalGO, new Color(0.95f, 0.55f, 0.65f));

            Vector3 outward = new Vector3(x, 0f, z).normalized;
            if (outward.sqrMagnitude > 0.001f)
                petalGO.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

            var petalRb = petalGO.AddComponent<Rigidbody>();
            petalRb.isKinematic = false;
            petalRb.interpolation = RigidbodyInterpolation.Interpolate;

            var petalPart = petalGO.AddComponent<FlowerPartRuntime>();
            var petalPartSO = new SerializedObject(petalPart);
            SetProp(petalPartSO, "PartId", $"Petal_Boss_{i}");
            SetEnumProp(petalPartSO, "kind", (int)FlowerPartKind.Petal);
            SetBoolProp(petalPartSO, "canCauseGameOver", false);
            SetBoolProp(petalPartSO, "contributesToScore", true);
            SetFloatProp(petalPartSO, "scoreWeight", 1f);
            petalPartSO.ApplyModifiedPropertiesWithoutUndo();

            var petalTether = petalGO.AddComponent<XYTetherJoint>();
            var petalTetherSO = new SerializedObject(petalTether);
            var connProp = petalTetherSO.FindProperty("connectedBody");
            if (connProp != null) connProp.objectReferenceValue = stemRb;
            petalTetherSO.ApplyModifiedPropertiesWithoutUndo();

            result.allParts.Add(petalPart);
        }

        // ── FlowerGameBrain + FlowerSessionController on root ──
        var brain = root.AddComponent<FlowerGameBrain>();
        var session = root.AddComponent<FlowerSessionController>();
        result.brain = brain;
        result.session = session;

        // Wire brain -> stem + parts
        var brainSO = new SerializedObject(brain);
        var stemProp = brainSO.FindProperty("stem");
        if (stemProp != null) stemProp.objectReferenceValue = stemRuntime;

        var partsArrayProp = brainSO.FindProperty("parts");
        if (partsArrayProp != null)
        {
            partsArrayProp.ClearArray();
            for (int i = 0; i < result.allParts.Count; i++)
            {
                partsArrayProp.InsertArrayElementAtIndex(i);
                partsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = result.allParts[i];
            }
        }
        brainSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire session -> brain
        var sessionSO = new SerializedObject(session);
        var brainRef = sessionSO.FindProperty("brain");
        if (brainRef != null) brainRef.objectReferenceValue = brain;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        // ── ScriptableObjects ──
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "Flowers");

        // IdealFlowerDefinition
        string idealPath = AssetDatabase.GenerateUniqueAssetPath(
            "Assets/ScriptableObjects/Flowers/Ideal_VenusMaw.asset");
        var ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();
        var idealSO = new SerializedObject(ideal);
        SetFloatProp(idealSO, "idealStemLength", 0.5f);
        SetFloatProp(idealSO, "idealCutAngleDeg", 45f);
        SetFloatProp(idealSO, "stemScoreWeight", 0.3f);
        SetFloatProp(idealSO, "cutAngleScoreWeight", 0.3f);
        SetFloatProp(idealSO, "stemPerfectDelta", 0.05f);
        SetFloatProp(idealSO, "stemHardFailDelta", 0.3f);
        SetFloatProp(idealSO, "cutAnglePerfectDelta", 5f);
        SetFloatProp(idealSO, "cutAngleHardFailDelta", 45f);

        var rulesList = idealSO.FindProperty("partRules");
        if (rulesList != null)
        {
            rulesList.ClearArray();
            for (int i = 0; i < result.allParts.Count; i++)
            {
                var p = result.allParts[i];
                var pSO = new SerializedObject(p);
                string partId = pSO.FindProperty("PartId")?.stringValue ?? p.name;
                int kindIdx = pSO.FindProperty("kind")?.enumValueIndex ?? 0;

                rulesList.InsertArrayElementAtIndex(i);
                var elem = rulesList.GetArrayElementAtIndex(i);

                var pidProp = elem.FindPropertyRelative("partId");
                if (pidProp != null) pidProp.stringValue = partId;

                var kindProp = elem.FindPropertyRelative("kind");
                if (kindProp != null) kindProp.enumValueIndex = kindIdx;

                var condProp = elem.FindPropertyRelative("idealCondition");
                if (condProp != null) condProp.enumValueIndex = (int)FlowerPartCondition.Normal;

                var scoreProp = elem.FindPropertyRelative("contributesToScore");
                if (scoreProp != null) scoreProp.boolValue = true;

                var goProp = elem.FindPropertyRelative("canCauseGameOver");
                if (goProp != null) goProp.boolValue = ((FlowerPartKind)kindIdx == FlowerPartKind.Crown);

                var wProp = elem.FindPropertyRelative("scoreWeight");
                if (wProp != null) wProp.floatValue = 1f;
            }
        }
        idealSO.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(ideal, idealPath);
        result.ideal = ideal;

        // Wire brain -> ideal
        brainSO = new SerializedObject(brain);
        var idealRef = brainSO.FindProperty("ideal");
        if (idealRef != null) idealRef.objectReferenceValue = ideal;
        brainSO.ApplyModifiedPropertiesWithoutUndo();

        // FlowerTypeDefinition
        string typePath = AssetDatabase.GenerateUniqueAssetPath(
            "Assets/ScriptableObjects/Flowers/Type_VenusMaw.asset");
        var flowerType = ScriptableObject.CreateInstance<FlowerTypeDefinition>();
        var typeSO = new SerializedObject(flowerType);
        SetProp(typeSO, "flowerId", "venus_maw");
        SetProp(typeSO, "displayName", "Venus Maw");
        SetEnumProp(typeSO, "difficulty", (int)FlowerTypeDefinition.Difficulty.Hard);
        var typeIdealRef = typeSO.FindProperty("ideal");
        if (typeIdealRef != null) typeIdealRef.objectReferenceValue = ideal;
        SetFloatProp(typeSO, "basePerfectScore", 100f);
        SetFloatProp(typeSO, "scoreMultiplier", 1.5f);
        SetIntProp(typeSO, "minDays", 1);
        SetIntProp(typeSO, "maxDays", 14);
        typeSO.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(flowerType, typePath);
        result.flowerType = flowerType;

        // Wire session -> FlowerType
        sessionSO = new SerializedObject(session);
        var ftRef = sessionSO.FindProperty("FlowerType");
        if (ftRef != null) ftRef.objectReferenceValue = flowerType;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // Snap Jaw
    // ════════════════════════════════════════════════════════════════════

    private static Transform BuildSnapJaw(Transform crownTransform)
    {
        Color jawColor = new Color(0.6f, 0.15f, 0.1f);

        // Upper jaw
        var jawUpper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        jawUpper.name = "Jaw_Upper";
        jawUpper.transform.SetParent(crownTransform);
        jawUpper.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        jawUpper.transform.localScale = new Vector3(0.15f, 0.01f, 0.1f);
        jawUpper.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f); // angled open upward
        jawUpper.isStatic = false;
        SetColor(jawUpper, jawColor);

        // Lower jaw
        var jawLower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        jawLower.name = "Jaw_Lower";
        jawLower.transform.SetParent(crownTransform);
        jawLower.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        jawLower.transform.localScale = new Vector3(0.15f, 0.01f, 0.1f);
        jawLower.transform.localRotation = Quaternion.Euler(20f, 0f, 0f); // angled open downward
        jawLower.isStatic = false;
        SetColor(jawLower, jawColor);

        return jawUpper.transform;
    }

    // ════════════════════════════════════════════════════════════════════
    // Cutting Tools (ScissorStation + CuttingPlaneController)
    // ════════════════════════════════════════════════════════════════════

    private static GameObject BuildCuttingTools()
    {
        var parent = new GameObject("CuttingTools");

        // ScissorStation
        var stationGO = new GameObject("ScissorStation");
        stationGO.transform.SetParent(parent.transform);
        stationGO.transform.position = new Vector3(0.8f, 0.5f, -1f);
        stationGO.AddComponent<ScissorStation>();

        // CuttingPlaneController
        var planeGO = new GameObject("CuttingPlaneController");
        planeGO.transform.SetParent(parent.transform);
        planeGO.transform.position = new Vector3(0f, 1.0f, 0f);
        planeGO.AddComponent<CuttingPlaneController>();

        return planeGO;
    }

    // ════════════════════════════════════════════════════════════════════
    // BossFlowerController wiring
    // ════════════════════════════════════════════════════════════════════

    private static void WireBossFlowerController(
        BossFlowerController bossCtrl,
        FlowerBuildResult flower,
        CuttingPlaneController planeController,
        Transform jawUpperTransform)
    {
        var so = new SerializedObject(bossCtrl);

        var stemProp = so.FindProperty("stem");
        if (stemProp != null) stemProp.objectReferenceValue = flower.stemRuntime;

        var sessionProp = so.FindProperty("session");
        if (sessionProp != null) sessionProp.objectReferenceValue = flower.session;

        var brainProp = so.FindProperty("brain");
        if (brainProp != null) brainProp.objectReferenceValue = flower.brain;

        var planeProp = so.FindProperty("planeController");
        if (planeProp != null) planeProp.objectReferenceValue = planeController;

        var jawProp = so.FindProperty("snapJaw");
        if (jawProp != null) jawProp.objectReferenceValue = jawUpperTransform;

        var snapTrapProp = so.FindProperty("enableSnapTrap");
        if (snapTrapProp != null) snapTrapProp.boolValue = true;

        var regrowthProp = so.FindProperty("enableRegrowth");
        if (regrowthProp != null) regrowthProp.boolValue = true;

        var scrollProp = so.FindProperty("enableScrollStem");
        if (scrollProp != null) scrollProp.boolValue = false;

        var nameProp = so.FindProperty("bossName");
        if (nameProp != null) nameProp.stringValue = "Venus Maw";

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════════
    // BossFlowerHUD wiring
    // ════════════════════════════════════════════════════════════════════

    private static void WireBossFlowerHUD(BossFlowerHUD hud, BossFlowerController bossCtrl)
    {
        var so = new SerializedObject(hud);

        var bossProp = so.FindProperty("boss");
        if (bossProp != null) bossProp.objectReferenceValue = bossCtrl;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════════
    // Boss UI (screen-space canvas)
    // ════════════════════════════════════════════════════════════════════

    private static void BuildBossUI(Transform managersParent, BossFlowerController bossCtrl)
    {
        var canvasGO = CreateScreenCanvas("BossUI_Canvas", managersParent);

        // ── Boss name plate (top-center, bold, 28pt) ──
        var nameLabel = CreateLabel("BossNameLabel", canvasGO.transform,
            new Vector2(0f, 300f), new Vector2(500f, 50f),
            "Venus Maw", 28f, FontStyles.Bold, TextAlignmentOptions.Center,
            Color.white);

        // Anchor to top-center
        var nameRT = nameLabel.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.5f, 1f);
        nameRT.anchorMax = new Vector2(0.5f, 1f);
        nameRT.anchoredPosition = new Vector2(0f, -40f);

        // ── Phase label (below boss name, 18pt) ──
        var phaseLabel = CreateLabel("PhaseLabel", canvasGO.transform,
            new Vector2(0f, 260f), new Vector2(400f, 35f),
            "Phase: Dormant", 18f, FontStyles.Normal, TextAlignmentOptions.Center,
            new Color(0.8f, 0.8f, 0.8f));

        var phaseRT = phaseLabel.GetComponent<RectTransform>();
        phaseRT.anchorMin = new Vector2(0.5f, 1f);
        phaseRT.anchorMax = new Vector2(0.5f, 1f);
        phaseRT.anchoredPosition = new Vector2(0f, -80f);

        // ── Warning flash CanvasGroup (semi-transparent red panel, center) ──
        var warningFlashGO = new GameObject("WarningFlash");
        warningFlashGO.transform.SetParent(canvasGO.transform);

        var flashRT = warningFlashGO.AddComponent<RectTransform>();
        flashRT.anchorMin = new Vector2(0f, 0f);
        flashRT.anchorMax = new Vector2(1f, 1f);
        flashRT.offsetMin = Vector2.zero;
        flashRT.offsetMax = Vector2.zero;
        flashRT.localScale = Vector3.one;

        var flashImage = warningFlashGO.AddComponent<UnityEngine.UI.Image>();
        flashImage.color = new Color(0.8f, 0.05f, 0.05f, 0.15f);

        var flashGroup = warningFlashGO.AddComponent<CanvasGroup>();
        flashGroup.alpha = 0f;
        flashGroup.interactable = false;
        flashGroup.blocksRaycasts = false;

        // ── Warning label (center, 36pt, red) ──
        var warningLabel = CreateLabel("WarningLabel", canvasGO.transform,
            Vector2.zero, new Vector2(600f, 60f),
            "SNAP IN 1.2s!", 36f, FontStyles.Bold, TextAlignmentOptions.Center,
            new Color(1f, 0.15f, 0.1f));

        var warningRT = warningLabel.GetComponent<RectTransform>();
        warningRT.anchorMin = new Vector2(0.5f, 0.5f);
        warningRT.anchorMax = new Vector2(0.5f, 0.5f);
        warningRT.anchoredPosition = Vector2.zero;

        // Start warning hidden
        warningLabel.SetActive(false);

        // Wire boss HUD labels if the BossFlowerHUD has the expected fields
        var bossHud = managersParent.GetComponentInChildren<BossFlowerHUD>();
        if (bossHud != null)
        {
            var hudSO = new SerializedObject(bossHud);

            var nlProp = hudSO.FindProperty("bossNameLabel");
            if (nlProp != null) nlProp.objectReferenceValue = nameLabel.GetComponent<TMP_Text>();

            var plProp = hudSO.FindProperty("phaseLabel");
            if (plProp != null) plProp.objectReferenceValue = phaseLabel.GetComponent<TMP_Text>();

            var wlProp = hudSO.FindProperty("warningLabel");
            if (wlProp != null) wlProp.objectReferenceValue = warningLabel.GetComponent<TMP_Text>();

            var wfProp = hudSO.FindProperty("warningFlash");
            if (wfProp != null) wfProp.objectReferenceValue = flashGroup;

            hudSO.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // FlowerGradingUI
    // ════════════════════════════════════════════════════════════════════

    private static void BuildGradingUI(Transform managersParent, FlowerSessionController session)
    {
        var gradingCanvasGO = CreateScreenCanvas("GradingUI_Canvas", managersParent);

        // Root CanvasGroup for fade
        var gradingRoot = new GameObject("GradingRoot");
        gradingRoot.transform.SetParent(gradingCanvasGO.transform);

        var rootRT = gradingRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;
        rootRT.localScale = Vector3.one;

        var rootBg = gradingRoot.AddComponent<UnityEngine.UI.Image>();
        rootBg.color = new Color(0f, 0f, 0f, 0.7f);

        var rootCG = gradingRoot.AddComponent<CanvasGroup>();
        rootCG.alpha = 0f;

        // Title
        var titleLabel = CreateLabel("TitleLabel", gradingRoot.transform,
            new Vector2(0f, 80f), new Vector2(500f, 50f),
            "Result", 32f, FontStyles.Bold, TextAlignmentOptions.Center,
            Color.white);

        // Score
        var scoreLabel = CreateLabel("ScoreLabel", gradingRoot.transform,
            new Vector2(0f, 20f), new Vector2(400f, 40f),
            "Score: 0", 24f, FontStyles.Normal, TextAlignmentOptions.Center,
            Color.white);

        // Days
        var daysLabel = CreateLabel("DaysLabel", gradingRoot.transform,
            new Vector2(0f, -30f), new Vector2(400f, 40f),
            "Days: 0", 24f, FontStyles.Normal, TextAlignmentOptions.Center,
            Color.white);

        // Reason
        var reasonLabel = CreateLabel("ReasonLabel", gradingRoot.transform,
            new Vector2(0f, -80f), new Vector2(500f, 40f),
            "", 20f, FontStyles.Italic, TextAlignmentOptions.Center,
            new Color(1f, 0.4f, 0.3f));

        // Start hidden
        gradingRoot.SetActive(false);

        // FlowerGradingUI component
        var gradingUI = gradingCanvasGO.AddComponent<FlowerGradingUI>();
        var gradingSO = new SerializedObject(gradingUI);

        var sessionRef = gradingSO.FindProperty("session");
        if (sessionRef != null) sessionRef.objectReferenceValue = session;

        var rootRef = gradingSO.FindProperty("root");
        if (rootRef != null) rootRef.objectReferenceValue = rootCG;

        var titleRef = gradingSO.FindProperty("titleLabel");
        if (titleRef != null) titleRef.objectReferenceValue = titleLabel.GetComponent<TMP_Text>();

        var scoreRef = gradingSO.FindProperty("scoreLabel");
        if (scoreRef != null) scoreRef.objectReferenceValue = scoreLabel.GetComponent<TMP_Text>();

        var daysRef = gradingSO.FindProperty("daysLabel");
        if (daysRef != null) daysRef.objectReferenceValue = daysLabel.GetComponent<TMP_Text>();

        var reasonRef = gradingSO.FindProperty("reasonLabel");
        if (reasonRef != null) reasonRef.objectReferenceValue = reasonLabel.GetComponent<TMP_Text>();

        gradingSO.ApplyModifiedPropertiesWithoutUndo();
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

    private static GameObject CreateScreenCanvas(string name, Transform parent)
    {
        var canvasGO = new GameObject(name);
        canvasGO.transform.SetParent(parent);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        return canvasGO;
    }

    private static GameObject CreateLabel(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, FontStyles style, TextAlignmentOptions alignment,
        Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        string fullPath = $"{parentFolder}/{newFolder}";
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parentFolder, newFolder);
    }

    // ── SerializedObject property helpers ──

    private static void SetProp(SerializedObject so, string propName, string value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.stringValue = value;
    }

    private static void SetFloatProp(SerializedObject so, string propName, float value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.floatValue = value;
    }

    private static void SetIntProp(SerializedObject so, string propName, int value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.intValue = value;
    }

    private static void SetBoolProp(SerializedObject so, string propName, bool value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.boolValue = value;
    }

    private static void SetEnumProp(SerializedObject so, string propName, int value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.enumValueIndex = value;
    }
}
