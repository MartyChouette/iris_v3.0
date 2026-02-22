using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool that procedurally builds the SettingsPanel prefab.
/// Menu: Window > Iris > Build Settings Panel
/// </summary>
public static class SettingsPanelBuilder
{
    private static readonly Color BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
    private static readonly Color TabActiveColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    private static readonly Color TabInactiveColor = new Color(0.15f, 0.15f, 0.18f, 0.8f);
    private static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
    private static readonly Color LabelColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color SliderBgColor = new Color(0.2f, 0.2f, 0.25f, 1f);
    private static readonly Color SliderFillColor = new Color(0.4f, 0.6f, 0.5f, 1f);

    private static readonly string[] TabNames = { "Visual", "Audio", "Motion", "Timing", "Controls", "Performance" };

    [MenuItem("Window/Iris/Build Settings Panel")]
    public static GameObject Build()
    {
        // Create canvas
        var canvasGO = new GameObject("SettingsPanel");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel root (full-screen dark background)
        var panelRoot = CreatePanel(canvasGO.transform, "PanelRoot", BgColor);
        Stretch(panelRoot);

        // Main container (centered, fixed size)
        var container = CreatePanel(panelRoot.transform, "Container", new Color(0, 0, 0, 0));
        var containerRT = container.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(900, 600);

        // Title
        var title = CreateText(container.transform, "Title", "Settings", 32);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, -10);
        titleRT.sizeDelta = new Vector2(0, 40);

        // Tab bar
        var tabBar = CreatePanel(container.transform, "TabBar", new Color(0, 0, 0, 0));
        var tabBarRT = tabBar.GetComponent<RectTransform>();
        tabBarRT.anchorMin = new Vector2(0, 1);
        tabBarRT.anchorMax = new Vector2(1, 1);
        tabBarRT.pivot = new Vector2(0.5f, 1);
        tabBarRT.anchoredPosition = new Vector2(0, -55);
        tabBarRT.sizeDelta = new Vector2(0, 36);

        var tabBarHLG = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabBarHLG.spacing = 4;
        tabBarHLG.childAlignment = TextAnchor.MiddleCenter;
        tabBarHLG.childForceExpandWidth = true;
        tabBarHLG.childForceExpandHeight = true;

        // Create tab buttons
        var tabButtons = new SettingsTabButton[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            var tabGO = CreatePanel(tabBar.transform, $"Tab_{TabNames[i]}", TabInactiveColor);
            var tabBtn = tabGO.AddComponent<Button>();
            var tabComp = tabGO.AddComponent<SettingsTabButton>();

            var tabLabel = CreateText(tabGO.transform, "Label", TabNames[i], 16);
            Stretch(tabLabel);

            // Wire SettingsTabButton fields
            var so = new SerializedObject(tabComp);
            so.FindProperty("_background").objectReferenceValue = tabGO.GetComponent<Image>();
            so.FindProperty("_label").objectReferenceValue = tabLabel.GetComponent<TMP_Text>();
            so.FindProperty("_tabIndex").intValue = i;
            so.ApplyModifiedPropertiesWithoutUndo();

            tabButtons[i] = tabComp;
        }

