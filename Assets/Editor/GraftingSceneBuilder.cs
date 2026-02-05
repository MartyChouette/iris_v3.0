using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor utility that programmatically builds the grafting scene with
/// two flowers (donor + target), graftable slots, grafting controller/HUD,
/// and a grading UI. Menu: Window > Iris > Build Grafting Scene
/// </summary>
public static class GraftingSceneBuilder
{
    // ── Flower build result (same pattern as other builders) ──
    private struct FlowerBuildResult
    {
        public GameObject root;
        public FlowerGameBrain brain;
        public FlowerSessionController session;
        public FlowerStemRuntime stemRuntime;
        public List<FlowerPartRuntime> parts;
    }

    [MenuItem("Window/Iris/Build Grafting Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Directional light (warm tone) ─────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera + AudioListener ───────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.10f, 0.10f);
        cam.fieldOfView = 60f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 0.8f, -2.0f);
        camGO.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

        // ── 3. Room geometry (floor + back wall) ─────────────────────
        BuildRoom();

        // ── 4. Donor flower (LEFT) ───────────────────────────────────
        var donor = BuildFlower("DonorFlower",
            new Vector3(-0.6f, 0.5f, 0f),
            stemHeight: 1.0f,
            leafCount: 5,
            petalCount: 4);

        // ── 5. Target flower (RIGHT) ─────────────────────────────────
        var target = BuildFlower("TargetFlower",
            new Vector3(0.6f, 0.5f, 0f),
            stemHeight: 1.0f,
            leafCount: 1,
            petalCount: 1);

        // ── 6. GraftableSlots on target flower ───────────────────────
        BuildGraftableSlots(target.root, target.stemRuntime, 1.0f);

        // ── 7. Managers (GraftingController + GraftingHUD) ───────────
        var managersGO = new GameObject("Managers");
        var controller = managersGO.AddComponent<GraftingController>();
        var hud = managersGO.AddComponent<GraftingHUD>();

        // Wire GraftingController
        var controllerSO = new SerializedObject(controller);
        controllerSO.FindProperty("donorBrain").objectReferenceValue = donor.brain;
        controllerSO.FindProperty("targetBrain").objectReferenceValue = target.brain;
        controllerSO.FindProperty("mainCamera").objectReferenceValue = cam;
        controllerSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire GraftingHUD -> controller
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("controller").objectReferenceValue = controller;

        // ── 8. Screen-space canvas + UI ──────────────────────────────
        var uiCanvasGO = CreateScreenCanvas("GraftingUI_Canvas", managersGO.transform);

        // Instruction label (bottom-center)
        var instructionLabel = CreateLabel("InstructionLabel", uiCanvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, -180f), new Vector2(600f, 40f),
            "Click a part on the LEFT flower to pick it up", 18f,
            TextAlignmentOptions.Center);

        // Donor count label (top-left area)
        var donorCountLabel = CreateLabel("DonorCountLabel", uiCanvasGO.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(120f, -30f), new Vector2(200f, 30f),
            "Donor: 9 parts", 18f,
            TextAlignmentOptions.Left);

