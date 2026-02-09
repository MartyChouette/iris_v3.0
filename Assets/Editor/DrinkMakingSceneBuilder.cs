using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that builds a complete drink-making prototype scene.
/// Creates SO assets, room geometry, glass, bottles, UI, and wires everything.
/// Menu: Window > Iris > Build Drink Making Scene
/// </summary>
public static class DrinkMakingSceneBuilder
{
    [MenuItem("Window/Iris/Build Drink Making Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Ensure SO folders ───────────────────────────────────────
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "DrinkMaking");

        // ── 2. Create ScriptableObject assets ──────────────────────────
        // Ingredients
        var gin          = CreateIngredient("Gin",          new Color(0.9f, 0.92f, 0.95f, 0.4f), 0.15f, 0f,   1f,   0.3f, 0f,   0.3f);
        var whisky       = CreateIngredient("Whisky",       new Color(0.7f, 0.45f, 0.15f, 0.8f), 0.12f, 0f,   1f,   0.3f, 0f,   0.6f);
        var tonic        = CreateIngredient("Tonic Water",  new Color(0.85f, 0.9f, 0.8f, 0.5f),  0.18f, 0.7f, 2.5f, 0.2f, 0.05f,0.3f);
        var milk         = CreateIngredient("Milk",         new Color(0.95f, 0.95f, 0.92f, 0.9f),0.10f, 0f,   1f,   0.4f, 0f,   0.8f);
        var champagne    = CreateIngredient("Champagne",    new Color(0.95f, 0.90f, 0.6f, 0.5f), 0.14f, 0.9f, 3f,   0.15f,0.08f,0.35f);
        var redWine      = CreateIngredient("Red Wine",     new Color(0.5f, 0.1f, 0.15f, 0.9f),  0.12f, 0f,   1f,   0.3f, 0f,   0.5f);
        var whiteWine    = CreateIngredient("White Wine",   new Color(0.9f, 0.85f, 0.5f, 0.6f),  0.13f, 0.1f, 1.2f, 0.25f,0f,   0.4f);

        // Glasses
        var shotGlass    = CreateGlass("Shot Glass",  0.3f, 0.85f, 0.05f, 0.15f, 0.06f, 0.015f, new Color(0.9f, 0.95f, 1f, 0.2f));
        var rocksGlass   = CreateGlass("Rocks Glass", 0.7f, 0.75f, 0.06f, 0.25f, 0.10f, 0.035f, new Color(0.9f, 0.9f, 0.85f, 0.25f));
        var highball     = CreateGlass("Highball",    1.0f, 0.80f, 0.05f, 0.20f, 0.14f, 0.025f, new Color(0.9f, 0.95f, 1f, 0.2f));

        // Recipes
        var ginTonic = CreateRecipe("Gin & Tonic", highball,
            new[] { gin, tonic }, new[] { 0.3f, 0.7f },
            true, 2f, 1.5f, 4f, 100);

        var whiskyNeat = CreateRecipe("Whisky Neat", rocksGlass,
            new[] { whisky }, new[] { 1.0f },
            false, 0f, 0f, 0f, 100);

        var champagneFlute = CreateRecipe("Champagne", highball,
            new[] { champagne }, new[] { 1.0f },
            false, 0f, 0f, 0f, 100);

        var mudslide = CreateRecipe("Mudslide", rocksGlass,
            new[] { whisky, milk }, new[] { 0.4f, 0.6f },
            true, 3f, 1.5f, 4f, 100);

        AssetDatabase.SaveAssets();

        // ── 3. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 4. Main Camera ────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.10f, 0.10f);
        cam.fieldOfView = 50f;
        camGO.AddComponent<AudioListener>();
        camGO.transform.position = new Vector3(0f, 0.75f, -0.8f);
        camGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

        // ── 5. Room geometry ──────────────────────────────────────────
        var roomParent = new GameObject("Room");

        CreateBox("Floor", roomParent.transform,
            new Vector3(0f, 0f, 0f), new Vector3(4f, 0.1f, 4f),
            new Color(0.25f, 0.22f, 0.20f));

        CreateBox("BackWall", roomParent.transform,
            new Vector3(0f, 1.5f, 1.5f), new Vector3(4f, 3f, 0.15f),
            new Color(0.35f, 0.33f, 0.30f));

        // ── 6. Counter ────────────────────────────────────────────────
        float counterY = 0.4f;
        var counterGO = CreateBox("Counter", roomParent.transform,
            new Vector3(0f, counterY * 0.5f, 0.3f), new Vector3(1.8f, counterY, 0.6f),
            new Color(0.45f, 0.30f, 0.18f));

        // ── 7. Glass ──────────────────────────────────────────────────
        var glassParent = new GameObject("Glass");
        glassParent.transform.position = new Vector3(0f, counterY, 0.3f);

        // Glass shell (transparent box)
        float glassH = highball.worldHeight;
        float glassR = highball.worldRadius;

        var shellGO = CreateBox("GlassShell", glassParent.transform,
            new Vector3(0f, glassH * 0.5f, 0f),
            new Vector3(glassR * 2f, glassH, glassR * 2f),
            new Color(0.9f, 0.95f, 1f, 0.15f));

        // Make shell transparent
        var shellRend = shellGO.GetComponent<Renderer>();
        if (shellRend != null && shellRend.sharedMaterial != null)
        {
            shellRend.sharedMaterial.SetFloat("_Surface", 1f); // Transparent
            shellRend.sharedMaterial.SetFloat("_Blend", 0f);
            shellRend.sharedMaterial.SetOverrideTag("RenderType", "Transparent");
            shellRend.sharedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            shellRend.sharedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            shellRend.sharedMaterial.SetInt("_ZWrite", 0);
            shellRend.sharedMaterial.renderQueue = 3000;
        }

        // Add collider on glass parent for raycasting
        var glassCollider = glassParent.AddComponent<BoxCollider>();
        glassCollider.center = new Vector3(0f, glassH * 0.5f, 0f);
        glassCollider.size = new Vector3(glassR * 3f, glassH, glassR * 3f);

        // Liquid visual (coloured box inside glass)
        var liquidGO = CreateBox("Liquid", glassParent.transform,
            new Vector3(0f, 0.001f, 0f),
            new Vector3(glassR * 1.8f, 0.001f, glassR * 1.8f),
            new Color(0.5f, 0.5f, 0.3f, 0.6f));
        liquidGO.isStatic = false;

        // Foam visual (white box on top of liquid)
        var foamGO = CreateBox("Foam", glassParent.transform,
            new Vector3(0f, 0.001f, 0f),
            new Vector3(glassR * 1.8f, 0.001f, glassR * 1.8f),
            new Color(1f, 1f, 0.95f, 0.7f));
        foamGO.isStatic = false;

        // Fill line marker (thin red line)
        var fillLineGO = CreateBox("FillLine", glassParent.transform,
            new Vector3(0f, glassH * highball.fillLineNormalized, 0f),
            new Vector3(glassR * 2.2f, 0.002f, glassR * 2.2f),
            new Color(0.9f, 0.2f, 0.2f, 0.8f));
        fillLineGO.isStatic = false;

        // GlassController component
        var glassCtrl = glassParent.AddComponent<GlassController>();
        var glassCtrlSO = new SerializedObject(glassCtrl);
        glassCtrlSO.FindProperty("definition").objectReferenceValue = highball;
        glassCtrlSO.FindProperty("liquidRenderer").objectReferenceValue = liquidGO.GetComponent<Renderer>();
        glassCtrlSO.FindProperty("foamRenderer").objectReferenceValue = foamGO.GetComponent<Renderer>();
        glassCtrlSO.FindProperty("fillLineMarker").objectReferenceValue = fillLineGO.transform;
        glassCtrlSO.FindProperty("liquidTransform").objectReferenceValue = liquidGO.transform;
        glassCtrlSO.FindProperty("foamTransform").objectReferenceValue = foamGO.transform;
        glassCtrlSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 8. Bottles ────────────────────────────────────────────────
        var bottleIngredients = new[] { gin, whisky, tonic, milk };
        var bottleColors = new[]
        {
            new Color(0.85f, 0.9f, 0.95f),   // gin — clear
            new Color(0.6f, 0.35f, 0.12f),   // whisky — amber
            new Color(0.4f, 0.7f, 0.35f),    // tonic — green
            new Color(0.92f, 0.92f, 0.9f),   // milk — white
        };
        var bottleNames = new[] { "Bottle_Gin", "Bottle_Whisky", "Bottle_Tonic", "Bottle_Milk" };
        var bottleCtrls = new BottleController[bottleIngredients.Length];

        float bottleSpacing = 0.22f;
        float bottleStartX = -(bottleIngredients.Length - 1) * bottleSpacing * 0.5f;

        for (int i = 0; i < bottleIngredients.Length; i++)
        {
            float bx = bottleStartX + i * bottleSpacing;
            var bottleGO = CreateBox(bottleNames[i], roomParent.transform,
                new Vector3(bx, counterY + 0.08f, 0.55f),
                new Vector3(0.04f, 0.16f, 0.04f),
                bottleColors[i]);
            bottleGO.isStatic = false;

            // Collider for click detection
            var existingCollider = bottleGO.GetComponent<Collider>();
            if (existingCollider == null)
                bottleGO.AddComponent<BoxCollider>();

            var bc = bottleGO.AddComponent<BottleController>();
            bottleCtrls[i] = bc;

            var bcSO = new SerializedObject(bc);
            bcSO.FindProperty("ingredient").objectReferenceValue = bottleIngredients[i];
            bcSO.FindProperty("bottleRenderer").objectReferenceValue = bottleGO.GetComponent<Renderer>();
            bcSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 9. Managers GO ────────────────────────────────────────────
        var managersGO = new GameObject("Managers");
        var stirCtrl = managersGO.AddComponent<StirController>();
        var mgr = managersGO.AddComponent<DrinkMakingManager>();
        var hud = managersGO.AddComponent<DrinkMakingHUD>();

        // Wire manager
        var mgrSO = new SerializedObject(mgr);
        mgrSO.FindProperty("glass").objectReferenceValue = glassCtrl;
        mgrSO.FindProperty("stirrer").objectReferenceValue = stirCtrl;
        mgrSO.FindProperty("mainCamera").objectReferenceValue = cam;
        mgrSO.FindProperty("hud").objectReferenceValue = hud;

        // Bottles array
        var bottlesProp = mgrSO.FindProperty("bottles");
        bottlesProp.ClearArray();
        for (int i = 0; i < bottleCtrls.Length; i++)
        {
            bottlesProp.InsertArrayElementAtIndex(i);
            bottlesProp.GetArrayElementAtIndex(i).objectReferenceValue = bottleCtrls[i];
        }

        // Recipes array
        var allRecipes = new[] { ginTonic, whiskyNeat, champagneFlute, mudslide };
        var recipesProp = mgrSO.FindProperty("availableRecipes");
        recipesProp.ClearArray();
        for (int i = 0; i < allRecipes.Length; i++)
        {
            recipesProp.InsertArrayElementAtIndex(i);
            recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = allRecipes[i];
        }
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 10. UI Canvas ─────────────────────────────────────────────
        var canvasGO = CreateScreenCanvas("DrinkMakingUI_Canvas", managersGO.transform);

        // Recipe name (top-center)
        var recipeNameLabel = CreateLabel("RecipeNameLabel", canvasGO.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(400f, 40f),
            "Choose a Recipe", 24f, TextAlignmentOptions.Center);

        // Instruction hint (bottom-center)
        var instructionLabel = CreateLabel("InstructionLabel", canvasGO.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(500f, 30f),
            "Click a recipe to begin", 16f, TextAlignmentOptions.Center);

        // Fill level (right side)
        var fillLabel = CreateLabel("FillLevelLabel", canvasGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, 30f), new Vector2(150f, 30f),
            "", 18f, TextAlignmentOptions.Right);

        // Foam level (right side, below fill)
        var foamLabel = CreateLabel("FoamLevelLabel", canvasGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, -10f), new Vector2(150f, 30f),
            "", 18f, TextAlignmentOptions.Right);

