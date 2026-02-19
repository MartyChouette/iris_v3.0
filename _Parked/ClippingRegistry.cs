using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of newspaper clippings saved after each date.
/// Clippings can be displayed on the fridge door.
/// </summary>
public static class ClippingRegistry
{
    private static readonly List<ClippingRecord> s_clippings = new List<ClippingRecord>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset() => s_clippings.Clear();

    public static IReadOnlyList<ClippingRecord> Clippings => s_clippings;

    /// <summary>Save a clipping from a completed date.</summary>
    public static void SaveClipping(RichDateHistoryEntry entry)
    {
        if (entry == null) return;

        var clipping = new ClippingRecord
        {
            characterId = entry.characterId,
            characterName = entry.characterName,
            adTextSnippet = entry.characterName, // brief label
            day = entry.day,
            grade = entry.grade,
            affection = entry.finalAffection
        };

        s_clippings.Add(clipping);
        Debug.Log($"[ClippingRegistry] Saved clipping: {entry.characterName} day {entry.day} ({entry.grade})");
    }

    /// <summary>Get all clippings for a specific character.</summary>
    public static List<ClippingRecord> GetClippingsFor(string characterId)
    {
        var result = new List<ClippingRecord>();
        for (int i = 0; i < s_clippings.Count; i++)
        {
            if (s_clippings[i].characterId == characterId)
                result.Add(s_clippings[i]);
        }
        return result;
    }

    /// <summary>Load from save data.</summary>
    public static void LoadFrom(List<ClippingRecord> saved)
    {
        s_clippings.Clear();
        if (saved != null)
            s_clippings.AddRange(saved);
    }

    /// <summary>Get all for serialization.</summary>
    public static List<ClippingRecord> GetAllForSave()
    {
        return new List<ClippingRecord>(s_clippings);
    }
}
