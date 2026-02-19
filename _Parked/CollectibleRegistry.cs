using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry tracking discovered collectibles (photographs, pressed flowers).
/// Found items are recorded here and persisted via IrisSaveData.
/// </summary>
public static class CollectibleRegistry
{
    private static readonly List<CollectibleRecord> s_records = new List<CollectibleRecord>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset() => s_records.Clear();

    public static IReadOnlyList<CollectibleRecord> Records => s_records;

    /// <summary>Record a discovered collectible. Returns true if new.</summary>
    public static bool Discover(string collectibleId, string type, int day)
    {
        if (HasFound(collectibleId)) return false;

        s_records.Add(new CollectibleRecord
        {
            collectibleId = collectibleId,
            collectibleType = type,
            dayFound = day
        });

        Debug.Log($"[CollectibleRegistry] Discovered: {collectibleId} ({type}) on day {day}");
        return true;
    }

    /// <summary>Check if a collectible has already been found.</summary>
    public static bool HasFound(string collectibleId)
    {
        for (int i = 0; i < s_records.Count; i++)
        {
            if (s_records[i].collectibleId == collectibleId)
                return true;
        }
        return false;
    }

    /// <summary>Load from save data.</summary>
    public static void LoadFrom(List<CollectibleRecord> saved)
    {
        s_records.Clear();
        if (saved != null)
            s_records.AddRange(saved);
    }

    /// <summary>Get all for serialization.</summary>
    public static List<CollectibleRecord> GetAllForSave()
    {
        return new List<CollectibleRecord>(s_records);
    }
}
