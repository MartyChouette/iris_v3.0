using System.Collections;
using UnityEngine;

/// <summary>
/// Runs the 3 entrance judgments when a date arrives:
///   1. Music    — is there music playing? (active vinyl/music ReactableTag)
///   2. Perfume  — evaluated against date's preferred mood range + smell check
///   3. Outfit   — evaluated against date's style preferences (null = skip)
/// Each judgment: pause → thought bubble → emote → affection change → brief wait.
/// </summary>
public class EntranceJudgmentSequence : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to pause before the first judgment.")]
    [SerializeField] private float _preJudgmentPause = 1.0f;

    [Tooltip("Seconds between judgments.")]
    [SerializeField] private float _interJudgmentPause = 1.5f;

    [Header("Behavior")]
    [Tooltip("When true, all reactions are forced to Like (tutorial/early game).")]
    [SerializeField] private bool _alwaysPositive = true;

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

        // --- Judgment 1: Music ---
        PlayJudgingSFX();
        var musicReaction = EvaluateMusic(date);
        if (_alwaysPositive) musicReaction = ReactionType.Like;
        reactionUI?.ShowReaction(musicReaction);
        DateSessionManager.Instance?.ApplyReaction(musicReaction);
        if (musicReaction == ReactionType.Dislike) PlaySneezeSFX();
        Debug.Log($"[EntranceJudgmentSequence] Music: {musicReaction}");
        DateDebugOverlay.Instance?.LogReaction($"[Entrance] Music → {musicReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);

        // --- Judgment 2: Perfume / Mood + Smell ---
        PlayJudgingSFX();
        var moodReaction = EvaluatePerfumeMood(date);

        // Smell downgrade (only when not always-positive)
        if (!_alwaysPositive)
        {
            float totalSmell = SmellTracker.TotalSmell;
            if (totalSmell > SmellTracker.SmellThreshold * 2f)
                moodReaction = ReactionType.Dislike;
            else if (totalSmell > SmellTracker.SmellThreshold && moodReaction == ReactionType.Like)
                moodReaction = ReactionType.Neutral;
        }

        if (_alwaysPositive) moodReaction = ReactionType.Like;
        reactionUI?.ShowReaction(moodReaction);
        DateSessionManager.Instance?.ApplyReaction(moodReaction);
        if (moodReaction == ReactionType.Dislike) PlaySneezeSFX();
        Debug.Log($"[EntranceJudgmentSequence] Perfume/Mood: {moodReaction}");
        DateDebugOverlay.Instance?.LogReaction($"[Entrance] Perfume → {moodReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);

        // --- Judgment 3: Outfit ---
        PlayJudgingSFX();
        var outfitReaction = EvaluateOutfit(date);
        if (_alwaysPositive) outfitReaction = ReactionType.Like;
        reactionUI?.ShowReaction(outfitReaction);
        DateSessionManager.Instance?.ApplyReaction(outfitReaction);
        if (outfitReaction == ReactionType.Dislike) PlaySneezeSFX();
        Debug.Log($"[EntranceJudgmentSequence] Outfit: {outfitReaction}");
        DateDebugOverlay.Instance?.LogReaction($"[Entrance] Outfit → {outfitReaction}");
        yield return new WaitForSeconds(_interJudgmentPause);

        // --- Judgment 4: Cleanliness ---
        PlayJudgingSFX();
        var cleanReaction = EvaluateCleanliness();
        if (_alwaysPositive) cleanReaction = ReactionType.Like;
        reactionUI?.ShowReaction(cleanReaction);
        DateSessionManager.Instance?.ApplyReaction(cleanReaction);
        if (cleanReaction == ReactionType.Dislike) PlaySneezeSFX();
        Debug.Log($"[EntranceJudgmentSequence] Cleanliness: {cleanReaction}");
        DateDebugOverlay.Instance?.LogReaction($"[Entrance] Cleanliness → {cleanReaction}");
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

    private ReactionType EvaluateMusic(DatePersonalDefinition date)
    {
        // Scan ReactableTag.All for active tags containing "vinyl" or "music"
        foreach (var tag in ReactableTag.All)
        {
            if (!tag.IsActive) continue;
            foreach (var t in tag.Tags)
            {
                if (t.Contains("vinyl") || t.Contains("music"))
                    return ReactionEvaluator.EvaluateReactable(tag, date.preferences);
            }
        }
        // No music playing — neutral
        return ReactionType.Neutral;
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
        float tidiness = TidyScorer.Instance != null ? TidyScorer.Instance.OverallTidiness : 1f;
        return ReactionEvaluator.EvaluateCleanliness(tidiness);
    }
}
