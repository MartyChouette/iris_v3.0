using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene-scoped singleton that drives the in-game calendar and clock.
/// Ticks game hours at a configurable pace and feeds MoodMachine a "TimeOfDay" source.
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Calendar")]
    [Tooltip("Total days in the game calendar.")]
    [SerializeField] private int totalDays = 7;

    [Tooltip("Hour the player wakes up each morning.")]
    [SerializeField] private float startHour = 8f;

    [Tooltip("Hour forced bedtime triggers (use >24 for after-midnight, e.g. 26 = 2am).")]
    [SerializeField] private float bedtimeHour = 26f;

    [Header("Pace")]
    [Tooltip("Real-world seconds per one game hour.")]
    [SerializeField] private float realSecondsPerGameHour = 60f;

    [Header("Time-of-Day Mood")]
    [Tooltip("Maps game hour (0-24) to mood value (0-1) for MoodMachine.")]
    [SerializeField] private AnimationCurve timeOfDayMoodCurve = new AnimationCurve(
        new Keyframe(0f, 0.8f),
        new Keyframe(6f, 0.3f),
        new Keyframe(8f, 0f),
        new Keyframe(12f, 0.05f),
        new Keyframe(18f, 0.3f),
        new Keyframe(21f, 0.6f),
        new Keyframe(24f, 0.8f)
    );

    [Header("References")]
    [Tooltip("DayManager for advancing days.")]
    [SerializeField] private DayManager dayManager;

    [Header("Events")]
    public UnityEvent<float> OnHourChanged;
    public UnityEvent OnForcedBedtime;
    public UnityEvent OnDayStarted;
    public UnityEvent OnCalendarComplete;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    private float _currentHour;
    private int _currentDay = 1;
    private bool _isSleeping;

    // ──────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────
    public float CurrentHour => _currentHour;
    public int CurrentDay => _currentDay;
    public bool IsSleeping => _isSleeping;
    public float NormalizedTimeOfDay => Mathf.Repeat(_currentHour, 24f) / 24f;

    // ──────────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameClock] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _currentHour = startHour;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_isSleeping) return;

        _currentHour += Time.deltaTime / realSecondsPerGameHour;

        // Feed mood machine with time-of-day value
        float displayHour = Mathf.Repeat(_currentHour, 24f);
        MoodMachine.Instance?.SetSource("TimeOfDay", timeOfDayMoodCurve.Evaluate(displayHour));

        OnHourChanged?.Invoke(_currentHour);

        if (_currentHour >= bedtimeHour)
        {
            OnForcedBedtime?.Invoke();
            GoToBed();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Public Methods
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Player-initiated sleep. Ends any active date, advances the day.
    /// </summary>
    public void GoToBed()
    {
        if (_isSleeping) return;
        StartCoroutine(SleepSequence());
    }

    /// <summary>Debug shortcut to immediately advance to next day.</summary>
    public void ForceNewDay()
    {
        if (_isSleeping) return;
        StartCoroutine(SleepSequence());
    }

    private IEnumerator SleepSequence()
    {
        _isSleeping = true;
        Debug.Log($"[GameClock] Going to bed on day {_currentDay} at hour {_currentHour:F1}");

        // End any active date session
        DateSessionManager.Instance?.EndDate();

        // Brief pause for transition
        yield return new WaitForSeconds(0.5f);

        _currentDay++;
        _currentHour = startHour;

        dayManager?.AdvanceDay();

        _isSleeping = false;
        OnDayStarted?.Invoke();
        Debug.Log($"[GameClock] Day {_currentDay} started at {_currentHour:F0}:00");

        if (_currentDay > totalDays)
        {
            OnCalendarComplete?.Invoke();
            Debug.Log("[GameClock] Calendar complete!");
        }
    }
}
