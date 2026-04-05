using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ambient watering system — always active, not a station.
/// Click any WaterablePlant from any camera position to pour.
/// States: Idle → Pouring → Scoring.
/// </summary>
[DisallowMultipleComponent]
public class WateringManager : MonoBehaviour
{
    public static WateringManager Instance { get; private set; }

    public enum State { Idle, Pouring, Scoring }

    [Header("References")]
    [Tooltip("Layer mask for plant pots with WaterablePlant component.")]
    [SerializeField] private LayerMask _plantLayer;

    [Tooltip("The pot controller (hidden — HUD shows meters).")]
    [SerializeField] private PotController _pot;

    [Tooltip("HUD reference for state-driven display.")]
    [SerializeField] private WateringHUD _hud;

    [Tooltip("Main camera (auto-found if null).")]
    [SerializeField] private Camera _mainCamera;

    [Header("Scoring")]
    [Tooltip("How long the score displays before returning to Idle.")]
    [SerializeField] private float _scoreDisplayTime = 2f;

    [Tooltip("Points deducted for overflow.")]
    [SerializeField] private float _overflowPenalty = 30f;

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

    // ── Public read-only API ─────────────────────────────────────────

    public State CurrentState { get; private set; } = State.Idle;
    public PlantDefinition CurrentPlant => _activePlant;
    public PotController Pot => _pot;

    [HideInInspector] public int lastScore;
    [HideInInspector] public float lastFillScore;
    [HideInInspector] public float lastOverflowScore;
    [HideInInspector] public float lastBonusScore;

    // ── Input ────────────────────────────────────────────────────────

    private InputAction _clickAction;
    private InputAction _mousePosition;

    // ── Runtime ──────────────────────────────────────────────────────

    private PlantDefinition _activePlant;
    private bool _overflowSFXPlayed;
    private bool _shownNearlyFull;
    private static bool s_hasWateredOnce;
    private float _scoreTimer;
    private float _pourTime;

    /// <summary>Current oscillating target level (updates each frame while pouring).</summary>
    public float OscillatingTarget { get; private set; }

