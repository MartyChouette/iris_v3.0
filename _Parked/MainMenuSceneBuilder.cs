using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the main menu scene matching L_Main_Menu layout:
/// Nema head with parallax, title on left, Start/Options/Quit buttons on right,
/// TextDissolveButton + TMP_FocusBlur effects, tutorial card overlay, ScreenFade.
/// Menu: Window > Iris > Build Main Menu Scene
/// </summary>
public static class MainMenuSceneBuilder
{
    private const string SoDir = "Assets/ScriptableObjects/GameModes";

    // Button layout (right side, matching L_Main_Menu)
    private static readonly Vector2 ButtonSize = new Vector2(400f, 100f);
    private static readonly float ButtonX = 800f;
    private static readonly float StartButtonY = -140f;
    private static readonly float OptionsButtonY = -240f;
    private static readonly float QuitButtonY = -340f;

    // Title layout (left side)
    private static readonly Vector2 TitlePos = new Vector2(-600f, 340f);
    private static readonly Vector2 TitleSize = new Vector2(500f, 300f);

    [MenuItem("Window/Iris/Build Main Menu Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Camera ──
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.192f, 0.302f, 0.475f, 0f);
        camGO.AddComponent<AudioListener>();

        // ── 2. Directional Light ──
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 3. EventSystem ──
        var eventSysGO = new GameObject("EventSystem");
        eventSysGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSysGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        // ── 4. Game Mode SOs ──
        var demoConfig = EnsureGameModeConfig("Mode_7Minutes", "7 Minutes",
            "Real-time 7-minute countdown. A quick taste of Iris.",
            totalDays: 1, demoTimeLimitSeconds: 420f, realSecondsPerGameHour: 60f, prepDuration: 60f);

        var showcaseConfig = EnsureGameModeConfig("Mode_7Days", "7 Days",
            "7 in-game days. The full dating loop at normal pace.",
            totalDays: 7, demoTimeLimitSeconds: 0f, realSecondsPerGameHour: 60f, prepDuration: 120f);

        var fullConfig = EnsureGameModeConfig("Mode_7Weeks", "7 Weeks",
            "49 in-game days. The complete Iris experience.",
            totalDays: 49, demoTimeLimitSeconds: 0f, realSecondsPerGameHour: 60f, prepDuration: 120f);

        // ── 5. Nema Head placeholder ──
        var nemaHead = new GameObject("Nema_Head");
        nemaHead.transform.position = new Vector3(-1.02f, 1.28f, 0f);
        nemaHead.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
        var jitter = nemaHead.AddComponent<MotionJitter>();
        jitter.inputMode = MotionJitter.InputMode.MousePosition;
        jitter.noiseFrequency = 2f;
        jitter.positionStrength = 0.03f;
        jitter.rotationStrength = 0.05f;
        jitter.scaleStrength = 0.01f;

        // Placeholder sprite children (empty GOs — real sprites added by hand)
        string[] layerNames = { "1", "2_bangs", "3_face", "4_neck", "5_hair", "6_background" };
        int[] sortOrders = { 3, 2, 1, 0, -1, -1 };
        var layerTransforms = new Transform[layerNames.Length];
        for (int i = 0; i < layerNames.Length; i++)
        {
            var layerGO = new GameObject(layerNames[i]);
            layerGO.transform.SetParent(nemaHead.transform, false);
            var sr = layerGO.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortOrders[i];
            layerTransforms[i] = layerGO.transform;
        }

        // ── 6. ParallaxManager ──
        var parallaxGO = new GameObject("ParallaxManager");
        var parallax = parallaxGO.AddComponent<MouseParallax>();
        parallax.smoothing = 5f;
        parallax.maxOffset = 20f;
        float[] moveSpeeds = { -200f, -0.1f, -0.15f, -0.2f, -0.3f, -0.35f };
        for (int i = 0; i < layerTransforms.Length; i++)
        {
            parallax.layers.Add(new MouseParallax.ParallaxLayer
            {
                layerObject = layerTransforms[i],
                moveSpeed = moveSpeeds[i]
            });
        }

        // ── 7. Canvas ──
        var canvasGO = new GameObject("Canvas");
        canvasGO.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Title — "Iris" on left with TMP_FocusBlur
        var titleGO = CreateLabel("Title_Text (TMP)", canvasGO.transform,
            TitlePos, TitleSize,
            "Iris", 300f, FontStyles.Normal, TextAlignmentOptions.Center,
            Color.white);
        var focusBlur = titleGO.AddComponent<TMP_FocusBlur>();
        focusBlur.morphSpeed = 3f;
        focusBlur.jitterIntensity = 4f;
        focusBlur.noiseScale = 10f;
        focusBlur.preserveCharacterShape = false;

        // Add title to parallax (extreme movement, matching L_Main_Menu layer 0)
        parallax.layers.Insert(0, new MouseParallax.ParallaxLayer
        {
            layerObject = titleGO.transform,
            moveSpeed = -200f
        });

        // Buttons — right side, text-only (disabled Image), with TextDissolveButton
        var startBtn = CreateMenuButton("Start_Button", canvasGO.transform,
            new Vector2(ButtonX, StartButtonY), ButtonSize, "Start", 2f);

        var optionsBtn = CreateMenuButton("Options_Button", canvasGO.transform,
            new Vector2(ButtonX, OptionsButtonY), ButtonSize, "Options", 5f);

        var quitBtn = CreateMenuButton("Quit_Button", canvasGO.transform,
            new Vector2(ButtonX, QuitButtonY), ButtonSize, "Quit", 5f);

        // ── 8. Tutorial Card ──
        var tutorialCard = BuildTutorialCard();

        // ── 9. ScreenFade overlay ──
        BuildScreenFade();

        // ── 10. MainMenuManager ──
        var menuMgrGO = new GameObject("GameMainMenuManager");
        var menuMgr = menuMgrGO.AddComponent<MainMenuManager>();
        var mgrSO = new SerializedObject(menuMgr);
        mgrSO.FindProperty("_demoConfig").objectReferenceValue = demoConfig;
        mgrSO.FindProperty("_showcaseConfig").objectReferenceValue = showcaseConfig;
        mgrSO.FindProperty("_fullConfig").objectReferenceValue = fullConfig;
        mgrSO.FindProperty("_demoButton").objectReferenceValue = startBtn;
        mgrSO.FindProperty("_quitButton").objectReferenceValue = quitBtn;
        mgrSO.FindProperty("_tutorialCard").objectReferenceValue = tutorialCard;
        mgrSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 11. Save scene ──
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/mainmenu.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[MainMenuSceneBuilder] Scene saved to {path}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════

    private static GameModeConfig EnsureGameModeConfig(string assetName, string modeName,
        string description, int totalDays, float demoTimeLimitSeconds,
        float realSecondsPerGameHour, float prepDuration)
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(SoDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "GameModes");

        string path = $"{SoDir}/{assetName}.asset";
        var config = AssetDatabase.LoadAssetAtPath<GameModeConfig>(path);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameModeConfig>();
            config.modeName = modeName;
            config.modeDescription = description;
            config.totalDays = totalDays;
            config.demoTimeLimitSeconds = demoTimeLimitSeconds;
            config.realSecondsPerGameHour = realSecondsPerGameHour;
            config.prepDuration = prepDuration;
            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[MainMenuSceneBuilder] Created GameModeConfig: {path}");
        }

