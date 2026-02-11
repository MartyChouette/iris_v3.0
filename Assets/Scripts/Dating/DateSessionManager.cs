using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene-scoped singleton orchestrating the date lifecycle: waiting for arrival,
/// tracking affection during the date, handling reactions, and ending with a score.
/// </summary>
public class DateSessionManager : MonoBehaviour
{
    public static DateSessionManager Instance { get; private set; }

    public enum SessionState { Idle, WaitingForArrival, DateInProgress, DateEnding }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Affection")]
    [Tooltip("Starting affection value (0-100 scale).")]
    [SerializeField] private float startingAffection = 50f;

    [Tooltip("Affection multiplier when mood matches date's preferences.")]
    [SerializeField] private float moodMatchMultiplier = 1.5f;

    [Tooltip("Affection multiplier when mood is outside date's preferences.")]
    [SerializeField] private float moodMismatchMultiplier = 0.5f;

    [Header("Reaction Values")]
    [Tooltip("Affection gained from a Like reaction.")]
    [SerializeField] private float likeAffection = 5f;

    [Tooltip("Affection gained from a Neutral reaction.")]
    [SerializeField] private float neutralAffection = 0.5f;

    [Tooltip("Affection lost from a Dislike reaction.")]
    [SerializeField] private float dislikeAffection = -4f;

    [Header("Ambient Check")]
    [Tooltip("Seconds between ambient mood evaluations.")]
    [SerializeField] private float moodCheckInterval = 15f;

    [Tooltip("Affection drift per check when mood matches.")]
    [SerializeField] private float ambientMoodDrift = 0.5f;

    [Header("References")]
    [Tooltip("Where the date character spawns (apartment entrance).")]
    [SerializeField] private Transform dateSpawnPoint;

    [Tooltip("Where the date character sits (couch seat target).")]
    [SerializeField] private Transform couchSeatTarget;

    [Tooltip("Where drinks are delivered (coffee table).")]
    [SerializeField] private Transform coffeeTableDeliveryPoint;

    [Header("Events")]
    public UnityEvent<DatePersonalDefinition> OnDateSessionStarted;
    public UnityEvent<float> OnAffectionChanged;
    public UnityEvent<DatePersonalDefinition, float> OnDateSessionEnded;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    private SessionState _state = SessionState.Idle;
    private DatePersonalDefinition _currentDate;
    private float _affection;
    private float _moodCheckTimer;
    private DateCharacterController _dateCharacter;
    private GameObject _dateCharacterGO;
    private float _arrivalTimer;
    private bool _arrivalTimerActive;

    // ──────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────
    public SessionState CurrentState => _state;
    public DatePersonalDefinition CurrentDate => _currentDate;
    public float Affection => _affection;
    public bool IsDateActive => _state == SessionState.DateInProgress;

    // ──────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DateSessionManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Arrival timer — ticks during WaitingForArrival
        if (_state == SessionState.WaitingForArrival && _arrivalTimerActive)
        {
            _arrivalTimer -= Time.deltaTime;
            if (_arrivalTimer <= 0f)
            {
                _arrivalTimer = 0f;
                _arrivalTimerActive = false;
                TriggerDateArrival();
            }
        }

        if (_state != SessionState.DateInProgress) return;

        // Periodic mood check
        _moodCheckTimer += Time.deltaTime;
        if (_moodCheckTimer >= moodCheckInterval)
        {
            _moodCheckTimer = 0f;
            EvaluateAmbientMood();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Session Flow
    // ──────────────────────────────────────────────────────────────

    /// <summary>Called after newspaper ad is cut — date is on the way. Starts arrival timer.</summary>
    public void ScheduleDate(DatePersonalDefinition date)
    {
        _currentDate = date;
        _state = SessionState.WaitingForArrival;
        _arrivalTimer = date.arrivalTimeSec;
        _arrivalTimerActive = true;
        Debug.Log($"[DateSessionManager] Scheduled date with {date.characterName}. Arriving in {date.arrivalTimeSec}s.");
    }

    /// <summary>Called when the arrival timer expires — triggers phone ring or direct arrival.</summary>
    private void TriggerDateArrival()
    {
        Debug.Log($"[DateSessionManager] {_currentDate?.characterName} is arriving!");

        if (PhoneController.Instance != null)
            PhoneController.Instance.StartRinging();
        else
            OnDateCharacterArrived();
    }

    /// <summary>Called when the date character has arrived (phone answered or doorbell).</summary>
    public void OnDateCharacterArrived()
    {
        if (_currentDate == null)
        {
            Debug.LogWarning("[DateSessionManager] No current date set.");
            return;
        }

        _state = SessionState.DateInProgress;
        _affection = startingAffection;
        _moodCheckTimer = 0f;

        // Spawn character
        SpawnDateCharacter();

        OnDateSessionStarted?.Invoke(_currentDate);
        OnAffectionChanged?.Invoke(_affection);
        Debug.Log($"[DateSessionManager] Date with {_currentDate.characterName} started! Affection: {_affection}");
    }

    /// <summary>Apply a reaction to affection (called by DateCharacterController or drink delivery).</summary>
    public void ApplyReaction(ReactionType type, float magnitude = 1f)
    {
        if (_state != SessionState.DateInProgress || _currentDate == null) return;

        float delta = type switch
        {
            ReactionType.Like => likeAffection,
            ReactionType.Neutral => neutralAffection,
            ReactionType.Dislike => dislikeAffection,
            _ => 0f
        };

        delta *= magnitude * GetMoodMultiplier() * _currentDate.preferences.reactionStrength;
        _affection = Mathf.Clamp(_affection + delta, 0f, 100f);

        OnAffectionChanged?.Invoke(_affection);
        Debug.Log($"[DateSessionManager] Reaction: {type} (delta={delta:+0.0;-0.0}) → Affection: {_affection:F1}");
    }

    /// <summary>Called when a drink is delivered to the coffee table.</summary>
    public void ReceiveDrink(DrinkRecipeDefinition recipe, int score)
    {
        if (_state != SessionState.DateInProgress || _currentDate == null) return;

        var reactionType = ReactionEvaluator.EvaluateDrink(recipe, score, _currentDate.preferences);
        float magnitude = score / 100f;

        ApplyReaction(reactionType, magnitude);

        // Have the date character investigate the drink
        if (_dateCharacter != null && coffeeTableDeliveryPoint != null)
            _dateCharacter.InvestigateSpecific(coffeeTableDeliveryPoint);

        Debug.Log($"[DateSessionManager] Drink received: {recipe?.drinkName} (score={score}) → {reactionType}");
    }

    /// <summary>End the current date session and show results.</summary>
    public void EndDate()
    {
        if (_state == SessionState.Idle || _state == SessionState.DateEnding) return;

        _state = SessionState.DateEnding;
        Debug.Log($"[DateSessionManager] Ending date with {_currentDate?.characterName}. Final affection: {_affection:F1}");

        // Record history
        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = _currentDate?.characterName ?? "Unknown",
            day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 0,
            affection = _affection,
            grade = DateEndScreen.ComputeGrade(_affection)
        });

