using System.Collections;
using UnityEngine;

/// <summary>
/// Runs the 3 entrance judgments when a date arrives:
///   1. Outfit    — evaluated against date's style preferences
///   2. Perfume/Mood — evaluated against date's preferred mood range
///   3. Cleanliness — based on remaining uncleaned stains
/// Each judgment: pause → thought bubble → emote → affection change → brief wait.
/// </summary>
public class EntranceJudgmentSequence : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to pause before the first judgment.")]
    [SerializeField] private float _preJudgmentPause = 1.0f;

    [Tooltip("Seconds between judgments.")]
    [SerializeField] private float _interJudgmentPause = 1.5f;

    /// <summary>
    /// Run all 3 entrance judgments. Yields until complete.
    /// </summary>
    public IEnumerator RunJudgments(DateReactionUI reactionUI, DatePersonalDefinition date)
    {
        if (date == null) yield break;

        yield return new WaitForSeconds(_preJudgmentPause);

        // --- Judgment 1: Outfit ---
        var outfitReaction = EvaluateOutfit(date);
        reactionUI?.ShowReaction(outfitReaction);
        DateSessionManager.Instance?.ApplyReaction(outfitReaction);
        Debug.Log($"[EntranceJudgmentSequence] Outfit: {outfitReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);

        // --- Judgment 2: Perfume / Mood ---
        var moodReaction = EvaluatePerfumeMood(date);
        reactionUI?.ShowReaction(moodReaction);
        DateSessionManager.Instance?.ApplyReaction(moodReaction);
        Debug.Log($"[EntranceJudgmentSequence] Perfume/Mood: {moodReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);

        // --- Judgment 3: Cleanliness ---
        var cleanReaction = EvaluateCleanliness();
        reactionUI?.ShowReaction(cleanReaction);
        DateSessionManager.Instance?.ApplyReaction(cleanReaction);
        Debug.Log($"[EntranceJudgmentSequence] Cleanliness: {cleanReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);
    }

    private ReactionType EvaluateOutfit(DatePersonalDefinition date)
    {
        var outfit = OutfitSelector.Instance?.SelectedOutfit;
        return ReactionEvaluator.EvaluateOutfit(outfit, date.preferences);
    }

    private ReactionType EvaluatePerfumeMood(DatePersonalDefinition date)
    {
        float mood = MoodMachine.Instance?.Mood ?? 0f;
        return ReactionEvaluator.EvaluateMood(mood, date.preferences);
    }

    private ReactionType EvaluateCleanliness()
    {
        int dirtyCount = 0;
        var surfaces = FindObjectsByType<CleanableSurface>(FindObjectsSortMode.None);
        foreach (var s in surfaces)
        {
            if (s.gameObject.activeInHierarchy && !s.IsFullyClean)
                dirtyCount++;
        }

        if (dirtyCount == 0) return ReactionType.Like;
        if (dirtyCount <= 2) return ReactionType.Neutral;
        return ReactionType.Dislike;
    }
}
