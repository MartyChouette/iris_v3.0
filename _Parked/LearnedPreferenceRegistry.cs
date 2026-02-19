using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry tracking what the player has discovered about each date's
/// likes/dislikes through observation. Populated when Like/Dislike reactions fire.
/// </summary>
[Serializable]
public class LearnedPreference
{
    public string characterId;
    public string tag;
    public string reaction;
    public int dayLearned;
    public bool isNew;
}

public static class LearnedPreferenceRegistry
{
    private static readonly List<LearnedPreference> s_preferences = new List<LearnedPreference>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset() => s_preferences.Clear();

    /// <summary>
    /// Record a discovered preference. Returns true if this was a NEW discovery.
    /// </summary>
    public static bool RecordPreference(string characterId, string tag, ReactionType reaction, int day)
    {
        if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(tag)) return false;

        // Check if already known
        if (HasLearned(characterId, tag)) return false;

        var pref = new LearnedPreference
        {
            characterId = characterId,
            tag = tag,
            reaction = reaction.ToString(),
            dayLearned = day,
            isNew = true
        };

        s_preferences.Add(pref);
        Debug.Log($"[LearnedPreferenceRegistry] NEW: {characterId} {reaction} '{tag}' (day {day})");
        return true;
    }

    /// <summary>Check if a preference for this character+tag is already known.</summary>
    public static bool HasLearned(string characterId, string tag)
    {
        for (int i = 0; i < s_preferences.Count; i++)
        {
            if (s_preferences[i].characterId == characterId &&
                string.Equals(s_preferences[i].tag, tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Get all learned preferences for a specific character.</summary>
    public static List<LearnedPreference> GetPreferencesFor(string characterId)
    {
        var result = new List<LearnedPreference>();
        for (int i = 0; i < s_preferences.Count; i++)
        {
            if (s_preferences[i].characterId == characterId)
                result.Add(s_preferences[i]);
        }
        return result;
    }

    /// <summary>Mark a preference as no longer "new" (player has seen the hint).</summary>
    public static void MarkSeen(string characterId, string tag)
    {
        for (int i = 0; i < s_preferences.Count; i++)
        {
            if (s_preferences[i].characterId == characterId &&
                string.Equals(s_preferences[i].tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                s_preferences[i].isNew = false;
                return;
            }
        }
    }

    /// <summary>Load preferences from save data.</summary>
    public static void LoadFrom(List<LearnedPreference> saved)
    {
        s_preferences.Clear();
        if (saved != null)
            s_preferences.AddRange(saved);
    }

    /// <summary>Get all preferences for serialization.</summary>
    public static List<LearnedPreference> GetAllForSave()
    {
        return new List<LearnedPreference>(s_preferences);
    }
}
