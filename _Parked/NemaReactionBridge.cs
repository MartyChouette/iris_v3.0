using System.Collections;
using UnityEngine;

/// <summary>
/// Maps NPC reactions + context to Nema's internal thoughts.
/// Fires 0.8-1.2s after NPC reaction (she's observing, then processing).
/// Insight mood = learned preference moment → gold text.
/// </summary>
public class NemaReactionBridge : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Minimum delay after NPC reaction before Nema's thought appears.")]
    [SerializeField] private float _minDelay = 0.8f;

    [Tooltip("Maximum delay after NPC reaction before Nema's thought appears.")]
    [SerializeField] private float _maxDelay = 1.2f;

    private void Start()
    {
        if (DateSessionManager.Instance != null)
            DateSessionManager.Instance.OnReactionApplied += OnReaction;
    }

    private void OnDestroy()
    {
        if (DateSessionManager.Instance != null)
            DateSessionManager.Instance.OnReactionApplied -= OnReaction;
    }

    private void OnReaction(ReactionType type, ReactableTag tag, float delta)
    {
        if (NemaThoughtBubble.Instance == null) return;

        string context = GetTagContext(tag);
        string thought = GetThought(type, context);
        ThoughtMood mood = GetMood(type, tag);

        if (string.IsNullOrEmpty(thought)) return;

        float delay = Random.Range(_minDelay, _maxDelay);
        StartCoroutine(DelayedThought(thought, mood, delay));
    }

    private IEnumerator DelayedThought(string thought, ThoughtMood mood, float delay)
    {
        yield return new WaitForSeconds(delay);
        NemaThoughtBubble.Instance?.ShowThought(thought, mood);
    }

    private static string GetTagContext(ReactableTag tag)
    {
        if (tag == null) return "general";

        foreach (string t in tag.Tags)
        {
            string lower = t.ToLowerInvariant();
            if (lower.Contains("outfit")) return "outfit";
            if (lower.Contains("perfume")) return "perfume";
            if (lower.Contains("vinyl") || lower.Contains("record") || lower.Contains("music")) return "music";
            if (lower.Contains("plant") || lower.Contains("flower")) return "plant";
            if (lower.Contains("book") || lower.Contains("coffee_table_book")) return "book";
            if (lower.Contains("trinket") || lower.Contains("gundam")) return "trinket";
            if (lower.Contains("drink")) return "drink";
            if (lower.Contains("clean")) return "cleanliness";
        }
        return "general";
    }

    private static string GetThought(ReactionType type, string context)
    {
        return (type, context) switch
        {
            // Love reactions
            (ReactionType.Love, "outfit") => "They really noticed the outfit!",
            (ReactionType.Love, "drink") => "Nailed it! They love the drink!",
            (ReactionType.Love, "music") => "They're absolutely vibing with this record!",
            (ReactionType.Love, _) => "They really love this!",

            // Like reactions
            (ReactionType.Like, "music") => "They seem to enjoy the record.",
            (ReactionType.Like, "plant") => "I think they like plants!",
            (ReactionType.Like, "book") => "Good taste in reading material...",
            (ReactionType.Like, "trinket") => "They're into the trinket!",
            (ReactionType.Like, "perfume") => "The scent seems to be working.",
            (ReactionType.Like, _) => "They seem happy about something.",

            // Curious reactions
            (ReactionType.Curious, _) => "Hmm, they seem curious about that...",

            // Surprised reactions
            (ReactionType.Surprised, "cleanliness") => "They noticed how clean everything is!",
            (ReactionType.Surprised, _) => "Oh, that caught them off guard!",

            // Nostalgic reactions
            (ReactionType.Nostalgic, _) => "Seems like this reminds them of something...",

            // Uncomfortable reactions
            (ReactionType.Uncomfortable, "perfume") => "Something about the vibe is off...",
            (ReactionType.Uncomfortable, _) => "I don't think they're fully comfortable...",

            // Dislike reactions
            (ReactionType.Dislike, "outfit") => "Maybe I should have dressed differently...",
            (ReactionType.Dislike, "music") => "Note to self: they hate this music.",
            (ReactionType.Dislike, "perfume") => "Wrong perfume choice...",
            (ReactionType.Dislike, "cleanliness") => "I should have cleaned more...",
            (ReactionType.Dislike, _) => "That wasn't a good sign...",

            // Neutral — no thought
            (ReactionType.Neutral, _) => null,
            _ => null
        };
    }

    private static ThoughtMood GetMood(ReactionType type, ReactableTag tag)
    {
        // Insight mood when a new preference was learned (Like/Dislike)
        if (type == ReactionType.Like || type == ReactionType.Dislike ||
            type == ReactionType.Love || type == ReactionType.Nostalgic)
        {
            if (tag != null && DateSessionManager.Instance?.CurrentDate != null)
            {
                string charId = DateSessionManager.Instance.CurrentDate.name;
                foreach (string t in tag.Tags)
                {
                    // Check if this was a NEW learning
                    if (LearnedPreferenceRegistry.HasLearned(charId, t))
                    {
                        var prefs = LearnedPreferenceRegistry.GetPreferencesFor(charId);
                        foreach (var p in prefs)
                        {
                            if (p.tag == t && p.isNew)
                                return ThoughtMood.Insight;
                        }
                    }
                }
            }
        }

        return type switch
        {
            ReactionType.Love => ThoughtMood.Positive,
            ReactionType.Like => ThoughtMood.Positive,
            ReactionType.Curious => ThoughtMood.Observation,
            ReactionType.Surprised => ThoughtMood.Positive,
            ReactionType.Nostalgic => ThoughtMood.Insight,
            ReactionType.Uncomfortable => ThoughtMood.Negative,
            ReactionType.Dislike => ThoughtMood.Negative,
            _ => ThoughtMood.Observation
        };
    }
}
