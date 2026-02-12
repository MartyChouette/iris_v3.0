using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Unity.Cinemachine;
using Unity.Mathematics;
using TMPro;

public class ApartmentManager : MonoBehaviour
{
    public enum State { Browsing, Selecting, Selected, InStation }

    public static ApartmentManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Areas")]
    [Tooltip("Ordered list of apartment areas. A/D cycles through these.")]
    [SerializeField] private ApartmentAreaDefinition[] areas;

    [Header("Cameras")]
    [Tooltip("Cinemachine camera used for browse vantage (should have CinemachineSplineDolly).")]
    [SerializeField] private CinemachineCamera browseCamera;

    [Tooltip("Cinemachine camera used for selected/interaction view.")]
    [SerializeField] private CinemachineCamera selectedCamera;

    [Tooltip("CinemachineBrain on the main camera. Auto-found if null.")]
    [SerializeField] private CinemachineBrain brain;

    [Header("Spline Dolly")]
    [Tooltip("CinemachineSplineDolly on the browse camera. Auto-found if null.")]
    [SerializeField] private CinemachineSplineDolly browseDolly;

    [Tooltip("Speed of spline-t animation (units per second in normalized space).")]
    [SerializeField, Range(0.1f, 3f)] private float dollyAnimSpeed = 0.8f;

    [Header("Look-At")]
    [Tooltip("How fast the camera rotates to face the current area's look-at point.")]
    [SerializeField, Range(1f, 20f)] private float lookAtSmoothing = 5f;

    [Header("Cursor Parallax")]
    [Tooltip("Maximum camera shift distance toward cursor.")]
    [SerializeField, Range(0f, 2f)] private float parallaxMaxOffset = 0.3f;

    [Tooltip("Smoothing speed for parallax follow.")]
    [SerializeField, Range(1f, 20f)] private float parallaxSmoothing = 8f;

    [Header("Interaction")]
    [Tooltip("ObjectGrabber to enable/disable based on state.")]
    [SerializeField] private ObjectGrabber objectGrabber;

    [Header("UI")]
    [Tooltip("Panel showing the current area name during browsing.")]
    [SerializeField] private GameObject areaNamePanel;

    [Tooltip("TMP_Text for the area name.")]
    [SerializeField] private TMP_Text areaNameText;

    [Tooltip("Panel showing browse-mode control hints.")]
    [SerializeField] private GameObject browseHintsPanel;

    [Tooltip("Panel showing selected-mode control hints.")]
    [SerializeField] private GameObject selectedHintsPanel;

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
    private InputAction _selectAction;
    private InputAction _cancelAction;
    private InputAction _mousePositionAction;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Browsing;

    private int _currentAreaIndex;
    private float _blendTimer;
    private float _blendDuration;
    private Vector3 _basePosition;
    private Vector3 _currentParallaxOffset;
    private Vector3 _currentLookAt;

    // Spline dolly animation
    private float _currentSplineT;
    private float _targetSplineT;
    private bool _isDollyAnimating;

    // Browse camera suppression (DayPhaseManager lowers during Morning)
    private bool _browseSuppressed;

    // Station lookup
    private Dictionary<StationType, StationRoot> _stationLookup;
    private StationRoot _activeStation;

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

        if (browseDolly == null && browseCamera != null)
            browseDolly = browseCamera.GetComponent<CinemachineSplineDolly>();

        // Inline InputActions
        _navigateLeftAction = new InputAction("NavLeft", InputActionType.Button);
        _navigateLeftAction.AddBinding("<Keyboard>/a");
        _navigateLeftAction.AddBinding("<Keyboard>/leftArrow");

        _navigateRightAction = new InputAction("NavRight", InputActionType.Button);
        _navigateRightAction.AddBinding("<Keyboard>/d");
        _navigateRightAction.AddBinding("<Keyboard>/rightArrow");

        _selectAction = new InputAction("Select", InputActionType.Button);
        _selectAction.AddBinding("<Keyboard>/enter");
        _selectAction.AddBinding("<Keyboard>/space");

