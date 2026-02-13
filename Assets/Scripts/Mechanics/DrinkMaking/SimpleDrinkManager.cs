using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple perfect-pour drink station — pick a recipe, click the glass, hold to pour, release to score.
/// Mirrors WateringManager. Implements IStationManager for apartment integration.
/// States: ChoosingRecipe → Pouring → Scoring.
/// </summary>
[DisallowMultipleComponent]
public class SimpleDrinkManager : MonoBehaviour, IStationManager
{
    public static SimpleDrinkManager Instance { get; private set; }

    public enum State { ChoosingRecipe, Pouring, Scoring }

    [Header("References")]
    [Tooltip("Available recipes the player can choose from.")]
    [SerializeField] private DrinkRecipeDefinition[] availableRecipes;

    [Tooltip("Layer mask for the glass collider (click target).")]
    [SerializeField] private LayerMask _glassLayer;

    [Tooltip("Main camera (auto-found if null).")]
    [SerializeField] private Camera _mainCamera;

    [Tooltip("HUD reference for state-driven display.")]
    [SerializeField] private SimpleDrinkHUD _hud;

    [Header("Scoring")]
    [Tooltip("How long the score displays before returning to ChoosingRecipe.")]
    [SerializeField] private float _scoreDisplayTime = 2f;

    [Tooltip("Points deducted for overflow.")]
    [SerializeField] private float _overflowPenalty = 30f;

    [Header("Audio")]
    public AudioClip pourSFX;
    public AudioClip overflowSFX;
    public AudioClip scoreSFX;

    // ── Public read-only API ─────────────────────────────────────────

    public State CurrentState { get; private set; } = State.ChoosingRecipe;
    public DrinkRecipeDefinition ActiveRecipe => _activeRecipe;
    public DrinkRecipeDefinition[] AvailableRecipes => availableRecipes;
    public float FillLevel => _fillLevel;
    public float FoamLevel => _foamLevel;
    public bool Overflowed => _overflowed;
    public bool PourStarted => _pourStarted;

    public bool IsAtIdleState => CurrentState == State.ChoosingRecipe;

    [HideInInspector] public int lastScore;
    [HideInInspector] public float lastFillScore;
    [HideInInspector] public float lastBonusScore;
    [HideInInspector] public float lastOverflowScore;

    // ── Input ────────────────────────────────────────────────────────

    private InputAction _clickAction;
    private InputAction _mousePosition;

    // ── Runtime ──────────────────────────────────────────────────────

    private DrinkRecipeDefinition _activeRecipe;
    private float _fillLevel;
    private float _foamLevel;
    private bool _overflowed;
    private bool _overflowSFXPlayed;
    private bool _pourStarted;
    private float _scoreTimer;

    // ── Singleton lifecycle ──────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _clickAction = new InputAction("DrinkClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePosition = new InputAction("DrinkPointer", InputActionType.Value, "<Mouse>/position");

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
        if (_mainCamera == null) return;

