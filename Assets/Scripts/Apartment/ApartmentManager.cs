using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using TMPro;
using Iris.Apartment;

public class ApartmentManager : MonoBehaviour
{
    public enum State { Browsing }

    public static ApartmentManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Areas")]
    [Tooltip("Ordered list of apartment areas. A/D cycles through these.")]
    [SerializeField] private ApartmentAreaDefinition[] areas;

    [Header("Cameras")]
    [Tooltip("Cinemachine camera used for browsing. Controlled directly by ApartmentManager.")]
    [SerializeField] private CinemachineCamera browseCamera;

    [Tooltip("CinemachineBrain on the main camera. Auto-found if null.")]
    [SerializeField] private CinemachineBrain brain;

    [Header("Transition")]
    [Tooltip("How fast the camera lerps between area positions (higher = faster).")]
    [SerializeField, Range(1f, 15f)] private float transitionSpeed = 5f;

    [Header("Cursor Parallax")]
    [Tooltip("Maximum camera shift distance toward cursor.")]
    [SerializeField, Range(0f, 0.5f)] private float parallaxMaxOffset = 0.05f;

    [Tooltip("Smoothing speed for parallax follow.")]
    [SerializeField, Range(1f, 20f)] private float parallaxSmoothing = 8f;

    [Header("World Bounds")]
    [Tooltip("Objects outside this box are recovered to the nearest surface.")]
    [SerializeField] private Bounds worldBounds = new Bounds(new Vector3(-3f, 1.5f, 0f), new Vector3(16f, 8f, 20f));

    [Header("Interaction")]
    [Tooltip("ObjectGrabber to enable/disable based on state.")]
    [SerializeField] private ObjectGrabber objectGrabber;

    [Header("Camera Test")]
    [Tooltip("Optional camera preset test controller for A/B/C comparison.")]
    [SerializeField] private CameraTestController cameraTestController;

    [Header("Default Preset")]
    [Tooltip("Camera preset used as the default browse angles (e.g. V1). If set, overrides ApartmentAreaDefinition camera values.")]
    [SerializeField] private CameraPresetDefinition defaultPreset;

    [Header("UI")]
    [Tooltip("Panel showing the current area name during browsing.")]
    [SerializeField] private GameObject areaNamePanel;

    [Tooltip("TMP_Text for the area name.")]
    [SerializeField] private TMP_Text areaNameText;

    [Tooltip("Panel showing browse-mode control hints.")]
    [SerializeField] private GameObject browseHintsPanel;

    // ──────────────────────────────────────────────────────────────
    // Constants
    // ──────────────────────────────────────────────────────────────
    private const int PriorityActive = 20;
    private const int PriorityInactive = 0;

    // ──────────────────────────────────────────────────────────────
    // Inline InputActions
    // ──────────────────────────────────────────────────────────────
    private InputAction _navigateLeftAction;
    private InputAction _navigateRightAction;
    private InputAction _mousePositionAction;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Browsing;
    public bool IsTransitioning => _isTransitioning;
    public int CurrentAreaIndex => _currentAreaIndex;

    private int _currentAreaIndex;

    // Base camera state (parallax-free, lerped between areas)
    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private float _baseFOV;

    // Transition targets
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private float _targetFOV;
    private bool _isTransitioning;

    // Parallax offset (applied on top of base, never fed back)
    private Vector3 _currentParallaxOffset;

    // Browse camera suppression (DayPhaseManager lowers during Morning)
    private bool _browseSuppressed;

    // Preset override (CameraTestController feeds its target here for parallax)
    private bool _presetOverrideActive;

    // Hover highlight tracking
    private InteractableHighlight _hoveredHighlight;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ApartmentManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (brain == null)
            brain = FindAnyObjectByType<CinemachineBrain>();

        if (brain == null)
            Debug.LogError("[ApartmentManager] No CinemachineBrain found in scene.");

        // Inline InputActions
        _navigateLeftAction = new InputAction("NavLeft", InputActionType.Button);
        _navigateLeftAction.AddBinding("<Keyboard>/a");
        _navigateLeftAction.AddBinding("<Keyboard>/leftArrow");

