using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.UI;

public class CameraTestController : MonoBehaviour
{
    [Header("Presets")]
    [Tooltip("Camera presets to compare (V1, V2, V3).")]
    [SerializeField] private CameraPresetDefinition[] presets;

    [Header("References")]
    [SerializeField] private CinemachineCamera browseCamera;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private ApartmentManager apartmentManager;

    [Header("Transition")]
    [SerializeField, Range(1f, 15f)] private float transitionSpeed = 5f;

    [Header("UI")]
    [SerializeField] private Button[] presetButtons;
    [SerializeField] private Color activeButtonColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);
    [SerializeField] private Color inactiveButtonColor = new Color(0.3f, 0.3f, 0.3f, 0.85f);

    public int ActivePresetIndex => _activePresetIndex;

    private int _activePresetIndex = -1;
    private Vector3 _targetPos;
    private Quaternion _targetRot;
    private float _targetFovOrOrtho;
    private bool _targetIsOrtho;
    private bool _isTransitioning;

    // Current lerped values (for smooth transition before handing to ApartmentManager)
    private Vector3 _currentPos;
    private Quaternion _currentRot;

    // Keyboard shortcuts
    private InputAction _preset1Action;
    private InputAction _preset2Action;
    private InputAction _preset3Action;
    private InputAction _clearPresetAction;

    private void Awake()
    {
        _preset1Action = new InputAction("Preset1", InputActionType.Button, "<Keyboard>/1");
        _preset2Action = new InputAction("Preset2", InputActionType.Button, "<Keyboard>/2");
        _preset3Action = new InputAction("Preset3", InputActionType.Button, "<Keyboard>/3");
        _clearPresetAction = new InputAction("ClearPreset", InputActionType.Button, "<Keyboard>/backquote");
    }

    private void OnEnable()
    {
        _preset1Action.Enable();
        _preset2Action.Enable();
        _preset3Action.Enable();
        _clearPresetAction.Enable();
    }

    private void OnDisable()
    {
        _preset1Action.Disable();
        _preset2Action.Disable();
        _preset3Action.Disable();
        _clearPresetAction.Disable();
    }

    private void Start()
    {
        // Wire button onClick at runtime (persistent listeners break on int params)
        if (presetButtons != null)
        {
            for (int i = 0; i < presetButtons.Length; i++)
            {
                if (presetButtons[i] == null) continue;
                int index = i;
                presetButtons[i].onClick.AddListener(() => ApplyPreset(index));
            }
        }
    }

    public void ApplyPreset(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return;

        _activePresetIndex = index;
        int areaIndex = apartmentManager != null ? apartmentManager.CurrentAreaIndex : 0;
        ApplyForArea(presets[index], areaIndex, immediate: true);
        UpdateButtonHighlights();
        Debug.Log($"[CameraTestController] Applied preset: {presets[index].label}");
    }

    public void OnAreaChanged(int areaIndex)
    {
        if (_activePresetIndex < 0 || presets == null) return;
        ApplyForArea(presets[_activePresetIndex], areaIndex, immediate: false);
    }

    public void ClearPreset()
    {
        _activePresetIndex = -1;
        _isTransitioning = false;

        if (browseCamera != null)
        {
            var lens = browseCamera.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.None;
            browseCamera.Lens = lens;
        }

        SetBrainOrthoOverride(false);
        UpdateButtonHighlights();

        if (apartmentManager != null)
            apartmentManager.ClearPresetBase();
    }

    private void ApplyForArea(CameraPresetDefinition preset, int areaIndex, bool immediate)
    {
        if (preset.areaConfigs == null || areaIndex >= preset.areaConfigs.Length) return;

        var config = preset.areaConfigs[areaIndex];
        _targetPos = config.position;
        _targetRot = Quaternion.Euler(config.rotation);
        _targetFovOrOrtho = config.fovOrOrthoSize;
        _targetIsOrtho = preset.orthographic;

        SetBrainOrthoOverride(true);

        if (immediate)
        {
            _currentPos = _targetPos;
            _currentRot = _targetRot;
            ApplyLens();
            FeedToApartmentManager();
            _isTransitioning = false;
        }
        else
        {
            // Start lerp from current camera position
            if (browseCamera != null)
            {
                _currentPos = browseCamera.transform.position;
                _currentRot = browseCamera.transform.rotation;
            }
            _isTransitioning = true;
        }
    }

    private void Update()
    {
        // Keyboard shortcuts
        if (_preset1Action.WasPressedThisFrame()) ApplyPreset(0);
        else if (_preset2Action.WasPressedThisFrame()) ApplyPreset(1);
        else if (_preset3Action.WasPressedThisFrame()) ApplyPreset(2);
        else if (_clearPresetAction.WasPressedThisFrame()) ClearPreset();

        if (!_isTransitioning || _activePresetIndex < 0 || browseCamera == null) return;

        float step = transitionSpeed * Time.deltaTime;

        _currentPos = Vector3.Lerp(_currentPos, _targetPos, step);
        _currentRot = Quaternion.Slerp(_currentRot, _targetRot, step);

        // Lerp FOV / ortho size
        var lens = browseCamera.Lens;
        if (_targetIsOrtho)
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, _targetFovOrOrtho, step);
        else
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, _targetFovOrOrtho, step);
        ApplyLensMode(ref lens);
        browseCamera.Lens = lens;

        FeedToApartmentManager();

        bool posClose = (_currentPos - _targetPos).sqrMagnitude < 0.0001f;
        bool rotClose = Quaternion.Angle(_currentRot, _targetRot) < 0.05f;
        if (posClose && rotClose)
        {
            _currentPos = _targetPos;
            _currentRot = _targetRot;
            ApplyLens();
            FeedToApartmentManager();
            _isTransitioning = false;
        }
    }

    private void FeedToApartmentManager()
    {
        if (apartmentManager != null)
            apartmentManager.SetPresetBase(_currentPos, _currentRot, _targetFovOrOrtho);
    }

    private void ApplyLens()
    {
        if (browseCamera == null) return;
        var lens = browseCamera.Lens;
        if (_targetIsOrtho)
            lens.OrthographicSize = _targetFovOrOrtho;
        else
            lens.FieldOfView = _targetFovOrOrtho;
        ApplyLensMode(ref lens);
        browseCamera.Lens = lens;
    }

    private void ApplyLensMode(ref LensSettings lens)
    {
        lens.ModeOverride = _targetIsOrtho
            ? LensSettings.OverrideModes.Orthographic
            : LensSettings.OverrideModes.None;
    }

    private void SetBrainOrthoOverride(bool enable)
    {
        if (brain == null) return;
        var lmo = brain.LensModeOverride;
        lmo.Enabled = enable;
        brain.LensModeOverride = lmo;
    }

    private void UpdateButtonHighlights()
    {
        if (presetButtons == null) return;
        for (int i = 0; i < presetButtons.Length; i++)
        {
            if (presetButtons[i] == null) continue;
            var img = presetButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == _activePresetIndex) ? activeButtonColor : inactiveButtonColor;
        }
    }
}
