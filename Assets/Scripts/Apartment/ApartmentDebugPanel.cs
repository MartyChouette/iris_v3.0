using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime debug panel for the apartment scene. Toggle with F3.
/// Grid snap, highlight style, atmosphere tuning, info readout.
/// </summary>
public class ApartmentDebugPanel : MonoBehaviour
{
    public static ApartmentDebugPanel Instance { get; private set; }

    private InputAction _toggleAction;
    private GameObject _panelGO;
    private bool _visible;

    private TMP_Text _gridLabel;
    private TMP_Text _infoText;

    // Highlight controls
    private TMP_Text _hlStyleLabel;
    private TMP_Text _hlWidthLabel;
    private TMP_Text _hlAlphaLabel;
    private TMP_Text _hlPulseLabel;
    private TMP_Text _hlRimLabel;
    private Slider _hlWidthSlider;
    private Slider _hlAlphaSlider;
    private Slider _hlPulseSlider;
    private Slider _hlRimSlider;

    // Grab feel controls
    private TMP_Text _grabFeelLabel;
    private TMP_Text _grabSpringLabel;
    private TMP_Text _grabDamperLabel;
    private TMP_Text _grabAccelLabel;
    private TMP_Text _grabSpeedLabel;

    // Atmosphere controls
    private TMP_Text _atmSatLabel;
    private TMP_Text _atmContrastLabel;
    private TMP_Text _atmExposureLabel;
    private TMP_Text _atmBloomIntLabel;
    private TMP_Text _atmBloomThreshLabel;
    private TMP_Text _atmBloomScatterLabel;
    private TMP_Text _atmVignetteLabel;
    private TMP_Text _atmGrainLabel;