        // Target count label (top-right area)
        var targetCountLabel = CreateLabel("TargetCountLabel", uiCanvasGO.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-120f, -30f), new Vector2(200f, 30f),
            "Grafted: 0/4 slots", 18f,
            TextAlignmentOptions.Right);

        // Wire HUD labels
        hudSO.FindProperty("instructionLabel").objectReferenceValue =
            instructionLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("donorCountLabel").objectReferenceValue =
            donorCountLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("targetCountLabel").objectReferenceValue =
            targetCountLabel.GetComponent<TMP_Text>();
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // "Evaluate Graft" Button (bottom-right)
        BuildEvaluateButton(uiCanvasGO.transform, target.session);

        // ── 9. FlowerGradingUI wired to target session ───────────────
        BuildGradingUI(managersGO.transform, target.session);

        // ── 10. Save scene ───────────────────────────────────────────
        string dir = "Assets/Scenes";
        EnsureFolder("Assets", "Scenes");

        string path = $"{dir}/grafting.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[GraftingSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Room geometry
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        // Floor
        CreateBox("Floor", parent.transform,
            new Vector3(0f, 0f, 0f), new Vector3(4f, 0.1f, 4f),
            new Color(0.30f, 0.28f, 0.25f));

        // Back wall
        CreateBox("BackWall", parent.transform,
            new Vector3(0f, 1.5f, 2f), new Vector3(4f, 3f, 0.15f),
            new Color(0.40f, 0.38f, 0.35f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Inline flower builder (parameterized by leaf/petal count)
    // ════════════════════════════════════════════════════════════════════

    private static FlowerBuildResult BuildFlower(string name, Vector3 position,
        float stemHeight, int leafCount, int petalCount)
    {
        var result = new FlowerBuildResult();
        result.parts = new List<FlowerPartRuntime>();

        // Root
        var root = new GameObject(name);
        root.transform.position = position;
        result.root = root;

        // ── Stem (Cylinder, green) ──
        var stemGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stemGO.name = "Stem";
        stemGO.transform.SetParent(root.transform);
        stemGO.transform.localPosition = new Vector3(0f, stemHeight * 0.5f, 0f);
        stemGO.transform.localScale = new Vector3(0.04f, stemHeight * 0.5f, 0.04f);
        stemGO.isStatic = false;
        SetColor(stemGO, new Color(0.2f, 0.55f, 0.2f));

        var stemRuntime = stemGO.AddComponent<FlowerStemRuntime>();
        var stemRb = stemGO.AddComponent<Rigidbody>();
        stemRb.isKinematic = true;

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
        cutNormal.transform.localRotation = Quaternion.identity;
        stemRuntime.cutNormalRef = cutNormal.transform;

        result.stemRuntime = stemRuntime;

        // ── Crown (Sphere) at stem top ──
        var crownGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crownGO.name = "Crown";
        crownGO.transform.SetParent(root.transform);
        crownGO.transform.localPosition = new Vector3(0f, stemHeight, 0f);
        crownGO.transform.localScale = Vector3.one * 0.08f;
        crownGO.isStatic = false;
        SetColor(crownGO, new Color(0.9f, 0.85f, 0.3f));

        var crownRb = crownGO.AddComponent<Rigidbody>();
        crownRb.isKinematic = true;
        var crownPart = crownGO.AddComponent<FlowerPartRuntime>();

        var crownPartSO = new SerializedObject(crownPart);
        SetProperty(crownPartSO, "PartId", $"{name}_Crown");
        SetEnumProperty(crownPartSO, "kind", (int)FlowerPartKind.Crown);
        SetBoolProperty(crownPartSO, "canCauseGameOver", true);
        SetBoolProperty(crownPartSO, "contributesToScore", true);
        SetFloatProperty(crownPartSO, "scoreWeight", 1f);
        crownPartSO.ApplyModifiedPropertiesWithoutUndo();

        result.parts.Add(crownPart);

        // ── Leaves ──
        for (int i = 0; i < leafCount; i++)
        {
            float angle = i * (360f / Mathf.Max(leafCount, 1));
            float rad = angle * Mathf.Deg2Rad;
            float t = leafCount > 1 ? (float)i / (leafCount - 1) : 0.5f;
            float height = Mathf.Lerp(0.2f, 0.6f, t) * stemHeight;
            float radius = 0.12f;

            float x = Mathf.Cos(rad) * radius;
            float z = Mathf.Sin(rad) * radius;

            var leafGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leafGO.name = $"Leaf_{i}";
            leafGO.transform.SetParent(root.transform);
            leafGO.transform.localPosition = new Vector3(x, height, z);
            leafGO.transform.localScale = new Vector3(0.06f, 0.02f, 0.08f);
            leafGO.isStatic = false;
            SetColor(leafGO, new Color(0.25f, 0.65f, 0.25f));

            var leafRb = leafGO.AddComponent<Rigidbody>();
            leafRb.isKinematic = false;
            leafRb.mass = 0.2f;

            var leafPart = leafGO.AddComponent<FlowerPartRuntime>();
            var leafSO = new SerializedObject(leafPart);
            SetProperty(leafSO, "PartId", $"{name}_Leaf_{i}");
            SetEnumProperty(leafSO, "kind", (int)FlowerPartKind.Leaf);
            SetBoolProperty(leafSO, "canCauseGameOver", false);
            SetBoolProperty(leafSO, "contributesToScore", true);
            SetFloatProperty(leafSO, "scoreWeight", 1f);
            leafSO.ApplyModifiedPropertiesWithoutUndo();

            result.parts.Add(leafPart);
        }

        // ── Petals ──
        for (int i = 0; i < petalCount; i++)
        {
            float angle = i * (360f / Mathf.Max(petalCount, 1));
            float rad = angle * Mathf.Deg2Rad;
            float t = petalCount > 1 ? (float)i / (petalCount - 1) : 0.5f;
            float height = Mathf.Lerp(0.7f, 0.9f, t) * stemHeight;
            float radius = 0.08f;

            float x = Mathf.Cos(rad) * radius;
            float z = Mathf.Sin(rad) * radius;

            var petalGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            petalGO.name = $"Petal_{i}";
            petalGO.transform.SetParent(root.transform);
            petalGO.transform.localPosition = new Vector3(x, height, z);
            petalGO.transform.localScale = new Vector3(0.05f, 0.015f, 0.05f);
            petalGO.isStatic = false;
            SetColor(petalGO, new Color(0.9f, 0.45f, 0.55f));

            var petalRb = petalGO.AddComponent<Rigidbody>();
            petalRb.isKinematic = false;
            petalRb.mass = 0.1f;

            var petalPart = petalGO.AddComponent<FlowerPartRuntime>();
            var petalSO = new SerializedObject(petalPart);
            SetProperty(petalSO, "PartId", $"{name}_Petal_{i}");
            SetEnumProperty(petalSO, "kind", (int)FlowerPartKind.Petal);
            SetBoolProperty(petalSO, "canCauseGameOver", false);
            SetBoolProperty(petalSO, "contributesToScore", true);
            SetFloatProperty(petalSO, "scoreWeight", 1f);
            petalSO.ApplyModifiedPropertiesWithoutUndo();

            result.parts.Add(petalPart);
        }

        // ── FlowerGameBrain + FlowerSessionController on root ──
        var brain = root.AddComponent<FlowerGameBrain>();
        var session = root.AddComponent<FlowerSessionController>();

        // Wire brain -> stem + parts
        var brainSO = new SerializedObject(brain);
        brainSO.FindProperty("stem").objectReferenceValue = stemRuntime;

        var partsArrayProp = brainSO.FindProperty("parts");
        partsArrayProp.ClearArray();
        for (int i = 0; i < result.parts.Count; i++)
        {
            partsArrayProp.InsertArrayElementAtIndex(i);
            partsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = result.parts[i];
        }
        brainSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire session -> brain
        var sessionSO = new SerializedObject(session);
        sessionSO.FindProperty("brain").objectReferenceValue = brain;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        // Create IdealFlowerDefinition + FlowerTypeDefinition SOs
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "Flowers");

        var ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();
        string idealPath = $"Assets/ScriptableObjects/Flowers/Ideal_{name}.asset";
        idealPath = AssetDatabase.GenerateUniqueAssetPath(idealPath);
        AssetDatabase.CreateAsset(ideal, idealPath);

        var typeDef = ScriptableObject.CreateInstance<FlowerTypeDefinition>();
        string typePath = $"Assets/ScriptableObjects/Flowers/Type_{name}.asset";
        typePath = AssetDatabase.GenerateUniqueAssetPath(typePath);
        AssetDatabase.CreateAsset(typeDef, typePath);

        // Wire brain -> ideal
        brainSO = new SerializedObject(brain);
        brainSO.FindProperty("ideal").objectReferenceValue = ideal;
        brainSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire session -> FlowerType
        sessionSO = new SerializedObject(session);
        sessionSO.FindProperty("FlowerType").objectReferenceValue = typeDef;
        sessionSO.ApplyModifiedPropertiesWithoutUndo();

        result.brain = brain;
        result.session = session;

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // GraftableSlots on target flower
    // ════════════════════════════════════════════════════════════════════

    private static void BuildGraftableSlots(GameObject targetRoot,
        FlowerStemRuntime stemRuntime, float stemHeight)
    {
        // Slot 0: Leaf slot at angle=90deg, height=0.3*stemHeight
        CreateGraftableSlot("GraftSlot_Leaf_0", targetRoot.transform,
            angleDeg: 90f, height: 0.3f * stemHeight, radius: 0.1f,
            acceptedKind: FlowerPartKind.Leaf);

        // Slot 1: Leaf slot at angle=210deg, height=0.4*stemHeight
        CreateGraftableSlot("GraftSlot_Leaf_1", targetRoot.transform,
            angleDeg: 210f, height: 0.4f * stemHeight, radius: 0.1f,
            acceptedKind: FlowerPartKind.Leaf);

        // Slot 2: Petal slot at angle=45deg, height=0.8*stemHeight
        CreateGraftableSlot("GraftSlot_Petal_0", targetRoot.transform,
            angleDeg: 45f, height: 0.8f * stemHeight, radius: 0.08f,
            acceptedKind: FlowerPartKind.Petal);

        // Slot 3: Petal slot at angle=180deg, height=0.85*stemHeight
        CreateGraftableSlot("GraftSlot_Petal_1", targetRoot.transform,
            angleDeg: 180f, height: 0.85f * stemHeight, radius: 0.08f,
            acceptedKind: FlowerPartKind.Petal);
    }

    private static void CreateGraftableSlot(string name, Transform parent,
        float angleDeg, float height, float radius, FlowerPartKind acceptedKind)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float x = Mathf.Cos(rad) * radius;
        float z = Mathf.Sin(rad) * radius;

        var slotGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        slotGO.name = name;
        slotGO.transform.SetParent(parent);
        slotGO.transform.localPosition = new Vector3(x, height, z);
        slotGO.transform.localScale = Vector3.one * 0.04f;
        slotGO.isStatic = false;

        // Semi-transparent green for empty slot indicator
        SetColor(slotGO, new Color(0.3f, 0.9f, 0.4f, 0.5f));

        var slot = slotGO.AddComponent<GraftableSlot>();

        // Wire acceptedKind via SerializedObject (Leaf=0, Petal=1)
        var so = new SerializedObject(slot);
        so.FindProperty("acceptedKind").enumValueIndex = (int)acceptedKind;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════════
    // Evaluate Button
    // ════════════════════════════════════════════════════════════════════

    private static void BuildEvaluateButton(Transform canvasTransform,
        FlowerSessionController targetSession)
    {
        var btnGO = new GameObject("EvaluateButton");
        btnGO.transform.SetParent(canvasTransform);

        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1f, 0f);
        btnRT.anchorMax = new Vector2(1f, 0f);
        btnRT.pivot = new Vector2(1f, 0f);
        btnRT.anchoredPosition = new Vector2(-20f, 20f);
        btnRT.sizeDelta = new Vector2(160f, 40f);
        btnRT.localScale = Vector3.one;

        var btnImg = btnGO.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = new Color(0.3f, 0.6f, 0.3f);

        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();

        // Wire onClick to targetSession.EvaluateCurrentFlower via persistent listener
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            btn.onClick, targetSession.EvaluateCurrentFlower);

        // Button label
        var labelGO = new GameObject("ButtonLabel");
        labelGO.transform.SetParent(btnGO.transform);

        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelRT.localScale = Vector3.one;

        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "Evaluate Graft";
        labelTMP.fontSize = 16f;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color = Color.white;
    }

    // ════════════════════════════════════════════════════════════════════
    // FlowerGradingUI wired to target session
    // ════════════════════════════════════════════════════════════════════

    private static void BuildGradingUI(Transform parent, FlowerSessionController targetSession)
    {
        var gradingGO = new GameObject("FlowerGradingUI");
        gradingGO.transform.SetParent(parent);

        var gradingUI = gradingGO.AddComponent<FlowerGradingUI>();

        // CanvasGroup for fade
        var canvasGroup = gradingGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Grading canvas (screen-space overlay)
        var gradCanvasGO = new GameObject("GradingCanvas");
        gradCanvasGO.transform.SetParent(gradingGO.transform);

        var gradCanvas = gradCanvasGO.AddComponent<Canvas>();
        gradCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gradCanvas.sortingOrder = 20;
        gradCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        gradCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Background panel
        var panelGO = new GameObject("GradingPanel");
        panelGO.transform.SetParent(gradCanvasGO.transform);

        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(400f, 250f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.localScale = Vector3.one;

        var panelBG = panelGO.AddComponent<UnityEngine.UI.Image>();
        panelBG.color = new Color(0f, 0f, 0f, 0.8f);

        // Title label
        var titleGO = CreateGradingLabel("TitleLabel", panelGO.transform,
            new Vector2(0f, 80f), new Vector2(380f, 40f),
            "Grade", 28f);

        // Score label
        var scoreGO = CreateGradingLabel("ScoreLabel", panelGO.transform,
            new Vector2(0f, 30f), new Vector2(380f, 30f),
            "Score: 0", 22f);

        // Days label
        var daysGO = CreateGradingLabel("DaysLabel", panelGO.transform,
            new Vector2(0f, -10f), new Vector2(380f, 30f),
            "Days: 0", 22f);

        // Reason label
        var reasonGO = CreateGradingLabel("ReasonLabel", panelGO.transform,
            new Vector2(0f, -50f), new Vector2(380f, 30f),
            "", 16f);

        // Wire FlowerGradingUI fields
        var gradingSO = new SerializedObject(gradingUI);
        gradingSO.FindProperty("session").objectReferenceValue = targetSession;
        gradingSO.FindProperty("root").objectReferenceValue = canvasGroup;
        gradingSO.FindProperty("titleLabel").objectReferenceValue = titleGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("scoreLabel").objectReferenceValue = scoreGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("daysLabel").objectReferenceValue = daysGO.GetComponent<TMP_Text>();
        gradingSO.FindProperty("reasonLabel").objectReferenceValue = reasonGO.GetComponent<TMP_Text>();
        gradingSO.ApplyModifiedPropertiesWithoutUndo();

        // Start hidden
        gradingGO.SetActive(false);
    }

    private static GameObject CreateGradingLabel(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size, string text, float fontSize)
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
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go;
    }

    // ════════════════════════════════════════════════════════════════════
    // Screen-space canvas helper
    // ════════════════════════════════════════════════════════════════════

    private static GameObject CreateScreenCanvas(string name, Transform parent)
    {
        var canvasGO = new GameObject(name);
        canvasGO.transform.SetParent(parent);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        return canvasGO;
    }

    // ════════════════════════════════════════════════════════════════════
    // Label helper
    // ════════════════════════════════════════════════════════════════════

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

    private static void EnsureFolder(string parentFolder, string newFolder)
    {
        if (!AssetDatabase.IsValidFolder($"{parentFolder}/{newFolder}"))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolder);
        }
    }

    // ── SerializedObject helpers ──

    private static void SetProperty(SerializedObject so, string propName, string value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.stringValue = value;
    }

    private static void SetEnumProperty(SerializedObject so, string propName, int value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.enumValueIndex = value;
    }

    private static void SetBoolProperty(SerializedObject so, string propName, bool value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.boolValue = value;
    }

    private static void SetFloatProperty(SerializedObject so, string propName, float value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.floatValue = value;
    }
}
