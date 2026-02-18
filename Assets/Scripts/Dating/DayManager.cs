using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    [Header("Pool")]
    [Tooltip("The ad pool to draw from each day.")]
    [SerializeField] private NewspaperPoolDefinition pool;

    [Header("Events")]
    [Tooltip("Fired when day advances (passes new day number).")]
    public UnityEvent<int> OnDayChanged;

    [Tooltip("Fired after day changes — signals NewspaperManager to regenerate.")]
    public UnityEvent OnNewNewspaper;

    public int CurrentDay { get; private set; } = 1;
    public NewspaperPoolDefinition Pool => pool;

    // Today's selected ads — set by AdvanceDay / Start
    private List<DatePersonalDefinition> _todayPersonals = new List<DatePersonalDefinition>();
    private List<CommercialAdDefinition> _todayCommercials = new List<CommercialAdDefinition>();

    // Previous day's picks for repeat prevention
    private HashSet<DatePersonalDefinition> _prevPersonals = new HashSet<DatePersonalDefinition>();
    private HashSet<CommercialAdDefinition> _prevCommercials = new HashSet<CommercialAdDefinition>();

    // Tutorial day-gating: locked personals cannot be selected
    private HashSet<DatePersonalDefinition> _lockedPersonals = new HashSet<DatePersonalDefinition>();

    public List<DatePersonalDefinition> TodayPersonals => _todayPersonals;
    public List<CommercialAdDefinition> TodayCommercials => _todayCommercials;

    /// <summary>True if this personal ad is visible but not selectable (Day 1 tutorial gating).</summary>
    public bool IsLocked(DatePersonalDefinition def) => _lockedPersonals.Contains(def);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DayManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Day 1 is deferred until NameEntryScreen calls BeginDay1()
    }

    /// <summary>
    /// Called by NameEntryScreen after the player confirms their name.
    /// Generates day 1 ads and fires the OnNewNewspaper event.
    /// </summary>
    public void BeginDay1()
    {
        GenerateTodayAds();
        OnNewNewspaper?.Invoke();
    }

    /// <summary>
    /// Generate today's ads without firing the newspaper event.
    /// Used when restoring mid-day from a save (newspaper already read).
    /// </summary>
    public void GenerateTodayAdsQuiet()
    {
        // Sync day with GameClock (restored from save)
        if (GameClock.Instance != null)
            CurrentDay = GameClock.Instance.CurrentDay;
        GenerateTodayAds();
    }

    public void AdvanceDay()
    {
        CurrentDay++;
        Debug.Log($"[DayManager] Day {CurrentDay}");

        GenerateTodayAds();
        OnDayChanged?.Invoke(CurrentDay);
        OnNewNewspaper?.Invoke();
    }

    private void GenerateTodayAds()
    {
        if (pool == null)
        {
            Debug.LogError("[DayManager] No NewspaperPoolDefinition assigned.");
            return;
        }

        _todayPersonals.Clear();
        _lockedPersonals.Clear();

        // ── Day 1 tutorial: force tutorialDate first, lock the rest ──
        bool isTutorialDay = CurrentDay == 1 && pool.tutorialDate != null;

        if (isTutorialDay)
        {
            _todayPersonals.Add(pool.tutorialDate);

            // Fill remaining slots with other pool members (locked)
            var others = new List<DatePersonalDefinition>(pool.personalAds);
            others.Remove(pool.tutorialDate);
            Shuffle(others);

            int remaining = Mathf.Min(pool.personalAdsPerDay - 1, others.Count);
            for (int i = 0; i < remaining; i++)
            {
                _todayPersonals.Add(others[i]);
                _lockedPersonals.Add(others[i]);
            }

            Debug.Log($"[DayManager] Tutorial day — {pool.tutorialDate.characterName} is the only selectable ad.");
        }
        else
        {
            // Normal day: shuffle and pick
            var availablePersonals = new List<DatePersonalDefinition>(pool.personalAds);

            if (!pool.allowRepeats)
                availablePersonals.RemoveAll(p => _prevPersonals.Contains(p));

            // Remove succeeded characters unless forceAvailable
            availablePersonals.RemoveAll(p =>
                DateHistory.HasSucceeded(p.characterName) && !p.forceAvailable);

            // Safety: if pool is empty after filtering, restore all characters
            if (availablePersonals.Count == 0)
                availablePersonals = new List<DatePersonalDefinition>(pool.personalAds);

            Shuffle(availablePersonals);
            int personalCount = Mathf.Min(pool.personalAdsPerDay, availablePersonals.Count);
            for (int i = 0; i < personalCount; i++)
                _todayPersonals.Add(availablePersonals[i]);
        }

        // Pick commercials (same for all days)
        _todayCommercials.Clear();
        var availableCommercials = new List<CommercialAdDefinition>(pool.commercialAds);

        if (!pool.allowRepeats)
            availableCommercials.RemoveAll(c => _prevCommercials.Contains(c));

        Shuffle(availableCommercials);
        int commercialCount = Mathf.Min(pool.commercialAdsPerDay, availableCommercials.Count);
        for (int i = 0; i < commercialCount; i++)
            _todayCommercials.Add(availableCommercials[i]);

        // Store for next day's repeat prevention
        _prevPersonals.Clear();
        foreach (var p in _todayPersonals) _prevPersonals.Add(p);

        _prevCommercials.Clear();
        foreach (var c in _todayCommercials) _prevCommercials.Add(c);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
