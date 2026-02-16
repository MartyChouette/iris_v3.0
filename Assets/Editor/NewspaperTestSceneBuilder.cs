using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Bare-minimum newspaper test: camera, newspaper quad, overlay canvas (two-page spread).
/// No Cinemachine, no scissors, no managers. Just visual verification.
/// Newspaper is held up in front of the camera (vertical plane, forward-facing).
/// Menu: Window > Iris > Build Newspaper Test Scene
/// </summary>
public static class NewspaperTestSceneBuilder
{
    private const int CanvasWidth = 1000;
    private const int CanvasHeight = 700;

    [MenuItem("Window/Iris/Archived Builders/Build Newspaper Test Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Light ──────────────────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── Camera — forward-facing, newspaper held up ───────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.1f;
        camGO.transform.position = new Vector3(0f, 1.5f, 0f);
        camGO.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        camGO.AddComponent<AudioListener>();

        // ── Newspaper quad (vertical, facing camera) ─────────────────
        var surfaceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surfaceGO.name = "NewspaperSurface";
        surfaceGO.transform.position = new Vector3(0f, 1.35f, 0.55f);
        surfaceGO.transform.rotation = Quaternion.Euler(-5f, 180f, 0f);
        surfaceGO.transform.localScale = new Vector3(1.0f, 0.7f, 1f);

        // Paper-color material
        var surfRend = surfaceGO.GetComponent<Renderer>();
        var surfMat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
        surfMat.color = new Color(0.92f, 0.90f, 0.85f);
        surfMat.SetInt("_Cull", 0); // both faces
        surfRend.sharedMaterial = surfMat;

        // ── WorldSpace Canvas overlay (two-page spread) ──────────────
        // Pivot GO holds 3D transform
        var pivotGO = new GameObject("NewspaperOverlayPivot");
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

        var cg = canvasGO.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        // ── Center fold line ─────────────────────────────────────────
        var foldGO = new GameObject("FoldLine");
        foldGO.transform.SetParent(canvasGO.transform, false);
        var foldRT = foldGO.AddComponent<RectTransform>();
        foldRT.anchoredPosition = new Vector2(0f, 0f);
        foldRT.sizeDelta = new Vector2(2f, CanvasHeight);
        foldRT.localScale = Vector3.one;
        foldGO.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.4f);

        // ── Left page (decorative) ──────────────────────────────────
        float leftCenter = -250f;

        CreateText("Title", canvasGO.transform,
            new Vector2(leftCenter, 300f), new Vector2(460f, 50f),
            "THE DAILY BLOOM", 38f, FontStyles.Bold, TextAlignmentOptions.Center);

        // Title rule
        var titleRuleGO = new GameObject("TitleRule");
        titleRuleGO.transform.SetParent(canvasGO.transform, false);
        var titleRuleRT = titleRuleGO.AddComponent<RectTransform>();
        titleRuleRT.anchoredPosition = new Vector2(leftCenter, 270f);
        titleRuleRT.sizeDelta = new Vector2(420f, 2f);
        titleRuleRT.localScale = Vector3.one;
        titleRuleGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

        CreateText("DateLine", canvasGO.transform,
            new Vector2(leftCenter, 252f), new Vector2(420f, 20f),
            "Vol. XLII  No. 7  |  The Garden District Gazette", 12f,
            FontStyles.Italic, TextAlignmentOptions.Center);

        CreateText("Headline1", canvasGO.transform,
            new Vector2(leftCenter, 215f), new Vector2(420f, 35f),
            "MYSTERIOUS BLOOM SPOTTED IN TOWN SQUARE", 20f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        CreateText("Body1", canvasGO.transform,
            new Vector2(leftCenter, 155f), new Vector2(420f, 80f),
            "Residents were astonished yesterday when a never-before-seen flower appeared overnight in the central fountain. Botanists remain baffled. \"It smells like Tuesday,\" said one local.",
            13f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        var rule2GO = new GameObject("Rule2");
        rule2GO.transform.SetParent(canvasGO.transform, false);
        var rule2RT = rule2GO.AddComponent<RectTransform>();
        rule2RT.anchoredPosition = new Vector2(leftCenter, 108f);
        rule2RT.sizeDelta = new Vector2(420f, 1f);
        rule2RT.localScale = Vector3.one;
        rule2GO.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        CreateText("Headline2", canvasGO.transform,
            new Vector2(leftCenter, 85f), new Vector2(420f, 30f),
            "ANNUAL PRUNING FESTIVAL DRAWS RECORD CROWDS", 18f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        CreateText("Body2", canvasGO.transform,
            new Vector2(leftCenter, 25f), new Vector2(420f, 80f),
            "The 47th Annual Pruning Festival exceeded all expectations with over 200 attendees. Highlights included the competitive hedge-sculpting finals and Mrs. Fernsby's award-winning topiary swan.",
            13f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        var rule3GO = new GameObject("Rule3");
        rule3GO.transform.SetParent(canvasGO.transform, false);
        var rule3RT = rule3GO.AddComponent<RectTransform>();
        rule3RT.anchoredPosition = new Vector2(leftCenter, -22f);
        rule3RT.sizeDelta = new Vector2(420f, 1f);
        rule3RT.localScale = Vector3.one;
        rule3GO.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        CreateText("Headline3", canvasGO.transform,
            new Vector2(leftCenter, -45f), new Vector2(420f, 30f),
            "WEATHER: PARTLY SUNNY WITH CHANCE OF PETALS", 16f,
            FontStyles.Bold, TextAlignmentOptions.Left);

        CreateText("Body3", canvasGO.transform,
            new Vector2(leftCenter, -100f), new Vector2(420f, 70f),
            "Meteorologists predict a mild week ahead with occasional floral precipitation. Residents advised to carry umbrellas and enjoy the fragrance.",
            12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        CreateText("Filler", canvasGO.transform,
            new Vector2(leftCenter, -185f), new Vector2(420f, 100f),
            "~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~",
            10f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // ── Right page (personal ads) ────────────────────────────────
        float rightCenter = 250f;

        CreateText("PersonalsLabel", canvasGO.transform,
            new Vector2(rightCenter, 310f), new Vector2(440f, 35f),
            "PERSONALS", 28f, FontStyles.Bold, TextAlignmentOptions.Center);

        var pRuleGO = new GameObject("PersonalsRule");
        pRuleGO.transform.SetParent(canvasGO.transform, false);
        var pRuleRT = pRuleGO.AddComponent<RectTransform>();
        pRuleRT.anchoredPosition = new Vector2(rightCenter, 290f);
        pRuleRT.sizeDelta = new Vector2(420f, 2f);
        pRuleRT.localScale = Vector3.one;
        pRuleGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

        // 4 fake personal ads (flex layout on right page)
        string[] names = { "Rose", "Thorn", "Lily", "Moss" };
        string[] ads = {
            "Romantic soul seeks someone who won't wilt under pressure.",
            "Sharp wit, sharper edges. Gardeners welcome.",
            "Gentle spirit, pure heart. Loves ponds.",
            "Low-maintenance, earthy, always there."
        };

        int adCount = 4;
        float contentTop = 620f;
        float contentBottom = 30f;
        float contentHeight = contentTop - contentBottom;
        float spacing = 10f;
        float slotHeight = (contentHeight - spacing * (adCount - 1)) / adCount;

        for (int i = 0; i < adCount; i++)
        {
            float slotTopY = contentTop - i * (slotHeight + spacing);
            float slotCenterY = slotTopY - slotHeight * 0.5f;
            float anchoredY = slotCenterY - CanvasHeight * 0.5f;

            float nameFontSize = Mathf.Clamp(slotHeight * 0.16f, 14f, 28f);
            float bodyFontSize = Mathf.Clamp(slotHeight * 0.11f, 10f, 18f);
            float phoneFontSize = Mathf.Clamp(slotHeight * 0.13f, 12f, 22f);
            float nameOffsetY = slotHeight * 0.35f;
            float phoneOffsetY = -slotHeight * 0.35f;

            CreateText($"Name_{i}", canvasGO.transform,
                new Vector2(rightCenter - 20f, anchoredY + nameOffsetY),
                new Vector2(380f, nameFontSize + 6f),
                names[i], nameFontSize, FontStyles.Bold, TextAlignmentOptions.Left);

            CreateText($"Ad_{i}", canvasGO.transform,
                new Vector2(rightCenter, anchoredY),
                new Vector2(420f, slotHeight * 0.4f),
                ads[i], bodyFontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            CreateText($"Phone_{i}", canvasGO.transform,
                new Vector2(rightCenter, anchoredY + phoneOffsetY),
                new Vector2(420f, phoneFontSize + 6f),
                $"555-{1000 + i * 111:0000}", phoneFontSize, FontStyles.Italic, TextAlignmentOptions.Left);
        }

        // ── Instruction text (screen-space) ────────────────────────
        var uiGO = new GameObject("ScreenUI");
        var uiCanvas = uiGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 10;
        var scaler = uiGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var instrGO = new GameObject("Instruction");
        instrGO.transform.SetParent(uiGO.transform);
        var instrRT = instrGO.AddComponent<RectTransform>();
        instrRT.anchorMin = new Vector2(0.5f, 0f);
        instrRT.anchorMax = new Vector2(0.5f, 0f);
        instrRT.anchoredPosition = new Vector2(0f, 30f);
        instrRT.sizeDelta = new Vector2(700f, 40f);
        instrRT.localScale = Vector3.one;
        var instrTMP = instrGO.AddComponent<TextMeshProUGUI>();
        instrTMP.text = "Two-page newspaper spread — held up in front of camera";
        instrTMP.fontSize = 20f;
        instrTMP.alignment = TextAlignmentOptions.Center;
        instrTMP.color = Color.white;

        // ── Save ───────────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        EditorSceneManager.SaveScene(scene, $"{dir}/newspaper_test.unity");
        Debug.Log("[NewspaperTestSceneBuilder] Done. Press Play — you should see a two-page newspaper spread.");
    }

    // ════════════════════════════════════════════════════════════════

    private static TMP_FontAsset s_font;

    private static GameObject CreateText(string name, Transform parent,
        Vector2 pos, Vector2 size, string text, float fontSize,
        FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var tmp = go.AddComponent<TextMeshProUGUI>();

        if (s_font == null)
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (s_font != null) tmp.font = s_font;

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = new Color(0.1f, 0.1f, 0.1f);
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }
}