        // Tab content area
        var contentArea = CreatePanel(container.transform, "ContentArea", PanelColor);
        var contentRT = contentArea.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 0);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.offsetMin = new Vector2(10, 50);   // bottom: space for close button
        contentRT.offsetMax = new Vector2(-10, -100); // top: space for title + tabs

        // Create tab panels
        var tabPanels = new GameObject[TabNames.Length];

        // === Tab 0: Visual ===
        tabPanels[0] = CreateTabContent(contentArea.transform, "Visual");
        var visualVLG = tabPanels[0].AddComponent<VerticalLayoutGroup>();
        ConfigureVLG(visualVLG);

        var cbDropdown = CreateDropdownRow(tabPanels[0].transform, "Colorblind Mode",
            new[] { "Normal", "Deuteranopia", "Protanopia", "Tritanopia" });
        var hcToggle = CreateToggleRow(tabPanels[0].transform, "High Contrast");
        var tsSlider = CreateSliderRow(tabPanels[0].transform, "Text Scale", 0.8f, 1.5f, 1f);

        // === Tab 1: Audio ===
        tabPanels[1] = CreateTabContent(contentArea.transform, "Audio");
        var audioVLG = tabPanels[1].AddComponent<VerticalLayoutGroup>();
        ConfigureVLG(audioVLG);

        var masterSlider = CreateSliderRow(tabPanels[1].transform, "Master Volume", 0f, 1f, 1f);
        var musicSlider = CreateSliderRow(tabPanels[1].transform, "Music Volume", 0f, 1f, 1f);
        var sfxSlider = CreateSliderRow(tabPanels[1].transform, "SFX Volume", 0f, 1f, 1f);
        var ambSlider = CreateSliderRow(tabPanels[1].transform, "Ambience Volume", 0f, 1f, 1f);
        var uiSlider = CreateSliderRow(tabPanels[1].transform, "UI Volume", 0f, 1f, 1f);
        var capToggle = CreateToggleRow(tabPanels[1].transform, "Captions");

        // === Tab 2: Motion ===
        tabPanels[2] = CreateTabContent(contentArea.transform, "Motion");
        var motionVLG = tabPanels[2].AddComponent<VerticalLayoutGroup>();
        ConfigureVLG(motionVLG);

        var rmToggle = CreateToggleRow(tabPanels[2].transform, "Reduce Motion");
        var ssSlider = CreateSliderRow(tabPanels[2].transform, "Screen Shake", 0f, 1f, 1f);

        // === Tab 3: Timing ===
        tabPanels[3] = CreateTabContent(contentArea.transform, "Timing");
        var timingVLG = tabPanels[3].AddComponent<VerticalLayoutGroup>();
        ConfigureVLG(timingVLG);

        var timerDropdown = CreateDropdownRow(tabPanels[3].transform, "Timer Speed",
            new[] { "Normal", "Relaxed (1.5x)", "Extended (2x)", "No Timer" });

        // === Tab 4: Controls ===
        tabPanels[4] = CreateTabContent(contentArea.transform, "Controls");
        var controlsVLG = tabPanels[4].AddComponent<VerticalLayoutGroup>();
        ConfigureVLG(controlsVLG);

        CreateText(tabPanels[4].transform, "ControlsInfo",
            "Rebind controls in the Controls page of the pause menu.\n\nKeyboard + Mouse is the primary input method.",
            18);

        // === Tab 5: Performance ===
        tabPanels[5] = CreateTabContent(contentArea.transform, "Performance");
        var perfVLG = tabPanels[5].AddComponent<VerticalLayoutGroup>();
        ConfigureVLG(perfVLG);

        var resSlider = CreateSliderRow(tabPanels[5].transform, "Resolution Scale", 0.5f, 1f, 1f);
        var qualDropdown = CreateDropdownRow(tabPanels[5].transform, "Quality Preset",
            new[] { "Low", "Medium", "High" });
        var psxToggle = CreateToggleRow(tabPanels[5].transform, "PSX Retro Effect");

        // Close button
        var closeBtn = CreateButton(container.transform, "CloseButton", "Close");
        var closeBtnRT = closeBtn.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(0.5f, 0);
        closeBtnRT.anchorMax = new Vector2(0.5f, 0);
        closeBtnRT.pivot = new Vector2(0.5f, 0);
        closeBtnRT.anchoredPosition = new Vector2(0, 10);
        closeBtnRT.sizeDelta = new Vector2(200, 40);

        // Reset All button
        var resetBtn = CreateButton(container.transform, "ResetButton", "Reset All");
        var resetBtnRT = resetBtn.GetComponent<RectTransform>();
        resetBtnRT.anchorMin = new Vector2(1, 0);
        resetBtnRT.anchorMax = new Vector2(1, 0);
        resetBtnRT.pivot = new Vector2(1, 0);
        resetBtnRT.anchoredPosition = new Vector2(-10, 10);
        resetBtnRT.sizeDelta = new Vector2(160, 40);

        // Add SettingsPanel component
        var settingsPanel = canvasGO.AddComponent<SettingsPanel>();

        // Wire everything via SerializedObject
        var spSO = new SerializedObject(settingsPanel);
        spSO.FindProperty("_panelRoot").objectReferenceValue = panelRoot;

        // Tab buttons array
        var tabBtnsProp = spSO.FindProperty("_tabButtons");
        tabBtnsProp.arraySize = tabButtons.Length;
        for (int i = 0; i < tabButtons.Length; i++)
            tabBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];

        // Tab panels array
        var tabPnlsProp = spSO.FindProperty("_tabPanels");
        tabPnlsProp.arraySize = tabPanels.Length;
        for (int i = 0; i < tabPanels.Length; i++)
            tabPnlsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabPanels[i];

        // Visual tab
        spSO.FindProperty("_colorblindDropdown").objectReferenceValue = cbDropdown;
        spSO.FindProperty("_highContrastToggle").objectReferenceValue = hcToggle;
        spSO.FindProperty("_textScaleSlider").objectReferenceValue = tsSlider.slider;
        spSO.FindProperty("_textScaleLabel").objectReferenceValue = tsSlider.label;

        // Audio tab
        spSO.FindProperty("_masterVolumeSlider").objectReferenceValue = masterSlider.slider;
        spSO.FindProperty("_musicVolumeSlider").objectReferenceValue = musicSlider.slider;
        spSO.FindProperty("_sfxVolumeSlider").objectReferenceValue = sfxSlider.slider;
        spSO.FindProperty("_ambienceVolumeSlider").objectReferenceValue = ambSlider.slider;
        spSO.FindProperty("_uiVolumeSlider").objectReferenceValue = uiSlider.slider;
        spSO.FindProperty("_captionsToggle").objectReferenceValue = capToggle;

        // Motion tab
        spSO.FindProperty("_reduceMotionToggle").objectReferenceValue = rmToggle;
        spSO.FindProperty("_screenShakeSlider").objectReferenceValue = ssSlider.slider;
        spSO.FindProperty("_screenShakeLabel").objectReferenceValue = ssSlider.label;

        // Timing tab
        spSO.FindProperty("_timerDropdown").objectReferenceValue = timerDropdown;

        // Performance tab
        spSO.FindProperty("_resolutionScaleSlider").objectReferenceValue = resSlider.slider;
        spSO.FindProperty("_resolutionScaleLabel").objectReferenceValue = resSlider.label;
        spSO.FindProperty("_qualityDropdown").objectReferenceValue = qualDropdown;
        spSO.FindProperty("_psxToggle").objectReferenceValue = psxToggle;

        spSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire close button
        var closeBtnComp = closeBtn.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            closeBtnComp.onClick, settingsPanel.UI_Close);

        // Wire reset button
        var resetBtnComp = resetBtn.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            resetBtnComp.onClick, settingsPanel.UI_ResetAll);

        // Save as prefab
        string prefabDir = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        string prefabPath = $"{prefabDir}/SettingsPanel.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGO, prefabPath, InteractionMode.AutomatedAction);

        Debug.Log($"[SettingsPanelBuilder] Settings panel built and saved to {prefabPath}.");
        Selection.activeGameObject = canvasGO;
        return canvasGO;
    }

    // ═══════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateText(Transform parent, string name, string text, int size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = LabelColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = size + 16;

        return go;
    }

    private static GameObject CreateTabContent(Transform parent, string name)
    {
        var go = new GameObject($"Tab_{name}");
        go.transform.SetParent(parent, false);
        Stretch(go);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void ConfigureVLG(VerticalLayoutGroup vlg)
    {
        vlg.spacing = 8;
        vlg.padding = new RectOffset(20, 20, 15, 15);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
    }

    private struct SliderResult
    {
        public Slider slider;
        public TMP_Text label;
    }

    private static SliderResult CreateSliderRow(Transform parent, string labelText, float min, float max, float defaultVal)
    {
        var row = new GameObject($"Row_{labelText}");
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 36;

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(row.transform, false);
        labelGO.AddComponent<RectTransform>();
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = 18;
        tmp.color = LabelColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 200;

        // Slider
        var sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(row.transform, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        var sliderLE = sliderGO.AddComponent<LayoutElement>();
        sliderLE.flexibleWidth = 1;

        // Slider background
        var bgGO = CreatePanel(sliderGO.transform, "Background", SliderBgColor);
        Stretch(bgGO);

        // Fill area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = new Vector2(5, 5);
        fillAreaRT.offsetMax = new Vector2(-5, -5);

        var fill = CreatePanel(fillArea.transform, "Fill", SliderFillColor);
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0, 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        // Handle
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleArea.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(10, 0);
        handleAreaRT.offsetMax = new Vector2(-10, 0);

        var handle = CreatePanel(handleArea.transform, "Handle", Color.white);
        var handleRT = handle.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(16, 0);

        var slider = sliderGO.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultVal;
        slider.wholeNumbers = false;

        // Value label
        var valGO = new GameObject("Value");
        valGO.transform.SetParent(row.transform, false);
        valGO.AddComponent<RectTransform>();
        var valTmp = valGO.AddComponent<TextMeshProUGUI>();
        valTmp.text = $"{defaultVal:P0}";
        valTmp.fontSize = 16;
        valTmp.color = LabelColor;
        valTmp.alignment = TextAlignmentOptions.MidlineRight;
        var valLE = valGO.AddComponent<LayoutElement>();
        valLE.preferredWidth = 60;

        return new SliderResult { slider = slider, label = valTmp };
    }

    private static TMP_Dropdown CreateDropdownRow(Transform parent, string labelText, string[] options)
    {
        var row = new GameObject($"Row_{labelText}");
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 36;

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(row.transform, false);
        labelGO.AddComponent<RectTransform>();
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = 18;
        tmp.color = LabelColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 200;

        // Dropdown
        var ddGO = new GameObject("Dropdown");
        ddGO.transform.SetParent(row.transform, false);
        ddGO.AddComponent<RectTransform>();
        var ddLE = ddGO.AddComponent<LayoutElement>();
        ddLE.flexibleWidth = 1;
        ddLE.preferredHeight = 32;

        var ddImg = ddGO.AddComponent<Image>();
        ddImg.color = SliderBgColor;

        // Caption text
        var captionGO = new GameObject("Label");
        captionGO.transform.SetParent(ddGO.transform, false);
        var captionRT = captionGO.AddComponent<RectTransform>();
        captionRT.anchorMin = Vector2.zero;
        captionRT.anchorMax = Vector2.one;
        captionRT.offsetMin = new Vector2(10, 2);
        captionRT.offsetMax = new Vector2(-30, -2);
        var captionTmp = captionGO.AddComponent<TextMeshProUGUI>();
        captionTmp.fontSize = 16;
        captionTmp.color = LabelColor;
        captionTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Template
        var templateGO = CreatePanel(ddGO.transform, "Template", new Color(0.15f, 0.15f, 0.2f, 1f));
        var templateRT = templateGO.GetComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0, 0);
        templateRT.anchorMax = new Vector2(1, 0);
        templateRT.pivot = new Vector2(0.5f, 1);
        templateRT.anchoredPosition = Vector2.zero;
        templateRT.sizeDelta = new Vector2(0, 250);

        var viewport = CreatePanel(templateGO.transform, "Viewport", new Color(0, 0, 0, 0));
        Stretch(viewport);
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRT2 = content.AddComponent<RectTransform>();
        contentRT2.anchorMin = new Vector2(0, 1);
        contentRT2.anchorMax = new Vector2(1, 1);
        contentRT2.pivot = new Vector2(0.5f, 1);
        contentRT2.sizeDelta = new Vector2(0, 0);

        // Auto-size content to fit all items
        var contentVLG = content.AddComponent<VerticalLayoutGroup>();
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = true;

        var contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Item template
        var itemGO = new GameObject("Item");
        itemGO.transform.SetParent(content.transform, false);
        var itemRT = itemGO.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 1);
        itemRT.anchorMax = new Vector2(1, 1);
        itemRT.pivot = new Vector2(0.5f, 1);
        itemRT.sizeDelta = new Vector2(0, 32);
        var itemToggle = itemGO.AddComponent<Toggle>();

        var itemBG = itemGO.AddComponent<Image>();
        itemBG.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        itemToggle.targetGraphic = itemBG;

        var itemLE = itemGO.AddComponent<LayoutElement>();
        itemLE.preferredHeight = 32;

        var itemLabelGO = new GameObject("Item Label");
        itemLabelGO.transform.SetParent(itemGO.transform, false);
        var itemLabelRT = itemLabelGO.AddComponent<RectTransform>();
        itemLabelRT.anchorMin = Vector2.zero;
        itemLabelRT.anchorMax = Vector2.one;
        itemLabelRT.offsetMin = new Vector2(10, 2);
        itemLabelRT.offsetMax = new Vector2(-10, -2);
        var itemTmp = itemLabelGO.AddComponent<TextMeshProUGUI>();
        itemTmp.fontSize = 16;
        itemTmp.color = LabelColor;
        itemTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var scrollRect = templateGO.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRT2;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        templateGO.SetActive(false);

        var dropdown = ddGO.AddComponent<TMP_Dropdown>();
        dropdown.captionText = captionTmp;
        dropdown.itemText = itemTmp;
        dropdown.template = templateRT;

        dropdown.ClearOptions();
        var optList = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
        foreach (var o in options)
            optList.Add(new TMP_Dropdown.OptionData(o));
        dropdown.AddOptions(optList);

        return dropdown;
    }

    private static Toggle CreateToggleRow(Transform parent, string labelText)
    {
        var row = new GameObject($"Row_{labelText}");
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 36;

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(row.transform, false);
        labelGO.AddComponent<RectTransform>();
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = 18;
        tmp.color = LabelColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 200;

        // Toggle
        var toggleGO = new GameObject("Toggle");
        toggleGO.transform.SetParent(row.transform, false);
        toggleGO.AddComponent<RectTransform>();
        var toggleLE = toggleGO.AddComponent<LayoutElement>();
        toggleLE.preferredWidth = 30;
        toggleLE.preferredHeight = 30;

        var bgImg = toggleGO.AddComponent<Image>();
        bgImg.color = SliderBgColor;

        var checkGO = CreatePanel(toggleGO.transform, "Checkmark", SliderFillColor);
        var checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.15f, 0.15f);
        checkRT.anchorMax = new Vector2(0.85f, 0.85f);
        checkRT.offsetMin = Vector2.zero;
        checkRT.offsetMax = Vector2.zero;

        var toggle = toggleGO.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkGO.GetComponent<Image>();

        // Spacer to push toggle to right side
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(row.transform, false);
        spacer.transform.SetSiblingIndex(1); // between label and toggle
        spacer.AddComponent<RectTransform>();
        var spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.flexibleWidth = 1;

        return toggle;
    }

    private static GameObject CreateButton(Transform parent, string name, string text)
    {
        var go = CreatePanel(parent, name, SliderBgColor);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = SliderFillColor;
        btn.colors = colors;

        var labelGO = CreateText(go.transform, "Label", text, 20);
        Stretch(labelGO);

        return go;
    }
}
