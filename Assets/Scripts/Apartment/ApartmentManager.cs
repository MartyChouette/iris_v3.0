using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using TMPro;

public class ApartmentManager : MonoBehaviour
{
    public enum State { Browsing, Selecting, Selected }

    public static ApartmentManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Areas")]
    [Tooltip("Ordered list of apartment areas. A/D cycles through these.")]
    [SerializeField] private ApartmentAreaDefinition[] areas;

    [Header("Cameras")]
    [Tooltip("Cinemachine camera used for browse vantage points.")]
    [SerializeField] private CinemachineCamera browseCamera;

    [Tooltip("Cinemachine camera used for selected/interaction view.")]
    [SerializeField] private CinemachineCamera selectedCamera;

    [Tooltip("CinemachineBrain on the main camera. Auto-found if null.")]
    [SerializeField] private CinemachineBrain brain;

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

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Browsing;

    private int _currentAreaIndex;
    private float _blendTimer;
    private float _blendDuration;

    private void Awake()
    {
        // Scene-scoped singleton (same pattern as HorrorCameraManager)
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

        // Inline InputActions (same pattern as SimpleTestCharacter / MarkerController)
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
    }

    private void OnEnable()
    {
        _navigateLeftAction.Enable();
        _navigateRightAction.Enable();
        _selectAction.Enable();
        _cancelAction.Enable();
    }

    private void OnDisable()
    {
        _navigateLeftAction.Disable();
        _navigateRightAction.Disable();
        _selectAction.Disable();
        _cancelAction.Disable();
    }

    private void Start()
    {
        if (areas == null || areas.Length == 0)
        {
            Debug.LogError("[ApartmentManager] No areas assigned.");
            return;
        }

        // Initialize: show first area in browse mode
        if (objectGrabber != null)
            objectGrabber.SetEnabled(false);

        _currentAreaIndex = 0;
        ApplyBrowseCamera(areas[0], hardCut: true);
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case State.Browsing:
                HandleBrowsingInput();
                break;

            case State.Selecting:
                // Wait for blend to complete
                _blendTimer -= Time.deltaTime;
                if (_blendTimer <= 0f)
                    EnterSelected();
                break;

            case State.Selected:
                HandleSelectedInput();
                break;
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

        if (_selectAction.WasPressedThisFrame())
            BeginSelecting();
    }

    private void CycleArea(int direction)
    {
        if (areas == null || areas.Length == 0) return;

        _currentAreaIndex = (_currentAreaIndex + direction + areas.Length) % areas.Length;
        ApplyBrowseCamera(areas[_currentAreaIndex], hardCut: false);
        UpdateUI();

        Debug.Log($"[ApartmentManager] Browsing: {areas[_currentAreaIndex].areaName}");
    }

    private void ApplyBrowseCamera(ApartmentAreaDefinition area, bool hardCut)
    {
        if (browseCamera == null) return;

        browseCamera.transform.position = area.browsePosition;
        browseCamera.transform.rotation = Quaternion.Euler(area.browseRotation);
        browseCamera.Lens = new LensSettings { FieldOfView = area.browseFOV };

        // Set blend style on brain
        if (brain != null)
        {
            if (hardCut)
            {
                brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.Cut, 0f);
            }
            else
            {
                brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, area.browseBlendDuration);
            }
        }

        // Ensure browse camera is active
        browseCamera.Priority = PriorityActive;
        if (selectedCamera != null)
            selectedCamera.Priority = PriorityInactive;
    }

    // ──────────────────────────────────────────────────────────────
    // Selecting (transition state)
    // ──────────────────────────────────────────────────────────────

    private void BeginSelecting()
    {
        if (areas == null || areas.Length == 0) return;

        var area = areas[_currentAreaIndex];
        CurrentState = State.Selecting;

        // Position selected camera
        if (selectedCamera != null)
        {
            selectedCamera.transform.position = area.selectedPosition;
            selectedCamera.transform.rotation = Quaternion.Euler(area.selectedRotation);
            selectedCamera.Lens = new LensSettings { FieldOfView = area.selectedFOV };
        }

        // Set blend duration and switch priority
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

    private void ReturnToBrowsing()
    {
        CurrentState = State.Browsing;

        if (objectGrabber != null)
            objectGrabber.SetEnabled(false);

        // Return to browse camera
        ApplyBrowseCamera(areas[_currentAreaIndex], hardCut: false);
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
}