        _cancelAction = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");

        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value,
            "<Mouse>/position");
    }

    private void OnEnable()
    {
        _navigateLeftAction.Enable();
        _navigateRightAction.Enable();
        _selectAction.Enable();
        _cancelAction.Enable();
        _mousePositionAction.Enable();
    }

    private void OnDisable()
    {
        _navigateLeftAction.Disable();
        _navigateRightAction.Disable();
        _selectAction.Disable();
        _cancelAction.Disable();
        _mousePositionAction.Disable();
    }

    private void Start()
    {
        if (areas == null || areas.Length == 0)
        {
            Debug.LogError("[ApartmentManager] No areas assigned.");
            return;
        }

        // Build station lookup from all StationRoot components in scene
        _stationLookup = new Dictionary<StationType, StationRoot>();
        var stations = FindObjectsByType<StationRoot>(FindObjectsSortMode.None);
        foreach (var station in stations)
        {
            if (station.Type != StationType.None && !_stationLookup.ContainsKey(station.Type))
            {
                _stationLookup[station.Type] = station;
                station.Deactivate(); // Ensure all start deactivated
            }
        }

        if (objectGrabber != null)
            objectGrabber.SetEnabled(false);

        _currentAreaIndex = 0;
        _currentSplineT = areas[0].splinePosition;
        _targetSplineT = _currentSplineT;
        _currentLookAt = areas[0].lookAtPosition;
        ApplyDollyPosition(_currentSplineT, hardCut: true);
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
    /// Also prevents ApplyDollyPosition from re-raising the browse camera.
    /// </summary>
    public void SetBrowseCameraActive(bool active)
    {
        _browseSuppressed = !active;
        if (browseCamera != null)
            browseCamera.Priority = active ? PriorityActive : PriorityInactive;
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case State.Browsing:
                UpdateDollyAnimation();
                HandleBrowsingInput();
                break;

            case State.Selecting:
                _blendTimer -= Time.deltaTime;
                if (_blendTimer <= 0f)
                    EnterSelected();
                break;

            case State.Selected:
                HandleSelectedInput();
                break;

            case State.InStation:
                HandleInStationInput();
                break;
        }

        ApplyParallax();
    }

    // ──────────────────────────────────────────────────────────────
    // Spline Dolly Animation
    // ──────────────────────────────────────────────────────────────

    private void ApplyDollyPosition(float t, bool hardCut)
    {
        if (browseDolly != null)
        {
            browseDolly.CameraPosition = t;
        }

        if (brain != null)
        {
            if (hardCut)
            {
                brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.Cut, 0f);
            }
        }

        if (!_browseSuppressed)
            browseCamera.Priority = PriorityActive;
        if (selectedCamera != null)
            selectedCamera.Priority = PriorityInactive;
    }

    private void UpdateDollyAnimation()
    {
        if (!_isDollyAnimating) return;

        float delta = ShortestSplineDelta(_currentSplineT, _targetSplineT);
        if (Mathf.Abs(delta) < 0.001f)
        {
            _currentSplineT = _targetSplineT;
            _isDollyAnimating = false;
        }
        else
        {
            float step = dollyAnimSpeed * Time.deltaTime;
            if (Mathf.Abs(delta) <= step)
                _currentSplineT = _targetSplineT;
            else
                _currentSplineT += Mathf.Sign(delta) * step;

            // Wrap to [0, 1)
            _currentSplineT = (_currentSplineT % 1f + 1f) % 1f;
        }

        if (browseDolly != null)
            browseDolly.CameraPosition = _currentSplineT;
    }

    /// <summary>
    /// Shortest signed delta on a closed [0,1) loop from 'from' to 'to'.
    /// </summary>
    private static float ShortestSplineDelta(float from, float to)
    {
        float delta = to - from;
        if (delta > 0.5f) delta -= 1f;
        else if (delta < -0.5f) delta += 1f;
        return delta;
    }

    // ──────────────────────────────────────────────────────────────
    // Browsing
    // ──────────────────────────────────────────────────────────────

    private void HandleBrowsingInput()
    {
        // During Morning phase, block navigation — newspaper is forced
        if (DayPhaseManager.Instance != null
            && DayPhaseManager.Instance.CurrentPhase == DayPhaseManager.DayPhase.Morning)
            return;

        if (_navigateLeftAction.WasPressedThisFrame())
            CycleArea(-1);
        else if (_navigateRightAction.WasPressedThisFrame())
            CycleArea(1);

        if (_selectAction.WasPressedThisFrame() && !_isDollyAnimating)
            BeginSelecting();
    }

    private void CycleArea(int direction)
    {
        if (areas == null || areas.Length == 0) return;

        _currentAreaIndex = (_currentAreaIndex + direction + areas.Length) % areas.Length;
        var area = areas[_currentAreaIndex];

        _targetSplineT = area.splinePosition;
        _isDollyAnimating = true;

        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, area.browseBlendDuration);
        }

        UpdateUI();
        Debug.Log($"[ApartmentManager] Browsing: {area.areaName}");
    }

    // ──────────────────────────────────────────────────────────────
    // Selecting (transition state)
    // ──────────────────────────────────────────────────────────────

    private void BeginSelecting()
    {
        if (areas == null || areas.Length == 0) return;

        var area = areas[_currentAreaIndex];

        // If the station has its own cameras, skip the selected camera and go straight in
        if (area.stationType != StationType.None
            && _stationLookup != null
            && _stationLookup.TryGetValue(area.stationType, out var station)
            && station.HasStationCameras)
        {
            // Check phase gating before entering
            if (!station.IsAvailableInCurrentPhase())
            {
                Debug.Log($"[ApartmentManager] Station {area.stationType} not available in current phase.");
                return;
            }
            CurrentState = State.InStation;
            _activeStation = station;

            browseCamera.Priority = PriorityInactive;
            station.Activate();

            if (objectGrabber != null)
                objectGrabber.SetEnabled(false);

            UpdateUI();
            Debug.Log($"[ApartmentManager] Direct to station: {area.stationType}");
            return;
        }

        CurrentState = State.Selecting;

        if (selectedCamera != null)
        {
            selectedCamera.transform.position = area.selectedPosition;
            selectedCamera.transform.rotation = Quaternion.Euler(area.selectedRotation);
            var selectedLens = selectedCamera.Lens;
            selectedLens.FieldOfView = area.selectedFOV;
            selectedCamera.Lens = selectedLens;
        }

        _basePosition = area.selectedPosition;
        _currentParallaxOffset = Vector3.zero;

        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, area.selectBlendDuration);
        }

        browseCamera.Priority = PriorityInactive;
        if (selectedCamera != null)
            selectedCamera.Priority = PriorityActive;

        _blendDuration = area.selectBlendDuration;
        _blendTimer = _blendDuration;

        UpdateUI();
        Debug.Log($"[ApartmentManager] Selecting: {area.areaName}");
    }

    // ──────────────────────────────────────────────────────────────
    // Selected
    // ──────────────────────────────────────────────────────────────

    private void EnterSelected()
    {
        var area = areas[_currentAreaIndex];

        // If this area has a station, enter it immediately
        if (area.stationType != StationType.None
            && _stationLookup != null
            && _stationLookup.TryGetValue(area.stationType, out var station))
        {
            // Check phase gating before entering
            if (!station.IsAvailableInCurrentPhase())
            {
                // No station available — enter standard Selected state
                CurrentState = State.Selected;
                if (objectGrabber != null)
                    objectGrabber.SetEnabled(true);
                UpdateUI();
                Debug.Log($"[ApartmentManager] Station {area.stationType} not available in current phase — Selected state.");
                return;
            }

            CurrentState = State.InStation;
            _activeStation = station;
            station.Activate();

            // Disable apartment-level interaction during station
            if (objectGrabber != null)
                objectGrabber.SetEnabled(false);

            UpdateUI();
            Debug.Log($"[ApartmentManager] Entered station: {area.stationType}");
            return;
        }

        // No station — enter standard Selected state with object grabber
        CurrentState = State.Selected;

        if (objectGrabber != null)
            objectGrabber.SetEnabled(true);

        UpdateUI();
        Debug.Log("[ApartmentManager] Entered Selected state.");
    }

    private void HandleSelectedInput()
    {
        if (_cancelAction.WasPressedThisFrame())
            ReturnToBrowsing();
    }

    // ──────────────────────────────────────────────────────────────
    // InStation
    // ──────────────────────────────────────────────────────────────

    private void HandleInStationInput()
    {
        if (!_cancelAction.WasPressedThisFrame()) return;

        // Only allow exit when the station's manager is at its idle state
        if (_activeStation != null)
        {
            var manager = _activeStation.Manager;
            if (manager != null && !manager.IsAtIdleState)
                return;
        }

        ExitStation();
    }

    private void ExitStation()
    {
        if (_activeStation != null)
        {
            _activeStation.Deactivate();
            _activeStation = null;
        }

        // Return to Selected state (shows grabber + selected hints)
        CurrentState = State.Selected;

        if (objectGrabber != null)
            objectGrabber.SetEnabled(true);

        // Re-raise selected camera (station may have lowered it)
        if (selectedCamera != null)
            selectedCamera.Priority = PriorityActive;

        UpdateUI();
        Debug.Log("[ApartmentManager] Exited station → Selected.");
    }

    // ──────────────────────────────────────────────────────────────
    // Return to Browsing
    // ──────────────────────────────────────────────────────────────

    private void ReturnToBrowsing()
    {
        CurrentState = State.Browsing;

        if (objectGrabber != null)
            objectGrabber.SetEnabled(false);

        // Re-activate browse dolly camera
        _currentSplineT = areas[_currentAreaIndex].splinePosition;
        _targetSplineT = _currentSplineT;
        _isDollyAnimating = false;

        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                areas[_currentAreaIndex].browseBlendDuration);
        }

        ApplyDollyPosition(_currentSplineT, hardCut: false);
        UpdateUI();

        Debug.Log("[ApartmentManager] Returned to Browsing.");
    }

    // ──────────────────────────────────────────────────────────────
    // UI
    // ──────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        bool browsing = CurrentState == State.Browsing;
        bool selected = CurrentState == State.Selected;

        if (areaNamePanel != null)
            areaNamePanel.SetActive(browsing || CurrentState == State.Selecting);

        if (areaNameText != null && areas != null && areas.Length > 0)
            areaNameText.text = areas[_currentAreaIndex].areaName;

        if (browseHintsPanel != null)
            browseHintsPanel.SetActive(browsing);

        if (selectedHintsPanel != null)
            selectedHintsPanel.SetActive(selected);
    }

    // ──────────────────────────────────────────────────────────────
    // Look-At (runs in LateUpdate, after Cinemachine)
    // ──────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        ApplyLookAt();
    }

    private void ApplyLookAt()
    {
        if (CurrentState != State.Browsing && CurrentState != State.Selecting) return;
        if (browseCamera == null || areas == null || areas.Length == 0) return;

        // Smoothly move the look-at target toward the current area's focus point
        Vector3 targetLookAt = areas[_currentAreaIndex].lookAtPosition;
        _currentLookAt = Vector3.Lerp(_currentLookAt, targetLookAt, Time.deltaTime * lookAtSmoothing);

        // Rotate the camera to face the look-at point
        Vector3 dir = _currentLookAt - browseCamera.transform.position;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        browseCamera.transform.rotation = Quaternion.Slerp(
            browseCamera.transform.rotation, targetRot, Time.deltaTime * lookAtSmoothing);
    }

    // ──────────────────────────────────────────────────────────────
    // Cursor Parallax
    // ──────────────────────────────────────────────────────────────

    private void ApplyParallax()
    {
        if (CurrentState == State.Selecting || CurrentState == State.InStation) return;
        if (parallaxMaxOffset <= 0f) return;

        CinemachineCamera activeCam = CurrentState == State.Selected ? selectedCamera : browseCamera;
        if (activeCam == null) return;

        // For browse state with dolly, read base position from dolly's evaluated transform
        if (CurrentState == State.Browsing)
            _basePosition = activeCam.transform.position;

        Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();
        float nx = (mousePos.x / Screen.width - 0.5f) * 2f;
        float ny = (mousePos.y / Screen.height - 0.5f) * 2f;
        nx = Mathf.Clamp(nx, -1f, 1f);
        ny = Mathf.Clamp(ny, -1f, 1f);

        Transform camT = activeCam.transform;
        Vector3 targetOffset = (camT.right * -nx + camT.up * -ny) * parallaxMaxOffset;

        _currentParallaxOffset = Vector3.Lerp(_currentParallaxOffset, targetOffset,
            Time.deltaTime * parallaxSmoothing);

        // Apply parallax offset — works in both Browse and Selected states
        if (CurrentState == State.Browsing)
            camT.position += _currentParallaxOffset; // add on top of dolly position
        else if (CurrentState == State.Selected)
            camT.position = _basePosition + _currentParallaxOffset;
    }

    // ──────────────────────────────────────────────────────────────
    // Scene View Gizmos
    // ──────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (areas == null || areas.Length == 0) return;

        // Resolve the spline container from the dolly
        SplineContainer container = null;
        if (browseDolly != null)
            container = browseDolly.Spline;
        if (container == null || container.Spline == null) return;

        var spline = container.Spline;

        for (int i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area == null) continue;

            SplineUtility.Evaluate(spline, area.splinePosition,
                out float3 localPos, out float3 tangent, out float3 up);
            Vector3 worldPos = container.transform.TransformPoint((Vector3)localPos);

            // Sphere at spline stop
            bool isCurrent = Application.isPlaying && i == _currentAreaIndex;
            Gizmos.color = isCurrent ? Color.yellow : Color.cyan;
            Gizmos.DrawSphere(worldPos, 0.15f);
            Gizmos.DrawLine(worldPos, worldPos + Vector3.up * 0.5f);

            // Label
            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                normal = { textColor = isCurrent ? Color.yellow : Color.white }
            };
            UnityEditor.Handles.Label(worldPos + Vector3.up * 0.6f, area.areaName, style);

            // Line from spline stop to look-at target
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawLine(worldPos, area.lookAtPosition);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(area.lookAtPosition, 0.1f);
        }
    }
#endif
}