        switch (CurrentState)
        {
            case State.ChoosingRecipe:
                // UI buttons handle recipe selection via SelectRecipe()
                break;
            case State.Pouring:
                UpdatePouring();
                break;
            case State.Scoring:
                UpdateScoring();
                break;
        }
    }

    // ── Recipe selection (called by UI buttons) ──────────────────────

    public void SelectRecipe(int index)
    {
        if (CurrentState != State.ChoosingRecipe) return;
        if (availableRecipes == null || index < 0 || index >= availableRecipes.Length) return;

        _activeRecipe = availableRecipes[index];
        _fillLevel = 0f;
        _foamLevel = 0f;
        _overflowed = false;
        _overflowSFXPlayed = false;
        _pourStarted = false;
        CurrentState = State.Pouring;

        Debug.Log($"[SimpleDrinkManager] Selected recipe: {_activeRecipe.drinkName}");
    }

    // ── Pouring ──────────────────────────────────────────────────────

    private void UpdatePouring()
    {
        // First click must hit the glass to begin pouring
        if (!_pourStarted)
        {
            if (_clickAction.WasPressedThisFrame())
            {
                Vector2 pointer = _mousePosition.ReadValue<Vector2>();
                Ray ray = _mainCamera.ScreenPointToRay(pointer);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, _glassLayer))
                {
                    _pourStarted = true;
                    Debug.Log("[SimpleDrinkManager] Pour started (glass clicked).");
                }
            }
            return;
        }

        if (_clickAction.IsPressed())
        {
            float dt = Time.deltaTime;
            float rate = _activeRecipe != null ? _activeRecipe.pourRate : 0.15f;
            float foamMult = _activeRecipe != null ? _activeRecipe.foamRateMultiplier : 2f;

            _fillLevel += rate * dt;
            _foamLevel += rate * foamMult * dt;

            // Clamp fill
            if (_fillLevel > 1f)
            {
                _fillLevel = 1f;
                _overflowed = true;
            }

            // Clamp foam
            _foamLevel = Mathf.Clamp01(_foamLevel);

            // Overflow detection
            if (_foamLevel >= 1f)
                _overflowed = true;

            // Overflow SFX (play once)
            if (_overflowed && !_overflowSFXPlayed)
            {
                if (AudioManager.Instance != null && overflowSFX != null)
                    AudioManager.Instance.PlaySFX(overflowSFX);
                _overflowSFXPlayed = true;
            }
        }
        else
        {
            // Foam settles when not pouring
            float settleRate = _activeRecipe != null ? _activeRecipe.foamSettleRate : 0.25f;
            _foamLevel = Mathf.Max(0f, _foamLevel - settleRate * Time.deltaTime);

            // Mouse released after pouring began → score
            if (_clickAction.WasReleasedThisFrame() && _fillLevel > 0f)
            {
                CalculateScore();
            }
        }
    }

    // ── Scoring ──────────────────────────────────────────────────────

    private void CalculateScore()
    {
        if (_activeRecipe == null)
        {
            CurrentState = State.Scoring;
            _scoreTimer = _scoreDisplayTime;
            return;
        }

        // Fill score (0-70): how close fill is to ideal level
        float fillDist = Mathf.Abs(_fillLevel - _activeRecipe.idealFillLevel);
        float fillNorm = Mathf.Clamp01(1f - fillDist / Mathf.Max(_activeRecipe.fillTolerance, 0.001f));
        lastFillScore = fillNorm * 70f;

        // Overflow penalty
        lastOverflowScore = _overflowed ? -_overflowPenalty : 0f;

        // Bonus (0-30): extra points for near-perfect without overflow
        if (!_overflowed && fillNorm > 0.9f)
            lastBonusScore = 30f;
        else
            lastBonusScore = fillNorm * 30f;

        float raw = lastFillScore + lastBonusScore + lastOverflowScore;
        lastScore = Mathf.Clamp((int)raw, 0, _activeRecipe.baseScore);

        if (AudioManager.Instance != null && scoreSFX != null)
            AudioManager.Instance.PlaySFX(scoreSFX);

        // Deliver drink to coffee table
        CoffeeTableDelivery.Instance?.DeliverDrink(_activeRecipe, _activeRecipe.liquidColor, lastScore);

        CurrentState = State.Scoring;
        _scoreTimer = _scoreDisplayTime;

        Debug.Log($"[SimpleDrinkManager] Score: {lastScore} (fill={lastFillScore:F0} bonus={lastBonusScore:F0} overflow={lastOverflowScore:F0})");
    }

    private void UpdateScoring()
    {
        _scoreTimer -= Time.deltaTime;
        if (_scoreTimer <= 0f)
        {
            _activeRecipe = null;
            CurrentState = State.ChoosingRecipe;

            Debug.Log("[SimpleDrinkManager] Returned to ChoosingRecipe.");
        }
    }
}
