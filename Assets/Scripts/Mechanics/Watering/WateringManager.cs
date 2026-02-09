using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-scoped singleton FSM for the watering prototype.
/// States: Browsing → Watering → Scoring.
/// Player cycles through potted plants, enters a close-up watering view,
/// and pours water — dirt foams up like the drink-making mechanic.
/// </summary>
[DisallowMultipleComponent]
public class WateringManager : MonoBehaviour, IStationManager
{
    public bool IsAtIdleState => CurrentState == State.Browsing;

    public static WateringManager Instance { get; private set; }

    public enum State { Browsing, Watering, Scoring }

    [Header("Plants")]
    [Tooltip("Plant definitions (one per pot on the shelf).")]
    [SerializeField] private PlantDefinition[] _plantDefinitions;

    [Tooltip("Shelf plant GameObjects (order matches definitions).")]
    [SerializeField] private Transform[] _plantVisuals;

    [Header("Highlight")]
    [Tooltip("Ring/frame that follows the selected plant on the shelf.")]
    [SerializeField] private Transform _highlightRing;

    [Tooltip("Colour tint for the highlight ring.")]
    [SerializeField] private Color _highlightColor = Color.yellow;

    [Header("Watering Station")]
    [Tooltip("The close-up pot controller.")]
    [SerializeField] private PotController _pot;

    [Tooltip("Visual for the watering can (tilts when pouring).")]
    [SerializeField] private Transform _wateringCanVisual;

    [Tooltip("Main camera (auto-found if null).")]
    [SerializeField] private Camera _mainCamera;

    [Header("Camera Positions")]
    [Tooltip("Camera position for browsing the shelf.")]
    [SerializeField] private Vector3 _browsePosition = new Vector3(0f, 1.3f, -0.6f);

    [Tooltip("Camera rotation for browsing (Euler angles).")]
    [SerializeField] private Vector3 _browseRotation = new Vector3(20f, 0f, 0f);

    [Tooltip("Camera position for close-up watering view.")]
    [SerializeField] private Vector3 _wateringPosition = new Vector3(0f, 0.8f, -0.1f);

    [Tooltip("Camera rotation for watering view (Euler angles).")]
    [SerializeField] private Vector3 _wateringRotation = new Vector3(40f, 0f, 0f);

    [Tooltip("Speed of camera lerp between positions.")]
    [SerializeField] private float _cameraBlendSpeed = 3f;

    [Header("Watering Can")]
    [Tooltip("Upright angle (degrees) when idle.")]
    [SerializeField] private float _canIdleAngle = 0f;

    [Tooltip("Tilted angle (degrees) when pouring.")]
    [SerializeField] private float _canPourAngle = -45f;

    [Header("Water Stream")]
    [Tooltip("Thin visual that stretches from the can spout to the pot while pouring.")]
    [SerializeField] private Transform _waterStreamVisual;

    [Tooltip("World position of the spout tip (stream starts here).")]
    [SerializeField] private Vector3 _spoutTipOffset = new Vector3(-0.08f, 0.0f, 0f);

    [Tooltip("Width of the water stream.")]
    [SerializeField] private float _streamWidth = 0.008f;

    [Header("HUD")]
    [Tooltip("HUD reference for state-driven display.")]
    [SerializeField] private WateringHUD _hud;

    [Header("Audio")]
    public AudioClip selectSFX;
    public AudioClip pourSFX;
    public AudioClip overflowSFX;
    public AudioClip scoreSFX;

    [Header("Scoring")]
    [Tooltip("Points deducted for overflow.")]
    [SerializeField] private float _overflowPenalty = 30f;

    // ── Public read-only API ─────────────────────────────────────────

    public State CurrentState { get; private set; } = State.Browsing;
    public int CurrentPlantIndex => _currentPlantIndex;
    public PlantDefinition CurrentPlant =>
        _plantDefinitions != null && _currentPlantIndex < _plantDefinitions.Length
            ? _plantDefinitions[_currentPlantIndex] : null;
    public PotController Pot => _pot;

    [HideInInspector] public int lastScore;
    [HideInInspector] public float lastFillScore;
    [HideInInspector] public float lastOverflowScore;
    [HideInInspector] public float lastBonusScore;

    // ── Input ────────────────────────────────────────────────────────

    private InputAction _navigateLeft;
    private InputAction _navigateRight;
    private InputAction _selectAction;
    private InputAction _cancelAction;
    private InputAction _pourAction;

    // ── Runtime ──────────────────────────────────────────────────────

    private int _currentPlantIndex;
    private Vector3 _targetCamPos;
    private Quaternion _targetCamRot;
    private float _currentCanAngle;
    private bool _overflowSFXPlayed;

    // ── Singleton lifecycle ──────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Inline InputActions (same pattern as ApartmentManager)
        _navigateLeft = new InputAction("WaterNavLeft", InputActionType.Button);
        _navigateLeft.AddBinding("<Keyboard>/a");
        _navigateLeft.AddBinding("<Keyboard>/leftArrow");

