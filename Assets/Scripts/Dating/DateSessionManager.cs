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

    /// <summary>
    /// Sub-phases within DateInProgress:
    ///   Arrival         — NPC walks in, sits on couch
    ///   DrinkJudging    — Player makes and delivers a drink, NPC judges it
    ///   ApartmentJudging— NPC wanders, investigates ReactableTags, judges apartment
    /// </summary>
    public enum DatePhase { None, Arrival, DrinkJudging, ApartmentJudging }

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

    [Header("Date Phases")]
    [Tooltip("Seconds the apartment judging phase lasts before the date ends.")]
    [SerializeField] private float apartmentJudgingDuration = 60f;

    [Header("Ambient Check")]
    [Tooltip("Seconds between ambient mood evaluations.")]
    [SerializeField] private float moodCheckInterval = 15f;

    [Tooltip("Affection drift per check when mood matches.")]
    [SerializeField] private float ambientMoodDrift = 0.5f;

    [Header("Audio")]
    [Tooltip("SFX played when the date character arrives.")]
    [SerializeField] private AudioClip dateArrivedSFX;

    [Tooltip("SFX played on a Like reaction.")]
    [SerializeField] private AudioClip likeSFX;

    [Tooltip("SFX played on a Dislike reaction.")]
    [SerializeField] private AudioClip dislikeSFX;

    [Tooltip("SFX played when transitioning to a new date phase.")]
    [SerializeField] private AudioClip phaseTransitionSFX;

    [Header("References")]
    [Tooltip("Where the date character spawns (apartment entrance).")]
    [SerializeField] private Transform dateSpawnPoint;

    [Tooltip("Where the date character sits (couch seat target).")]
    [SerializeField] private Transform couchSeatTarget;

    [Tooltip("Where drinks are delivered (coffee table).")]
    [SerializeField] private Transform coffeeTableDeliveryPoint;

    [Tooltip("Where the NPC pauses for entrance judgments (between entrance and couch).")]
    [SerializeField] private Transform judgmentStopPoint;

    [Tooltip("Runs the 3 entrance judgments (outfit, mood, cleanliness).")]
    [SerializeField] private EntranceJudgmentSequence _entranceJudgments;

    [Header("Events")]
    public UnityEvent<DatePersonalDefinition> OnDateSessionStarted;
    public UnityEvent<float> OnAffectionChanged;
    public UnityEvent<DatePersonalDefinition, float> OnDateSessionEnded;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    private SessionState _state = SessionState.Idle;
    private DatePhase _datePhase = DatePhase.None;
    private DatePersonalDefinition _currentDate;
    private float _affection;
    private float _moodCheckTimer;
    private DateCharacterController _dateCharacter;
    private GameObject _dateCharacterGO;
    private float _arrivalTimer;
    private bool _arrivalTimerActive;
    private float _apartmentJudgingTimer;

    // ──────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────
    public SessionState CurrentState => _state;
    public DatePhase CurrentDatePhase => _datePhase;
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

        // Apartment judging timer — auto-end date when it expires
        if (_datePhase == DatePhase.ApartmentJudging)
        {
            _apartmentJudgingTimer += Time.deltaTime;
            if (_apartmentJudgingTimer >= apartmentJudgingDuration)
            {
                Debug.Log("[DateSessionManager] Apartment judging phase complete.");
                EndDate();
                return;
            }
        }

        // Periodic mood check (only during ApartmentJudging)
        if (_datePhase == DatePhase.ApartmentJudging)
        {
            _moodCheckTimer += Time.deltaTime;
            if (_moodCheckTimer >= moodCheckInterval)
            {
                _moodCheckTimer = 0f;
                EvaluateAmbientMood();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Session Flow
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called after newspaper ad is selected — date is pending.
    /// Arrival is triggered externally by DayPhaseManager (prep timer expired)
    /// or PhoneController (player clicks phone to end prep early).
    /// </summary>
    public void ScheduleDate(DatePersonalDefinition date)
    {
        _currentDate = date;
        _state = SessionState.WaitingForArrival;
        _arrivalTimerActive = false; // Arrival now controlled by prep timer, not internal timer
        Debug.Log($"[DateSessionManager] Scheduled date with {date.characterName}. Waiting for prep phase to end.");
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
        _datePhase = DatePhase.Arrival;
        _affection = startingAffection;
        _moodCheckTimer = 0f;
        _apartmentJudgingTimer = 0f;

        // Spawn character
        SpawnDateCharacter();

        if (dateArrivedSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(dateArrivedSFX);

        OnDateSessionStarted?.Invoke(_currentDate);
        OnAffectionChanged?.Invoke(_affection);
        Debug.Log($"[DateSessionManager] Phase 1: Arrival — {_currentDate.characterName} walking in.");
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

        // Reaction SFX
        if (type == ReactionType.Like && likeSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(likeSFX);
        else if (type == ReactionType.Dislike && dislikeSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(dislikeSFX);

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

        Debug.Log($"[DateSessionManager] Phase 2 → 3: Drink received: {recipe?.drinkName} (score={score}) → {reactionType}");

        // Transition to Phase 3: Apartment Judging
        EnterApartmentJudging();
    }

    // ──────────────────────────────────────────────────────────────
    // Date Phase Transitions
    // ──────────────────────────────────────────────────────────────

    private void OnCharacterReachedJudgmentPoint()
    {
        if (_datePhase != DatePhase.Arrival) return;
        Debug.Log("[DateSessionManager] NPC reached judgment point — running entrance judgments.");
        StartCoroutine(RunEntranceJudgmentsAndContinue());
    }

    private IEnumerator RunEntranceJudgmentsAndContinue()
    {
        if (_entranceJudgments != null && _currentDate != null)
        {
            var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();
            yield return _entranceJudgments.RunJudgments(reactionUI, _currentDate);
        }

        // Resume walking to couch
        if (_dateCharacter != null)
            _dateCharacter.ContinueToCouch();
    }

    private void OnCharacterSatDown()
    {
        if (_datePhase != DatePhase.Arrival) return;

        _datePhase = DatePhase.DrinkJudging;

        if (phaseTransitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phaseTransitionSFX);

        Debug.Log("[DateSessionManager] Phase 2: DrinkJudging — make a drink and deliver it!");
    }

    private void EnterApartmentJudging()
    {
        _datePhase = DatePhase.ApartmentJudging;
        _apartmentJudgingTimer = 0f;
        _moodCheckTimer = 0f;

        if (phaseTransitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phaseTransitionSFX);

        // Enable NPC excursions so they walk around and judge items
        if (_dateCharacter != null)
            _dateCharacter.EnableExcursions();

        Debug.Log("[DateSessionManager] Phase 3: ApartmentJudging — NPC exploring apartment.");
    }

    /// <summary>End the current date session and show results.</summary>
    public void EndDate()
    {
        if (_state == SessionState.Idle || _state == SessionState.DateEnding) return;

        _state = SessionState.DateEnding;
        _datePhase = DatePhase.None;
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
        _dateCharacter.Initialize(couch, spawnPosition, judgmentStopPoint);

        // Subscribe to reactions and sat-down event
        _dateCharacter.OnReaction += HandleCharacterReaction;
        _dateCharacter.OnSatDown += OnCharacterSatDown;
        _dateCharacter.OnReachedJudgmentPoint += OnCharacterReachedJudgmentPoint;
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
