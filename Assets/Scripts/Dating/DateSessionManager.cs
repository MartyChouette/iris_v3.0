using System.Collections;
using System.Collections.Generic;
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
    ///   Arrival           — NPC walks in, entrance judgments, sits on couch
    ///   BackgroundJudging — NPC excursions run in parallel with drink making
    ///   Reveal            — NPC sits, accumulated reactions replayed one-by-one
    /// </summary>
    public enum DatePhase { None, Arrival, BackgroundJudging, Reveal }

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

    [Header("Fail Thresholds")]
    [Tooltip("Affection below this after Arrival → NPC leaves.")]
    [SerializeField] private float _arrivalFailThreshold = 25f;

    [Tooltip("Affection below this after drink delivery → NPC leaves.")]
    [SerializeField] private float _bgJudgingFailThreshold = 20f;

    [Tooltip("Affection below this after Reveal → NPC leaves without flower.")]
    [SerializeField] private float _revealFailThreshold = 30f;

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
    // Accumulated reactions (replayed during Reveal)
    // ──────────────────────────────────────────────────────────────
    public struct AccumulatedReaction
    {
        public string itemName;
        public ReactionType type;
    }

    /// <summary>Flower prefab stashed after a successful date for the flower trimming scene.</summary>
    public static GameObject PendingFlowerPrefab { get; set; }

    /// <summary>Fired for each reaction during the Reveal phase replay.</summary>
    public event System.Action<AccumulatedReaction> OnRevealReaction;

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
    private readonly List<AccumulatedReaction> _accumulatedReactions = new();

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

        // Periodic mood check during BackgroundJudging
        if (_datePhase == DatePhase.BackgroundJudging)
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
        _accumulatedReactions.Clear();

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

        Debug.Log($"[DateSessionManager] Drink received: {recipe?.drinkName} (score={score}) → {reactionType}");

        // Fail check after drink
        if (CheckPhaseFailAndExit(_bgJudgingFailThreshold)) return;

        // Transition to Reveal phase
        EnterReveal();
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

        // Fail check after entrance judgments
        if (CheckPhaseFailAndExit(_arrivalFailThreshold)) return;

        _datePhase = DatePhase.BackgroundJudging;
        _moodCheckTimer = 0f;
        _accumulatedReactions.Clear();

        // Enable excursions immediately — NPC wanders while player makes drink
        if (_dateCharacter != null)
            _dateCharacter.EnableExcursions();

        if (phaseTransitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phaseTransitionSFX);

        Debug.Log("[DateSessionManager] Phase 2: BackgroundJudging — NPC exploring while player makes drink.");
    }

    private void EnterReveal()
    {
        _datePhase = DatePhase.Reveal;

        // Disable excursions — NPC sits on couch for reveal
        if (_dateCharacter != null)
            _dateCharacter.DisableExcursions();

        if (phaseTransitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phaseTransitionSFX);

        Debug.Log("[DateSessionManager] Phase 3: Reveal — replaying accumulated reactions.");
        StartCoroutine(RunRevealSequence());
    }

    /// <summary>Public safety fallback (e.g. GameClock bed time). Routes to fail or succeed based on affection.</summary>
    public void EndDate()
    {
        if (_state == SessionState.Idle || _state == SessionState.DateEnding) return;

        if (_affection < _revealFailThreshold)
            FailDate();
        else
            SucceedDate();
    }

    /// <summary>Returns true (and triggers failure) if affection is below threshold.</summary>
    private bool CheckPhaseFailAndExit(float threshold)
    {
        if (_affection < threshold)
        {
            Debug.Log($"[DateSessionManager] Affection {_affection:F1} < {threshold} — date failed!");
            FailDate();
            return true;
        }
        return false;
    }

    private void FailDate()
    {
        if (_state == SessionState.Idle || _state == SessionState.DateEnding) return;

        _state = SessionState.DateEnding;
        _datePhase = DatePhase.None;
        Debug.Log($"[DateSessionManager] Date FAILED with {_currentDate?.characterName}. Affection: {_affection:F1}");

        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = _currentDate?.characterName ?? "Unknown",
            day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 0,
            affection = _affection,
            grade = "F"
        });

        DismissCharacter();
        OnDateSessionEnded?.Invoke(_currentDate, _affection);
        DateEndScreen.Instance?.Show(_currentDate, _affection, failed: true);
        AutoSaveController.Instance?.PerformSave("date_failed");
        _state = SessionState.Idle;
    }

    private void SucceedDate()
    {
        if (_state == SessionState.Idle || _state == SessionState.DateEnding) return;

        _state = SessionState.DateEnding;
        _datePhase = DatePhase.None;
        Debug.Log($"[DateSessionManager] Date SUCCEEDED with {_currentDate?.characterName}. Affection: {_affection:F1}");

        DateHistory.Record(new DateHistory.DateHistoryEntry
        {
            name = _currentDate?.characterName ?? "Unknown",
            day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 0,
            affection = _affection,
            grade = DateEndScreen.ComputeGrade(_affection)
        });

        // Stash flower prefab for the flower trimming scene
        if (_currentDate != null && _currentDate.flowerPrefab != null)
            PendingFlowerPrefab = _currentDate.flowerPrefab;

        DismissCharacter();
        OnDateSessionEnded?.Invoke(_currentDate, _affection);
        DateEndScreen.Instance?.Show(_currentDate, _affection, failed: false);
        AutoSaveController.Instance?.PerformSave("date_succeeded");
        _state = SessionState.Idle;
    }

    private void DismissCharacter()
    {
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

        // Accumulate during BackgroundJudging for Reveal replay
        if (_datePhase == DatePhase.BackgroundJudging && tag != null)
        {
            _accumulatedReactions.Add(new AccumulatedReaction
            {
                itemName = tag.gameObject.name,
                type = type
            });
        }
    }

    private IEnumerator RunRevealSequence()
    {
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();

        yield return new WaitForSeconds(1f);

        foreach (var reaction in _accumulatedReactions)
        {
            reactionUI?.ShowReaction(reaction.type);
            OnRevealReaction?.Invoke(reaction);
            Debug.Log($"[DateSessionManager] Reveal: {reaction.itemName} → {reaction.type}");
            yield return new WaitForSeconds(2f);
        }

        yield return new WaitForSeconds(1f);

        // Final fail check
        if (CheckPhaseFailAndExit(_revealFailThreshold))
            yield break;

        SucceedDate();
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
