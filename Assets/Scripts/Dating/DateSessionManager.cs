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

    // ── Cached WaitForSeconds to avoid per-yield allocations ──
    private static readonly WaitForSeconds s_wait03 = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds s_wait05 = new WaitForSeconds(0.5f);
    private static readonly WaitForSeconds s_wait1  = new WaitForSeconds(1f);
    private static readonly WaitForSeconds s_wait2  = new WaitForSeconds(2f);
    private static readonly WaitForSeconds s_wait25 = new WaitForSeconds(2.5f);
    private static readonly WaitForSeconds s_wait3  = new WaitForSeconds(3f);
    private static readonly WaitForSeconds s_wait35 = new WaitForSeconds(3.5f);

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

    [Tooltip("If affection drops below this at ANY point, date immediately fails. 0 = disabled.")]
    [SerializeField] private float _bailOutThreshold = 10f;

    [Tooltip("Minimum affection required for the date to give you a flower (and trigger flower trimming).")]
    [SerializeField] private float _flowerAffectionThreshold = 90f;

    [Header("Ambient Check")]
    [Tooltip("Seconds between ambient mood evaluations.")]
    [SerializeField] private float moodCheckInterval = 15f;

    [Tooltip("Affection drift per check when mood matches.")]
    [SerializeField] private float ambientMoodDrift = 0.5f;

    [Header("Phase 3 Timing")]
#pragma warning disable 0414
    [Tooltip("Duration of Phase 3 (couch judging) in seconds before the date ends.")]
    [SerializeField] private float phase3Duration = 40f;
#pragma warning restore 0414

    [Header("Fade Timing")]
    [Tooltip("Fade duration for phase transitions (seconds).")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Tooltip("Seconds to show phase title on black screen.")]
    [SerializeField] private float phaseTitleHold = 2.0f;

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

    [Header("Phase 2 Highlights")]
    [Tooltip("Renderer on the fridge to pulse during drink phase.")]
    [SerializeField] private Renderer _fridgeHighlightRenderer;

    [Tooltip("Renderer on the drink station/counter to pulse during drink phase.")]
    [SerializeField] private Renderer _drinkStationHighlightRenderer;

    [Tooltip("Pulse color for Phase 2 interactive objects.")]
    [SerializeField] private Color _phase2PulseColor = new Color(1f, 0.9f, 0.6f, 0.5f);

    [Tooltip("Pulse speed for Phase 2 highlights.")]
    [SerializeField] private float _phase2PulseSpeed = 1.5f;

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
    private Coroutine _phase2PulseCoroutine;
    private Color _fridgeOrigColor;
    private Color _drinkOrigColor;

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
        // Unsubscribe from character events if still alive
        if (_dateCharacter != null)
            _dateCharacter.OnReaction -= HandleCharacterReaction;

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

        // Reset affection to 0 immediately so the HUD shows fresh for this date
        _affection = 0f;
        OnAffectionChanged?.Invoke(_affection);

#if UNITY_EDITOR
        Debug.Log($"[DateSessionManager] Scheduled date with {date.characterName}. Waiting for prep phase to end.");