    private const float FontSize = 18f;
    private const float PanelWidth = 340f;
    private const float RowHeight = 28f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _toggleAction = new InputAction("ToggleDebugPanel", InputActionType.Button,
            "<Keyboard>/f3");
    }

    private void Start()
    {
        BuildPanel();
        _panelGO.SetActive(false);
    }

    private void OnEnable() => _toggleAction?.Enable();
    private void OnDisable() => _toggleAction?.Disable();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _toggleAction?.Dispose();
    }

    private void Update()
    {
        if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
        {
            _visible = !_visible;
            _panelGO.SetActive(_visible);
        }

        // F5 cycles highlight style
        if (Input.GetKeyDown(KeyCode.F5))
        {
            InteractableHighlight.CycleStyle();
            RefreshHighlightStyleLabel();
        }

        // F6 cycles grab feel preset
        if (Input.GetKeyDown(KeyCode.F6))
        {
            ObjectGrabber.CycleGrabFeel();
            RefreshGrabFeelLabel();
        }

        if (_visible)
            UpdateInfo();
    }

    private void UpdateInfo()
    {
        if (_infoText == null) return;

        var sb = new System.Text.StringBuilder();

        // ── Game State ──
        if (ObjectGrabber.IsHoldingObject)
            sb.AppendLine("Holding: " + ObjectGrabber.HeldObject.ItemDescription);

        if (TidyScorer.Instance != null)
            sb.AppendLine($"Tidiness: {TidyScorer.Instance.OverallTidiness:P0}");

        if (GameClock.Instance != null)
        {
            float h = GameClock.Instance.CurrentHour;
            int hours = Mathf.FloorToInt(Mathf.Repeat(h, 24f));
            int mins = Mathf.FloorToInt((Mathf.Repeat(h, 24f) - hours) * 60f);
            sb.AppendLine($"Day {GameClock.Instance.CurrentDay}  {hours:D2}:{mins:D2}");
        }

        if (DayPhaseManager.Instance != null)
            sb.AppendLine($"Phase: {DayPhaseManager.Instance.CurrentPhase}");

        // ── Systems Status ──
        sb.AppendLine("--- systems ---");

        // MoodMachine
        if (MoodMachine.Instance != null)
            sb.AppendLine($"Mood: {MoodMachine.Instance.Mood:F2}");
        else
            sb.AppendLine("Mood: OFF");

        // Weather
        if (WeatherSystem.Instance != null)
            sb.AppendLine($"Weather: {WeatherSystem.Instance.CurrentWeather}");
        else
            sb.AppendLine("Weather: OFF");

        // Atmosphere
        var atm = AtmosphereController.Instance;
        if (atm != null)
        {
            float mood = MoodMachine.Instance != null ? MoodMachine.Instance.Mood : -1f;
            sb.Append($"Atmo: ON");
            if (mood >= 0f) sb.Append($" (mood>{mood:F2})");
            sb.AppendLine();
        }
        else
            sb.AppendLine("Atmo: OFF");

        // NatureBox
        var nature = NatureBoxController.Instance;
        if (nature != null)
        {
            float tod = GameClock.Instance != null ? GameClock.Instance.NormalizedTimeOfDay : -1f;
            sb.AppendLine(tod >= 0f ? $"NatureBox: ON (t={tod:F2})" : "NatureBox: ON (manual)");
        }
        else
            sb.AppendLine("NatureBox: OFF");

        // PSX
        if (PSXRenderController.Instance != null && PSXRenderController.Instance.enabled)
            sb.AppendLine($"PSX: ON (snap={PSXRenderController.Instance.VertexSnapResolution.x:F0})");
        else
            sb.AppendLine("PSX: OFF");

        // Light
        var sun = RenderSettings.sun;
        if (sun != null)
            sb.AppendLine($"Light: {sun.intensity:F2} ({sun.color.r:F1},{sun.color.g:F1},{sun.color.b:F1})");

        // Fog
        if (RenderSettings.fog)
            sb.AppendLine($"Fog: {RenderSettings.fogDensity:F4} ({RenderSettings.fogColor.r:F1},{RenderSettings.fogColor.g:F1},{RenderSettings.fogColor.b:F1})");

        _infoText.text = sb.ToString();
    }

    // ── Highlight tuning ──

    private void RefreshHighlightStyleLabel()
    {
        if (_hlStyleLabel != null)
            _hlStyleLabel.text = $"Highlight: {InteractableHighlight.CurrentStyle} (F5)";
    }

    private void OnHighlightParamChanged()
    {
        float width = _hlWidthSlider != null ? _hlWidthSlider.value : 0.008f;
        float alpha = _hlAlphaSlider != null ? _hlAlphaSlider.value : 0.25f;
        float pulse = _hlPulseSlider != null ? _hlPulseSlider.value : 0.1f;
        float rim = _hlRimSlider != null ? _hlRimSlider.value : 2.5f;

        if (_hlWidthLabel != null) _hlWidthLabel.text = $"Width: {width:F3}";
        if (_hlAlphaLabel != null) _hlAlphaLabel.text = $"Alpha: {alpha:F2}";
        if (_hlPulseLabel != null) _hlPulseLabel.text = $"Pulse: {pulse:F2}";
        if (_hlRimLabel != null) _hlRimLabel.text = $"Rim Power: {rim:F1}";

        InteractableHighlight.SetTuningOverrides(width, alpha, pulse, rim);
    }

    // ── Grab feel tuning ──

    private void RefreshGrabFeelLabel()
    {
        if (_grabFeelLabel != null)
            _grabFeelLabel.text = $"Grab: {ObjectGrabber.CurrentFeel} (F6)";
    }

    private void OnGrabParamChanged()
    {
        float spring = 35f, damper = 6f, accel = 20f, speed = 5f;
        if (_grabSpringLabel != null) { spring = GetLastSlider(_grabSpringLabel.transform.parent.parent).value; _grabSpringLabel.text = $"Spring: {spring:F0}"; }
        if (_grabDamperLabel != null) { damper = GetLastSlider(_grabDamperLabel.transform.parent.parent).value; _grabDamperLabel.text = $"Damper: {damper:F0}"; }
        if (_grabAccelLabel != null) { accel = GetLastSlider(_grabAccelLabel.transform.parent.parent).value; _grabAccelLabel.text = $"Max Accel: {accel:F0}"; }
        if (_grabSpeedLabel != null) { speed = GetLastSlider(_grabSpeedLabel.transform.parent.parent).value; _grabSpeedLabel.text = $"Max Speed: {speed:F0}"; }
        ObjectGrabber.SetGrabOverrides(spring, damper, accel, speed);
    }

    // ── Panel construction ──

    private void BuildPanel()
    {
        // Screen-space overlay canvas
        var canvasGO = new GameObject("ApartmentDebugCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        _panelGO = new GameObject("DebugPanel");
        _panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRT = _panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 0f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot = new Vector2(1f, 0.5f);
        panelRT.anchoredPosition = new Vector2(-10f, 0f);
        panelRT.sizeDelta = new Vector2(PanelWidth, -20f);

        var panelImg = _panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.88f);
        panelImg.raycastTarget = false;

        var layout = _panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.spacing = 2f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var contentParent = _panelGO.transform;

        // ═════════════════════════════════════
        //  CONTENT
        // ═════════════════════════════════════

        AddLabel(contentParent, "DEBUG (F3)", FontSize + 2f, FontStyles.Bold);

        // ── Grid snap ──
        var initGrabber = Object.FindAnyObjectByType<ObjectGrabber>();
        float initGrid = initGrabber != null ? initGrabber.GridSize : 0.11f;
        AddSliderRow(contentParent, "Grid Size", 0.05f, 1.0f, initGrid,
            val =>
            {
                var grabber = Object.FindAnyObjectByType<ObjectGrabber>();
                if (grabber != null) grabber.GridSize = val;
                if (_gridLabel != null) _gridLabel.text = $"Grid: {val:F2}m";
            },
            out _gridLabel);

        // ── Highlight ──
        AddSeparator(contentParent);
        _hlStyleLabel = AddLabel(contentParent, $"Highlight: {InteractableHighlight.CurrentStyle} (F5)",
            FontSize, FontStyles.Bold);
        _hlStyleLabel.color = new Color(1f, 0.9f, 0.7f);

        AddSliderRow(contentParent, "Width", 0.001f, 0.05f, 0.008f,
            _ => OnHighlightParamChanged(), out _hlWidthLabel);
        _hlWidthSlider = GetLastSlider(contentParent);

        AddSliderRow(contentParent, "Alpha", 0.05f, 1.0f, 0.25f,
            _ => OnHighlightParamChanged(), out _hlAlphaLabel);
        _hlAlphaSlider = GetLastSlider(contentParent);

        AddSliderRow(contentParent, "Pulse", 0f, 0.5f, 0.1f,
            _ => OnHighlightParamChanged(), out _hlPulseLabel);
        _hlPulseSlider = GetLastSlider(contentParent);

        AddSliderRow(contentParent, "Rim Power", 0.5f, 8.0f, 2.5f,
            _ => OnHighlightParamChanged(), out _hlRimLabel);
        _hlRimSlider = GetLastSlider(contentParent);

        TMP_Text hlSnapResLabel = null, hlOffsetLabel = null, hlJitterLabel = null;

        AddSliderRow(contentParent, "HL Snap Res", 0f, 500f, 160f,
            val => { InteractableHighlight.HLSnapEnabled = val > 1f;
                     InteractableHighlight.HLSnapRes = val;
                     hlSnapResLabel.text = val > 1f ? $"HL Snap: {val:F0}" : "HL Snap: OFF"; },
            out hlSnapResLabel);

        AddSliderRow(contentParent, "HL Offset", 0f, 0.02f, 0.001f,
            val => { InteractableHighlight.HLNormalOffset = val;
                     hlOffsetLabel.text = $"HL Offset: {val:F3}"; },
            out hlOffsetLabel);

        AddSliderRow(contentParent, "HL Jitter", 0f, 0.02f, 0f,
            val => { InteractableHighlight.HLJitter = val;
                     hlJitterLabel.text = $"HL Jitter: {val:F3}"; },
            out hlJitterLabel);

        // ── Grab Feel ──
        AddSeparator(contentParent);
        _grabFeelLabel = AddLabel(contentParent, $"Grab: {ObjectGrabber.CurrentFeel} (F6)",
            FontSize, FontStyles.Bold);
        _grabFeelLabel.color = new Color(0.9f, 0.75f, 1f);

        AddSliderRow(contentParent, "Spring", 5f, 500f, 35f,
            _ => OnGrabParamChanged(), out _grabSpringLabel);

        AddSliderRow(contentParent, "Damper", 1f, 50f, 6f,
            _ => OnGrabParamChanged(), out _grabDamperLabel);

        AddSliderRow(contentParent, "Max Accel", 5f, 200f, 20f,
            _ => OnGrabParamChanged(), out _grabAccelLabel);

        AddSliderRow(contentParent, "Max Speed", 1f, 30f, 5f,
            _ => OnGrabParamChanged(), out _grabSpeedLabel);

        // ── Atmosphere ──
        AddSeparator(contentParent);
        var atmTitle = AddLabel(contentParent, "Atmosphere", FontSize, FontStyles.Bold);
        atmTitle.color = new Color(0.7f, 0.85f, 1f);

        AddSliderRow(contentParent, "Saturation", -80f, 20f, -25f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.Saturation = val;
                     if (_atmSatLabel != null) _atmSatLabel.text = $"Saturation: {val:F0}"; },
            out _atmSatLabel);

        AddSliderRow(contentParent, "Contrast", -50f, 50f, 12f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.Contrast = val;
                     if (_atmContrastLabel != null) _atmContrastLabel.text = $"Contrast: {val:F0}"; },
            out _atmContrastLabel);

        AddSliderRow(contentParent, "Exposure", -2f, 3f, 0.3f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.PostExposure = val;
                     if (_atmExposureLabel != null) _atmExposureLabel.text = $"Exposure: {val:F1}"; },
            out _atmExposureLabel);

        AddSliderRow(contentParent, "Bloom Int", 0f, 3f, 0.6f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.BloomIntensity = val;
                     if (_atmBloomIntLabel != null) _atmBloomIntLabel.text = $"Bloom Int: {val:F2}"; },
            out _atmBloomIntLabel);

        AddSliderRow(contentParent, "Bloom Thresh", 0f, 2f, 0.7f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.BloomThreshold = val;
                     if (_atmBloomThreshLabel != null) _atmBloomThreshLabel.text = $"Bloom Thresh: {val:F2}"; },
            out _atmBloomThreshLabel);

        AddSliderRow(contentParent, "Bloom Scatter", 0f, 1f, 0.75f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.BloomScatter = val;
                     if (_atmBloomScatterLabel != null) _atmBloomScatterLabel.text = $"Bloom Scatter: {val:F2}"; },
            out _atmBloomScatterLabel);

        AddSliderRow(contentParent, "Vignette", 0f, 1f, 0.3f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.VignetteIntensity = val;
                     if (_atmVignetteLabel != null) _atmVignetteLabel.text = $"Vignette: {val:F2}"; },
            out _atmVignetteLabel);

        AddSliderRow(contentParent, "Film Grain", 0f, 1f, 0.15f,
            val => { if (AtmosphereController.Instance != null) AtmosphereController.Instance.GrainIntensity = val;
                     if (_atmGrainLabel != null) _atmGrainLabel.text = $"Film Grain: {val:F2}"; },
            out _atmGrainLabel);

        // ── Info text ──
        AddSeparator(contentParent);
        _infoText = AddLabel(contentParent, "", FontSize - 2f, FontStyles.Normal);
        _infoText.color = new Color(0.8f, 0.9f, 0.8f);
    }

    // ── UI Helpers ──

    private Slider GetLastSlider(Transform parent)
    {
        var sliders = parent.GetComponentsInChildren<Slider>();
        return sliders.Length > 0 ? sliders[sliders.Length - 1] : null;
    }

    private TMP_Text AddLabel(Transform parent, string text, float size, FontStyles style)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = RowHeight;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        return tmp;
    }

    private void AddSeparator(Transform parent)
    {
        var go = new GameObject("Separator");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 2f;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        img.raycastTarget = false;
    }

    private void AddSliderRow(Transform parent, string label, float min, float max,
        float initial, System.Action<float> onChange, out TMP_Text valueLabel)
    {
        var rowGO = new GameObject("SliderRow");
        rowGO.transform.SetParent(parent, false);

        rowGO.AddComponent<RectTransform>();
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = RowHeight + 8f;

        var rowLayout = rowGO.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 1f;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;

        valueLabel = AddLabel(rowGO.transform, $"{label}: {initial:F2}", FontSize - 2f, FontStyles.Normal);

        // Slider
        var sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(rowGO.transform, false);

        sliderGO.AddComponent<RectTransform>();
        var sliderLE = sliderGO.AddComponent<LayoutElement>();
        sliderLE.preferredHeight = 16f;

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = Vector2.zero;
        fillAreaRT.offsetMax = Vector2.zero;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillGO.AddComponent<Image>().color = new Color(0.35f, 0.55f, 0.7f, 1f);

        var handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = Vector2.zero;
        handleAreaRT.offsetMax = Vector2.zero;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(14f, 0f);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;

        var slider = sliderGO.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = initial;
        slider.onValueChanged.AddListener(val => onChange?.Invoke(val));
    }
}
