using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene-scoped singleton orchestrating the date lifecycle.
/// Phases use fade-to-black + teleport (no NPC walking).
///   Phase 1: NPC at entrance — entrance judgments
///   Phase 2: NPC at kitchen — player makes drink, NPC judges
///   Phase 3: NPC on couch — seated excursions evaluate apartment items
/// </summary>
public class DateSessionManager : MonoBehaviour
{
    public static DateSessionManager Instance { get; private set; }

    public enum SessionState { Idle, WaitingForArrival, DateInProgress, DateEnding }

    /// <summary>
    /// Sub-phases within DateInProgress:
    ///   Arrival           — NPC at entrance, entrance judgments
    ///   BackgroundJudging — NPC at kitchen, player makes drink
    ///   Reveal            — NPC on couch, seated excursions
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

    [Tooltip("Affection below this after Phase 3 → NPC leaves without flower.")]
    [SerializeField] private float _revealFailThreshold = 30f;

    [Header("Ambient Check")]
    [Tooltip("Seconds between ambient mood evaluations.")]
    [SerializeField] private float moodCheckInterval = 15f;

    [Tooltip("Affection drift per check when mood matches.")]
    [SerializeField] private float ambientMoodDrift = 0.5f;

    [Header("Phase 3 Timing")]
    [Tooltip("Duration of Phase 3 (couch judging) in seconds before the date ends.")]
    [SerializeField] private float phase3Duration = 40f;

    [Header("Fade Timing")]
    [Tooltip("Fade duration for phase transitions (seconds).")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Tooltip("Seconds to show phase title on black screen.")]
    [SerializeField] private float phaseTitleHold = 1.0f;

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

    [Tooltip("Where the NPC stands for entrance judgments.")]
    [SerializeField] private Transform judgmentStopPoint;

    [Tooltip("Where the NPC stands during the kitchen/drink phase.")]
    [SerializeField] private Transform kitchenStandPoint;

    [Tooltip("Runs the entrance judgments (music, perfume, outfit, cleanliness).")]
    [SerializeField] private EntranceJudgmentSequence _entranceJudgments;

    [Header("Events")]
    public UnityEvent<DatePersonalDefinition> OnDateSessionStarted;
    public UnityEvent<float> OnAffectionChanged;
    public UnityEvent<DatePersonalDefinition, float> OnDateSessionEnded;

    // ──────────────────────────────────────────────────────────────
    // Accumulated reactions
    // ──────────────────────────────────────────────────────────────
    public struct AccumulatedReaction
    {
        public string itemName;
        public ReactionType type;
    }

    /// <summary>True when a successful date should trigger flower trimming before evening.</summary>
    public static bool PendingFlowerTrim { get; set; }

    /// <summary>Fired for each reaction (HUD display).</summary>
    public event System.Action<AccumulatedReaction> OnRevealReaction;

    // ──────────────────────────────────────────────────────────────
    // Phase transition dialogue
    // ──────────────────────────────────────────────────────────────
    private static readonly string[] s_prePhase2Lines = { "Why don't we go to the kitchen?", "I could use a drink..." };
    private static readonly string[] s_postPhase2Lines = { "Make me something good!", "What are you pouring?" };
    private static readonly string[] s_prePhase3Lines = { "Let's sit down for a bit.", "Show me the living room!" };
    private static readonly string[] s_postPhase3Lines = { "Nice place you've got here...", "Let me look around." };

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
    public DateCharacterController DateCharacter => _dateCharacter;

    // Debug read-only accessors
    public float StartingAffection => startingAffection;
    public float MoodMatchMultiplier => moodMatchMultiplier;
    public float MoodMismatchMultiplier => moodMismatchMultiplier;
    public float ArrivalFailThreshold => _arrivalFailThreshold;
    public float BgJudgingFailThreshold => _bgJudgingFailThreshold;
    public float RevealFailThreshold => _revealFailThreshold;
    public IReadOnlyList<AccumulatedReaction> AccumulatedReactions => _accumulatedReactions;
    public float ArrivalTimer => _arrivalTimer;
    public bool ArrivalTimerActive => _arrivalTimerActive;

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
        if (_state == SessionState.WaitingForArrival && _arrivalTimerActive && !DateDebugOverlay.IsTimePaused)
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

