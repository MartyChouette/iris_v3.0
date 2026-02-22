using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Runtime controller for the lighting test scene.
/// Builds its own HUD at runtime — no editor-time UI wiring needed.
/// Handles 8-camera switching, time-of-day, weather, NatureBox params, and PSX controls.
/// </summary>
public class LightingTestController : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("8 CinemachineCameras: 4 perspective + 4 orthographic.")]
    [SerializeField] private CinemachineCamera[] _cameras = new CinemachineCamera[8];

    // ── Runtime UI references (built in Start) ──
    private GameObject _hudRoot;
    private TMP_Text _cameraLabel;
    private TMP_Text _psxLabel;
    private TMP_Text _gridLabel;
    private Slider _timeSlider;
    private Slider _gridSizeSlider;

    // NatureBox sliders
    private Slider _cloudDensitySlider;
    private Slider _cloudSpeedSlider;
    private Slider _horizonFogSlider;
    private Slider _sunSizeSlider;
    private Slider _starDensitySlider;
    private Slider _rainIntensitySlider;
    private Slider _snowIntensitySlider;
    private Slider _leafIntensitySlider;
    private Slider _overcastDarkenSlider;
    private Slider _snowCapSlider;

    // PSX sliders
    private Slider _vertexSnapSlider;
    private Slider _affineSlider;
    private Slider _resolutionDivisorSlider;
    private Slider _colorDepthSlider;
    private Slider _ditherIntensitySlider;
    private Slider _shadowDitherSlider;

    // Inline InputActions
    private InputAction _hudToggleAction;
    private InputAction[] _cameraActions;
    private InputAction _drawerClickAction;
    private InputAction _mousePositionAction;

    private int _activeCameraIndex;
    private bool _hudVisible = true;
    private bool _manualNatureMode;

    // Shader property IDs
    private static readonly int CloudDensityId = Shader.PropertyToID("_CloudDensity");
    private static readonly int CloudSpeedId = Shader.PropertyToID("_CloudSpeed");
    private static readonly int HorizonFogId = Shader.PropertyToID("_HorizonFog");
    private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
    private static readonly int StarDensityId = Shader.PropertyToID("_StarDensity");
    private static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");
    private static readonly int SnowIntensityId = Shader.PropertyToID("_SnowIntensity");
    private static readonly int LeafIntensityId = Shader.PropertyToID("_LeafIntensity");
    private static readonly int OvercastDarkenId = Shader.PropertyToID("_OvercastDarken");
    private static readonly int SnowCapId = Shader.PropertyToID("_SnowCapIntensity");

    private DrawerController[] _drawers;
    private ObjectGrabber _grabber;
    private Camera _cachedCamera;

    // ── HUD layout constants ──
    private const float PanelW = 310f;
    private const float LH = 18f;
    private const float SH = 22f;
    private const float BH = 22f;
    private const float SliderW = 280f;
    private const float SliderH = 14f;

    private void Awake()
    {
        _hudToggleAction = new InputAction("HUDToggle", InputActionType.Button, "<Keyboard>/f1");
        _drawerClickAction = new InputAction("DrawerClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePositionAction = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");

        _cameraActions = new InputAction[8];
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8" };
        for (int i = 0; i < 8; i++)
            _cameraActions[i] = new InputAction($"Cam{i + 1}", InputActionType.Button, $"<Keyboard>/{keys[i]}");
    }

    private void OnEnable()
    {
        _hudToggleAction.Enable();
        _drawerClickAction.Enable();
        _mousePositionAction.Enable();
        for (int i = 0; i < _cameraActions.Length; i++)
            _cameraActions[i].Enable();
    }

    private void OnDisable()
    {
        _hudToggleAction.Disable();
        _drawerClickAction.Disable();
        _mousePositionAction.Disable();
        for (int i = 0; i < _cameraActions.Length; i++)
            _cameraActions[i].Disable();
    }

    private void OnDestroy()
    {
        _hudToggleAction?.Dispose();
        _drawerClickAction?.Dispose();
        _mousePositionAction?.Dispose();
        if (_cameraActions != null)
            for (int i = 0; i < _cameraActions.Length; i++)
                _cameraActions[i]?.Dispose();
    }

    private void Start()
    {
        _drawers = FindObjectsByType<DrawerController>(FindObjectsSortMode.None);
        _grabber = FindFirstObjectByType<ObjectGrabber>();
        _cachedCamera = Camera.main;

        SetActiveCamera(0);
        BuildHUD();
    }

    private void Update()
    {
        if (_hudToggleAction.WasPressedThisFrame())
        {
            _hudVisible = !_hudVisible;
            if (_hudRoot != null) _hudRoot.SetActive(_hudVisible);
        }

        for (int i = 0; i < _cameraActions.Length; i++)
        {
            if (_cameraActions[i].WasPressedThisFrame())
            {
                SetActiveCamera(i);
                break;
            }
        }

        if (_drawerClickAction.WasPressedThisFrame() && !ObjectGrabber.IsHoldingObject)
            TryToggleDrawer();

        UpdateStatusLabels();
    }

    // ══════════════════════════════════════════════════════════════
    // Runtime HUD Builder
    // ══════════════════════════════════════════════════════════════

    private void BuildHUD()
    {
        // Canvas
        var canvasGO = new GameObject("LightingTestHUD");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        _hudRoot = canvasGO;

        // Scrollable panel
        var panelGO = MakePanel(canvasGO.transform);
        var content = panelGO.transform; // content is the panel itself (scroll child)

        float y = -8f;

        // ── Title ──
        MakeLabel(content, "Lighting Test", 14, FontStyles.Bold, ref y);
        y -= 4f;

        // ── Camera ──
        _cameraLabel = MakeLabel(content, "Camera 1 (Persp)", 11, FontStyles.Normal, ref y);
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                int idx = row * 4 + col;
                int camIdx = idx; // capture for closure
                MakeButton(content, $"Cam {idx + 1}",
                    new Vector2(10f + col * 72f, y), new Vector2(68f, BH),
                    () => SetActiveCamera(camIdx));
            }
            y -= BH + 3f;
        }
        y -= 4f;

        // ── PSX status + toggle ──
        _psxLabel = MakeLabel(content, "PSX: OFF (F4)", 11, FontStyles.Normal, ref y);
        y += LH; // back up to same line
        MakeButton(content, "Toggle PSX", new Vector2(200f, y), new Vector2(100f, BH), TogglePSX);
        y -= BH + 2f;

        _gridLabel = MakeLabel(content, "Grid: G toggle", 11, FontStyles.Normal, ref y);
        _gridSizeSlider = MakeSliderRow(content, "Grid Size", 0.05f, 1f, 0.3f, ref y);
        _gridSizeSlider.onValueChanged.AddListener(OnGridSizeChanged);
        y -= 4f;

        // ══════════════════════════════════════════════════════════
        // PSX Rendering
        // ══════════════════════════════════════════════════════════
        MakeLabel(content, "── PSX Rendering ──", 11, FontStyles.Bold, ref y);

        _vertexSnapSlider = MakeSliderRow(content, "Vertex Snap", 32f, 320f, 160f, ref y);
        _affineSlider = MakeSliderRow(content, "Affine Warp", 0f, 1f, 1f, ref y);
        _resolutionDivisorSlider = MakeSliderRow(content, "Res Divisor", 1f, 6f, 3f, ref y);
        _colorDepthSlider = MakeSliderRow(content, "Color Depth", 4f, 256f, 32f, ref y);
        _ditherIntensitySlider = MakeSliderRow(content, "Dither", 0f, 1f, 0.5f, ref y);
        _shadowDitherSlider = MakeSliderRow(content, "Shadow Dither", 0f, 1f, 1f, ref y);

        _vertexSnapSlider.onValueChanged.AddListener((_) => ApplyPSX());
        _affineSlider.onValueChanged.AddListener((_) => ApplyPSX());
        _resolutionDivisorSlider.onValueChanged.AddListener((_) => ApplyPSX());
        _colorDepthSlider.onValueChanged.AddListener((_) => ApplyPSX());
        _ditherIntensitySlider.onValueChanged.AddListener((_) => ApplyPSX());
        _shadowDitherSlider.onValueChanged.AddListener((_) => ApplyPSX());

        y -= 4f;

        // ══════════════════════════════════════════════════════════
        // Time & Weather
        // ══════════════════════════════════════════════════════════
        MakeLabel(content, "── Time & Weather ──", 11, FontStyles.Bold, ref y);

        _timeSlider = MakeSliderRow(content, "Time of Day", 0f, 1f, 0.5f, ref y);
        _timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);

        MakeLabel(content, "Weather", 10, FontStyles.Normal, ref y);
        string[] wNames = { "Clear", "Overcast", "Rainy", "Stormy", "Snowy", "Leaves" };
        System.Action[] wActions = {
            () => ForceWeather(WeatherSystem.WeatherState.Clear),
            () => ForceWeather(WeatherSystem.WeatherState.Overcast),
            () => ForceWeather(WeatherSystem.WeatherState.Rainy),
            () => ForceWeather(WeatherSystem.WeatherState.Stormy),
            () => ForceWeather(WeatherSystem.WeatherState.Snowy),
            () => ForceWeather(WeatherSystem.WeatherState.FallingLeaves)
        };
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int idx = row * 3 + col;
                int wIdx = idx;
                MakeButton(content, wNames[idx],
                    new Vector2(10f + col * 95f, y), new Vector2(90f, BH),
                    () => wActions[wIdx]());
            }
            y -= BH + 3f;
        }
        y -= 4f;

        // ══════════════════════════════════════════════════════════
        // Nature Params
        // ══════════════════════════════════════════════════════════
        MakeLabel(content, "── Nature Params ──", 11, FontStyles.Bold, ref y);

        _cloudDensitySlider = MakeSliderRow(content, "CloudDensity", 0f, 1f, 0.45f, ref y);
        _cloudSpeedSlider = MakeSliderRow(content, "CloudSpeed", 0f, 0.1f, 0.015f, ref y);
        _horizonFogSlider = MakeSliderRow(content, "HorizonFog", 0f, 1f, 0.35f, ref y);
        _sunSizeSlider = MakeSliderRow(content, "SunSize", 0.90f, 1f, 0.97f, ref y);
        _starDensitySlider = MakeSliderRow(content, "StarDensity", 0.95f, 1f, 0.985f, ref y);
        _rainIntensitySlider = MakeSliderRow(content, "RainIntensity", 0f, 1f, 0f, ref y);
        _snowIntensitySlider = MakeSliderRow(content, "SnowIntensity", 0f, 1f, 0f, ref y);
        _leafIntensitySlider = MakeSliderRow(content, "LeafIntensity", 0f, 1f, 0f, ref y);
        _overcastDarkenSlider = MakeSliderRow(content, "OvercastDarken", 0f, 1f, 0f, ref y);
        _snowCapSlider = MakeSliderRow(content, "SnowCap", 0f, 1f, 0f, ref y);

        System.Action<float> natureChanged = (_) => ApplyNature();
        _cloudDensitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _cloudSpeedSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _horizonFogSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _sunSizeSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _starDensitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _rainIntensitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _snowIntensitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _leafIntensitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _overcastDarkenSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));
        _snowCapSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(natureChanged));

        y -= 4f;
        MakeLabel(content, "1-8 cam | F1 HUD | F2 weather | F4 PSX | G grid", 9, FontStyles.Italic, ref y);

        // Set content height for scrolling
        var contentRect = panelGO.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(PanelW, Mathf.Abs(y) + 20f);

        Debug.Log("[LightingTestController] HUD built.");
    }

    // ══════════════════════════════════════════════════════════════
    // HUD UI Helpers (runtime)
    // ══════════════════════════════════════════════════════════════

    private GameObject MakePanel(Transform canvasTransform)
    {
        // Scroll view
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(canvasTransform, false);
        var scrollRect = scrollGO.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(0f, 1f);
        scrollRect.pivot = new Vector2(0f, 1f);
        scrollRect.offsetMin = new Vector2(10f, 10f);
        scrollRect.offsetMax = new Vector2(PanelW + 20f, -10f);

        var scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // Mask
        scrollGO.AddComponent<Mask>().showMaskGraphic = true;

        // Viewport (same as scrollGO for simplicity — mask handles clipping)

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 2000f); // will be resized after building

        scroll.content = contentRect;
        scroll.viewport = scrollGO.GetComponent<RectTransform>();

        return contentGO;
    }

    private TMP_Text MakeLabel(Transform parent, string text, float fontSize, FontStyles style, ref float y)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(10f, y);
        rect.sizeDelta = new Vector2(-20f, LH);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = new Color(0.9f, 0.9f, 0.85f);
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        y -= LH;
        return tmp;
    }

    private void MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.28f, 0.3f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(0.25f, 0.28f, 0.3f);
        colors.highlightedColor = new Color(0.4f, 0.45f, 0.48f);
        colors.pressedColor = new Color(0.15f, 0.18f, 0.2f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick());

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 10;
        tmp.color = new Color(0.9f, 0.9f, 0.85f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private Slider MakeSliderRow(Transform parent, string label, float min, float max, float defaultVal, ref float y)
    {
        // Label
        var labelGO = new GameObject($"Lbl_{label}");
        labelGO.transform.SetParent(parent, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(10f, y);
        labelRect.sizeDelta = new Vector2(-20f, 14f);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 10;
        tmp.color = new Color(0.75f, 0.75f, 0.7f);
        tmp.raycastTarget = false;
        y -= 13f;

        // Slider
        var go = new GameObject($"Slider_{label}");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(10f, y);
        rect.sizeDelta = new Vector2(SliderW, SliderH);

        var slider = go.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultVal;

        // Background
        var bgGO = new GameObject("Bg");
        bgGO.transform.SetParent(go.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.18f, 0.18f, 0.2f);
        bgImg.raycastTarget = false;

        // Fill area + fill
        var fillAreaGO = new GameObject("FillArea");
        fillAreaGO.transform.SetParent(go.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.sizeDelta = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.45f, 0.65f, 0.45f);
        fillImg.raycastTarget = false;
        slider.fillRect = fillRect;

        // Handle area + handle
        var handleAreaGO = new GameObject("HandleArea");
        handleAreaGO.transform.SetParent(go.transform, false);
        var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(5f, 0f);
        handleAreaRect.offsetMax = new Vector2(-5f, 0f);

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(10f, 0f);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(0.85f, 0.85f, 0.8f);
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;

        y -= SH;
        return slider;
    }

    // ══════════════════════════════════════════════════════════════
    // Camera
    // ══════════════════════════════════════════════════════════════

    private void SetActiveCamera(int index)
    {
        if (_cameras == null || index < 0 || index >= _cameras.Length) return;
        _activeCameraIndex = index;
        for (int i = 0; i < _cameras.Length; i++)
        {
            if (_cameras[i] != null)
                _cameras[i].Priority = (i == index) ? 20 : 0;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Apply callbacks
    // ══════════════════════════════════════════════════════════════

    private void OnTimeSliderChanged(float value)
    {
        // Route through NatureBoxController so it sets _TimeOfDay in its own Update
        // (avoids the overwrite race where NatureBoxController writes after us)
        if (NatureBoxController.Instance != null)
            NatureBoxController.Instance.SetManualTime(value);
    }

    private void ApplyNature()
    {
        // Disable NatureBoxController so it stops overwriting weather-driven properties each frame.
        // Weather buttons re-enable it via ForceWeather().
        if (NatureBoxController.Instance != null && NatureBoxController.Instance.enabled)
        {
            NatureBoxController.Instance.enabled = false;
            _manualNatureMode = true;
        }

        var mat = GetNatureBoxMaterial();
        if (mat == null) return;

        if (_cloudDensitySlider) mat.SetFloat(CloudDensityId, _cloudDensitySlider.value);
        if (_cloudSpeedSlider) mat.SetFloat(CloudSpeedId, _cloudSpeedSlider.value);
        if (_horizonFogSlider) mat.SetFloat(HorizonFogId, _horizonFogSlider.value);
        if (_sunSizeSlider) mat.SetFloat(SunSizeId, _sunSizeSlider.value);
        if (_starDensitySlider) mat.SetFloat(StarDensityId, _starDensitySlider.value);
        if (_rainIntensitySlider) mat.SetFloat(RainIntensityId, _rainIntensitySlider.value);
        if (_snowIntensitySlider) mat.SetFloat(SnowIntensityId, _snowIntensitySlider.value);
        if (_leafIntensitySlider) mat.SetFloat(LeafIntensityId, _leafIntensitySlider.value);
        if (_overcastDarkenSlider) mat.SetFloat(OvercastDarkenId, _overcastDarkenSlider.value);
        if (_snowCapSlider) mat.SetFloat(SnowCapId, _snowCapSlider.value);
    }

    private void ApplyPSX()
    {
        if (PSXRenderController.Instance == null || !PSXRenderController.Instance.enabled) return;

        if (_vertexSnapSlider)
        {
            float v = _vertexSnapSlider.value;
            PSXRenderController.Instance.VertexSnapResolution = new Vector2(v, v * 0.75f);
        }
        if (_affineSlider)
            PSXRenderController.Instance.AffineIntensity = _affineSlider.value;
        if (_resolutionDivisorSlider)
            PSXRenderController.Instance.ResolutionDivisor = Mathf.RoundToInt(_resolutionDivisorSlider.value);
        if (_colorDepthSlider)
            PSXRenderController.Instance.ColorDepth = _colorDepthSlider.value;
        if (_ditherIntensitySlider)
            PSXRenderController.Instance.DitherIntensity = _ditherIntensitySlider.value;
        if (_shadowDitherSlider)
            PSXRenderController.Instance.ShadowDitherIntensity = _shadowDitherSlider.value;
    }

    private void OnGridSizeChanged(float value)
    {
        if (_grabber != null)
            _grabber.GridSize = value;
    }

    private void TogglePSX()
    {
        if (PSXRenderController.Instance != null)
            PSXRenderController.Instance.enabled = !PSXRenderController.Instance.enabled;
    }

    private void ForceWeather(WeatherSystem.WeatherState state)
    {
        // Re-enable NatureBoxController so WeatherSystem can drive it normally
        if (_manualNatureMode && NatureBoxController.Instance != null)
        {
            NatureBoxController.Instance.enabled = true;
            _manualNatureMode = false;
        }

        if (WeatherSystem.Instance != null)
            WeatherSystem.Instance.ForceWeather(state);
    }

    // ══════════════════════════════════════════════════════════════
    // Drawer click
    // ══════════════════════════════════════════════════════════════

    private void TryToggleDrawer()
    {
        if (_cachedCamera == null) _cachedCamera = Camera.main;
        if (_cachedCamera == null || _drawers == null) return;

        Vector2 screenPos = _mousePositionAction.ReadValue<Vector2>();
        Ray ray = _cachedCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return;

        var drawer = hit.collider.GetComponent<DrawerController>();
        if (drawer == null) drawer = hit.collider.GetComponentInParent<DrawerController>();
        if (drawer == null) return;

        if (drawer.CurrentState == DrawerController.State.Closed) drawer.Open();
        else if (drawer.CurrentState == DrawerController.State.Open) drawer.Close();
    }

    // ══════════════════════════════════════════════════════════════
    // Status labels
    // ══════════════════════════════════════════════════════════════

    private void UpdateStatusLabels()
    {
        if (_cameraLabel != null)
        {
            bool isOrtho = _activeCameraIndex >= 4;
            string camName = (_cameras != null && _activeCameraIndex < _cameras.Length && _cameras[_activeCameraIndex] != null)
                ? _cameras[_activeCameraIndex].gameObject.name : "?";
            _cameraLabel.text = $"Cam {_activeCameraIndex + 1}: {camName} ({(isOrtho ? "Ortho" : "Persp")})";
        }

        if (_psxLabel != null)
        {
            bool on = PSXRenderController.Instance != null && PSXRenderController.Instance.enabled;
            _psxLabel.text = $"PSX: {(on ? "ON" : "OFF")} (F4)";
        }

        if (_gridLabel != null)
        {
            float gs = _grabber != null ? _grabber.GridSize : 0.3f;
            _gridLabel.text = $"Grid: G toggle | {gs:F2}m";
        }
    }

    private Material GetNatureBoxMaterial()
    {
        if (NatureBoxController.Instance == null) return null;
        var rend = NatureBoxController.Instance.GetComponent<Renderer>();
        return rend != null ? rend.material : null;
    }
}