        // Dismiss character
        if (_dateCharacter != null && dateSpawnPoint != null)
        {
            _dateCharacter.Dismiss(dateSpawnPoint, () =>
            {
                if (_dateCharacterGO != null)
                    Destroy(_dateCharacterGO);
                _dateCharacterGO = null;
                _dateCharacter = null;
            });
        }

        OnDateSessionEnded?.Invoke(_currentDate, _affection);

        // Show end screen
        DateEndScreen.Instance?.Show(_currentDate, _affection);

        _state = SessionState.Idle;
    }

    // ──────────────────────────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────────────────────────

    private void SpawnDateCharacter()
    {
        if (_currentDate.characterModelPrefab != null)
        {
            Vector3 spawnPos = dateSpawnPoint != null ? dateSpawnPoint.position : Vector3.zero;
            _dateCharacterGO = Instantiate(_currentDate.characterModelPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Fallback: create a capsule placeholder
            _dateCharacterGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dateCharacterGO.name = $"Date_{_currentDate.characterName}";
            _dateCharacterGO.transform.position = dateSpawnPoint != null ? dateSpawnPoint.position : Vector3.zero;
        }

        // Add character controller
        _dateCharacter = _dateCharacterGO.GetComponent<DateCharacterController>();
        if (_dateCharacter == null)
            _dateCharacter = _dateCharacterGO.AddComponent<DateCharacterController>();

        // Add reaction UI
        var reactionUI = _dateCharacterGO.GetComponent<DateReactionUI>();
        if (reactionUI == null)
            reactionUI = _dateCharacterGO.AddComponent<DateReactionUI>();

        // Add NavMeshAgent if missing
        var agent = _dateCharacterGO.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null)
            agent = _dateCharacterGO.AddComponent<UnityEngine.AI.NavMeshAgent>();

        // Initialize
        Vector3 spawnPosition = dateSpawnPoint != null ? dateSpawnPoint.position : Vector3.zero;
        Transform couch = couchSeatTarget != null ? couchSeatTarget : transform;
        _dateCharacter.Initialize(couch, spawnPosition);

        // Subscribe to reactions
        _dateCharacter.OnReaction += HandleCharacterReaction;
    }

    private void HandleCharacterReaction(ReactableTag tag, ReactionType type)
    {
        ApplyReaction(type);

        // Show reaction bubble on the character
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();
        reactionUI?.ShowReaction(type);
    }

    private void EvaluateAmbientMood()
    {
        if (_currentDate == null) return;

        float mood = MoodMachine.Instance?.Mood ?? 0f;
        var moodReaction = ReactionEvaluator.EvaluateMood(mood, _currentDate.preferences);

        if (moodReaction == ReactionType.Like)
        {
            _affection = Mathf.Clamp(_affection + ambientMoodDrift, 0f, 100f);
            OnAffectionChanged?.Invoke(_affection);
        }
        else if (moodReaction == ReactionType.Dislike)
        {
            _affection = Mathf.Clamp(_affection - ambientMoodDrift * 0.5f, 0f, 100f);
            OnAffectionChanged?.Invoke(_affection);
        }
    }

    private float GetMoodMultiplier()
    {
        if (_currentDate == null) return 1f;

        float mood = MoodMachine.Instance?.Mood ?? 0f;
        var prefs = _currentDate.preferences;

        if (mood >= prefs.preferredMoodMin && mood <= prefs.preferredMoodMax)
            return moodMatchMultiplier;

        return moodMismatchMultiplier;
    }
}
