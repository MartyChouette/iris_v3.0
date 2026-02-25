using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using TMPro;

/// <summary>
/// Single authority for daily phase transitions, camera priorities, and screen fades.
/// Phases: Morning (newspaper) → Exploration (free-roam) → DateInProgress → Evening.
///
/// ── Transition quick-reference ──────────────────────────────────────
///
///  MORNING (OnNewNewspaper event)
///    1. Suppress browse camera (priority 0)
///    2. Raise read camera (priority 30)
///    3. Enable NewspaperManager + show newspaper HUD
///    4. Hide apartment UI
///
///  EXPLORATION (OnNewspaperDone event)
///    1. Fade to black               (0.5 s)
///    2. Lower read camera            (priority 0)
///    3. Raise browse camera           (priority 20 via ApartmentManager)
///    4. Toss newspaper to coffee table
///    5. Disable NewspaperManager
///    6. Show apartment UI, hide newspaper HUD
///    7. Spawn daily stains
///    8. Fade in from black           (0.5 s)
///
///  DATE IN PROGRESS (OnDateSessionStarted event)
///    — DateSessionManager / DateCharacterController handle their own flow
///
///  EVENING (OnDateSessionEnded event)
///    — DateEndScreen handles its own flow
/// ─────────────────────────────────────────────────────────────────────
/// </summary>
public class DayPhaseManager : MonoBehaviour
{
    public enum DayPhase { Morning, Exploration, DateInProgress, FlowerTrimming, Evening }

    public static DayPhaseManager Instance { get; private set; }

    [Header("Current Phase")]
    [SerializeField] private DayPhase _currentPhase = DayPhase.Evening;

    [Header("References")]
    [Tooltip("NewspaperManager to enable/disable at phase transitions.")]
    [SerializeField] private NewspaperManager _newspaperManager;

    [Tooltip("Read camera — raised to priority 30 during Morning, lowered during Exploration.")]
    [SerializeField] private CinemachineCamera _readCamera;

    [Tooltip("Transform for the tossed newspaper position on the coffee table.")]
    [SerializeField] private Transform _tossedNewspaperPosition;

    [Tooltip("Authored mess spawner triggered at exploration start (stains + objects).")]
    [SerializeField] private AuthoredMessSpawner _authoredMessSpawner;

    [Tooltip("Daily mess spawner for entrance item misplacement.")]
    [SerializeField] private DailyMessSpawner _entranceMessSpawner;

    [Tooltip("Bridge for loading flower trimming scene after successful dates.")]
    [SerializeField] private FlowerTrimmingBridge _flowerTrimmingBridge;

    [Tooltip("Apartment UI canvas root — hidden during Morning, shown during Exploration.")]
    [SerializeField] private GameObject _apartmentUI;

    [Tooltip("Newspaper HUD root — shown during Morning, hidden during Exploration.")]
    [SerializeField] private GameObject _newspaperHUD;

    [Header("Fade Timing")]
    [Tooltip("Duration of fade-to-black and fade-from-black in seconds.")]
    [SerializeField] private float _fadeDuration = 0.5f;

    [Header("Preparation Timer")]
    [Tooltip("Duration of the preparation phase in seconds.")]
    [SerializeField] private float _prepDuration = 120f;

    [Tooltip("TMP_Text displaying the countdown timer.")]
    [SerializeField] private TMP_Text _prepTimerText;

    [Tooltip("Panel root for the prep timer UI.")]
    [SerializeField] private GameObject _prepTimerPanel;

    [Header("Go to Bed")]
    [Tooltip("Panel with Go to Bed button — shown only during Evening phase.")]
    [SerializeField] private GameObject _goToBedPanel;

    [Header("Audio")]
    [Tooltip("SFX played at the start of a new day (morning transition).")]
    [SerializeField] private AudioClip nextDaySFX;

    [Tooltip("SFX played when prep timer hits 10 seconds remaining.")]
    [SerializeField] private AudioClip timerWarningSFX;

    [Tooltip("Optional ambience loop for the morning newspaper phase. If null, MoodMachine ambient runs.")]
    [SerializeField] private AudioClip _morningAmbienceClip;

    [Tooltip("Optional ambience loop for exploration/prep phase. If null, MoodMachine ambient runs.")]
    [SerializeField] private AudioClip _explorationAmbienceClip;

