using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

/// <summary>
/// Orchestrates the daily apartment gameplay loop.
/// Phases: Morning (newspaper) → Exploration (free-roam) → DateInProgress → Evening (end screen).
/// Transitions are event-driven — no Update logic.
/// </summary>
public class DayPhaseManager : MonoBehaviour
{
    public enum DayPhase { Morning, Exploration, DateInProgress, Evening }

    public static DayPhaseManager Instance { get; private set; }

    [Header("Current Phase")]
    [SerializeField] private DayPhase _currentPhase = DayPhase.Morning;

    [Header("References")]
    [Tooltip("NewspaperManager to enable/disable at phase transitions.")]
    [SerializeField] private NewspaperManager _newspaperManager;

    [Tooltip("Read camera controlled during morning phase.")]
    [SerializeField] private CinemachineCamera _readCamera;

    [Tooltip("Transform for the tossed newspaper position on the kitchen counter.")]
    [SerializeField] private Transform _tossedNewspaperPosition;

    [Tooltip("Stain spawner triggered at exploration start.")]
    [SerializeField] private ApartmentStainSpawner _stainSpawner;

    [Header("Events")]
    public UnityEvent<int> OnPhaseChanged;

    private const int PriorityActive = 30;
    private const int PriorityInactive = 0;

    public DayPhase CurrentPhase => _currentPhase;

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
        // (multi-param UnityEvents are difficult to wire in editor)
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

    /// <summary>
    /// Called by DayManager.OnNewNewspaper event.
    /// </summary>
    public void EnterMorning()
    {
        SetPhase(DayPhase.Morning);
    }

    /// <summary>
    /// Called by NewspaperManager.OnNewspaperDone event.
    /// </summary>
    public void EnterExploration()
    {
        SetPhase(DayPhase.Exploration);
    }

    /// <summary>
    /// Called by DateSessionManager.OnDateSessionStarted event.
    /// </summary>
    public void EnterDateInProgress(DatePersonalDefinition _)
    {
        SetPhase(DayPhase.DateInProgress);
    }

    /// <summary>
    /// Called by DateSessionManager.OnDateSessionEnded event.
    /// </summary>
    public void EnterEvening(DatePersonalDefinition _, float __)
    {
        SetPhase(DayPhase.Evening);
    }

    public void SetPhase(DayPhase phase)
    {
        if (_currentPhase == phase) return;

        _currentPhase = phase;
        Debug.Log($"[DayPhaseManager] Phase → {phase}");

        switch (phase)
        {
            case DayPhase.Morning:
                OnEnterMorning();
                break;
            case DayPhase.Exploration:
                OnEnterExploration();
                break;
            case DayPhase.DateInProgress:
                // DateCharacterController spawns via existing DateSessionManager flow
                break;
            case DayPhase.Evening:
                // DateEndScreen shows via existing DateSessionManager flow
                break;
        }

        OnPhaseChanged?.Invoke((int)phase);
    }

    private void OnEnterMorning()
    {
        // Enable newspaper manager so it can receive OnNewNewspaper
        if (_newspaperManager != null)
            _newspaperManager.enabled = true;

        // Raise read camera so newspaper takes over view
        if (_readCamera != null)
            _readCamera.Priority = PriorityActive;
    }

    private void OnEnterExploration()
    {
        // Lower read camera — browse camera takes over
        if (_readCamera != null)
            _readCamera.Priority = PriorityInactive;

        // Move newspaper to tossed position on kitchen counter (cosmetic)
        if (_tossedNewspaperPosition != null)
        {
            var surface = _newspaperManager != null ? _newspaperManager.NewspaperTransform : null;
            if (surface != null)
            {
                surface.position = _tossedNewspaperPosition.position;
                surface.rotation = _tossedNewspaperPosition.rotation;
            }
        }

        // Disable newspaper manager (done for the day)
        if (_newspaperManager != null)
            _newspaperManager.enabled = false;

        // Spawn daily stains
        if (_stainSpawner != null)
            _stainSpawner.SpawnDailyStains();
    }
}
