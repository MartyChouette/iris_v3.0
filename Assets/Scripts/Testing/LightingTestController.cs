using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Runtime controller for the lighting test scene.
/// Handles 8-camera switching (keys 1-8), debug HUD toggle (F1),
/// time-of-day slider, weather buttons, NatureBox parameter sliders,
/// and PSX rendering controls.
/// </summary>
public class LightingTestController : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("8 CinemachineCameras: 4 perspective + 4 orthographic.")]
    [SerializeField] private CinemachineCamera[] _cameras = new CinemachineCamera[8];

    [Header("HUD")]
    [Tooltip("Root canvas GO for the debug HUD (toggled with F1).")]
    [SerializeField] private GameObject _hudRoot;

    [Tooltip("Label showing current camera name + type.")]
    [SerializeField] private TMP_Text _cameraLabel;

    [Tooltip("Label showing PSX on/off status.")]
    [SerializeField] private TMP_Text _psxLabel;

    [Tooltip("Label showing grid snap on/off status.")]
    [SerializeField] private TMP_Text _gridLabel;

    [Tooltip("Slider for time of day (0-1).")]
    [SerializeField] private Slider _timeSlider;

    [Header("NatureBox Sliders")]
    [SerializeField] private Slider _cloudDensitySlider;
    [SerializeField] private Slider _cloudSpeedSlider;
    [SerializeField] private Slider _horizonFogSlider;
    [SerializeField] private Slider _sunSizeSlider;
    [SerializeField] private Slider _starDensitySlider;
    [SerializeField] private Slider _rainIntensitySlider;
    [SerializeField] private Slider _snowIntensitySlider;
    [SerializeField] private Slider _leafIntensitySlider;
    [SerializeField] private Slider _overcastDarkenSlider;
    [SerializeField] private Slider _snowCapSlider;

    [Header("PSX Sliders")]
    [SerializeField] private Slider _vertexSnapSlider;
    [SerializeField] private Slider _affineSlider;
    [SerializeField] private Slider _resolutionDivisorSlider;
    [SerializeField] private Slider _colorDepthSlider;
    [SerializeField] private Slider _ditherIntensitySlider;
    [SerializeField] private Slider _shadowDitherSlider;

    // Inline InputActions
    private InputAction _hudToggleAction;
    private InputAction[] _cameraActions;
    private InputAction _drawerClickAction;
    private InputAction _mousePositionAction;

    private int _activeCameraIndex;
    private bool _hudVisible = true;
    private bool _manualTimeMode;

    // Shader property IDs for direct NatureBox material control
    private static readonly int TimeOfDayId = Shader.PropertyToID("_TimeOfDay");
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

    // DrawerController references for click-to-toggle
    private DrawerController[] _drawers;
    private Camera _cachedCamera;

    private void Awake()
    {
        _hudToggleAction = new InputAction("HUDToggle", InputActionType.Button, "<Keyboard>/f1");
        _drawerClickAction = new InputAction("DrawerClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePositionAction = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");

        _cameraActions = new InputAction[8];
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8" };
        for (int i = 0; i < 8; i++)
        {
            _cameraActions[i] = new InputAction($"Cam{i + 1}", InputActionType.Button,
                $"<Keyboard>/{keys[i]}");
        }
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
        {
            for (int i = 0; i < _cameraActions.Length; i++)
                _cameraActions[i]?.Dispose();
        }
    }

    private void Start()
    {
        _drawers = FindObjectsByType<DrawerController>(FindObjectsSortMode.None);
        _cachedCamera = Camera.main;

        // Set initial camera
        SetActiveCamera(0);

        // Wire time slider
        if (_timeSlider != null)
        {
            _timeSlider.minValue = 0f;
            _timeSlider.maxValue = 1f;
            _timeSlider.value = 0.5f;
            _timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        }

        // Wire nature sliders
        WireSlider(_cloudDensitySlider, 0f, 1f, 0.45f, OnNatureSliderChanged);
        WireSlider(_cloudSpeedSlider, 0f, 2f, 0.5f, OnNatureSliderChanged);
        WireSlider(_horizonFogSlider, 0f, 1f, 0.35f, OnNatureSliderChanged);
        WireSlider(_sunSizeSlider, 0f, 1f, 0.15f, OnNatureSliderChanged);
        WireSlider(_starDensitySlider, 0f, 1f, 0.5f, OnNatureSliderChanged);
        WireSlider(_rainIntensitySlider, 0f, 1f, 0f, OnNatureSliderChanged);
        WireSlider(_snowIntensitySlider, 0f, 1f, 0f, OnNatureSliderChanged);
        WireSlider(_leafIntensitySlider, 0f, 1f, 0f, OnNatureSliderChanged);
        WireSlider(_overcastDarkenSlider, 0f, 1f, 0f, OnNatureSliderChanged);
        WireSlider(_snowCapSlider, 0f, 1f, 0f, OnNatureSliderChanged);

        // Wire PSX sliders
        WireSlider(_vertexSnapSlider, 32f, 320f, 160f, OnPSXSliderChanged);
        WireSlider(_affineSlider, 0f, 1f, 1f, OnPSXSliderChanged);
        WireSlider(_resolutionDivisorSlider, 1f, 6f, 3f, OnPSXSliderChanged);
        WireSlider(_colorDepthSlider, 4f, 256f, 32f, OnPSXSliderChanged);
        WireSlider(_ditherIntensitySlider, 0f, 1f, 0.5f, OnPSXSliderChanged);
        WireSlider(_shadowDitherSlider, 0f, 1f, 1f, OnPSXSliderChanged);

        if (_hudRoot != null)
            _hudRoot.SetActive(_hudVisible);
    }

    private void Update()
    {
        // F1 toggle HUD
        if (_hudToggleAction.WasPressedThisFrame())
        {
            _hudVisible = !_hudVisible;
            if (_hudRoot != null)
                _hudRoot.SetActive(_hudVisible);
        }

        // Camera switching (1-8)
        for (int i = 0; i < _cameraActions.Length; i++)
        {
            if (_cameraActions[i].WasPressedThisFrame())
            {
                SetActiveCamera(i);
                break;
            }
        }

        // Click drawer toggle (simple raycast for DrawerController objects)
        if (_drawerClickAction.WasPressedThisFrame() && !ObjectGrabber.IsHoldingObject)
        {
            TryToggleDrawer();
        }

        // Manual time mode: drive NatureBox directly
        if (_manualTimeMode && NatureBoxController.Instance != null)
        {
            var mat = GetNatureBoxMaterial();
            if (mat != null && _timeSlider != null)
                mat.SetFloat(TimeOfDayId, _timeSlider.value);
        }

        UpdateStatusLabels();
    }

    // ── Camera switching ──────────────────────────────────────────

    private void SetActiveCamera(int index)
    {
        if (_cameras == null || index < 0 || index >= _cameras.Length) return;

        _activeCameraIndex = index;

        for (int i = 0; i < _cameras.Length; i++)
        {
            if (_cameras[i] != null)
                _cameras[i].Priority = (i == index) ? 20 : 0;
        }

        Debug.Log($"[LightingTestController] Camera {index + 1}: {GetCameraName(index)}");
    }

    // Public methods for UI button wiring (one per camera)
    public void SetCamera1() => SetActiveCamera(0);
    public void SetCamera2() => SetActiveCamera(1);
    public void SetCamera3() => SetActiveCamera(2);
    public void SetCamera4() => SetActiveCamera(3);
    public void SetCamera5() => SetActiveCamera(4);
    public void SetCamera6() => SetActiveCamera(5);
    public void SetCamera7() => SetActiveCamera(6);
    public void SetCamera8() => SetActiveCamera(7);

    private string GetCameraName(int index)
    {
        if (_cameras == null || index < 0 || index >= _cameras.Length || _cameras[index] == null)
            return "Unknown";
        return _cameras[index].gameObject.name;
    }

    // ── Time slider ──────────────────────────────────────────────

    private void OnTimeSliderChanged(float value)
    {
        _manualTimeMode = true;

        // Pause GameClock if present
        if (GameClock.Instance != null)
            GameClock.Instance.enabled = false;

        var mat = GetNatureBoxMaterial();
        if (mat != null)
            mat.SetFloat(TimeOfDayId, value);
    }

    // ── Nature sliders ──────────────────────────────────────────

    private void OnNatureSliderChanged(float _)
    {
        var mat = GetNatureBoxMaterial();
        if (mat == null) return;

        if (_cloudDensitySlider != null)
            mat.SetFloat(CloudDensityId, _cloudDensitySlider.value);
        if (_cloudSpeedSlider != null)
            mat.SetFloat(CloudSpeedId, _cloudSpeedSlider.value);
        if (_horizonFogSlider != null)
            mat.SetFloat(HorizonFogId, _horizonFogSlider.value);
        if (_sunSizeSlider != null)
            mat.SetFloat(SunSizeId, _sunSizeSlider.value);
        if (_starDensitySlider != null)
            mat.SetFloat(StarDensityId, _starDensitySlider.value);
        if (_rainIntensitySlider != null)
            mat.SetFloat(RainIntensityId, _rainIntensitySlider.value);
        if (_snowIntensitySlider != null)
            mat.SetFloat(SnowIntensityId, _snowIntensitySlider.value);
        if (_leafIntensitySlider != null)
            mat.SetFloat(LeafIntensityId, _leafIntensitySlider.value);
        if (_overcastDarkenSlider != null)
            mat.SetFloat(OvercastDarkenId, _overcastDarkenSlider.value);
        if (_snowCapSlider != null)
            mat.SetFloat(SnowCapId, _snowCapSlider.value);
    }

    // ── PSX sliders ──────────────────────────────────────────────

    private void OnPSXSliderChanged(float _)
    {
        if (PSXRenderController.Instance == null || !PSXRenderController.Instance.enabled) return;

        if (_vertexSnapSlider != null)
        {
            float v = _vertexSnapSlider.value;
            PSXRenderController.Instance.VertexSnapResolution = new Vector2(v, v * 0.75f);
        }
        if (_affineSlider != null)
            PSXRenderController.Instance.AffineIntensity = _affineSlider.value;
        if (_resolutionDivisorSlider != null)
            PSXRenderController.Instance.ResolutionDivisor = Mathf.RoundToInt(_resolutionDivisorSlider.value);
        if (_colorDepthSlider != null)
            PSXRenderController.Instance.ColorDepth = _colorDepthSlider.value;
        if (_ditherIntensitySlider != null)
            PSXRenderController.Instance.DitherIntensity = _ditherIntensitySlider.value;
        if (_shadowDitherSlider != null)
            PSXRenderController.Instance.ShadowDitherIntensity = _shadowDitherSlider.value;
    }

    // ── Weather buttons (called from UI) ────────────────────────

    public void ForceWeatherClear() => ForceWeather(WeatherSystem.WeatherState.Clear);
    public void ForceWeatherOvercast() => ForceWeather(WeatherSystem.WeatherState.Overcast);
    public void ForceWeatherRainy() => ForceWeather(WeatherSystem.WeatherState.Rainy);
    public void ForceWeatherStormy() => ForceWeather(WeatherSystem.WeatherState.Stormy);
    public void ForceWeatherSnowy() => ForceWeather(WeatherSystem.WeatherState.Snowy);
    public void ForceWeatherLeaves() => ForceWeather(WeatherSystem.WeatherState.FallingLeaves);

    private void ForceWeather(WeatherSystem.WeatherState state)
    {
        if (WeatherSystem.Instance != null)
            WeatherSystem.Instance.ForceWeather(state);
    }

    // ── PSX toggle (called from UI) ─────────────────────────────

    public void TogglePSX()
    {
        if (PSXRenderController.Instance != null)
            PSXRenderController.Instance.enabled = !PSXRenderController.Instance.enabled;
    }

    // ── Drawer click toggle ─────────────────────────────────────

    private void TryToggleDrawer()
    {
        if (_cachedCamera == null)
            _cachedCamera = Camera.main;
        if (_cachedCamera == null || _drawers == null) return;

        Vector2 screenPos = _mousePositionAction.ReadValue<Vector2>();
        Ray ray = _cachedCamera.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
            return;

        var drawer = hit.collider.GetComponent<DrawerController>();
        if (drawer == null)
            drawer = hit.collider.GetComponentInParent<DrawerController>();
        if (drawer == null) return;

        if (drawer.CurrentState == DrawerController.State.Closed)
            drawer.Open();
        else if (drawer.CurrentState == DrawerController.State.Open)
            drawer.Close();
    }

    // ── Status labels ──────────────────────────────────────────

    private void UpdateStatusLabels()
    {
        if (_cameraLabel != null)
        {
            bool isOrtho = _activeCameraIndex >= 4;
            _cameraLabel.text = $"Camera {_activeCameraIndex + 1}: {GetCameraName(_activeCameraIndex)} ({(isOrtho ? "Ortho" : "Perspective")})";
        }

        if (_psxLabel != null)
        {
            bool psxOn = PSXRenderController.Instance != null && PSXRenderController.Instance.enabled;
            _psxLabel.text = $"PSX: {(psxOn ? "ON" : "OFF")} (F4)";
        }

        if (_gridLabel != null)
        {
            _gridLabel.text = $"Grid: {(ObjectGrabber.IsHoldingObject ? "—" : "G toggle")}";
        }
    }

    // ── Helpers ─────────────────────────────────────────────────

    private Material GetNatureBoxMaterial()
    {
        if (NatureBoxController.Instance == null) return null;
        var rend = NatureBoxController.Instance.GetComponent<Renderer>();
        return rend != null ? rend.material : null;
    }

    private static void WireSlider(Slider slider, float min, float max, float defaultValue,
        UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null) return;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        slider.onValueChanged.AddListener(callback);
    }
}