        _navigateRight = new InputAction("WaterNavRight", InputActionType.Button);
        _navigateRight.AddBinding("<Keyboard>/d");
        _navigateRight.AddBinding("<Keyboard>/rightArrow");

        _selectAction = new InputAction("WaterSelect", InputActionType.Button);
        _selectAction.AddBinding("<Keyboard>/enter");
        _selectAction.AddBinding("<Keyboard>/space");

        _cancelAction = new InputAction("WaterCancel", InputActionType.Button, "<Keyboard>/escape");

        _pourAction = new InputAction("WaterPour", InputActionType.Button, "<Mouse>/leftButton");

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        _navigateLeft.Enable();
        _navigateRight.Enable();
        _selectAction.Enable();
        _cancelAction.Enable();
        _pourAction.Enable();
    }

    void OnDisable()
    {
        _navigateLeft.Disable();
        _navigateRight.Disable();
        _selectAction.Disable();
        _cancelAction.Disable();
        _pourAction.Disable();
    }

    void Start()
    {
        _currentPlantIndex = 0;
        _targetCamPos = _browsePosition;
        _targetCamRot = Quaternion.Euler(_browseRotation);

        if (_mainCamera != null)
        {
            _mainCamera.transform.position = _browsePosition;
            _mainCamera.transform.rotation = Quaternion.Euler(_browseRotation);
        }

        UpdateHighlight();

        // Hide watering can and stream initially
        if (_wateringCanVisual != null)
            _wateringCanVisual.gameObject.SetActive(false);
        if (_waterStreamVisual != null)
            _waterStreamVisual.gameObject.SetActive(false);
    }

    // ── Update dispatch ──────────────────────────────────────────────

    void Update()
    {
        switch (CurrentState)
        {
            case State.Browsing:
                HandleBrowsingInput();
                break;
            case State.Watering:
                UpdateWatering();
                break;
            case State.Scoring:
                // Scoring screen — buttons call Retry / NextPlant
                break;
        }

        LerpCamera();
    }

    // ── Browsing ─────────────────────────────────────────────────────

    private void HandleBrowsingInput()
    {
        if (_navigateLeft.WasPressedThisFrame())
            CyclePlant(-1);
        else if (_navigateRight.WasPressedThisFrame())
            CyclePlant(1);

        if (_selectAction.WasPressedThisFrame())
            EnterWatering();
    }

    private void CyclePlant(int direction)
    {
        if (_plantDefinitions == null || _plantDefinitions.Length == 0) return;

        _currentPlantIndex = (_currentPlantIndex + direction + _plantDefinitions.Length)
            % _plantDefinitions.Length;

        UpdateHighlight();

        if (AudioManager.Instance != null && selectSFX != null)
            AudioManager.Instance.PlaySFX(selectSFX);

        Debug.Log($"[WateringManager] Browsing: {CurrentPlant?.plantName ?? "null"}");
    }

    private void UpdateHighlight()
    {
        if (_highlightRing == null || _plantVisuals == null) return;
        if (_currentPlantIndex < _plantVisuals.Length && _plantVisuals[_currentPlantIndex] != null)
        {
            Vector3 pos = _plantVisuals[_currentPlantIndex].position;
            _highlightRing.position = new Vector3(pos.x, _highlightRing.position.y, pos.z);
        }
    }

    // ── Enter Watering ───────────────────────────────────────────────

    private void EnterWatering()
    {
        if (_plantDefinitions == null || _currentPlantIndex >= _plantDefinitions.Length) return;

        // Load plant into pot
        if (_pot != null)
        {
            _pot.definition = _plantDefinitions[_currentPlantIndex];
            _pot.Clear();
        }

        // Set camera target to watering position
        _targetCamPos = _wateringPosition;
        _targetCamRot = Quaternion.Euler(_wateringRotation);

        // Show watering can
        if (_wateringCanVisual != null)
            _wateringCanVisual.gameObject.SetActive(true);

        _overflowSFXPlayed = false;
        CurrentState = State.Watering;

        Debug.Log($"[WateringManager] Watering: {CurrentPlant?.plantName ?? "null"}");
    }

    // ── Watering ─────────────────────────────────────────────────────

    private void UpdateWatering()
    {
        float targetAngle = _canIdleAngle;

        if (_pourAction.IsPressed())
        {
            if (_pot != null)
                _pot.Pour(Time.deltaTime);

            targetAngle = _canPourAngle;

            // Overflow SFX (play once)
            if (_pot != null && _pot.Overflowed && !_overflowSFXPlayed)
            {
                if (AudioManager.Instance != null && overflowSFX != null)
                    AudioManager.Instance.PlaySFX(overflowSFX);
                _overflowSFXPlayed = true;
            }
        }
        else
        {
            if (_pot != null)
                _pot.StopPouring();
        }

        // Tilt watering can
        if (_wateringCanVisual != null)
        {
            _currentCanAngle = Mathf.Lerp(_currentCanAngle, targetAngle, Time.deltaTime * 8f);
            _wateringCanVisual.localRotation = Quaternion.Euler(0f, 0f, _currentCanAngle);
        }

        UpdateWaterStream(_pourAction.IsPressed());

        if (_cancelAction.WasPressedThisFrame())
            ReturnToBrowsing();
    }

    /// <summary>Called by the "Done Watering" UI button.</summary>
    public void FinishWatering()
    {
        if (CurrentState != State.Watering) return;

        if (_pot != null)
            _pot.StopPouring();

        CalculateScore();
    }

    // ── Scoring ──────────────────────────────────────────────────────

    private void CalculateScore()
    {
        if (_pot == null || _pot.definition == null)
        {
            CurrentState = State.Scoring;
            return;
        }

        var def = _pot.definition;

        // Fill score (0-70): how close water is to ideal level
        float fillDist = Mathf.Abs(_pot.WaterLevel - def.idealWaterLevel);
        float fillNorm = Mathf.Clamp01(1f - fillDist / Mathf.Max(def.waterTolerance, 0.001f));
        lastFillScore = fillNorm * 70f;

        // Overflow penalty
        lastOverflowScore = _pot.Overflowed ? -_overflowPenalty : 0f;

        // Bonus (0-30): extra points for near-perfect without overflow
        if (!_pot.Overflowed && fillNorm > 0.9f)
            lastBonusScore = 30f;
        else
            lastBonusScore = fillNorm * 30f;

        float raw = lastFillScore + lastBonusScore + lastOverflowScore;
        lastScore = Mathf.Clamp((int)raw, 0, def.baseScore);

        if (AudioManager.Instance != null && scoreSFX != null)
            AudioManager.Instance.PlaySFX(scoreSFX);

        CurrentState = State.Scoring;

        Debug.Log($"[WateringManager] Score: {lastScore} (fill={lastFillScore:F0} bonus={lastBonusScore:F0} overflow={lastOverflowScore:F0})");
    }

    /// <summary>Called by "Retry" button — replay the same plant.</summary>
    public void RetryPlant()
    {
        if (_pot != null)
            _pot.Clear();

        _overflowSFXPlayed = false;
        CurrentState = State.Watering;

        Debug.Log("[WateringManager] Retry plant.");
    }

    /// <summary>Called by "Next Plant" button — return to browsing and advance.</summary>
    public void NextPlant()
    {
        ReturnToBrowsing();

        // Auto-advance to next plant
        if (_plantDefinitions != null && _plantDefinitions.Length > 0)
        {
            _currentPlantIndex = (_currentPlantIndex + 1) % _plantDefinitions.Length;
            UpdateHighlight();
        }
    }

    // ── Return to Browsing ───────────────────────────────────────────

    private void ReturnToBrowsing()
    {
        _targetCamPos = _browsePosition;
        _targetCamRot = Quaternion.Euler(_browseRotation);

        // Hide watering can and stream
        if (_wateringCanVisual != null)
            _wateringCanVisual.gameObject.SetActive(false);
        if (_waterStreamVisual != null)
            _waterStreamVisual.gameObject.SetActive(false);

        if (_pot != null)
            _pot.StopPouring();

        CurrentState = State.Browsing;

        Debug.Log("[WateringManager] Returned to Browsing.");
    }

    // ── Water Stream ────────────────────────────────────────────────

    private void UpdateWaterStream(bool pouring)
    {
        if (_waterStreamVisual == null) return;

        _waterStreamVisual.gameObject.SetActive(pouring);
        if (!pouring) return;

        // Spout tip in world space (relative to watering can)
        Vector3 spoutWorld = _wateringCanVisual != null
            ? _wateringCanVisual.TransformPoint(_spoutTipOffset)
            : _waterStreamVisual.position;

        // Target: top of the pot
        Vector3 potTop = _pot != null
            ? _pot.transform.position + Vector3.up * _pot.potWorldHeight
            : spoutWorld + Vector3.down * 0.15f;

        // Position stream at midpoint, orient along the gap
        Vector3 midpoint = (spoutWorld + potTop) * 0.5f;
        float height = Vector3.Distance(spoutWorld, potTop);

        _waterStreamVisual.position = midpoint;
        _waterStreamVisual.up = (spoutWorld - potTop).normalized;

        // Wobble the width slightly for a hand-drawn look
        float wobble = 1f + Mathf.Sin(Time.time * 12f) * 0.15f;
        float w = _streamWidth * wobble;
        _waterStreamVisual.localScale = new Vector3(w, height * 0.5f, w);
    }

    // ── Camera Lerp ──────────────────────────────────────────────────

    private void LerpCamera()
    {
        if (_mainCamera == null) return;

        float t = Time.deltaTime * _cameraBlendSpeed;
        _mainCamera.transform.position = Vector3.Lerp(
            _mainCamera.transform.position, _targetCamPos, t);
        _mainCamera.transform.rotation = Quaternion.Slerp(
            _mainCamera.transform.rotation, _targetCamRot, t);
    }
}
