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

    // Input managed by IrisInput singleton

    // ── Runtime ─────────────────────────────────────────────────

    private PlantDefinition _activePlant;
    private WaterablePlant _activeWaterablePlant;
    private bool _overflowSFXPlayed;
    private float _scoreTimer;

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
        CurrentState = State.Pouring;

        Debug.Log($"[WateringManager] Pouring: {_activePlant.plantName}");
    }

    // ── Pouring ─────────────────────────────────────────────────

    private void UpdatePouring()
    {
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

        // Moisture score (0-70): how close soil moisture is to perfect
        float accuracy = _pot.MoistureAccuracy;
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

        Debug.Log($"[WateringManager] Score: {lastScore} (moisture={_pot.SoilMoisture:F2}, " +
                  $"perfect={def.perfectMoisture:F2}, accuracy={accuracy:F2})");
    }

    private void UpdateScoring()
    {
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
        _activePlant = null;
        _activeWaterablePlant = null;
        CurrentState = State.Idle;
    }
}
