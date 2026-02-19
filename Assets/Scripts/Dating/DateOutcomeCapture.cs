using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static bridge that captures date outcome data at date end
/// so the next morning's AuthoredMessSpawner can filter blueprints.
/// </summary>
public static class DateOutcomeCapture
{
    public struct DateOutcome
    {
        public bool hadDate;
        public bool succeeded;
        public float affection;
        public string characterName;
        public string[] reactionTags;
        public bool drinkServed;
    }

    public static DateOutcome LastOutcome { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset() => LastOutcome = default;

    /// <summary>
    /// Called by DateSessionManager at the end of a date (fail or succeed).
    /// Snapshots the session data for use next morning.
    /// </summary>
    public static void Capture(
        DatePersonalDefinition date,
        float affection,
        bool succeeded,
        IReadOnlyList<DateSessionManager.AccumulatedReaction> reactions)
    {
        var tags = new string[reactions.Count];
        for (int i = 0; i < reactions.Count; i++)
            tags[i] = reactions[i].itemName;

        bool drink = false;
        foreach (var r in reactions)
        {
            if (r.itemName.Contains("drink", System.StringComparison.OrdinalIgnoreCase))
            {
                drink = true;
                break;
            }
        }

        LastOutcome = new DateOutcome
        {
            hadDate = true,
            succeeded = succeeded,
            affection = affection,
            characterName = date != null ? date.characterName : "",
            reactionTags = tags,
            drinkServed = drink
        };

        Debug.Log($"[DateOutcomeCapture] Captured: {LastOutcome.characterName}, " +
                  $"succeeded={succeeded}, affection={affection:F1}, " +
                  $"reactions={tags.Length}, drink={drink}");
    }

    /// <summary>Called after spawning daily mess to reset for the next day.</summary>
    public static void ClearForNewDay() => LastOutcome = default;
}
