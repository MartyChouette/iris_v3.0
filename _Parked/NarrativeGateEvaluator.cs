using UnityEngine;

/// <summary>
/// Static utility for evaluating NarrativeCondition gates.
/// Queries DateHistory, LearnedPreferenceRegistry, and GameClock.
/// </summary>
public static class NarrativeGateEvaluator
{
    /// <summary>
    /// Evaluate whether a condition is met. Returns true if condition is null (always available).
    /// </summary>
    public static bool Evaluate(NarrativeCondition condition)
    {
        if (condition == null) return true;

        int currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;

        // Day range
        if (condition.minDay > 0 && currentDay < condition.minDay)
            return false;
        if (condition.maxDay > 0 && currentDay > condition.maxDay)
            return false;

        // Minimum dates completed
        if (condition.minDatesCompleted > 0 && DateHistory.TotalDatesCompleted() < condition.minDatesCompleted)
            return false;

        // Required dated characters
        if (condition.requiredDatedCharacterIds != null)
        {
            foreach (string charId in condition.requiredDatedCharacterIds)
            {
                if (!string.IsNullOrEmpty(charId) && !DateHistory.HasDated(charId))
                    return false;
            }
        }

        // Minimum best affection
        if (condition.minBestAffection > 0f && DateHistory.BestAffection() < condition.minBestAffection)
            return false;

        // Required learned preferences
        if (condition.requiredPreferences != null)
        {
            foreach (var req in condition.requiredPreferences)
            {
                if (!string.IsNullOrEmpty(req.characterId) && !string.IsNullOrEmpty(req.tag))
                {
                    if (!LearnedPreferenceRegistry.HasLearned(req.characterId, req.tag))
                        return false;
                }
            }
        }

        return true;
    }
}
