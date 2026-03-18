using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime debug panel for the apartment scene. Toggle with F3.
/// Shows grid snap slider, highlight style controls, tidiness info, and other debug controls.
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

    private const float FontSize = 20f;
    private const float SliderWidth = 200f;
    private const float PanelWidth = 340f;
    private const float RowHeight = 32f;

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

        if (_visible)
            UpdateInfo();
    }

    private void UpdateInfo()
    {
        if (_infoText == null) return;

        var sb = new System.Text.StringBuilder();

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

        if (MoodMachine.Instance != null)
            sb.AppendLine($"Mood: {MoodMachine.Instance.Mood:F2}");

        if (WeatherSystem.Instance != null)
            sb.AppendLine($"Weather: {WeatherSystem.Instance.CurrentWeather}");

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

    // ── Panel construction ──

    private void BuildPanel()
    {
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
        panelRT.anchorMin = new Vector2(1f, 1f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-10f, -10f);
        panelRT.sizeDelta = new Vector2(PanelWidth, 520f);

        var panelImg = _panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        panelImg.raycastTarget = false;

        var layout = _panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 4f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // ── Title ──
        AddLabel(_panelGO.transform, "DEBUG (F3)", FontSize + 2f, FontStyles.Bold);

        // ── Grid snap slider ──
        var initGrabber = Object.FindAnyObjectByType<ObjectGrabber>();
        float initGrid = initGrabber != null ? initGrabber.GridSize : 0.11f;
        AddSliderRow(_panelGO.transform, "Grid Size", 0.05f, 1.0f, initGrid,
            val =>
            {
                var grabber = Object.FindAnyObjectByType<ObjectGrabber>();
                if (grabber != null) grabber.GridSize = val;
                if (_gridLabel != null) _gridLabel.text = $"Grid Size: {val:F2}m";
            },
            out _gridLabel);

        // ── Separator ──
        AddSeparator(_panelGO.transform);

        // ── Highlight style label ──
        _hlStyleLabel = AddLabel(_panelGO.transform, $"Highlight: {InteractableHighlight.CurrentStyle} (F5)",
            FontSize, FontStyles.Bold);
        _hlStyleLabel.color = new Color(1f, 0.9f, 0.7f);

        // ── Outline Width ──
        AddSliderRow(_panelGO.transform, "Width", 0.001f, 0.05f, 0.008f,
            val => OnHighlightParamChanged(), out _hlWidthLabel);
        _hlWidthSlider = _panelGO.GetComponentsInChildren<Slider>()[1]; // second slider

        // ── Alpha / Intensity ──
        AddSliderRow(_panelGO.transform, "Alpha", 0.05f, 1.0f, 0.25f,
            val => OnHighlightParamChanged(), out _hlAlphaLabel);
        _hlAlphaSlider = _panelGO.GetComponentsInChildren<Slider>()[2];

        // ── Pulse Amount ──
        AddSliderRow(_panelGO.transform, "Pulse", 0f, 0.5f, 0.1f,
            val => OnHighlightParamChanged(), out _hlPulseLabel);
        _hlPulseSlider = _panelGO.GetComponentsInChildren<Slider>()[3];

        // ── Rim Power (RimGlow only) ──
        AddSliderRow(_panelGO.transform, "Rim Power", 0.5f, 8.0f, 2.5f,
            val => OnHighlightParamChanged(), out _hlRimLabel);
        _hlRimSlider = _panelGO.GetComponentsInChildren<Slider>()[4];

        // ── Info text ──
        AddSeparator(_panelGO.transform);
        _infoText = AddLabel(_panelGO.transform, "", FontSize - 2f, FontStyles.Normal);
        _infoText.color = new Color(0.8f, 0.9f, 0.8f);
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
        rowLE.preferredHeight = RowHeight + 10f;

        var rowLayout = rowGO.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 2f;
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
        sliderLE.preferredHeight = 20f;

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
        fillGO.AddComponent<Image>().color = new Color(0.4f, 0.7f, 0.4f, 1f);

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
        handleRT.sizeDelta = new Vector2(16f, 0f);
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