    [Header("Events")]
    public UnityEvent<int> OnPhaseChanged;

    private const int PriorityActive = 30;
    private const int PriorityInactive = 0;

    private float _prepTimer;
    private bool _prepTimerActive;
    private bool _timerWarningPlayed;

    public DayPhase CurrentPhase => _currentPhase;
    public float PrepTimer => _prepTimer;
    public bool PrepTimerActive => _prepTimerActive;

    /// <summary>True during Exploration, DateInProgress, or Evening — stations can accept input.</summary>
    public bool IsInteractionPhase => _currentPhase == DayPhase.Exploration
                                   || _currentPhase == DayPhase.DateInProgress
                                   || _currentPhase == DayPhase.Evening;

    /// <summary>True only during DateInProgress — drink making is allowed.</summary>
    public bool IsDrinkPhase => _currentPhase == DayPhase.DateInProgress;

    // ─── Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DayPhaseManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Runtime subscription for DateSessionManager events
        // (multi-param UnityEvents can't be wired via UnityEventTools in editor)
        if (DateSessionManager.Instance != null)
        {
            DateSessionManager.Instance.OnDateSessionStarted.AddListener(EnterDateInProgress);
            DateSessionManager.Instance.OnDateSessionEnded.AddListener(EnterEvening);
        }

        // Apply game mode prep duration if set from main menu
        if (MainMenuManager.ActiveConfig != null)
            _prepDuration = MainMenuManager.ActiveConfig.prepDuration;

