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

    [Header("Audio")]
    [Tooltip("SFX played before each judgment evaluation.")]
    [SerializeField] private AudioClip judgingSFX;

    [Tooltip("SFX played on a Dislike judgment (comedic sneeze).")]
    [SerializeField] private AudioClip sneezeSFX;

    /// <summary>
    /// Run all 3 entrance judgments. Yields until complete.
    /// </summary>
    public IEnumerator RunJudgments(DateReactionUI reactionUI, DatePersonalDefinition date)
    {
        if (date == null) yield break;

        yield return new WaitForSeconds(_preJudgmentPause);

        // --- Judgment 1: Outfit ---
        PlayJudgingSFX();
        var outfitReaction = EvaluateOutfit(date);
        reactionUI?.ShowReaction(outfitReaction);
        DateSessionManager.Instance?.ApplyReaction(outfitReaction);
        if (outfitReaction == ReactionType.Dislike) PlaySneezeSFX();
        Debug.Log($"[EntranceJudgmentSequence] Outfit: {outfitReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);

        // --- Judgment 2: Perfume / Mood ---
        PlayJudgingSFX();
        var moodReaction = EvaluatePerfumeMood(date);
        reactionUI?.ShowReaction(moodReaction);
        DateSessionManager.Instance?.ApplyReaction(moodReaction);
        if (moodReaction == ReactionType.Dislike) PlaySneezeSFX();
        Debug.Log($"[EntranceJudgmentSequence] Perfume/Mood: {moodReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);

        // --- Judgment 3: Cleanliness ---
        PlayJudgingSFX();
        var cleanReaction = EvaluateCleanliness();
        reactionUI?.ShowReaction(cleanReaction);
        DateSessionManager.Instance?.ApplyReaction(cleanReaction);
        if (cleanReaction == ReactionType.Dislike) PlaySneezeSFX();
        Debug.Log($"[EntranceJudgmentSequence] Cleanliness: {cleanReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);
    }

    private void PlayJudgingSFX()
    {
        if (judgingSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(judgingSFX);
    }

    private void PlaySneezeSFX()
    {
        if (sneezeSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(sneezeSFX);
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