#endif
    }

    /// <summary>Called when the arrival timer expires — triggers phone ring or direct arrival.</summary>
    private void TriggerDateArrival()
    {
#if UNITY_EDITOR
        Debug.Log($"[DateSessionManager] {_currentDate?.characterName} is arriving!");
#endif

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
            ScreenFade.Instance.ShowPhaseTitle("Impressions");

        yield return new WaitForSeconds(phaseTitleHold);

        if (ScreenFade.Instance != null)
            ScreenFade.Instance.HidePhaseTitle();

        // Set up session while screen is black
        _state = SessionState.DateInProgress;
        _datePhase = DatePhase.Arrival;
        NemaController.Instance?.MoveToDatePhase(DatePhase.Arrival);
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

#if UNITY_EDITOR
        Debug.Log($"[DateSessionManager] Phase 1: Arrival — entrance judgments for {_currentDate.characterName}.");
#endif

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
        yield return s_wait25;

        // Fade out
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(fadeDuration);

        // Phase title
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.ShowPhaseTitle("Drinks");

        yield return new WaitForSeconds(phaseTitleHold);

        if (ScreenFade.Instance != null)
            ScreenFade.Instance.HidePhaseTitle();

        _datePhase = DatePhase.BackgroundJudging;
        NemaController.Instance?.MoveToDatePhase(DatePhase.BackgroundJudging);
        _moodCheckTimer = 0f;

        // Start pulsing fridge + drink station
        StartPhase2Pulse();

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
        yield return s_wait05;
        string postLine = s_postPhase2Lines[UnityEngine.Random.Range(0, s_postPhase2Lines.Length)];
        reactionUI?.ShowText(postLine, 2.0f);

#if UNITY_EDITOR
        Debug.Log("[DateSessionManager] Phase 2: Kitchen — player makes drink, NPC watches.");
#endif
    }

    private IEnumerator TransitionToPhase3()
    {
        StopPhase2Pulse();

        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();

        // Pre-transition NPC dialogue
        string preLine = s_prePhase3Lines[UnityEngine.Random.Range(0, s_prePhase3Lines.Length)];
        reactionUI?.ShowText(preLine, 2.0f);
        yield return s_wait25;

        // Fade out
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(fadeDuration);

        // Phase title
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.ShowPhaseTitle("Warming Up");

        yield return new WaitForSeconds(phaseTitleHold);

        if (ScreenFade.Instance != null)
            ScreenFade.Instance.HidePhaseTitle();

        _datePhase = DatePhase.Reveal;
        NemaController.Instance?.MoveToDatePhase(DatePhase.Reveal);

        // Teleport NPC to couch
        Vector3 couchPos = couchSeatTarget != null ? couchSeatTarget.position : Vector3.zero;
        if (_dateCharacter != null)
        {
            _dateCharacter.WarpTo(couchPos);
            _dateCharacter.SetSitting();
        }

        if (phaseTransitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phaseTransitionSFX);

        // Fade in
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeIn(fadeDuration);

        // Post-transition NPC dialogue
        yield return s_wait05;
        string postLine = s_postPhase3Lines[UnityEngine.Random.Range(0, s_postPhase3Lines.Length)];
        reactionUI?.ShowText(postLine, 2.0f);
        yield return s_wait25;

#if UNITY_EDITOR
        Debug.Log("[DateSessionManager] Phase 3: Instant reveal — evaluating all apartment items.");
#endif

        // Reveal all reactions at once with staggered heart particles
        yield return StartCoroutine(RevealAllReactions());

        // Brief pause, then end the date
        yield return s_wait2;
        StartCoroutine(RunEndSequence());
    }

    /// <summary>
    /// Instantly evaluate all active ReactableTags against the date's preferences.
    /// Liked items emit heart particles; disliked emit a grey puff.
    /// Staggered with a short delay between each for visual readability.
    /// </summary>
    private IEnumerator RevealAllReactions()
    {
        if (_currentDate == null || _currentDate.preferences == null) yield break;

        var prefs = _currentDate.preferences;
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();

        foreach (var tag in ReactableTag.All)
        {
            if (!tag.IsActive) continue;
            if (tag.IsPrivate) continue;

            var reaction = ReactionEvaluator.EvaluateReactable(tag, prefs);
            if (reaction == ReactionType.Neutral) continue;

            // Apply affection
            ApplyReaction(reaction);

            // Fire reveal event for HUD
            OnRevealReaction?.Invoke(new AccumulatedReaction
            {
                itemName = tag.DisplayName,
                type = reaction
            });

            // Spawn particles at the item's visual center (not pivot)
            SpawnReactionParticles(GetVisualCenter(tag.transform), reaction);

#if UNITY_EDITOR
            Debug.Log($"[DateSessionManager] Reveal: {tag.DisplayName} → {reaction}");
#endif

            // Stagger for visual clarity
            yield return s_wait03;
        }

        // Also evaluate cleanliness as a whole-room judgment
        if (TidyScorer.Instance != null)
        {
            var cleanReaction = ReactionEvaluator.EvaluateCleanliness(TidyScorer.Instance.OverallTidiness);
            if (cleanReaction != ReactionType.Neutral)
            {
                ApplyReaction(cleanReaction);
                if (reactionUI != null)
                {
                    string cleanText = cleanReaction == ReactionType.Like
                        ? "So clean and tidy!"
                        : "It's a bit messy...";
                    reactionUI.ShowText(cleanText, 2f);
                }
                yield return s_wait1;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Reaction Particles (runtime-built, no prefab needed)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Get the visual center of an object using renderer bounds.
    /// Falls back to transform.position if no renderer found.
    /// </summary>
    // ── Phase 2 highlight pulse ──────────────────────────────────

    private void StartPhase2Pulse()
    {
        if (_phase2PulseCoroutine != null) StopCoroutine(_phase2PulseCoroutine);

        if (_fridgeHighlightRenderer != null)
            _fridgeOrigColor = _fridgeHighlightRenderer.material.color;
        if (_drinkStationHighlightRenderer != null)
            _drinkOrigColor = _drinkStationHighlightRenderer.material.color;

        _phase2PulseCoroutine = StartCoroutine(Phase2PulseLoop());
    }

    private void StopPhase2Pulse()
    {
        if (_phase2PulseCoroutine != null)
        {
            StopCoroutine(_phase2PulseCoroutine);
            _phase2PulseCoroutine = null;
        }

        if (_fridgeHighlightRenderer != null)
            _fridgeHighlightRenderer.material.color = _fridgeOrigColor;
        if (_drinkStationHighlightRenderer != null)
            _drinkStationHighlightRenderer.material.color = _drinkOrigColor;
    }

    private IEnumerator Phase2PulseLoop()
    {
        while (true)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _phase2PulseSpeed * Mathf.PI * 2f);

            if (_fridgeHighlightRenderer != null)
                _fridgeHighlightRenderer.material.color = Color.Lerp(_fridgeOrigColor, _phase2PulseColor, pulse);
            if (_drinkStationHighlightRenderer != null)
                _drinkStationHighlightRenderer.material.color = Color.Lerp(_drinkOrigColor, _phase2PulseColor, pulse);

            yield return null;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static Vector3 GetVisualCenter(Transform t)
    {
        var renderer = t.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds.center;
        return t.position;
    }

    private static void SpawnReactionParticles(Vector3 position, ReactionType reaction)
    {
        var go = new GameObject("ReactionParticles");
        // Position above the item so particles are clearly visible
        go.transform.position = position + Vector3.up * 0.15f;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        if (reaction == ReactionType.Like)
        {
            main.duration = 2.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.gravityModifier = -0.4f; // float upward
            main.maxParticles = 30;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.45f, 0.55f),    // hot pink
                new Color(1f, 0.7f, 0.75f));     // soft pink
        }
        else if (reaction == ReactionType.Dislike)
        {
            main.duration = 1.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.gravityModifier = 0.1f; // sink slightly
            main.maxParticles = 8;
            main.startColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
        }
        else
        {
            Object.Destroy(go);
            return;
        }

        // Emission — multiple bursts for juiciness
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        if (reaction == ReactionType.Like)
        {
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 12),
                new ParticleSystem.Burst(0.3f, 8),
                new ParticleSystem.Burst(0.6f, 6),
            });
        }
        else
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
        }

        // Shape — spread around the item
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = reaction == ReactionType.Like ? 0.2f : 0.1f;

        // Size over lifetime — pop in, hold, fade out
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.15f, 1.2f),  // pop!
            new Keyframe(0.4f, 1f),     // hold
            new Keyframe(1f, 0f)        // fade
        ));

        // Color over lifetime — bright start, gentle fade
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = gradient;

        // Rotation for visual variety
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        // Velocity — slight random spread
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        // Material
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            renderer.material = mat;
        }

        ps.Play();
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
#if UNITY_EDITOR
        Debug.Log($"[DateSessionManager] Reaction: {type} (delta={delta:+0.0;-0.0}) → Affection: {_affection:F1}");