        // Periodic mood check during BackgroundJudging and Reveal
        if (_datePhase == DatePhase.BackgroundJudging || _datePhase == DatePhase.Reveal)
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
        _arrivalTimerActive = false;
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

    /// <summary>Called when the player answers the door. Starts the date.</summary>
    public void OnDateCharacterArrived()
    {
        if (_currentDate == null)
        {
            Debug.LogWarning("[DateSessionManager] No current date set.");
            return;
        }

        StartCoroutine(ArrivalTransition());
    }

    // ──────────────────────────────────────────────────────────────
    // Phase Transitions (fade → teleport → fade)
    // ──────────────────────────────────────────────────────────────

    private IEnumerator ArrivalTransition()
    {
        // Fade to black
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(fadeDuration);

        if (ScreenFade.Instance != null)
            ScreenFade.Instance.ShowPhaseTitle("First Impressions");

        yield return new WaitForSeconds(phaseTitleHold);

        if (ScreenFade.Instance != null)
            ScreenFade.Instance.HidePhaseTitle();

        // Set up session while screen is black
        _state = SessionState.DateInProgress;
        _datePhase = DatePhase.Arrival;
        _affection = startingAffection;
        _moodCheckTimer = 0f;
        _accumulatedReactions.Clear();

        // Spawn NPC at judgment point (teleported, no walking)
        SpawnDateCharacter();

        if (dateArrivedSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(dateArrivedSFX);

        OnDateSessionStarted?.Invoke(_currentDate);
        OnAffectionChanged?.Invoke(_affection);

        // Fade in to reveal NPC at entrance
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeIn(fadeDuration);

        Debug.Log($"[DateSessionManager] Phase 1: Arrival — entrance judgments for {_currentDate.characterName}.");

        // Run entrance judgments (NPC is already at judgment point)
        if (_entranceJudgments != null && _currentDate != null)
        {
            var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();
            yield return _entranceJudgments.RunJudgments(reactionUI, _currentDate);
        }

        // Fail check after entrance
        if (CheckPhaseFailAndExit(_arrivalFailThreshold)) yield break;

        // Auto-transition to Phase 2
        yield return TransitionToPhase2();
    }

    private IEnumerator TransitionToPhase2()
    {
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();

        // Pre-transition NPC dialogue
        string preLine = s_prePhase2Lines[UnityEngine.Random.Range(0, s_prePhase2Lines.Length)];
        reactionUI?.ShowText(preLine, 2.0f);
        yield return new WaitForSeconds(2.5f);

        // Fade out
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(fadeDuration);

        // Phase title
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.ShowPhaseTitle("Making Drinks");

        yield return new WaitForSeconds(phaseTitleHold);

        if (ScreenFade.Instance != null)
            ScreenFade.Instance.HidePhaseTitle();

        _datePhase = DatePhase.BackgroundJudging;
        _moodCheckTimer = 0f;

        // Teleport NPC to kitchen
        Vector3 kitchenPos = kitchenStandPoint != null ? kitchenStandPoint.position
            : new Vector3(-4f, 0f, -4.5f);
        if (_dateCharacter != null)
        {
            _dateCharacter.WarpTo(kitchenPos);
            _dateCharacter.SetSitting();
        }

        if (phaseTransitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phaseTransitionSFX);

        // Fade in
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeIn(fadeDuration);

        // Post-transition NPC dialogue
        yield return new WaitForSeconds(0.5f);
        string postLine = s_postPhase2Lines[UnityEngine.Random.Range(0, s_postPhase2Lines.Length)];
        reactionUI?.ShowText(postLine, 2.0f);

        Debug.Log("[DateSessionManager] Phase 2: Kitchen — player makes drink, NPC watches.");
    }

    private IEnumerator TransitionToPhase3()
    {
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();

        // Pre-transition NPC dialogue
        string preLine = s_prePhase3Lines[UnityEngine.Random.Range(0, s_prePhase3Lines.Length)];
        reactionUI?.ShowText(preLine, 2.0f);
        yield return new WaitForSeconds(2.5f);

        // Fade out
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(fadeDuration);

        // Phase title
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.ShowPhaseTitle("Getting Comfortable");

        yield return new WaitForSeconds(phaseTitleHold);

        if (ScreenFade.Instance != null)
            ScreenFade.Instance.HidePhaseTitle();

        _datePhase = DatePhase.Reveal;

        // Teleport NPC to couch
        Vector3 couchPos = couchSeatTarget != null ? couchSeatTarget.position : Vector3.zero;
        if (_dateCharacter != null)
        {
            _dateCharacter.WarpTo(couchPos);
            _dateCharacter.SetSitting();
            _dateCharacter.EnableExcursions();
        }

        if (phaseTransitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phaseTransitionSFX);

        // Fade in
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeIn(fadeDuration);

        // Post-transition NPC dialogue
        yield return new WaitForSeconds(0.5f);
        string postLine = s_postPhase3Lines[UnityEngine.Random.Range(0, s_postPhase3Lines.Length)];
        reactionUI?.ShowText(postLine, 2.0f);

        Debug.Log("[DateSessionManager] Phase 3: Couch — NPC evaluating apartment items.");

        // Start Phase 3 duration timer
        StartCoroutine(Phase3Timer());
    }

    private IEnumerator Phase3Timer()
    {
        yield return new WaitForSeconds(phase3Duration);

        if (_state != SessionState.DateInProgress || _datePhase != DatePhase.Reveal)
            yield break;

        Debug.Log("[DateSessionManager] Phase 3 complete — ending date.");

        if (_dateCharacter != null)
            _dateCharacter.DisableExcursions();

        StartCoroutine(RunEndSequence());
    }

    // ──────────────────────────────────────────────────────────────
    // Reactions
    // ──────────────────────────────────────────────────────────────

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

        // Transition to Phase 3
        StartCoroutine(TransitionToPhase3());
    }