        _navigateRightAction = new InputAction("NavRight", InputActionType.Button);
        _navigateRightAction.AddBinding("<Keyboard>/d");
        _navigateRightAction.AddBinding("<Keyboard>/rightArrow");

        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value,
            "<Mouse>/position");
    }

    private void OnEnable()
    {
        _navigateLeftAction.Enable();
        _navigateRightAction.Enable();
        _mousePositionAction.Enable();
    }

    private void OnDisable()
    {
        _navigateLeftAction.Disable();
        _navigateRightAction.Disable();
        _mousePositionAction.Disable();
    }

    private void Start()
    {
        if (areas == null || areas.Length == 0)
        {
            Debug.LogError("[ApartmentManager] No areas assigned.");
            return;
        }

        PlaceableObject.SetWorldBounds(worldBounds);

        if (objectGrabber != null)
            objectGrabber.SetEnabled(true);

        _currentAreaIndex = 0;
        ApplyCameraImmediate(areas[0]);

        // Restore smooth blend after the initial hard cut
        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Called by DayPhaseManager to raise or lower the browse camera.
    /// During Morning phase the browse camera should be suppressed so
    /// the read camera wins without a competing priority-20 camera.
    /// </summary>
    public void SetBrowseCameraActive(bool active)
    {
        _browseSuppressed = !active;
        if (browseCamera != null)
            browseCamera.Priority = active ? PriorityActive : PriorityInactive;
    }

    private void Update()
    {
        if (DayPhaseManager.Instance != null && !DayPhaseManager.Instance.IsInteractionPhase)
            return;

        UpdateTransition();
        HandleBrowsingInput();
        ApplyParallax();
        UpdateHoverHighlight();
    }

    // ──────────────────────────────────────────────────────────────
    // Camera Transition (pos/rot/FOV lerp)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads camera position/rotation/FOV for an area index.
    /// Prefers defaultPreset SO; falls back to ApartmentAreaDefinition.
    /// </summary>
    private void GetCameraValues(int areaIndex, out Vector3 pos, out Quaternion rot, out float fov)
    {
        if (defaultPreset != null &&
            defaultPreset.areaConfigs != null &&
            areaIndex < defaultPreset.areaConfigs.Length)
        {
            var cfg = defaultPreset.areaConfigs[areaIndex];
            pos = cfg.position;
            rot = Quaternion.Euler(cfg.rotation);
            fov = cfg.lens.FieldOfView;
            return;
        }

        // Fallback to area definition
        var area = areas[areaIndex];
        pos = area.cameraPosition;
        rot = Quaternion.Euler(area.cameraRotation);
        fov = area.cameraFOV;
    }

    private void ApplyCameraImmediate(ApartmentAreaDefinition area)
    {
        if (browseCamera == null) return;

        GetCameraValues(_currentAreaIndex, out _basePosition, out _baseRotation, out _baseFOV);

        _targetPosition = _basePosition;
        _targetRotation = _baseRotation;
        _targetFOV = _baseFOV;
        _isTransitioning = false;
        _currentParallaxOffset = Vector3.zero;

        // Write to transform
        var t = browseCamera.transform;
        t.position = _basePosition;
        t.rotation = _baseRotation;

        var lens = browseCamera.Lens;
        lens.FieldOfView = _baseFOV;
        browseCamera.Lens = lens;

        if (!_browseSuppressed)
            browseCamera.Priority = PriorityActive;

        // Hard cut for initial position
        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut, 0f);
        }
    }

    private void UpdateTransition()
    {
        if (cameraTestController != null && cameraTestController.ActivePresetIndex >= 0) return;
        if (!_isTransitioning) return;
        if (browseCamera == null) return;

        float step = transitionSpeed * Time.deltaTime;

        _basePosition = Vector3.Lerp(_basePosition, _targetPosition, step);
        _baseRotation = Quaternion.Slerp(_baseRotation, _targetRotation, step);
        _baseFOV = Mathf.Lerp(_baseFOV, _targetFOV, step);

        // Check if close enough to stop
        bool posClose = (_basePosition - _targetPosition).sqrMagnitude < 0.0001f;
        bool rotClose = Quaternion.Angle(_baseRotation, _targetRotation) < 0.05f;
        bool fovClose = Mathf.Abs(_baseFOV - _targetFOV) < 0.05f;

        if (posClose && rotClose && fovClose)
        {
            _basePosition = _targetPosition;
            _baseRotation = _targetRotation;
            _baseFOV = _targetFOV;
            _isTransitioning = false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Browsing
    // ──────────────────────────────────────────────────────────────

    private void HandleBrowsingInput()
    {
        if (_navigateLeftAction.WasPressedThisFrame())
            CycleArea(-1);
        else if (_navigateRightAction.WasPressedThisFrame())
            CycleArea(1);
    }

    private void CycleArea(int direction)
    {
        if (areas == null || areas.Length == 0) return;

        _currentAreaIndex = (_currentAreaIndex + direction + areas.Length) % areas.Length;
        var area = areas[_currentAreaIndex];

        if (cameraTestController != null && cameraTestController.ActivePresetIndex >= 0)
        {
            cameraTestController.OnAreaChanged(_currentAreaIndex);
            if (!_browseSuppressed)
                browseCamera.Priority = PriorityActive;
            UpdateUI();
            Debug.Log($"[ApartmentManager] Browsing: {area.areaName}");
            return;
        }

        GetCameraValues(_currentAreaIndex, out _targetPosition, out _targetRotation, out _targetFOV);
        _isTransitioning = true;

        if (!_browseSuppressed)
            browseCamera.Priority = PriorityActive;

        UpdateUI();
        Debug.Log($"[ApartmentManager] Browsing: {area.areaName}");
    }

    /// <summary>Navigate to the previous area. Called by UI button.</summary>
    public void NavigateLeft() => CycleArea(-1);

    /// <summary>Navigate to the next area. Called by UI button.</summary>
    public void NavigateRight() => CycleArea(1);

    // ──────────────────────────────────────────────────────────────
    // UI
    // ──────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (areaNamePanel != null)
            areaNamePanel.SetActive(true);

        if (areaNameText != null && areas != null && areas.Length > 0)
            areaNameText.text = areas[_currentAreaIndex].areaName;

        if (browseHintsPanel != null)
            browseHintsPanel.SetActive(true);
    }

    // ──────────────────────────────────────────────────────────────
    // Preset Override (CameraTestController feeds values here)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by CameraTestController to set the parallax base from a preset.
    /// ApartmentManager applies mouse parallax on top of these values.
    /// </summary>
    public void SetPresetBase(Vector3 pos, Quaternion rot, float fov)
    {
        _basePosition = pos;
        _baseRotation = rot;
        _baseFOV = fov;
        _presetOverrideActive = true;
    }

    /// <summary>Called by CameraTestController when preset is cleared.</summary>
    public void ClearPresetBase()
    {
        _presetOverrideActive = false;
        // Snap back to current area (reads from defaultPreset if set)
        if (areas != null && areas.Length > 0)
        {
            GetCameraValues(_currentAreaIndex, out _basePosition, out _baseRotation, out _baseFOV);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Cursor Parallax (offset on top of base — never feeds back)
    // ──────────────────────────────────────────────────────────────

    private void ApplyParallax()
    {
        if (browseCamera == null) return;

        // Always write base + offset to transform, even with parallax disabled
        var t = browseCamera.transform;

        if (parallaxMaxOffset > 0f)
        {
            Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();
            float nx = (mousePos.x / Screen.width - 0.5f) * 2f;
            float ny = (mousePos.y / Screen.height - 0.5f) * 2f;
            nx = Mathf.Clamp(nx, -1f, 1f);
            ny = Mathf.Clamp(ny, -1f, 1f);

            // Use base rotation for offset direction (never reads from transform)
            Vector3 right = _baseRotation * Vector3.right;
            Vector3 up = _baseRotation * Vector3.up;
            Vector3 targetOffset = (right * -nx + up * -ny) * parallaxMaxOffset;

            _currentParallaxOffset = Vector3.Lerp(_currentParallaxOffset, targetOffset,
                Time.deltaTime * parallaxSmoothing);
        }

        // Write final position = base + parallax offset
        t.position = _basePosition + _currentParallaxOffset;
        t.rotation = _baseRotation;

        // Skip lens override when preset is active (CameraTestController owns the lens)
        if (!_presetOverrideActive)
        {
            var lens = browseCamera.Lens;
            lens.FieldOfView = _baseFOV;
            browseCamera.Lens = lens;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Hover Highlight
    // ──────────────────────────────────────────────────────────────

    private void UpdateHoverHighlight()
    {
        var cam = UnityEngine.Camera.main;
        if (cam == null) return;

        Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(mousePos);

        InteractableHighlight hit = null;
        if (Physics.Raycast(ray, out RaycastHit hitInfo, 50f))
        {
            hit = hitInfo.collider.GetComponentInParent<InteractableHighlight>();
        }

        if (hit == _hoveredHighlight) return;

        if (_hoveredHighlight != null)
            _hoveredHighlight.SetHighlighted(false);

        _hoveredHighlight = hit;

        if (_hoveredHighlight != null)
            _hoveredHighlight.SetHighlighted(true);
    }

    // ──────────────────────────────────────────────────────────────
    // Scene View Gizmos
    // ──────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (areas == null || areas.Length == 0) return;

        // World bounds box
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);

        for (int i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area == null) continue;

            Vector3 pos = area.cameraPosition;
            Quaternion rot = Quaternion.Euler(area.cameraRotation);

            // Sphere at camera position
            bool isCurrent = Application.isPlaying && i == _currentAreaIndex;
            Gizmos.color = isCurrent ? Color.yellow : Color.cyan;
            Gizmos.DrawSphere(pos, 0.15f);

            // Forward direction ray
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawRay(pos, rot * Vector3.forward * 1.5f);

            // Label
            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                normal = { textColor = isCurrent ? Color.yellow : Color.white }
            };
            UnityEditor.Handles.Label(pos + Vector3.up * 0.3f, area.areaName, style);
        }
    }
#endif
}