        // Stir quality (right side, below foam)
        var stirLabel = CreateLabel("StirQualityLabel", canvasGO.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-80f, -50f), new Vector2(150f, 30f),
            "", 18f, TextAlignmentOptions.Right);

        // Score label (center, used in scoring state)
        var scoreLabel = CreateLabel("ScoreLabel", canvasGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(500f, 120f),
            "", 20f, TextAlignmentOptions.Center);

        // ── Recipe selection panel ─────────────────────────────────────
        var recipePanelGO = new GameObject("RecipePanel");
        recipePanelGO.transform.SetParent(canvasGO.transform);
        var recipePanelRT = recipePanelGO.AddComponent<RectTransform>();
        recipePanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        recipePanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        recipePanelRT.sizeDelta = new Vector2(500f, 200f);
        recipePanelRT.anchoredPosition = Vector2.zero;
        recipePanelRT.localScale = Vector3.one;

        for (int i = 0; i < allRecipes.Length; i++)
        {
            BuildRecipeButton(recipePanelGO.transform, allRecipes[i].drinkName, i, mgr,
                new Vector2(-150f + i * 100f, 0f));
        }

        // ── Pouring panel (Done Pouring button) ───────────────────────
        var pouringPanelGO = new GameObject("PouringPanel");
        pouringPanelGO.transform.SetParent(canvasGO.transform);
        var pouringRT = pouringPanelGO.AddComponent<RectTransform>();
        pouringRT.anchorMin = new Vector2(1f, 0f);
        pouringRT.anchorMax = new Vector2(1f, 0f);
        pouringRT.pivot = new Vector2(1f, 0f);
        pouringRT.anchoredPosition = new Vector2(-20f, 20f);
        pouringRT.sizeDelta = new Vector2(160f, 40f);
        pouringRT.localScale = Vector3.one;

        BuildActionButton(pouringPanelGO.transform, "Done Pouring",
            mgr, nameof(DrinkMakingManager.FinishPouring), Vector2.zero,
            new Color(0.3f, 0.55f, 0.3f));

        // ── Stirring panel (Done Stirring button) ─────────────────────
        var stirringPanelGO = new GameObject("StirringPanel");
        stirringPanelGO.transform.SetParent(canvasGO.transform);
        var stirringRT = stirringPanelGO.AddComponent<RectTransform>();
        stirringRT.anchorMin = new Vector2(1f, 0f);
        stirringRT.anchorMax = new Vector2(1f, 0f);
        stirringRT.pivot = new Vector2(1f, 0f);
        stirringRT.anchoredPosition = new Vector2(-20f, 20f);
        stirringRT.sizeDelta = new Vector2(160f, 40f);
        stirringRT.localScale = Vector3.one;

        BuildActionButton(stirringPanelGO.transform, "Done Stirring",
            mgr, nameof(DrinkMakingManager.FinishStirring), Vector2.zero,
            new Color(0.3f, 0.45f, 0.6f));

        // ── Scoring panel (Retry + Next buttons) ──────────────────────
        var scoringPanelGO = new GameObject("ScoringPanel");
        scoringPanelGO.transform.SetParent(canvasGO.transform);
        var scoringPanelRT = scoringPanelGO.AddComponent<RectTransform>();
        scoringPanelRT.anchorMin = new Vector2(0.5f, 0.5f);
        scoringPanelRT.anchorMax = new Vector2(0.5f, 0.5f);
        scoringPanelRT.sizeDelta = new Vector2(350f, 50f);
        scoringPanelRT.anchoredPosition = new Vector2(0f, -80f);
        scoringPanelRT.localScale = Vector3.one;

        BuildActionButton(scoringPanelGO.transform, "Retry",
            mgr, nameof(DrinkMakingManager.Retry), new Vector2(-80f, 0f),
            new Color(0.6f, 0.45f, 0.3f));

        BuildActionButton(scoringPanelGO.transform, "Next Recipe",
            mgr, nameof(DrinkMakingManager.NextRecipe), new Vector2(80f, 0f),
            new Color(0.3f, 0.55f, 0.3f));

        // ── Wire HUD ──────────────────────────────────────────────────
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("manager").objectReferenceValue = mgr;
        hudSO.FindProperty("glass").objectReferenceValue = glassCtrl;
        hudSO.FindProperty("stirrer").objectReferenceValue = stirCtrl;
        hudSO.FindProperty("recipeNameLabel").objectReferenceValue = recipeNameLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("instructionLabel").objectReferenceValue = instructionLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("fillLevelLabel").objectReferenceValue = fillLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("foamLevelLabel").objectReferenceValue = foamLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("stirQualityLabel").objectReferenceValue = stirLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("scoreLabel").objectReferenceValue = scoreLabel.GetComponent<TMP_Text>();
        hudSO.FindProperty("scoringPanel").objectReferenceValue = scoringPanelGO;
        hudSO.FindProperty("recipePanel").objectReferenceValue = recipePanelGO;
        hudSO.FindProperty("pouringPanel").objectReferenceValue = pouringPanelGO;
        hudSO.FindProperty("stirringPanel").objectReferenceValue = stirringPanelGO;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 11. Save scene ────────────────────────────────────────────
        EnsureFolder("Assets", "Scenes");
        string path = "Assets/Scenes/drink_making.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[DrinkMakingSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // SO creation helpers
    // ════════════════════════════════════════════════════════════════════

    private static DrinkIngredientDefinition CreateIngredient(
        string name, Color color, float pourRate, float fizziness,
        float foamRateMul, float foamSettle, float flattenRate, float viscosity)
    {
        var so = ScriptableObject.CreateInstance<DrinkIngredientDefinition>();
        so.ingredientName = name;
        so.liquidColor = color;
        so.pourRate = pourRate;
        so.fizziness = fizziness;
        so.foamRateMultiplier = foamRateMul;
        so.foamSettleRate = foamSettle;
        so.flattenRate = flattenRate;
        so.viscosity = viscosity;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/DrinkMaking/Ingredient_{name.Replace(" ", "_")}.asset");
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    private static GlassDefinition CreateGlass(
        string name, float capacity, float fillLine, float fillTol,
        float foamHead, float worldH, float worldR, Color color)
    {
        var so = ScriptableObject.CreateInstance<GlassDefinition>();
        so.glassName = name;
        so.capacity = capacity;
        so.fillLineNormalized = fillLine;
        so.fillLineTolerance = fillTol;
        so.foamHeadroom = foamHead;
        so.worldHeight = worldH;
        so.worldRadius = worldR;
        so.glassColor = color;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/DrinkMaking/Glass_{name.Replace(" ", "_")}.asset");
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    private static DrinkRecipeDefinition CreateRecipe(
        string name, GlassDefinition glass,
        DrinkIngredientDefinition[] ingredients, float[] portions,
        bool requiresStir, float stirDur, float stirMin, float stirMax, int baseScore)
    {
        var so = ScriptableObject.CreateInstance<DrinkRecipeDefinition>();
        so.drinkName = name;
        so.requiredGlass = glass;
        so.ingredients = ingredients;
        so.portionNormalized = portions;
        so.requiresStir = requiresStir;
        so.stirDuration = stirDur;
        so.perfectStirSpeedMin = stirMin;
        so.perfectStirSpeedMax = stirMax;
        so.baseScore = baseScore;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/ScriptableObjects/DrinkMaking/Recipe_{name.Replace(" ", "_").Replace("&", "And")}.asset");
        AssetDatabase.CreateAsset(so, assetPath);
        return so;
    }

    // ════════════════════════════════════════════════════════════════════
    // UI button builders
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRecipeButton(Transform parent, string recipeName,
        int recipeIndex, DrinkMakingManager mgr, Vector2 position)
    {
        var btnGO = new GameObject($"RecipeBtn_{recipeIndex}");
        btnGO.transform.SetParent(parent);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(90f, 60f);
        rt.anchoredPosition = position;
        rt.localScale = Vector3.one;

        var img = btnGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.25f, 0.25f, 0.35f);

        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();

        // Wire onClick → manager.SelectRecipe(index) via persistent listener
        int capturedIndex = recipeIndex;
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
            btn.onClick, mgr.SelectRecipe, capturedIndex);

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform);

        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelRT.localScale = Vector3.one;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = recipeName;
        tmp.fontSize = 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
    }

    private static void BuildActionButton(Transform parent, string label,
        DrinkMakingManager mgr, string methodName, Vector2 position, Color bgColor)
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
        var method = typeof(DrinkMakingManager).GetMethod(methodName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), mgr, method)
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