    // ── Singleton lifecycle ──────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _clickAction = new InputAction("WaterClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePosition = new InputAction("WaterPointer", InputActionType.Value, "<Mouse>/position");

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        _clickAction.Enable();
        _mousePosition.Enable();
    }

    void OnDisable()
    {
        _clickAction.Disable();
        _mousePosition.Disable();
    }

    // ── Update dispatch ──────────────────────────────────────────────

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
            case State.Scoring:
                UpdateScoring();
                break;
        }
    }

    // ── Idle ─────────────────────────────────────────────────────────

    private void UpdateIdle()
    {
        Vector2 pointer = _mousePosition.ReadValue<Vector2>();

        if (_clickAction.WasPressedThisFrame())
        {
            Ray ray = _mainCamera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _plantLayer))
            {
                var plant = hit.collider.GetComponent<WaterablePlant>();
                if (plant == null)
                    plant = hit.collider.GetComponentInParent<WaterablePlant>();

                if (plant != null && plant.definition != null)
                {
                    if (plantClickSFX != null && AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFX(plantClickSFX);
                    ObjectGrabber.ConsumeClickExternal();
                    BeginPouring(plant.definition);
                }
            }
        }
    }

    private void BeginPouring(PlantDefinition def)
    {
        _activePlant = def;

        if (_pot != null)
        {
            _pot.definition = def;
            _pot.Clear();
        }

        _overflowSFXPlayed = false;
        _pourTime = 0f;
        OscillatingTarget = def.idealWaterLevel;
        if (_pot != null)
            _pot.TargetLevel = def.idealWaterLevel;
        CurrentState = State.Pouring;
        _shownNearlyFull = false;

        // First-time watering hint
        if (!s_hasWateredOnce)
        {
            s_hasWateredOnce = true;
            DialoguePortraitBox.Instance?.Say("Click and hold to pour. Let go when the pot's full!", 4f);
        }

        // Lock the pour overlay to the watering cursor so it matches the hover seamlessly
        if (PourCursorOverlay.Instance != null && GlobalCursorManager.Instance != null)
            PourCursorOverlay.Instance.LockTexture(GlobalCursorManager.Instance.GetCurrentCursorTexture());
        PourDragHelper.Begin();

        Debug.Log($"[WateringManager] Pouring: {def.plantName}");
    }

    // ── Pouring ──────────────────────────────────────────────────────

    private void UpdatePouring()
    {
        // Update oscillating target every frame
        if (_activePlant != null)
        {
            _pourTime += Time.deltaTime;
            float amp = _activePlant.targetOscAmplitude;
            float spd = _activePlant.targetOscSpeed;
            OscillatingTarget = _activePlant.idealWaterLevel
                + amp * Mathf.Sin(2f * Mathf.PI * spd * _pourTime);

            if (_pot != null)
                _pot.TargetLevel = OscillatingTarget;
        }

        if (_clickAction.IsPressed())
        {
            float dragRate = PourDragHelper.UpdateDrag();

            if (_pot != null && dragRate > 0f)
                _pot.Pour(Time.deltaTime * dragRate);

            // Nearly full hint (once per pour)
            if (_pot != null && !_shownNearlyFull && _pot.WaterLevel >= 0.7f)
            {
                _shownNearlyFull = true;
                DialoguePortraitBox.Instance?.Say("Getting close...", 1.5f);
            }

            // Overflow SFX (play once)
            if (_pot != null && _pot.Overflowed && !_overflowSFXPlayed)
            {
                if (AudioManager.Instance != null && overflowSFX != null)
                    AudioManager.Instance.PlaySFX(overflowSFX);
                _overflowSFXPlayed = true;
                DialoguePortraitBox.Instance?.Say("Oops, a little too much!", 2f);
            }
        }
        else
        {
            // Mouse released → finish pouring
            PourDragHelper.End();
            if (_pot != null)
                _pot.StopPouring();

            CalculateScore();
        }
    }

    // ── Scoring ──────────────────────────────────────────────────────

    private void CalculateScore()
    {
        if (_pot == null || _pot.definition == null)
        {
            CurrentState = State.Scoring;
            _scoreTimer = _scoreDisplayTime;
            return;
        }

        var def = _pot.definition;

        // Fill score (0-70): how close water is to the oscillating target at release
        float fillDist = Mathf.Abs(_pot.WaterLevel - OscillatingTarget);
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

        // Perfect / fail SFX based on score threshold
        bool isPerfect = !_pot.Overflowed && fillNorm > 0.9f;
        if (isPerfect && perfectSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(perfectSFX);
        else if (!isPerfect && lastScore < 30 && failSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(failSFX);

        // Voice reaction
        if (!_pot.Overflowed && fillDist <= 0.15f)
            DialoguePortraitBox.Instance?.Say("Perfect! That's just right.", 2f);

        CurrentState = State.Scoring;
        _scoreTimer = _scoreDisplayTime;

        Debug.Log($"[WateringManager] Score: {lastScore} (fill={lastFillScore:F0} bonus={lastBonusScore:F0} overflow={lastOverflowScore:F0})");
    }

    private void UpdateScoring()
    {
        // Allow clicking the next plant to interrupt the score display
        if (_clickAction.WasPressedThisFrame())
        {
            Vector2 pointer = _mousePosition.ReadValue<Vector2>();
            Ray ray = _mainCamera.ScreenPointToRay(pointer);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _plantLayer))
            {
                var plant = hit.collider.GetComponent<WaterablePlant>();
                if (plant == null)
                    plant = hit.collider.GetComponentInParent<WaterablePlant>();

                if (plant != null && plant.definition != null)
                {
                    if (plantClickSFX != null && AudioManager.Instance != null)
                        AudioManager.Instance.PlaySFX(plantClickSFX);
                    BeginPouring(plant.definition);
                    return;
                }
            }
        }

        _scoreTimer -= Time.deltaTime;
        if (_scoreTimer <= 0f)
        {
            _activePlant = null;
            CurrentState = State.Idle;

            Debug.Log("[WateringManager] Returned to Idle.");
        }
    }

    /// <summary>Force back to idle. Called on phase transitions to close HUD.</summary>
    public void ForceIdle()
    {
        _activePlant = null;
        CurrentState = State.Idle;
    }
}