    // ──────────────────────────────────────────────────────────────
    // End of Date
    // ──────────────────────────────────────────────────────────────

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

    private IEnumerator RunEndSequence()
    {
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();

        yield return new WaitForSeconds(1f);

        if (_affection >= _revealFailThreshold)
        {
            if (reactionUI != null)
            {
                reactionUI.ShowText("I had a wonderful time...", 3f);
                yield return new WaitForSeconds(3.5f);
                reactionUI.ShowText("Here... I brought you something.", 3f);
                yield return new WaitForSeconds(3.5f);
            }
            SucceedDate();
        }
        else
        {
            if (reactionUI != null)
            {
                reactionUI.ShowText("I think I should go...", 3f);
                yield return new WaitForSeconds(3.5f);
                reactionUI.ShowText("Goodnight.", 2.5f);
                yield return new WaitForSeconds(3f);
            }
            FailDate();
        }
    }

    private void FailDate()
    {
        if (_state == SessionState.Idle || _state == SessionState.DateEnding) return;

        _state = SessionState.DateEnding;
        _datePhase = DatePhase.None;
        Debug.Log($"[DateSessionManager] Date FAILED with {_currentDate?.characterName}. Affection: {_affection:F1}");

        DateOutcomeCapture.Capture(_currentDate, _affection, false, _accumulatedReactions);

        var failEntry = new DateHistory.DateHistoryEntry
        {
            name = _currentDate?.characterName ?? "Unknown",
            day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 0,
            affection = _affection,
            grade = "F",
            succeeded = false
        };
        PopulateLearnedPreferences(failEntry);
        DateHistory.Record(failEntry);

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
        StartCoroutine(SucceedDateSequence());
    }