        // Subscribe to calendar completion
        if (GameClock.Instance != null)
            GameClock.Instance.OnCalendarComplete.AddListener(OnCalendarComplete);
    }

    private void OnDestroy()
    {
        if (DateSessionManager.Instance != null)
        {
            DateSessionManager.Instance.OnDateSessionStarted.RemoveListener(EnterDateInProgress);
            DateSessionManager.Instance.OnDateSessionEnded.RemoveListener(EnterEvening);
        }
        if (GameClock.Instance != null)
            GameClock.Instance.OnCalendarComplete.RemoveListener(OnCalendarComplete);
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_prepTimerActive) return;
        if (DateDebugOverlay.IsTimePaused) return;

        _prepTimer -= Time.deltaTime;

        if (_prepTimerText != null)
        {
            int mins = Mathf.FloorToInt(Mathf.Max(_prepTimer, 0f) / 60f);
            int secs = Mathf.FloorToInt(Mathf.Max(_prepTimer, 0f) % 60f);
            _prepTimerText.SetText("{0}:{1:00}", mins, secs);
        }

        // Warning SFX at 10 seconds
        if (!_timerWarningPlayed && _prepTimer <= 10f && _prepTimer > 0f)
        {
            _timerWarningPlayed = true;
            if (timerWarningSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(timerWarningSFX);
        }

        if (_prepTimer <= 0f)
        {
            _prepTimerActive = false;
            OnPrepTimerExpired();
        }
    }

    // ─── Prep Timer ──────────────────────────────────────────────────

    private void StartPrepTimer()
    {
        float multiplier = AccessibilitySettings.TimerMultiplier;

        // 0 = unlimited — no timer
        if (multiplier <= 0f)
        {
            _prepTimerActive = false;
            if (_prepTimerPanel != null) _prepTimerPanel.SetActive(false);
            Debug.Log("[DayPhaseManager] Prep timer disabled (unlimited mode).");
            return;
        }

        _prepTimer = _prepDuration * multiplier;
        _prepTimerActive = true;
        _timerWarningPlayed = false;
        if (_prepTimerPanel != null) _prepTimerPanel.SetActive(true);
        Debug.Log($"[DayPhaseManager] Prep timer started: {_prepTimer}s (base {_prepDuration} x {multiplier}).");
    }

    private void StopPrepTimer()
    {
        _prepTimerActive = false;
        if (_prepTimerPanel != null) _prepTimerPanel.SetActive(false);
    }

    private void OnPrepTimerExpired()
    {
        StopPrepTimer();
        Debug.Log("[DayPhaseManager] Prep timer expired — doorbell!");

        // Date arrives via doorbell
        PhoneController.Instance?.PlayDoorbell();
    }

    /// <summary>Called by PhoneController when player clicks phone to end prep early.</summary>
    public void EndPrepEarly()
    {
        StopPrepTimer();
        Debug.Log("[DayPhaseManager] Prep ended early by player.");
    }

    // ─── Public entry points (called by events) ─────────────────────

    /// <summary>Called by DayManager.OnNewNewspaper event.</summary>
    public void EnterMorning()
    {
        SetPhase(DayPhase.Morning);
    }

    /// <summary>Called by NewspaperManager.OnNewspaperDone event.</summary>
    public void EnterExploration()
    {
        SetPhase(DayPhase.Exploration);
    }

    /// <summary>Called by DateSessionManager.OnDateSessionStarted event.</summary>
    public void EnterDateInProgress(DatePersonalDefinition _)
    {
        SetPhase(DayPhase.DateInProgress);
    }

    /// <summary>Called by DateSessionManager.OnDateSessionEnded event.</summary>
    public void EnterEvening(DatePersonalDefinition _, float __)
    {
        // If there's a pending flower trim from a successful date, do it first
        if (DateSessionManager.PendingFlowerTrim)
        {
            SetPhase(DayPhase.FlowerTrimming);
            return;
        }

        SetPhase(DayPhase.Evening);
    }

    // ─── Save/Load ─────────────────────────────────────────────────

    /// <summary>
    /// Instantly restore to a saved phase without transition coroutines.
    /// Sets cameras, UI, and manager states to match the target phase.
    /// </summary>
    public void RestoreToPhase(DayPhase phase)
    {
        _currentPhase = phase;
        StopPrepTimer();

        // Go to Bed panel
        if (_goToBedPanel != null)
            _goToBedPanel.SetActive(phase == DayPhase.Evening);

        switch (phase)
        {
            case DayPhase.Morning:
                // Suspend ortho preset so read camera displays correctly
                CameraTestController.Instance?.SuspendPreset();
                // Newspaper is showing — raise read camera, suppress browse
                if (ApartmentManager.Instance != null)
                    ApartmentManager.Instance.SetBrowseCameraActive(false);
                if (_readCamera != null)
                    _readCamera.Priority = PriorityActive;
                if (_newspaperManager != null)
                    _newspaperManager.enabled = true;
                if (_apartmentUI != null) _apartmentUI.SetActive(false);
                if (_newspaperHUD != null) _newspaperHUD.SetActive(true);
                break;

            case DayPhase.Exploration:
            case DayPhase.DateInProgress:
            case DayPhase.FlowerTrimming:
            case DayPhase.Evening:
                // Free-roam — browse camera active, newspaper off
                if (_readCamera != null)
                    _readCamera.Priority = PriorityInactive;
                if (ApartmentManager.Instance != null)
                    ApartmentManager.Instance.SetBrowseCameraActive(true);
                if (_newspaperManager != null)
                    _newspaperManager.enabled = false;
                if (_apartmentUI != null) _apartmentUI.SetActive(true);
                if (_newspaperHUD != null) _newspaperHUD.SetActive(false);

                // Toss newspaper to coffee table
                if (_tossedNewspaperPosition != null && _newspaperManager != null
                    && _newspaperManager.NewspaperTransform != null)
                {
                    _newspaperManager.NewspaperTransform.position = _tossedNewspaperPosition.position;
                    _newspaperManager.NewspaperTransform.rotation = _tossedNewspaperPosition.rotation;
                }
                break;
        }

        // Fade in immediately
        ScreenFade.Instance?.FadeIn(_fadeDuration);

        Debug.Log($"[DayPhaseManager] Restored to phase {phase}.");
        OnPhaseChanged?.Invoke((int)phase);
    }

    // ─── Phase dispatch ─────────────────────────────────────────────

    public void SetPhase(DayPhase phase)
    {
        if (_currentPhase == phase) return;

        _currentPhase = phase;
        Debug.Log($"[DayPhaseManager] Phase → {phase}");

        // Close all station UIs/HUDs on any phase change
        DismissAllStationUI();

        // Go to Bed panel is only visible during Evening
        if (_goToBedPanel != null)
            _goToBedPanel.SetActive(phase == DayPhase.Evening);
        switch (phase)
        {
            case DayPhase.Morning:
                StartCoroutine(MorningTransition());
                break;
            case DayPhase.Exploration:
                StartCoroutine(ExplorationTransition());
                break;
            case DayPhase.DateInProgress:
                // Stop prep timer — date is in progress
                StopPrepTimer();
                break;
            case DayPhase.FlowerTrimming:
                StartCoroutine(FlowerTrimmingTransition());
                break;
            case DayPhase.Evening:
                // DateEndScreen shows via existing DateSessionManager flow
                break;
        }

        OnPhaseChanged?.Invoke((int)phase);
    }

    // ═══════════════════════════════════════════════════════════════
    // MORNING TRANSITION
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator MorningTransition()
    {
        if (nextDaySFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(nextDaySFX);

        // Phase ambience override (gentle newspaper-reading tone)
        if (_morningAmbienceClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayAmbience(_morningAmbienceClip, 0.5f);

        // 0. Suspend ortho preset so Cinemachine can blend to perspective read camera
        CameraTestController.Instance?.SuspendPreset();

        // 1. Suppress browse camera so it doesn't compete with read camera
        if (ApartmentManager.Instance != null)
            ApartmentManager.Instance.SetBrowseCameraActive(false);

        // 2. Raise read camera
        if (_readCamera != null)
            _readCamera.Priority = PriorityActive;

        // 3. Enable newspaper manager so it can populate ads
        if (_newspaperManager != null)
            _newspaperManager.enabled = true;

        // 4. UI: hide apartment browse, show newspaper HUD
        if (_apartmentUI != null)
            _apartmentUI.SetActive(false);
        if (_newspaperHUD != null)
            _newspaperHUD.SetActive(true);

        // 5. Fade in from black (scene started fully black)
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeIn(_fadeDuration);
    }

    // ═══════════════════════════════════════════════════════════════
    // EXPLORATION TRANSITION
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator ExplorationTransition()
    {
        // Smooth camera blend — no fade. Cinemachine brain handles the transition.

        // 1. Lower read camera → browse camera wins via priority
        if (_readCamera != null)
            _readCamera.Priority = PriorityInactive;

        // 2. Raise browse camera (ApartmentManager owns its priority value)
        if (ApartmentManager.Instance != null)
            ApartmentManager.Instance.SetBrowseCameraActive(true);

        // 3. Toss newspaper to coffee table
        if (_tossedNewspaperPosition != null)
        {
            var surface = _newspaperManager != null ? _newspaperManager.NewspaperTransform : null;
            if (surface != null)
            {
                surface.position = _tossedNewspaperPosition.position;
                surface.rotation = _tossedNewspaperPosition.rotation;
            }
        }

        // 4. Disable newspaper manager (done for the day)
        if (_newspaperManager != null)
            _newspaperManager.enabled = false;

        // 5. UI: show apartment browse, hide newspaper HUD
        if (_apartmentUI != null)
            _apartmentUI.SetActive(true);
        if (_newspaperHUD != null)
            _newspaperHUD.SetActive(false);

        // 6. Spawn authored messes (stains + objects) + misplace entrance items
        if (_authoredMessSpawner != null)
            _authoredMessSpawner.SpawnDailyMess();
        if (_entranceMessSpawner != null)
            _entranceMessSpawner.SpawnDailyMess();

        // 7. Wait for Cinemachine blend to finish (default 0.8s EaseInOut)
        yield return new WaitForSeconds(0.9f);

        // 8. Restore suspended camera preset (if player had one active before morning)
        CameraTestController.Instance?.RestorePreset();

        // 9. Swap to exploration ambience (or let MoodMachine take over if null)
        if (_explorationAmbienceClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayAmbience(_explorationAmbienceClip, 0.5f);

        // 10. Safety: ensure ScreenFade is not blocking raycasts.
        // MorningTransition's FadeIn should have already cleared this, but
        // guard against edge cases (skipped morning, interrupted fade, etc.).
        if (ScreenFade.Instance != null && ScreenFade.Instance.IsFading == false)
        {
            var cg = ScreenFade.Instance.GetComponentInChildren<CanvasGroup>();
            if (cg != null && cg.blocksRaycasts)
            {
                cg.blocksRaycasts = false;
                cg.alpha = 0f;
                Debug.LogWarning("[DayPhaseManager] ScreenFade was still blocking — forced clear.");
            }
        }

        // 11. Start preparation countdown
        StartPrepTimer();
    }

    // ═══════════════════════════════════════════════════════════════
    // FLOWER TRIMMING TRANSITION
    // ═══════════════════════════════════════════════════════════════

    private IEnumerator FlowerTrimmingTransition()
    {
        if (!DateSessionManager.PendingFlowerTrim)
        {
            Debug.LogWarning("[DayPhaseManager] FlowerTrimming phase but no pending trim. Skipping to Evening.");
            SetPhase(DayPhase.Evening);
            yield break;
        }

        var bridge = _flowerTrimmingBridge != null ? _flowerTrimmingBridge : FlowerTrimmingBridge.Instance;
        if (bridge == null)
        {
            Debug.LogWarning("[DayPhaseManager] No FlowerTrimmingBridge found. Skipping to Evening.");
            DateSessionManager.PendingFlowerTrim = false;
            SetPhase(DayPhase.Evening);
            yield break;
        }

        // 1. Fade to black
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(_fadeDuration);

        // 2. Show phase title
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.ShowPhaseTitle("Flower Trimming");

        // 3. Begin scene load while still black — camera will activate off-screen
        bool trimmingComplete = false;
        bridge.BeginTrimming((score, days, gameOver) =>
        {
            trimmingComplete = true;
        });

        // 4. Wait for the flower scene to finish loading
        while (!bridge.IsSceneReady)
            yield return null;

        // 5. Hold title so the player can read it (2 seconds total)
        yield return new WaitForSeconds(2.0f);
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.HidePhaseTitle();

        // 6. Brief pause after title fades before revealing the scene
        yield return new WaitForSeconds(0.3f);

        // 7. Fade in — the flower scene's own Camera is now active (apartment camera disabled)
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeIn(_fadeDuration * 2f);

        // 7. Wait for the player to finish trimming
        while (!trimmingComplete)
            yield return null;

        // 8. Fade to black, unload flower scene, transition to Evening
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(_fadeDuration);

        // 8b. Restore apartment camera now that screen is fully black
        if (bridge != null)
            bridge.RestoreApartmentCamera();

        // 9. Enter Evening phase
        _currentPhase = DayPhase.Evening;
        Debug.Log("[DayPhaseManager] Phase → Evening (after flower trimming)");

        if (_goToBedPanel != null)
            _goToBedPanel.SetActive(true);

        OnPhaseChanged?.Invoke((int)DayPhase.Evening);

        // 10. Fade in from black
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeIn(_fadeDuration);
    }

    // ═══════════════════════════════════════════════════════════════
    // CALENDAR COMPLETE
    // ═══════════════════════════════════════════════════════════════

    private void OnCalendarComplete()
    {
        StartCoroutine(CalendarCompleteSequence());
    }

    private IEnumerator CalendarCompleteSequence()
    {
        Debug.Log("[DayPhaseManager] Calendar complete — showing end screen.");

        // 1. Fade to black
        if (ScreenFade.Instance != null)
            yield return ScreenFade.Instance.FadeOut(_fadeDuration);

        // 2. Show end title
        if (ScreenFade.Instance != null)
            ScreenFade.Instance.ShowPhaseTitle("7 Days Complete");

        // 3. Hold for the player to read
        yield return new WaitForSeconds(4f);

        // 4. Return to main menu if one exists, otherwise just stay on black
        if (SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenu.unity") >= 0)
        {
            TimeScaleManager.ClearAll();
            SceneManager.LoadScene("MainMenu");
        }
        else
        {
            Debug.Log("[DayPhaseManager] No MainMenu scene in build settings — staying on end screen.");
        }
    }

    // ─── UI Cleanup ─────────────────────────────────────────────────

    /// <summary>
    /// Force-close all station UIs and HUDs. Called on every phase transition
    /// so no stale UI persists across phases or into the next day.
    /// </summary>
    private void DismissAllStationUI()
    {
        WateringManager.Instance?.ForceIdle();
        RecordSlot.Instance?.Stop();
        SimpleDrinkManager.Instance?.ForceIdle();
        FridgeController.Instance?.CloseDoor();

        var calendar = Object.FindAnyObjectByType<ApartmentCalendar>();
        if (calendar != null) calendar.CloseCalendar();

        var pause = Object.FindAnyObjectByType<PauseMenuController>();
        if (pause != null) pause.ClosePauseMenu();
    }
}
