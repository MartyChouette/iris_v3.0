using UnityEngine;

/// <summary>
/// Ambient watering system — always active, not a station.
/// Click any WaterablePlant to start pouring from the watering can.
/// Hold to pour, release to stop. Soil absorbs pooled water over time.
/// Score when pooled water fully absorbs — based on final soil moisture
/// versus the plant's ideal level. Judge by soil color alone.
///
/// States: Idle → Pouring → Absorbing → Scoring.
/// </summary>
[DisallowMultipleComponent]
public class WateringManager : MonoBehaviour
{
    public static WateringManager Instance { get; private set; }

    public enum State { Idle, Pouring, Absorbing, Scoring }

    [Header("References")]
    [Tooltip("Layer mask for plant pots with WaterablePlant component.")]
    [SerializeField] private LayerMask _plantLayer;

    [Tooltip("The pot controller (hidden — soil/water visuals).")]
    [SerializeField] private PotController _pot;

    [Tooltip("HUD reference for state-driven display.")]
    [SerializeField] private WateringHUD _hud;

    [Tooltip("Main camera (auto-found if null).")]
    [SerializeField] private Camera _mainCamera;

    [Header("Scoring")]
    [Tooltip("How long the score displays before returning to Idle.")]
    [SerializeField] private float _scoreDisplayTime = 2f;

    [Header("Audio")]
    public AudioClip pourSFX;
    public AudioClip overflowSFX;
    public AudioClip scoreSFX;

    [Tooltip("SFX played when a plant is clicked to start watering.")]
    public AudioClip plantClickSFX;

    [Tooltip("SFX played on a perfect watering score.")]
    public AudioClip perfectSFX;

    [Tooltip("SFX played on a failed watering score.")]
    public AudioClip failSFX;

    // ── Public read-only API ────────────────────────────────────

    public State CurrentState { get; private set; } = State.Idle;
    public PlantDefinition CurrentPlant => _activePlant;
    public PotController Pot => _pot;

    [HideInInspector] public int lastScore;
    [HideInInspector] public float lastMoistureScore;
    [HideInInspector] public float lastBonusScore;
    [HideInInspector] public float lastOverflowPenalty;

    [Header("Camera Zoom")]
    [Tooltip("How far above the pot rim the camera sits during watering.")]
    [SerializeField] private float _zoomHeight = 0.25f;

    [Tooltip("How far back from the pot the camera sits during watering.")]
    [SerializeField] private float _zoomDistance = 0.3f;

    [Tooltip("Camera lerp speed for zooming in/out.")]
    [SerializeField] private float _zoomSpeed = 4f;

    [Tooltip("FOV when zoomed into the pot.")]
    [SerializeField] private float _zoomFOV = 35f;

    // Input managed by IrisInput singleton

    // ── Runtime ─────────────────────────────────────────────────

    private PlantDefinition _activePlant;
    private WaterablePlant _activeWaterablePlant;
    private bool _overflowSFXPlayed;
    private float _scoreTimer;
    private float _pourTime;

    /// <summary>Current oscillating target moisture level.</summary>
    public float OscillatingTarget { get; private set; }

    // Camera zoom state
    private Vector3 _savedCamPos;
    private Quaternion _savedCamRot;
    private float _savedCamFOV;
    private bool _cameraZoomed;
    private bool _cameraRestoring;

    // ── Singleton lifecycle ─────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Input managed by IrisInput singleton — no local enable/disable needed.

    // ── Update dispatch ─────────────────────────────────────────