    private IEnumerator SucceedDateSequence()
    {
        Debug.Log($"[DateSessionManager] Date SUCCEEDED with {_currentDate?.characterName}. Affection: {_affection:F1}");

        DateOutcomeCapture.Capture(_currentDate, _affection, true, _accumulatedReactions);

        var successEntry = new DateHistory.DateHistoryEntry
        {
            name = _currentDate?.characterName ?? "Unknown",
            day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 0,
            affection = _affection,
            grade = DateEndScreen.ComputeGrade(_affection),
            succeeded = true
        };
        PopulateLearnedPreferences(successEntry);
        DateHistory.Record(successEntry);

        // Signal flower trimming if this date has a flower scene configured
        if (_currentDate != null && !string.IsNullOrEmpty(_currentDate.flowerSceneName))
            PendingFlowerTrim = true;

        // Zelda-style flower gift presentation (before dismissing character)
        if (_currentDate != null && _currentDate.flowerPrefab != null
            && FlowerGiftPresenter.Instance != null)
        {
            yield return FlowerGiftPresenter.Instance.Present(
                _currentDate.flowerPrefab, _currentDate.characterName);
        }

        DismissCharacter();
        OnDateSessionEnded?.Invoke(_currentDate, _affection);
        DateEndScreen.Instance?.Show(_currentDate, _affection, failed: false);
        AutoSaveController.Instance?.PerformSave("date_succeeded");
        _state = SessionState.Idle;
    }

    private void DismissCharacter()
    {
        if (_dateCharacter != null)
            _dateCharacter.Dismiss();

        if (_dateCharacterGO != null)
            Destroy(_dateCharacterGO);

        _dateCharacterGO = null;
        _dateCharacter = null;
    }

    // ──────────────────────────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────────────────────────

    private void SpawnDateCharacter()
    {
        // Spawn at judgment point (entrance area)
        Vector3 spawnPos = judgmentStopPoint != null ? judgmentStopPoint.position
            : new Vector3(-1.0f, 0f, 5.5f);

        if (_currentDate.characterModelPrefab != null)
        {
            _dateCharacterGO = Instantiate(_currentDate.characterModelPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Fallback: create a capsule placeholder
            _dateCharacterGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dateCharacterGO.name = $"Date_{_currentDate.characterName}";
            _dateCharacterGO.transform.position = spawnPos;
        }

        // Add character controller
        _dateCharacter = _dateCharacterGO.GetComponent<DateCharacterController>();
        if (_dateCharacter == null)
            _dateCharacter = _dateCharacterGO.AddComponent<DateCharacterController>();

        // Add reaction UI
        var reactionUI = _dateCharacterGO.GetComponent<DateReactionUI>();
        if (reactionUI == null)
            reactionUI = _dateCharacterGO.AddComponent<DateReactionUI>();

        // Add gaze highlight driver
        if (_dateCharacterGO.GetComponent<NPCGazeHighlight>() == null)
            _dateCharacterGO.AddComponent<NPCGazeHighlight>();

        // Add NavMeshAgent if missing
        var agent = _dateCharacterGO.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null)
            agent = _dateCharacterGO.AddComponent<UnityEngine.AI.NavMeshAgent>();

        // Initialize at position and set to sitting (idle, no walking)
        _dateCharacter.Initialize(spawnPos);
        _dateCharacter.SetSitting();

        // Subscribe to reactions
        _dateCharacter.OnReaction += HandleCharacterReaction;
    }

    private void HandleCharacterReaction(ReactableTag tag, ReactionType type, string displayName)
    {
        ApplyReaction(type);

        // Show labeled reaction bubble on the character
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();
        reactionUI?.ShowLabeledReaction(type, displayName);

        // Accumulate during all date phases (reactions shown live)
        if (tag != null)
        {
            var reaction = new AccumulatedReaction
            {
                itemName = displayName,
                type = type
            };
            _accumulatedReactions.Add(reaction);
            OnRevealReaction?.Invoke(reaction);
        }

        // Debug overlay logging
        DateDebugOverlay.Instance?.LogReaction($"{displayName} → {type}");
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

    private void PopulateLearnedPreferences(DateHistory.DateHistoryEntry entry)
    {
        foreach (var reaction in _accumulatedReactions)
        {
            if (reaction.type == ReactionType.Like)
                entry.learnedLikes.Add(reaction.itemName);
            else if (reaction.type == ReactionType.Dislike)
                entry.learnedDislikes.Add(reaction.itemName);
        }
    }
}
