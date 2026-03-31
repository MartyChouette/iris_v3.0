using System.Collections.Generic;
using UnityEngine;
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

    [Tooltip("Which area index to start on (0=Kitchen, 1=Living Room, etc.).")]
    [SerializeField] private int _startAreaIndex = 1;

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

    [Header("Pan (MMB Drag)")]
    [Tooltip("Pan speed multiplier for middle-mouse drag.")]
    [SerializeField, Range(0.001f, 0.1f)] private float panSpeed = 0.01f;

    [Tooltip("Maximum pan distance from area center.")]
    [SerializeField] private float panMaxDistance = 3f;

    [Header("Zoom (Stepped)")]
    [Tooltip("Discrete zoom levels (FOV or ortho size). Index 0 = most zoomed out, last = most zoomed in.")]
    [SerializeField] private float[] _zoomSteps = { 100f, 80f, 60f, 45f, 30f };

    [Tooltip("Starting zoom step index (0-based).")]
    [SerializeField] private int _defaultZoomStep = 0;

    [Tooltip("Lerp speed for smooth zoom transitions.")]
    [SerializeField] private float _zoomLerpSpeed = 8f;

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

    [Header("Audio")]
    [Tooltip("SFX played when navigating between areas (A/D or click-arrows).")]
    [SerializeField] private AudioClip _areaTransitionSFX;

    [Header("UI")]
    [Tooltip("Panel showing the current area name during browsing.")]
    [SerializeField] private GameObject areaNamePanel;

    [Tooltip("TMP_Text for the area name.")]
    [SerializeField] private TMP_Text areaNameText;

    [Tooltip("Panel showing browse-mode control hints.")]
    [SerializeField] private GameObject browseHintsPanel;

    [Tooltip("Label on the left nav button showing the previous area name.")]
    [SerializeField] private TMP_Text _navLeftLabel;

    [Tooltip("Label on the right nav button showing the next area name.")]
    [SerializeField] private TMP_Text _navRightLabel;

    // ──────────────────────────────────────────────────────────────
    // Constants
    // ──────────────────────────────────────────────────────────────
    private const int PriorityActive = 20;
    private const int PriorityInactive = 0;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Browsing;
    public bool IsTransitioning => _isTransitioning;
    public int CurrentAreaIndex => _currentAreaIndex;

    /// <summary>Fired when the player switches apartment areas. Arg = new area index.</summary>
    public event System.Action<int> OnAreaChanged;

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

    // External camera lock (watering zoom etc.) — skips camera writes but keeps everything else running
    private bool _cameraLocked;
    public void LockCamera() => _cameraLocked = true;
    public void UnlockCamera() => _cameraLocked = false;

    // Preset override (CameraTestController feeds its target here for parallax)
    private bool _presetOverrideActive;

    // Persistent zoom level (written to lens each frame)
    private int _currentZoomStep = -1; // -1 = uninitialized
    private float _currentZoom = -1f; // smoothed zoom value for lerping
    private float _targetZoom = -1f;  // target zoom from step

    // Pan offset (MMB drag, reset on area change)
    private Vector3 _panOffset;

    // Hover highlight tracking
    private InteractableHighlight _hoveredHighlight;

    /// <summary>The InteractableHighlight currently under the cursor, or null.</summary>
    public InteractableHighlight HoveredHighlight => _hoveredHighlight;
    // _proximityHighlights removed — proximity highlight stage dropped
    private Camera _cachedMainCamera;

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

    }

    // Input managed by IrisInput singleton — no local enable/disable needed.

    private void Start()
    {
        GlobalCursorManager.EnsureExists();

        if (areas == null || areas.Length == 0)
        {
            Debug.LogError("[ApartmentManager] No areas assigned.");
            return;
        }

        PlaceableObject.SetWorldBounds(worldBounds);

        if (objectGrabber != null)
            objectGrabber.SetEnabled(true);

        _currentAreaIndex = Mathf.Clamp(_startAreaIndex, 0, areas.Length - 1);
        ApplyCameraImmediate(areas[_currentAreaIndex]);

        // Restore smooth blend after the initial hard cut
        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);
        }

        ResetZoom();
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

        if (active)
        {
            // Re-apply default preset lens (including ortho mode) when browse camera is raised
            ReapplyDefaultLens();
        }
        else
        {
            // Clear brain ortho override so the read camera can render in perspective
            ApplyBrainOrthoMode(false);
        }
    }

    /// <summary>
    /// Re-applies the default preset lens settings for the current area.
    /// Restores orthographic mode after the read camera (perspective) phase.
    /// </summary>
    public void ReapplyDefaultLens()
    {
        if (browseCamera == null || defaultPreset == null) return;
        if (areas == null || areas.Length == 0) return;

        GetCameraValues(_currentAreaIndex, out _, out _, out _, out LensSettings? presetLens);
        if (presetLens.HasValue)
        {
            var pl = presetLens.Value;
            pl.NearClipPlane = -9f;
            browseCamera.Lens = pl;
            ApplyBrainOrthoMode(pl.ModeOverride == LensSettings.OverrideModes.Orthographic);

            // Reset zoom to default step
            ResetZoom();
        }
    }

    private void Update()
    {
        if (DayPhaseManager.Instance != null && !DayPhaseManager.Instance.IsInteractionPhase)
            return;

        UpdateTransition();
        HandleBrowsingInput();
        HandleZoomInput();
        HandlePanInput();
        ApplyParallax();
        UpdateHoverHighlight();
    }

    // ──────────────────────────────────────────────────────────────
    // Camera Transition (pos/rot/FOV lerp)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads camera position/rotation/lens for an area index.
    /// Prefers defaultPreset SO; falls back to ApartmentAreaDefinition.
    /// </summary>
    private void GetCameraValues(int areaIndex, out Vector3 pos, out Quaternion rot, out float fov)
    {
        GetCameraValues(areaIndex, out pos, out rot, out fov, out _);
    }

    private void GetCameraValues(int areaIndex, out Vector3 pos, out Quaternion rot, out float fov, out LensSettings? lens)
    {
        lens = null;

        if (defaultPreset != null &&
            defaultPreset.areaConfigs != null &&
            areaIndex < defaultPreset.areaConfigs.Length)
        {
            var cfg = defaultPreset.areaConfigs[areaIndex];
            pos = cfg.position;
            rot = Quaternion.Euler(cfg.rotation);
            lens = cfg.lens;
            bool isOrtho = cfg.lens.ModeOverride == LensSettings.OverrideModes.Orthographic;
            fov = isOrtho ? cfg.lens.OrthographicSize : cfg.lens.FieldOfView;
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

        GetCameraValues(_currentAreaIndex, out _basePosition, out _baseRotation, out _baseFOV, out LensSettings? presetLens);

        _targetPosition = _basePosition;
        _targetRotation = _baseRotation;
        _targetFOV = _baseFOV;
        _isTransitioning = false;
        _currentParallaxOffset = Vector3.zero;

        // Write to transform
        var t = browseCamera.transform;
        t.position = _basePosition;
        t.rotation = _baseRotation;

        // Apply full lens from preset (includes ortho mode, near/far, etc.)
        if (presetLens.HasValue)
        {
            var pl = presetLens.Value;
            pl.NearClipPlane = -9f;
            browseCamera.Lens = pl;
            ApplyBrainOrthoMode(pl.ModeOverride == LensSettings.OverrideModes.Orthographic);
        }
        else
        {
            var lens = browseCamera.Lens;
            lens.FieldOfView = _baseFOV;
            lens.NearClipPlane = -9f;
            browseCamera.Lens = lens;
        }

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
        if (IrisInput.Instance == null) return;
        if (IrisInput.Instance.NavigateLeft.WasPressedThisFrame())
            CycleArea(-1);
        else if (IrisInput.Instance.NavigateRight.WasPressedThisFrame())
            CycleArea(1);
    }

    private void ResetZoom()
    {
        if (_zoomSteps == null || _zoomSteps.Length == 0) return;
        _currentZoomStep = Mathf.Clamp(_defaultZoomStep, 0, _zoomSteps.Length - 1);
        _targetZoom = _zoomSteps[_currentZoomStep];
        _currentZoom = _targetZoom;
    }

    private void HandleZoomInput()
    {
        if (browseCamera == null) return;
        if (_zoomSteps == null || _zoomSteps.Length == 0) return;

        // Scroll always zooms — rotation moved to R key

        // Initialize zoom step on first use
        if (_currentZoomStep < 0)
        {
            _currentZoomStep = Mathf.Clamp(_defaultZoomStep, 0, _zoomSteps.Length - 1);
            _targetZoom = _zoomSteps[_currentZoomStep];
            _currentZoom = _targetZoom;
        }

        float scroll = IrisInput.Instance != null ? IrisInput.Instance.Scroll.ReadValue<float>() : 0f;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Scroll up = zoom in. Respect invert scroll setting.
            int raw = scroll > 0f ? -1 : 1;
            int direction = AccessibilitySettings.InvertScroll ? -raw : raw;
            int newStep = Mathf.Clamp(_currentZoomStep + direction, 0, _zoomSteps.Length - 1);
            if (newStep != _currentZoomStep)
            {
                float oldZoom = _targetZoom > 0f ? _targetZoom : _zoomSteps[_currentZoomStep];
                _currentZoomStep = newStep;
                _targetZoom = _zoomSteps[_currentZoomStep];

                // Bias pan toward cursor so zoom centers on what you're pointing at
                BiasZoomTowardCursor(oldZoom, _targetZoom);
            }
        }

        // Smooth lerp toward target
        if (_targetZoom > 0f)
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.unscaledDeltaTime * _zoomLerpSpeed);
    }

    /// <summary>
    /// When zooming in, shift the pan offset toward the cursor position so the
    /// zoom feels centered on what the player is pointing at.
    /// </summary>
    private void BiasZoomTowardCursor(float oldZoom, float newZoom)
    {
        if (browseCamera == null || oldZoom <= 0f || newZoom <= 0f) return;

        // How much of the view is changing (positive = zooming in)
        float zoomRatio = 1f - (newZoom / oldZoom);
        if (Mathf.Abs(zoomRatio) < 0.001f) return;

        // Cursor position in normalized screen space (-1 to 1)
        Vector2 mousePos = IrisInput.CursorPosition;
        float nx = (mousePos.x / Screen.width - 0.5f) * 2f;
        float ny = (mousePos.y / Screen.height - 0.5f) * 2f;

        // Offset in camera-local space toward the cursor
        Vector3 right = _baseRotation * Vector3.right;
        Vector3 up = _baseRotation * Vector3.up;

        // Scale by current zoom size so the offset matches what the player sees
        float worldScale = browseCamera.Lens.ModeOverride == Unity.Cinemachine.LensSettings.OverrideModes.Orthographic
            ? oldZoom  // ortho: size = half-height in world units
            : oldZoom * 0.02f; // perspective: approximate world scale from FOV

        _panOffset += (right * nx + up * ny) * worldScale * zoomRatio;
    }

    private void HandlePanInput()
    {
        if (IrisInput.Instance == null || !IrisInput.Instance.PanButton.IsPressed()) return;

        Vector2 delta = IrisInput.Instance.PanDelta.ReadValue<Vector2>();
        if (delta.sqrMagnitude < 0.01f) return;

        // Move along camera-local right/up axes
        Vector3 right = _baseRotation * Vector3.right;
        Vector3 up = _baseRotation * Vector3.up;
        _panOffset -= (right * delta.x + up * delta.y) * panSpeed;

        // Scale pan limit with zoom — zoomed in = more pan range to reach edges
        float zoomFactor = 1f;
        if (_zoomSteps != null && _zoomSteps.Length > 0 && _currentZoom > 0f)
        {
            float baseZoom = _zoomSteps[0]; // most zoomed out
            zoomFactor = baseZoom / Mathf.Max(_currentZoom, 1f);
        }
        float effectiveMaxPan = panMaxDistance * Mathf.Max(zoomFactor, 1f);

        if (_panOffset.magnitude > effectiveMaxPan)
            _panOffset = _panOffset.normalized * effectiveMaxPan;
    }

    private void CycleArea(int direction)
    {
        if (areas == null || areas.Length == 0) return;

        _currentAreaIndex = (_currentAreaIndex + direction + areas.Length) % areas.Length;
        var area = areas[_currentAreaIndex];
        _panOffset = Vector3.zero;

        AudioManager.Instance?.PlaySFX(_areaTransitionSFX);

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
        OnAreaChanged?.Invoke(_currentAreaIndex);
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

        // Update nav button labels with destination area names
        if (areas != null && areas.Length > 1)
        {
            int leftIdx = (_currentAreaIndex - 1 + areas.Length) % areas.Length;
            int rightIdx = (_currentAreaIndex + 1) % areas.Length;

            if (_navLeftLabel != null)
                _navLeftLabel.text = areas[leftIdx].areaName;
            if (_navRightLabel != null)
                _navRightLabel.text = areas[rightIdx].areaName;
        }
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
        if (_cameraLocked) return; // external system controlling camera

        // Always write base + offset to transform, even with parallax disabled
        var t = browseCamera.transform;

        if (parallaxMaxOffset > 0f && !AccessibilitySettings.ReduceMotion)
        {
            Vector2 mousePos = IrisInput.CursorPosition;
            float nx = (mousePos.x / Screen.width - 0.5f) * 2f;
            float ny = (mousePos.y / Screen.height - 0.5f) * 2f;
            nx = Mathf.Clamp(nx, -1f, 1f);
            ny = Mathf.Clamp(ny, -1f, 1f);

            // Use base rotation for offset direction (never reads from transform)
            Vector3 right = _baseRotation * Vector3.right;
            Vector3 up = _baseRotation * Vector3.up;
            float effectiveOffset = parallaxMaxOffset * AccessibilitySettings.ScreenShakeScale;
            Vector3 targetOffset = (right * -nx + up * -ny) * effectiveOffset;

            _currentParallaxOffset = Vector3.Lerp(_currentParallaxOffset, targetOffset,
                Time.deltaTime * parallaxSmoothing);
        }
        else if (AccessibilitySettings.ReduceMotion)
        {
            _currentParallaxOffset = Vector3.Lerp(_currentParallaxOffset, Vector3.zero,
                Time.deltaTime * parallaxSmoothing);
        }

        // Write final position = base + parallax + pan
        t.position = _basePosition + _currentParallaxOffset + _panOffset;
        t.rotation = _baseRotation;

        // Write lens — preset or area default, then layer zoom on top
        {
            var lens = browseCamera.Lens;
            bool isOrtho = lens.ModeOverride == LensSettings.OverrideModes.Orthographic;

            // Base lens: preset owns it when active, otherwise use area FOV
            if (!_presetOverrideActive)
                lens.FieldOfView = _baseFOV;

            // Layer zoom on top (works with both preset and default lens)
            if (_currentZoom >= 0f)
            {
                if (isOrtho)
                    lens.OrthographicSize = _currentZoom;
                else
                    lens.FieldOfView = _currentZoom;
            }

            lens.NearClipPlane = -9f;
            browseCamera.Lens = lens;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Brain Ortho Override
    // ──────────────────────────────────────────────────────────────

    private void ApplyBrainOrthoMode(bool ortho)
    {
        if (brain == null) return;
        var lmo = brain.LensModeOverride;
        lmo.Enabled = ortho;
        if (ortho)
            lmo.DefaultMode = LensSettings.OverrideModes.Orthographic;
        brain.LensModeOverride = lmo;

        var cam = brain.GetComponent<UnityEngine.Camera>();
        if (cam != null)
            cam.orthographic = ortho;
    }

    // ──────────────────────────────────────────────────────────────
    // Hover Highlight
    // ──────────────────────────────────────────────────────────────

    [Tooltip("Angular radius in degrees for hover detection. Scales naturally with distance.")]
    private const float HoverAngle = 1.5f;

    /// <summary>
    /// Angular distance (degrees) between the ray direction and the direction to the point.
    /// Scales naturally with distance — far objects are just as easy to hover as near ones.
    /// </summary>
    private static float AngleToRay(Ray ray, Vector3 point)
    {
        Vector3 toPoint = point - ray.origin;
        if (toPoint.sqrMagnitude < 0.0001f) return 0f;
        return Vector3.Angle(ray.direction, toPoint);
    }

    private void UpdateHoverHighlight()
    {
        if (_cachedMainCamera == null) _cachedMainCamera = UnityEngine.Camera.main;
        var cam = _cachedMainCamera;
        if (cam == null) return;

        Vector2 mousePos = IrisInput.CursorPosition;
        Ray ray = cam.ScreenPointToRay(mousePos);

        // ── Find nearest InteractableHighlight to the cursor ray ──
        // Scans all registered highlights (placeables, plants, etc.).
        InteractableHighlight hit = null;
        {
            float bestAngle = HoverAngle;
            var allHighlights = InteractableHighlight.All;
            for (int i = 0; i < allHighlights.Count; i++)
            {
                var hl = allHighlights[i];
                if (hl == null) continue;

                float angle = AngleToRay(ray, hl.transform.position);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    hit = hl;
                }
            }
        }

        // ── Direct hover (strongest highlight) ──
        if (hit != _hoveredHighlight)
        {
            if (_hoveredHighlight != null)
                _hoveredHighlight.SetHighlighted(false);

            _hoveredHighlight = hit;

            if (_hoveredHighlight != null)
                _hoveredHighlight.SetHighlighted(true);
        }
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