    void Update()
    {
        if (DayPhaseManager.Instance != null && !DayPhaseManager.Instance.IsInteractionPhase)
            return;

        if (ObjectGrabber.IsHoldingObject) return;
        if (ObjectGrabber.ClickConsumedThisFrame) return;

        if (_mainCamera == null) return;

        switch (CurrentState)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Pouring:
                UpdatePouring();
                break;
            case State.Absorbing:
                UpdateAbsorbing();
                break;
            case State.Scoring:
                UpdateScoring();
                break;
        }
    }

    // ── Idle ─────────────────────────────────────────────────────

    private void UpdateIdle()
    {
        if (IrisInput.Instance == null) return;
        if (!IrisInput.Instance.Click.WasPressedThisFrame()) return;

        var plant = RaycastPlant();
        if (plant != null)
        {
            AudioManager.Instance?.PlaySFX(plantClickSFX);
            BeginPouring(plant);
        }
    }

    private WaterablePlant RaycastPlant()
    {
        Vector2 pointer = IrisInput.CursorPosition;
        Ray ray = _mainCamera.ScreenPointToRay(pointer);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, _plantLayer))
            return null;

        var plant = hit.collider.GetComponent<WaterablePlant>();
        if (plant == null)
            plant = hit.collider.GetComponentInParent<WaterablePlant>();

        return (plant != null && plant.definition != null) ? plant : null;
    }

    private void BeginPouring(WaterablePlant plant)
    {
        _activePlant = plant.definition;
        _activeWaterablePlant = plant;

        if (_pot != null)
        {
            _pot.definition = _activePlant;
            _pot.Clear();
        }

        _overflowSFXPlayed = false;
        _pourTime = 0f;
        OscillatingTarget = _activePlant.perfectMoisture;

        // Zoom camera to pot rim
        ZoomToPot(plant.transform);

        CurrentState = State.Pouring;
        Debug.Log($"[WateringManager] Pouring: {_activePlant.plantName}");
    }

    // ── Pouring ─────────────────────────────────────────────────

    private void UpdatePouring()
    {
        // Update oscillating target
        if (_activePlant != null)
        {
            _pourTime += Time.deltaTime;
            OscillatingTarget = _activePlant.perfectMoisture
                + _activePlant.targetOscAmplitude
                * Mathf.Sin(2f * Mathf.PI * _activePlant.targetOscSpeed * _pourTime);
            OscillatingTarget = Mathf.Clamp01(OscillatingTarget);

            if (_pot != null)
                _pot.TargetLevel = OscillatingTarget;
        }

        UpdateCameraZoom();

        if (IrisInput.Instance != null && IrisInput.Instance.Click.IsPressed())
        {
            if (_pot != null)
                _pot.Pour(Time.deltaTime);

            // Overflow SFX (play once)
            if (_pot != null && _pot.Overflowed && !_overflowSFXPlayed)
            {
                AudioManager.Instance?.PlaySFX(overflowSFX);
                _overflowSFXPlayed = true;
            }
        }
        else
        {
            // Released — stop pouring, enter absorbing phase
            if (_pot != null)
                _pot.StopPouring();

            CurrentState = State.Absorbing;
            Debug.Log("[WateringManager] Released — watching soil absorb.");
        }
    }

    // ── Absorbing (watching pooled water soak in) ───────────────

    private void UpdateAbsorbing()
    {
        // Keep oscillating target moving during absorption
        if (_activePlant != null)
        {
            _pourTime += Time.deltaTime;
            OscillatingTarget = _activePlant.perfectMoisture
                + _activePlant.targetOscAmplitude
                * Mathf.Sin(2f * Mathf.PI * _activePlant.targetOscSpeed * _pourTime);
            OscillatingTarget = Mathf.Clamp01(OscillatingTarget);

            if (_pot != null)
                _pot.TargetLevel = OscillatingTarget;
        }

        UpdateCameraZoom();

        // Wait for pooled water to fully absorb into soil
        if (_pot != null && _pot.PooledWater > 0.005f)
        {
            // Player can click again to add more water
            if (IrisInput.Instance != null && IrisInput.Instance.Click.WasPressedThisFrame())
            {
                CurrentState = State.Pouring;
                return;
            }
            return; // still absorbing
        }

        // All pooled water absorbed — score
        CalculateScore();
    }

    // ── Scoring ─────────────────────────────────────────────────

    private void CalculateScore()
    {
        if (_pot == null || _pot.definition == null)
        {
            CurrentState = State.Scoring;
            _scoreTimer = _scoreDisplayTime;
            return;
        }

        var def = _pot.definition;

        // Moisture score (0-70): how close soil moisture is to oscillating target at moment of scoring
        float dist = Mathf.Abs(_pot.SoilMoisture - OscillatingTarget);
        float accuracy = Mathf.Clamp01(1f - dist / Mathf.Max(def.moistureTolerance, 0.001f));
        lastMoistureScore = accuracy * 70f;

        // Overflow penalty
        lastOverflowPenalty = _pot.Overflowed ? -20f : 0f;

        // Bonus (0-30): near-perfect without overflow
        if (!_pot.Overflowed && accuracy > 0.85f)
            lastBonusScore = 30f;
        else
            lastBonusScore = accuracy * 15f;

        float raw = lastMoistureScore + lastBonusScore + lastOverflowPenalty;
        lastScore = Mathf.Clamp((int)raw, 0, def.baseScore);

        AudioManager.Instance?.PlaySFX(scoreSFX);

        // Perfect / fail SFX
        bool isPerfect = !_pot.Overflowed && accuracy > 0.85f;
        if (isPerfect)
            AudioManager.Instance?.PlaySFX(perfectSFX);
        else if (lastScore < 30)
            AudioManager.Instance?.PlaySFX(failSFX);

        CurrentState = State.Scoring;
        _scoreTimer = _scoreDisplayTime;

        // Start restoring camera
        RestoreCamera();

        Debug.Log($"[WateringManager] Score: {lastScore} (moisture={_pot.SoilMoisture:F2}, " +
                  $"target={OscillatingTarget:F2}, accuracy={accuracy:F2})");
    }

    private void UpdateScoring()
    {
        UpdateCameraRestore();

        // Allow clicking the next plant to interrupt
        if (IrisInput.Instance != null && IrisInput.Instance.Click.WasPressedThisFrame())
        {
            var plant = RaycastPlant();
            if (plant != null)
            {
                AudioManager.Instance?.PlaySFX(plantClickSFX);
                BeginPouring(plant);
                return;
            }
        }

        _scoreTimer -= Time.deltaTime;
        if (_scoreTimer <= 0f)
        {
            _activePlant = null;
            _activeWaterablePlant = null;
            CurrentState = State.Idle;
        }
    }

    /// <summary>Force back to idle. Called on phase transitions.</summary>
    public void ForceIdle()
    {
        if (_cameraZoomed) SnapCameraBack();
        _activePlant = null;
        _activeWaterablePlant = null;
        CurrentState = State.Idle;
    }

    // ── Camera Zoom ─────────────────────────────────────────────

    private void ZoomToPot(Transform plantTransform)
    {
        if (_mainCamera == null || _cameraZoomed) return;

        _savedCamPos = _mainCamera.transform.position;
        _savedCamRot = _mainCamera.transform.rotation;
        _savedCamFOV = _mainCamera.fieldOfView;
        _cameraZoomed = true;
        _cameraRestoring = false;

        // Compute a fixed target position centered on the plant
        // Camera sits above and slightly in front, looking down at the pot
        Vector3 plantPos = plantTransform.position;
        Vector3 toCam = (_savedCamPos - plantPos).normalized;
        // Keep the horizontal direction from original camera but flatten it
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.01f) toCam = Vector3.back;
        toCam.Normalize();

        _zoomTargetPos = plantPos + toCam * _zoomDistance + Vector3.up * _zoomHeight;
        _zoomTargetRot = Quaternion.LookRotation(plantPos + Vector3.up * 0.05f - _zoomTargetPos, Vector3.up);

        ApartmentManager.Instance?.LockCamera();
    }

    private Vector3 _zoomTargetPos;
    private Quaternion _zoomTargetRot;

    private void UpdateCameraZoom()
    {
        if (!_cameraZoomed || _cameraRestoring || _mainCamera == null) return;

        float t = _zoomSpeed * Time.deltaTime;
        _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, _zoomTargetPos, t);
        _mainCamera.transform.rotation = Quaternion.Slerp(_mainCamera.transform.rotation, _zoomTargetRot, t);
        _mainCamera.fieldOfView = Mathf.Lerp(_mainCamera.fieldOfView, _zoomFOV, t);
    }

    private void RestoreCamera()
    {
        _cameraRestoring = true;
    }

    private void UpdateCameraRestore()
    {
        if (!_cameraRestoring || _mainCamera == null) return;

        float t = _zoomSpeed * Time.deltaTime;
        _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, _savedCamPos, t);
        _mainCamera.transform.rotation = Quaternion.Slerp(_mainCamera.transform.rotation, _savedCamRot, t);
        _mainCamera.fieldOfView = Mathf.Lerp(_mainCamera.fieldOfView, _savedCamFOV, t);

        if (Vector3.Distance(_mainCamera.transform.position, _savedCamPos) < 0.02f)
            SnapCameraBack();
    }

    private void SnapCameraBack()
    {
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = _savedCamPos;
            _mainCamera.transform.rotation = _savedCamRot;
            _mainCamera.fieldOfView = _savedCamFOV;
        }

        _cameraZoomed = false;
        _cameraRestoring = false;
        ApartmentManager.Instance?.UnlockCamera();
    }
}