        return config;
    }

    /// <summary>
    /// Creates a text-only menu button matching L_Main_Menu style:
    /// disabled Image (transparent), white TMP text, TextDissolveButton effect.
    /// </summary>
    private static Button CreateMenuButton(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size, string label, float dissolveSpeed)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        // Image disabled (transparent button, text-only)
        var img = go.AddComponent<Image>();
        img.enabled = false;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.fadeDuration = 0.1f;
        btn.colors = colors;

        // TextDissolveButton effect
        var dissolve = go.AddComponent<TextDissolveButton>();
        dissolve.animationSpeed = dissolveSpeed;
        dissolve.normalSoftness = 0f;
        dissolve.blurredSoftness = 1f;
        dissolve.normalDilate = 0f;
        dissolve.dissolvedDilate = -1f;
        dissolve.normalScale = 1f;
        dissolve.blurredScale = 1.2f;
        dissolve.normalAlpha = 1f;
        dissolve.blurredAlpha = 0.4f;

        // Child TMP text
        var textGO = new GameObject($"{name}_Text (TMP)");
        textGO.transform.SetParent(go.transform, false);
        textGO.layer = LayerMask.NameToLayer("UI");
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 48f;
        tmp.fontStyle = FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    private static GameObject CreateLabel(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size, string text,
        float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;

        return go;
    }

    private static TutorialCard BuildTutorialCard()
    {
        // Tutorial card canvas — sorting order 50 (above menu at 0, below ScreenFade at 100)
        var cardCanvasGO = new GameObject("TutorialCard");
        var cardCanvas = cardCanvasGO.AddComponent<Canvas>();
        cardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cardCanvas.sortingOrder = 50;

        var cardScaler = cardCanvasGO.AddComponent<CanvasScaler>();
        cardScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cardScaler.referenceResolution = new Vector2(1920f, 1080f);
        cardCanvasGO.AddComponent<GraphicRaycaster>();

        // Backdrop — full-screen black 70% opacity, blocks clicks to menu
        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(cardCanvasGO.transform, false);
        var bdRT = backdrop.AddComponent<RectTransform>();
        bdRT.anchorMin = Vector2.zero;
        bdRT.anchorMax = Vector2.one;
        bdRT.offsetMin = Vector2.zero;
        bdRT.offsetMax = Vector2.zero;
        var bdImg = backdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.7f);
        bdImg.raycastTarget = true;

        // Card panel — off-white, 900x600, centered
        var panel = new GameObject("CardPanel");
        panel.transform.SetParent(cardCanvasGO.transform, false);
        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(900f, 600f);
        panelRT.anchoredPosition = Vector2.zero;
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.95f, 0.93f, 0.88f);

        // Title — "HOW TO PLAY"
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -50f);
        titleRT.sizeDelta = new Vector2(800f, 60f);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "HOW TO PLAY";
        titleTMP.fontSize = 36f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = new Color(0.15f, 0.15f, 0.15f);
        titleTMP.raycastTarget = false;

        // Controls text — rich text with key badges
        var controlsGO = new GameObject("Controls");
        controlsGO.transform.SetParent(panel.transform, false);
        var ctrlRT = controlsGO.AddComponent<RectTransform>();
        ctrlRT.anchorMin = new Vector2(0.5f, 0.5f);
        ctrlRT.anchorMax = new Vector2(0.5f, 0.5f);
        ctrlRT.anchoredPosition = new Vector2(0f, 30f);
        ctrlRT.sizeDelta = new Vector2(700f, 360f);
        var ctrlTMP = controlsGO.AddComponent<TextMeshProUGUI>();
        ctrlTMP.text =
            "<b>[A] [D]</b>              Browse rooms\n" +
            "<b>[Click]</b>              Interact with objects\n" +
            "<b>[Esc / RMB]</b>     Back out / Cancel\n" +
            "<b>[Scroll]</b>            Straighten hung items\n" +
            "\n" +
            "<b>Your Day</b>\n" +
            "Read the paper. Pick your date.\n" +
            "Prep your apartment. Host your guest.\n" +
            "Then... tend to your flowers.";
        ctrlTMP.fontSize = 22f;
        ctrlTMP.fontStyle = FontStyles.Normal;
        ctrlTMP.alignment = TextAlignmentOptions.Left;
        ctrlTMP.color = new Color(0.2f, 0.2f, 0.2f);
        ctrlTMP.raycastTarget = false;
        ctrlTMP.richText = true;
        ctrlTMP.lineSpacing = 10f;

        // Start button — dark background, "START" label
        var startBtnGO = new GameObject("StartButton");
        startBtnGO.transform.SetParent(panel.transform, false);
        var sBtnRT = startBtnGO.AddComponent<RectTransform>();
        sBtnRT.anchorMin = new Vector2(0.5f, 0f);
        sBtnRT.anchorMax = new Vector2(0.5f, 0f);
        sBtnRT.anchoredPosition = new Vector2(0f, 60f);
        sBtnRT.sizeDelta = new Vector2(200f, 50f);
        var sBtnImg = startBtnGO.AddComponent<Image>();
        sBtnImg.color = new Color(0.12f, 0.12f, 0.14f);

        var sBtn = startBtnGO.AddComponent<Button>();
        var sBtnColors = sBtn.colors;
        sBtnColors.normalColor = new Color(0.12f, 0.12f, 0.14f);
        sBtnColors.highlightedColor = new Color(0.25f, 0.25f, 0.30f);
        sBtnColors.pressedColor = new Color(0.08f, 0.08f, 0.10f);
        sBtnColors.selectedColor = new Color(0.18f, 0.18f, 0.22f);
        sBtn.colors = sBtnColors;

        var sBtnLabelGO = new GameObject("Label");
        sBtnLabelGO.transform.SetParent(startBtnGO.transform, false);
        var sBtnLabelRT = sBtnLabelGO.AddComponent<RectTransform>();
        sBtnLabelRT.anchorMin = Vector2.zero;
        sBtnLabelRT.anchorMax = Vector2.one;
        sBtnLabelRT.offsetMin = Vector2.zero;
        sBtnLabelRT.offsetMax = Vector2.zero;
        var sBtnTMP = sBtnLabelGO.AddComponent<TextMeshProUGUI>();
        sBtnTMP.text = "START";
        sBtnTMP.fontSize = 22f;
        sBtnTMP.fontStyle = FontStyles.Bold;
        sBtnTMP.alignment = TextAlignmentOptions.Center;
        sBtnTMP.color = new Color(0.9f, 0.88f, 0.82f);
        sBtnTMP.raycastTarget = false;

        // Card starts hidden
        cardCanvasGO.SetActive(false);

        // Wire TutorialCard component
        var tc = cardCanvasGO.AddComponent<TutorialCard>();
        var tcSO = new SerializedObject(tc);
        tcSO.FindProperty("_root").objectReferenceValue = cardCanvasGO;
        tcSO.FindProperty("_startButton").objectReferenceValue = sBtn;
        tcSO.ApplyModifiedPropertiesWithoutUndo();

        return tc;
    }

    private static void BuildScreenFade()
    {
        var go = new GameObject("ScreenFade");

        var fadeCanvas = go.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 100;

        var blackPanel = new GameObject("BlackPanel");
        blackPanel.transform.SetParent(go.transform, false);
        var rt = blackPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = blackPanel.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;

        var fade = go.AddComponent<ScreenFade>();
        var fadeSO = new SerializedObject(fade);
        fadeSO.FindProperty("_canvasGroup").objectReferenceValue = cg;
        fadeSO.FindProperty("defaultFadeOutDuration").floatValue = 0.5f;
        fadeSO.FindProperty("defaultFadeInDuration").floatValue = 0.5f;
        fadeSO.ApplyModifiedPropertiesWithoutUndo();
    }
}
