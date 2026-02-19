using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-scoped singleton tracking per-plant health across days.
/// Health tiers: Healthy → Wilting → Dead, with recovery via watering.
/// </summary>
public class PlantHealthTracker : MonoBehaviour
{
    public static PlantHealthTracker Instance { get; private set; }

    public enum HealthTier { Healthy, Wilting, Dead }
    public enum WaterQuality { Perfect, Normal, Missed }

    [System.Serializable]
    public class PlantHealthState
    {
        public string plantId;
        public HealthTier tier = HealthTier.Healthy;
        public int lastWateredDay = -1;
        public WaterQuality lastQuality = WaterQuality.Missed;
        public int consecutiveMisses;
        public int recoveryProgress;
    }

    private readonly Dictionary<string, PlantHealthState> _states = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlantHealthTracker] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Register a plant by id. Creates Healthy state if not already tracked.</summary>
    public void RegisterPlant(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!_states.ContainsKey(id))
            _states[id] = new PlantHealthState { plantId = id };
    }

    /// <summary>Record a watering event. Score > 90 = Perfect, else Normal.</summary>
    public void RecordWatering(string id, int score)
    {
        if (!_states.TryGetValue(id, out var state)) return;

        int day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
        state.lastWateredDay = day;
        state.lastQuality = score > 90 ? WaterQuality.Perfect : WaterQuality.Normal;
        state.consecutiveMisses = 0;

        Debug.Log($"[PlantHealthTracker] {id} watered (score={score}, quality={state.lastQuality}).");
    }

    /// <summary>
    /// Process end-of-day health transitions for all plants.
    /// Called by DayPhaseManager at exploration start.
    /// </summary>
    public void ProcessDayTransition(int newDay)
    {
        foreach (var kv in _states)
        {
            var s = kv.Value;
            bool wateredToday = s.lastWateredDay == newDay - 1;

            if (!wateredToday)
            {
                s.consecutiveMisses++;
                s.lastQuality = WaterQuality.Missed;
            }

            HealthTier prev = s.tier;

            switch (s.tier)
            {
                case HealthTier.Healthy:
                    if (!wateredToday)
                        s.tier = HealthTier.Wilting;
                    break;

                case HealthTier.Wilting:
                    if (wateredToday && s.lastQuality == WaterQuality.Perfect)
                        s.tier = HealthTier.Healthy;
                    else if (!wateredToday)
                        s.tier = HealthTier.Dead;
                    // Normal watering keeps Wilting but resets misses (already done above)
                    break;

                case HealthTier.Dead:
                    if (wateredToday && s.lastQuality == WaterQuality.Perfect)
                    {
                        s.tier = HealthTier.Wilting;
                        s.recoveryProgress = 0;
                    }
                    else if (wateredToday)
                    {
                        s.recoveryProgress++;
                        if (s.recoveryProgress >= 2)
                        {
                            s.tier = HealthTier.Wilting;
                            s.recoveryProgress = 0;
                        }
                    }
                    break;
            }

            if (s.tier != prev)
                Debug.Log($"[PlantHealthTracker] {s.plantId}: {prev} → {s.tier}");

            ApplyVisuals(s.plantId);
        }
    }

    /// <summary>Push visual health value to the plant's PlantHealthVisualDriver.</summary>
    public void ApplyVisuals(string id)
    {
        if (!_states.TryGetValue(id, out var state)) return;

        float h = state.tier switch
        {
            HealthTier.Healthy => 1f,
            HealthTier.Wilting => 0.5f,
            HealthTier.Dead => 0f,
            _ => 1f
        };

        var drivers = FindObjectsByType<PlantHealthVisualDriver>(FindObjectsSortMode.None);
        foreach (var driver in drivers)
        {
            if (driver.PlantId == id)
            {
                driver.ApplyHealth(h);
                return;
            }
        }
    }

    /// <summary>Apply visuals for all tracked plants (e.g. after load).</summary>
    public void ApplyAllVisuals()
    {
        foreach (var kv in _states)
            ApplyVisuals(kv.Key);
    }

    // ── Persistence ───────────────────────────────────────────────

    public List<PlantHealthRecord> GetAllForSave()
    {
        var list = new List<PlantHealthRecord>();
        foreach (var kv in _states)
        {
            var s = kv.Value;
            list.Add(new PlantHealthRecord
            {
                plantId = s.plantId,
                tier = (int)s.tier,
                lastWateredDay = s.lastWateredDay,
                lastQuality = (int)s.lastQuality,
                consecutiveMisses = s.consecutiveMisses,
                recoveryProgress = s.recoveryProgress
            });
        }
        return list;
    }

    public void LoadFrom(List<PlantHealthRecord> records)
    {
        if (records == null) return;

        _states.Clear();
        foreach (var r in records)
        {
            _states[r.plantId] = new PlantHealthState
            {
                plantId = r.plantId,
                tier = (HealthTier)r.tier,
                lastWateredDay = r.lastWateredDay,
                lastQuality = (WaterQuality)r.lastQuality,
                consecutiveMisses = r.consecutiveMisses,
                recoveryProgress = r.recoveryProgress
            };
        }

        ApplyAllVisuals();
    }
}
