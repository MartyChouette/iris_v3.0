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
        public bool succeeded;
        public List<string> learnedLikes = new List<string>();
        public List<string> learnedDislikes = new List<string>();
    }

    private static readonly List<DateHistoryEntry> s_entries = new List<DateHistoryEntry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset() => s_entries.Clear();

    public static IReadOnlyList<DateHistoryEntry> Entries => s_entries;

    /// <summary>True if the named character has a succeeded entry in history.</summary>
    public static bool HasSucceeded(string characterName)
    {
        for (int i = 0; i < s_entries.Count; i++)
            if (s_entries[i].name == characterName && s_entries[i].succeeded)
                return true;
        return false;
    }

    /// <summary>Get the most recent entry for a character, or null.</summary>
    public static DateHistoryEntry GetLatestEntry(string characterName)
    {
        for (int i = s_entries.Count - 1; i >= 0; i--)
            if (s_entries[i].name == characterName)
                return s_entries[i];
        return null;
    }

    public static void Record(DateHistoryEntry entry)
    {
        s_entries.Add(entry);
        Debug.Log($"[DateHistory] Recorded: {entry.name} day {entry.day} → {entry.grade} ({entry.affection:F0}%) succeeded={entry.succeeded}");
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
