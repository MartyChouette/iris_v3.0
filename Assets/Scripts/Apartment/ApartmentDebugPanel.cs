using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime debug panel for the apartment scene. Toggle with F3.
/// Shows grid snap slider, tidiness info, and other debug controls.
/// </summary>
public class ApartmentDebugPanel : MonoBehaviour
{
    public static ApartmentDebugPanel Instance { get; private set; }

    private InputAction _toggleAction;
    private GameObject _panelGO;
    private bool _visible;

    private TMP_Text _gridLabel;
    private TMP_Text _infoText;

    private const float FontSize = 20f;
    private const float SliderWidth = 200f;
    private const float PanelWidth = 320f;
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

        if (_visible)
            UpdateInfo();
    }

    private void UpdateInfo()
    {
        if (_infoText == null) return;

        var sb = new System.Text.StringBuilder();

        // Grid snap
        if (ObjectGrabber.IsHoldingObject)
            sb.AppendLine("Holding: " + ObjectGrabber.HeldObject.ItemDescription);

        // Tidiness
        if (TidyScorer.Instance != null)
        {
            sb.Append($"Tidiness: {TidyScorer.Instance.OverallTidiness:P0}");
            sb.AppendLine();
        }

        // GameClock
        if (GameClock.Instance != null)
        {
            float h = GameClock.Instance.CurrentHour;
            int hours = Mathf.FloorToInt(Mathf.Repeat(h, 24f));
            int mins = Mathf.FloorToInt((Mathf.Repeat(h, 24f) - hours) * 60f);
            sb.AppendLine($"Day {GameClock.Instance.CurrentDay}  {hours:D2}:{mins:D2}");
        }

        // MoodMachine
        if (MoodMachine.Instance != null)
            sb.AppendLine($"Mood: {MoodMachine.Instance.Mood:F2}");

        // Weather
        if (WeatherSystem.Instance != null)
            sb.AppendLine($"Weather: {WeatherSystem.Instance.CurrentWeather}");

        _infoText.text = sb.ToString();
    }

    private void BuildPanel()
    {
        // Screen-space overlay canvas
        var canvasGO = new GameObject("ApartmentDebugCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        _panelGO = new GameObject("DebugPanel");
        _panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRT = _panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 1f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-10f, -10f);
        panelRT.sizeDelta = new Vector2(PanelWidth, 260f);

        var panelImg = _panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        panelImg.raycastTarget = false;

        var layout = _panelGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 6f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // Title
        AddLabel(_panelGO.transform, "DEBUG (F3)", FontSize + 2f, FontStyles.Bold);

        // Grid snap slider
        AddSliderRow(_panelGO.transform, "Grid Size", 0.05f, 1.0f,
            ObjectGrabber.IsHoldingObject ? 0.3f : 0.3f,
            val =>
            {
                // Find the ObjectGrabber in the scene
                var grabber = Object.FindAnyObjectByType<ObjectGrabber>();
                if (grabber != null)
                    grabber.GridSize = val;
                if (_gridLabel != null)
                    _gridLabel.text = $"Grid Size: {val:F2}m";
            },
            out _gridLabel);

        // Info text
        _infoText = AddLabel(_panelGO.transform, "", FontSize - 2f, FontStyles.Normal);
        _infoText.color = new Color(0.8f, 0.9f, 0.8f);
    }

    private TMP_Text AddLabel(Transform parent, string text, float size, FontStyles style)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
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

    private void AddSliderRow(Transform parent, string label, float min, float max,
        float initial, System.Action<float> onChange, out TMP_Text valueLabel)
    {
        var rowGO = new GameObject("SliderRow");
        rowGO.transform.SetParent(parent, false);

        var rowRT = rowGO.AddComponent<RectTransform>();
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = RowHeight + 10f;

        var rowLayout = rowGO.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 2f;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;

        // Label
        valueLabel = AddLabel(rowGO.transform, $"{label}: {initial:F2}m", FontSize, FontStyles.Normal);

        // Slider
        var sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(rowGO.transform, false);

        var sliderRT = sliderGO.AddComponent<RectTransform>();
        var sliderLE = sliderGO.AddComponent<LayoutElement>();
        sliderLE.preferredHeight = 20f;

        // Slider background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Fill area
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
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.4f, 0.7f, 0.4f, 1f);

        // Handle
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
