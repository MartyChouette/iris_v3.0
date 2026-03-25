using UnityEngine;

/// <summary>
/// Ambient watering system — always active, not a station.
/// Click plant → release → hold to pour → release to stop → score.
/// Camera zooms to plant via ApartmentManager.SetPresetBase.
/// Placeholder pail tips while pouring, soil darkens, water rises.
/// </summary>
[DisallowMultipleComponent]
public class WateringManager : MonoBehaviour
{
    public static WateringManager Instance { get; private set; }

    public enum State { Idle, Pouring, Absorbing, Scoring }

    [Header("References")]
    [SerializeField] private LayerMask _plantLayer;
    [SerializeField] private PotController _pot;
    [SerializeField] private WateringHUD _hud;
    [SerializeField] private Camera _mainCamera;

    [Header("Scoring")]
    [SerializeField] private float _scoreDisplayTime = 2f;

    [Header("Audio")]
    public AudioClip pourSFX;
    public AudioClip overflowSFX;
    public AudioClip scoreSFX;
    public AudioClip plantClickSFX;
    public AudioClip perfectSFX;
    public AudioClip failSFX;

    [Header("Camera Zoom")]
    [SerializeField] private float _zoomHeight = 0.35f;
    [SerializeField] private float _zoomDistance = 0.45f;
    [SerializeField] private float _zoomFOV = 40f;

    // ── Public API ──────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Idle;
    public PlantDefinition CurrentPlant => _activePlant;
    public PotController Pot => _pot;
    public float OscillatingTarget { get; private set; }

    [HideInInspector] public int lastScore;
    [HideInInspector] public float lastMoistureScore;
    [HideInInspector] public float lastBonusScore;
    [HideInInspector] public float lastOverflowPenalty;

    // ── Runtime ─────────────────────────────────────────────────
    private PlantDefinition _activePlant;
    private WaterablePlant _activeWaterablePlant;
    private bool _overflowSFXPlayed;
    private float _scoreTimer;
    private float _pourTime;
    private bool _waitForRelease;

    // Pail
    private GameObject _pailGO;

    // Camera
    private bool _cameraOverrideActive;

    // ── Lifecycle ───────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (_mainCamera == null) _mainCamera = Camera.main;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Update ──────────────────────────────────────────────────