#endif

        // Continuous bail-out: if affection drops too low at any point, date fails immediately
        CheckBailOut();
    }

    /// <summary>If affection is below the bail-out threshold, the date fails immediately.</summary>
    private void CheckBailOut()
    {
        if (_bailOutThreshold <= 0f) return;
        if (_state != SessionState.DateInProgress) return;
        if (_affection < _bailOutThreshold)
        {
#if UNITY_EDITOR
            Debug.Log($"[DateSessionManager] Affection {_affection:F1} below bail-out threshold {_bailOutThreshold} — date fails!");
#endif
            FailDate();
        }
    }

    /// <summary>Called when a drink is delivered to the coffee table.</summary>
    public void ReceiveDrink(DrinkRecipeDefinition recipe, int score)
    {
        if (_state != SessionState.DateInProgress || _currentDate == null) return;

        var reactionType = ReactionEvaluator.EvaluateDrink(recipe, score, _currentDate.preferences);
        float magnitude = score / 100f;

        ApplyReaction(reactionType, magnitude);

#if UNITY_EDITOR
        Debug.Log($"[DateSessionManager] Drink received: {recipe?.drinkName} (score={score}) → {reactionType}");
#endif

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
#if UNITY_EDITOR
            Debug.Log($"[DateSessionManager] Affection {_affection:F1} < {threshold} — date failed!");
#endif
            FailDate();
            return true;
        }
        return false;
    }

    private IEnumerator RunEndSequence()
    {
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();

        yield return s_wait1;

        if (_affection >= _revealFailThreshold)
        {
            if (reactionUI != null)
            {
                reactionUI.ShowText("I had a wonderful time...", 3f);
                yield return s_wait35;

                if (_affection >= _flowerAffectionThreshold)
                {
                    reactionUI.ShowText("Here... I brought you something.", 3f);
                    yield return s_wait35;
                }
                else
                {
                    reactionUI.ShowText("See you around.", 2.5f);
                    yield return s_wait3;
                }
            }
            SucceedDate();
        }
        else
        {
            if (reactionUI != null)
            {
                reactionUI.ShowText("I think I should go...", 3f);
                yield return s_wait35;
                reactionUI.ShowText("Goodnight.", 2.5f);
                yield return s_wait3;
            }
            FailDate();
        }
    }

    private void FailDate()
    {
        if (_state == SessionState.Idle || _state == SessionState.DateEnding) return;

        string failedPhaseName = _datePhase.ToString();
        _state = SessionState.DateEnding;
        _datePhase = DatePhase.None;
#if UNITY_EDITOR
        Debug.Log($"[DateSessionManager] Date FAILED at {failedPhaseName} with {_currentDate?.characterName}. Affection: {_affection:F1}");
#endif

        DateOutcomeCapture.Capture(_currentDate, _affection, false, _accumulatedReactions);

        var failEntry = new DateHistory.DateHistoryEntry
        {
            name = _currentDate?.characterName ?? "Unknown",
            day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 0,
            affection = _affection,
            grade = "F",
            succeeded = false,
            failedPhase = failedPhaseName
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
#if UNITY_EDITOR
        Debug.Log($"[DateSessionManager] Date SUCCEEDED with {_currentDate?.characterName}. Affection: {_affection:F1}");
#endif

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

        // Award flower if affection is high enough, OR if the date guarantees flower success (tutorial)
        bool guaranteeFlower = _currentDate != null && _currentDate.guaranteeFlowerSuccess;
        bool earnedFlower = guaranteeFlower || _affection >= _flowerAffectionThreshold;

        // Signal flower trimming if this date has a flower scene configured AND player earned it
        if (earnedFlower && _currentDate != null && !string.IsNullOrEmpty(_currentDate.flowerSceneName))
            PendingFlowerTrim = true;

        // 1. Zelda-style flower gift presentation (only if earned)
        if (earnedFlower && _currentDate != null && _currentDate.flowerPrefab != null
            && FlowerGiftPresenter.Instance != null)
        {
            yield return FlowerGiftPresenter.Instance.Present(
                _currentDate.flowerPrefab, _currentDate.characterName);
        }

        // 2. Dismiss NPC
        DismissCharacter();

        // 3. Show date grade screen and wait for Continue click
        if (DateEndScreen.Instance != null)
        {
            bool dismissed = false;
            DateEndScreen.Instance.OnDismissed += OnEndScreenDismissed;
            DateEndScreen.Instance.Show(_currentDate, _affection, failed: false);

            void OnEndScreenDismissed()
            {
                dismissed = true;
                DateEndScreen.Instance.OnDismissed -= OnEndScreenDismissed;
            }

            while (!dismissed)
                yield return null;
        }

        // 4. Now fire event → DayPhaseManager routes to FlowerTrimming (if pending) or Evening
        AutoSaveController.Instance?.PerformSave("date_succeeded");
        _state = SessionState.Idle;
        OnDateSessionEnded?.Invoke(_currentDate, _affection);
    }

    private void DismissCharacter()
    {
        if (_dateCharacter != null)
        {
            _dateCharacter.OnReaction -= HandleCharacterReaction;
            _dateCharacter.Dismiss();
        }

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

        // Add occluded silhouette so player can see NPC through walls
        if (_dateCharacterGO.GetComponent<OccludedSilhouette>() == null)
            _dateCharacterGO.AddComponent<OccludedSilhouette>();

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

        // Show labeled reaction bubble on the character (with item icon if available)
        var reactionUI = _dateCharacterGO?.GetComponent<DateReactionUI>();
        Sprite itemIcon = tag != null ? tag.ReactionIcon : null;
        reactionUI?.ShowLabeledReaction(type, displayName, itemIcon);

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
