using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry tracking all completed dates across the calendar.
/// </summary>
public static class DateHistory
{
    [System.Serializable]
    public class DateHistoryEntry
    {
        public string name;
        public int day;
        public float affection;
        public string grade;
    }

    private static readonly List<DateHistoryEntry> s_entries = new List<DateHistoryEntry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset() => s_entries.Clear();

    public static IReadOnlyList<DateHistoryEntry> Entries => s_entries;

    public static void Record(DateHistoryEntry entry)
    {
        s_entries.Add(entry);
        Debug.Log($"[DateHistory] Recorded: {entry.name} day {entry.day} → {entry.grade} ({entry.affection:F0}%)");
    }

    /// <summary>Return a copy of all entries for serialization.</summary>
    public static List<DateHistoryEntry> GetAllForSave()
    {
        return new List<DateHistoryEntry>(s_entries);
    }

    /// <summary>Replace all entries from loaded save data.</summary>
    public static void LoadFrom(List<DateHistoryEntry> entries)
    {
        s_entries.Clear();
        if (entries != null)
            s_entries.AddRange(entries);
    }
}
