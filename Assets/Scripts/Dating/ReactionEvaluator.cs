using UnityEngine;

/// <summary>
/// Static utility for evaluating how a date character reacts to various stimuli.
/// </summary>
public static class ReactionEvaluator
{
    /// <summary>Evaluate a reactable object against date preferences.</summary>
    public static ReactionType EvaluateReactable(ReactableTag tag, DatePreferences prefs)
    {
        if (tag == null || prefs == null) return ReactionType.Neutral;

        foreach (string t in tag.Tags)
        {
            foreach (string liked in prefs.likedTags)
            {
                if (string.Equals(t, liked, System.StringComparison.OrdinalIgnoreCase))
                    return ReactionType.Like;
            }
        }

        foreach (string t in tag.Tags)
        {
            foreach (string disliked in prefs.dislikedTags)
            {
                if (string.Equals(t, disliked, System.StringComparison.OrdinalIgnoreCase))
                    return ReactionType.Dislike;
            }
        }

        return ReactionType.Neutral;
    }

    /// <summary>Evaluate a drink against date preferences and quality.</summary>
    public static ReactionType EvaluateDrink(DrinkRecipeDefinition recipe, int score, DatePreferences prefs)
    {
        if (recipe == null || prefs == null) return ReactionType.Neutral;

        // Check liked drinks
        if (prefs.likedDrinks != null)
        {
            foreach (var liked in prefs.likedDrinks)
            {
                if (liked == recipe && score >= 60)
                    return ReactionType.Like;
            }
        }

        // Check disliked drinks
        if (prefs.dislikedDrinks != null)
        {
            foreach (var disliked in prefs.dislikedDrinks)
            {
                if (disliked == recipe)
                    return ReactionType.Dislike;
            }
        }

        // A well-made drink is always nice
        if (score >= 80)
            return ReactionType.Like;

        return ReactionType.Neutral;
    }

    /// <summary>Evaluate an outfit against the date's style preferences.</summary>
    public static ReactionType EvaluateOutfit(OutfitDefinition outfit, DatePreferences prefs)
    {
        if (outfit == null || prefs == null) return ReactionType.Neutral;
        if (outfit.styleTags == null) return ReactionType.Neutral;

        // Check liked outfit tags first
        if (prefs.likedOutfitTags != null)
        {
            foreach (string tag in outfit.styleTags)
            {
                foreach (string liked in prefs.likedOutfitTags)
                {
                    if (string.Equals(tag, liked, System.StringComparison.OrdinalIgnoreCase))
                        return ReactionType.Like;
                }
            }
        }

        // Check disliked outfit tags
        if (prefs.dislikedOutfitTags != null)
        {
            foreach (string tag in outfit.styleTags)
            {
                foreach (string disliked in prefs.dislikedOutfitTags)
                {
                    if (string.Equals(tag, disliked, System.StringComparison.OrdinalIgnoreCase))
                        return ReactionType.Dislike;
                }
            }
        }

        return ReactionType.Neutral;
    }

    /// <summary>Evaluate apartment cleanliness/tidiness. 0 = filthy, 1 = spotless.</summary>
    public static ReactionType EvaluateCleanliness(float tidiness)
    {
        if (tidiness >= 0.8f) return ReactionType.Like;
        if (tidiness >= 0.5f) return ReactionType.Neutral;
        return ReactionType.Dislike;
    }

    /// <summary>Evaluate how the current mood matches the date's preferences.</summary>
    public static ReactionType EvaluateMood(float currentMood, DatePreferences prefs)
    {
        if (prefs == null) return ReactionType.Neutral;

        if (currentMood >= prefs.preferredMoodMin && currentMood <= prefs.preferredMoodMax)
            return ReactionType.Like;

        float distMin = Mathf.Abs(currentMood - prefs.preferredMoodMin);
        float distMax = Mathf.Abs(currentMood - prefs.preferredMoodMax);
        float distance = Mathf.Min(distMin, distMax);

        if (distance > 0.3f)
            return ReactionType.Dislike;

        return ReactionType.Neutral;
    }
}