    void Update()
    {
        if (DayPhaseManager.Instance != null && !DayPhaseManager.Instance.IsInteractionPhase)
            return;
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        switch (CurrentState)
        {
            case State.Idle:
                if (ObjectGrabber.IsHoldingObject) return;
                if (ObjectGrabber.ClickConsumedThisFrame) return;
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

    // ── Idle ────────────────────────────────────────────────────

    private void UpdateIdle()
    {
        if (IrisInput.Instance == null || !IrisInput.Instance.Click.WasPressedThisFrame()) return;

        var plant = RaycastPlant();
        if (plant != null)
        {
            AudioManager.Instance?.PlaySFX(plantClickSFX);
            BeginPouring(plant);
        }
    }

    private WaterablePlant RaycastPlant()
    {
        Ray ray = _mainCamera.ScreenPointToRay(IrisInput.CursorPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, _plantLayer)) return null;
        var p = hit.collider.GetComponentInParent<WaterablePlant>();
        return (p != null && p.definition != null) ? p : null;
    }

    // ── Begin ───────────────────────────────────────────────────

    private void BeginPouring(WaterablePlant plant)
    {
        _activePlant = plant.definition;
        _activeWaterablePlant = plant;
        _overflowSFXPlayed = false;
        _pourTime = 0f;
        _waitForRelease = true;
        OscillatingTarget = _activePlant.perfectMoisture;

        // Move PotController to the plant so auto-created discs appear at the right spot
        if (_pot != null)
        {
            _pot.transform.position = plant.transform.position;
            _pot.definition = _activePlant;
            _pot.Clear();
        }

        ShowPail(plant.transform);
        ZoomToPlant(plant.transform);

        CurrentState = State.Pouring;
        Debug.Log($"[WateringManager] Pouring: {_activePlant.plantName} at {plant.transform.position}");
    }

    // ── Pouring ─────────────────────────────────────────────────

    private void UpdatePouring()
    {
        UpdateOscillation();

        bool mouseDown = IrisInput.Instance != null && IrisInput.Instance.Click.IsPressed();

        // Wait for initial click release before tracking hold-to-pour
        if (_waitForRelease)
        {
            if (!mouseDown) _waitForRelease = false;
            UpdatePail(false);
            return;
        }

        UpdatePail(mouseDown);

        if (mouseDown)
        {
            if (_pot != null) _pot.Pour(Time.deltaTime);
            if (_pot != null && _pot.Overflowed && !_overflowSFXPlayed)
            {
                AudioManager.Instance?.PlaySFX(overflowSFX);
                _overflowSFXPlayed = true;
            }
        }
        else
        {
            if (_pot != null) _pot.StopPouring();
            CurrentState = State.Absorbing;
        }
    }

    // ── Absorbing ───────────────────────────────────────────────

    private void UpdateAbsorbing()
    {
        UpdateOscillation();
        UpdatePail(false);

        if (_pot != null && _pot.PooledWater > 0.005f)
        {
            // Can click again to add more water
            if (IrisInput.Instance != null && IrisInput.Instance.Click.WasPressedThisFrame())
            {
                _waitForRelease = true;
                CurrentState = State.Pouring;
                return;
            }
            return;
        }

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
        float dist = Mathf.Abs(_pot.SoilMoisture - OscillatingTarget);
        float accuracy = Mathf.Clamp01(1f - dist / Mathf.Max(def.moistureTolerance, 0.001f));
        lastMoistureScore = accuracy * 70f;
        lastOverflowPenalty = _pot.Overflowed ? -20f : 0f;
        lastBonusScore = (!_pot.Overflowed && accuracy > 0.85f) ? 30f : accuracy * 15f;
        lastScore = Mathf.Clamp((int)(lastMoistureScore + lastBonusScore + lastOverflowPenalty), 0, def.baseScore);

        AudioManager.Instance?.PlaySFX(scoreSFX);
        if (!_pot.Overflowed && accuracy > 0.85f) AudioManager.Instance?.PlaySFX(perfectSFX);
        else if (lastScore < 30) AudioManager.Instance?.PlaySFX(failSFX);

        HidePail();
        RestoreCamera();

        CurrentState = State.Scoring;
        _scoreTimer = _scoreDisplayTime;
        Debug.Log($"[WateringManager] Score: {lastScore} (moisture={_pot.SoilMoisture:F2}, target={OscillatingTarget:F2})");
    }

    private void UpdateScoring()
    {
        if (IrisInput.Instance != null && IrisInput.Instance.Click.WasPressedThisFrame())
        {
            var plant = RaycastPlant();
            if (plant != null) { BeginPouring(plant); return; }
        }

        _scoreTimer -= Time.deltaTime;
        if (_scoreTimer <= 0f)
        {
            _activePlant = null;
            _activeWaterablePlant = null;
            CurrentState = State.Idle;
        }
    }

    public void ForceIdle()
    {
        RestoreCamera();
        HidePail();
        _activePlant = null;
        _activeWaterablePlant = null;
        CurrentState = State.Idle;
    }

    // ── Oscillation ─────────────────────────────────────────────

    private void UpdateOscillation()
    {
        if (_activePlant == null) return;
        _pourTime += Time.deltaTime;
        OscillatingTarget = Mathf.Clamp01(
            _activePlant.perfectMoisture
            + _activePlant.targetOscAmplitude
            * Mathf.Sin(2f * Mathf.PI * _activePlant.targetOscSpeed * _pourTime));
        if (_pot != null) _pot.TargetLevel = OscillatingTarget;
    }

    // ── Pail ────────────────────────────────────────────────────

    private void ShowPail(Transform plant)
    {
        if (_pailGO == null)
        {
            _pailGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _pailGO.name = "WateringPail";
            _pailGO.transform.localScale = new Vector3(0.12f, 0.15f, 0.10f);
            var c = _pailGO.GetComponent<Collider>();
            if (c != null) Destroy(c);
            var r = _pailGO.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.45f, 0.55f, 0.65f);
            _pailGO.SetActive(false);
        }

        Vector3 rimPos = plant.position + Vector3.up * 0.15f;
        Vector3 camDir = (_mainCamera.transform.position - rimPos);
        camDir.y = 0f;
        if (camDir.sqrMagnitude < 0.01f) camDir = Vector3.back;
        camDir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, camDir).normalized;

        _pailGO.transform.position = rimPos + right * 0.12f;
        _pailGO.transform.rotation = Quaternion.LookRotation(-camDir, Vector3.up);
        _pailGO.SetActive(true);
    }

    private void UpdatePail(bool pouring)
    {
        if (_pailGO == null || !_pailGO.activeSelf) return;
        float target = pouring ? -45f : 0f;
        var euler = _pailGO.transform.eulerAngles;
        float current = euler.z > 180f ? euler.z - 360f : euler.z;
        euler.z = Mathf.Lerp(current, target, Time.deltaTime * 6f);
        _pailGO.transform.eulerAngles = euler;
    }

    private void HidePail()
    {
        if (_pailGO != null) _pailGO.SetActive(false);
    }

    // ── Camera (uses ApartmentManager.SetPresetBase) ────────────

    private void ZoomToPlant(Transform plant)
    {
        if (ApartmentManager.Instance == null) return;

        Vector3 plantPos = plant.position;
        Vector3 camPos = _mainCamera.transform.position;
        Vector3 toCam = camPos - plantPos;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.01f) toCam = Vector3.back;
        toCam.Normalize();

        Vector3 zoomPos = plantPos + toCam * _zoomDistance + Vector3.up * _zoomHeight;
        Quaternion zoomRot = Quaternion.LookRotation(plantPos + Vector3.up * 0.05f - zoomPos, Vector3.up);

        ApartmentManager.Instance.SetPresetBase(zoomPos, zoomRot, _zoomFOV);
        _cameraOverrideActive = true;
        Debug.Log($"[WateringManager] Camera zoom to {zoomPos}");
    }

    private void RestoreCamera()
    {
        if (!_cameraOverrideActive) return;
        ApartmentManager.Instance?.ClearPresetBase();
        _cameraOverrideActive = false;
    }
}
