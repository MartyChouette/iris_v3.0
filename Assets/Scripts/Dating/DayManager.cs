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

    public List<DatePersonalDefinition> TodayPersonals => _todayPersonals;
    public List<CommercialAdDefinition> TodayCommercials => _todayCommercials;

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

        // Pick personals
        _todayPersonals.Clear();
        var availablePersonals = new List<DatePersonalDefinition>(pool.personalAds);

        if (!pool.allowRepeats)
        {
            availablePersonals.RemoveAll(p => _prevPersonals.Contains(p));
        }

        Shuffle(availablePersonals);
        int personalCount = Mathf.Min(pool.personalAdsPerDay, availablePersonals.Count);
        for (int i = 0; i < personalCount; i++)
            _todayPersonals.Add(availablePersonals[i]);

        // Pick commercials
        _todayCommercials.Clear();
        var availableCommercials = new List<CommercialAdDefinition>(pool.commercialAds);

        if (!pool.allowRepeats)
        {
            availableCommercials.RemoveAll(c => _prevCommercials.Contains(c));
        }

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
