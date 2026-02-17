using System.Collections;
using UnityEngine;
using UnityEngine.Events;
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
    public enum DayPhase { Morning, Exploration, DateInProgress, Evening }

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

    [Tooltip("Stain spawner triggered at exploration start.")]
    [SerializeField] private ApartmentStainSpawner _stainSpawner;

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

    [Header("Events")]
    public UnityEvent<int> OnPhaseChanged;

    private const int PriorityActive = 30;
    private const int PriorityInactive = 0;

    private float _prepTimer;
    private bool _prepTimerActive;
    private bool _timerWarningPlayed;

    public DayPhase CurrentPhase => _currentPhase;

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
    }

    private void OnDestroy()
    {
        if (DateSessionManager.Instance != null)
        {
            DateSessionManager.Instance.OnDateSessionStarted.RemoveListener(EnterDateInProgress);
            DateSessionManager.Instance.OnDateSessionEnded.RemoveListener(EnterEvening);
        }
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_prepTimerActive) return;

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
        _prepTimer = _prepDuration;
        _prepTimerActive = true;
        _timerWarningPlayed = false;
        if (_prepTimerPanel != null) _prepTimerPanel.SetActive(true);
        Debug.Log($"[DayPhaseManager] Prep timer started: {_prepDuration}s.");
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
        SetPhase(DayPhase.Evening);
    }

    // ─── Phase dispatch ─────────────────────────────────────────────

    public void SetPhase(DayPhase phase)
    {
        if (_currentPhase == phase) return;

        _currentPhase = phase;
        Debug.Log($"[DayPhaseManager] Phase → {phase}");

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

        // 6. Spawn daily stains
        if (_stainSpawner != null)
            _stainSpawner.SpawnDailyStains();

        // 7. Wait for Cinemachine blend to finish (default 0.8s EaseInOut)
        yield return new WaitForSeconds(0.9f);

        // 8. Start preparation countdown
        StartPrepTimer();
    }
}
