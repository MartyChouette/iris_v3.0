using UnityEngine;

/// <summary>
/// Serializable data block defining what a date character likes and dislikes.
/// Stored on <see cref="DatePersonalDefinition"/>.
/// </summary>
[System.Serializable]
public class DatePreferences
{
    [Header("Item Tags")]
    [Tooltip("Tags this date likes. Matched against ReactableTag.")]
    public string[] likedTags = { };

    [Tooltip("Tags this date dislikes.")]
    public string[] dislikedTags = { };

    [Header("Mood")]
    [Tooltip("Preferred mood range minimum (0=sunny, 1=stormy).")]
    [Range(0f, 1f)]
    public float preferredMoodMin = 0.2f;

    [Tooltip("Preferred mood range maximum.")]
    [Range(0f, 1f)]
    public float preferredMoodMax = 0.5f;

    [Header("Drinks")]
    [Tooltip("Drink recipes this date enjoys.")]
    public DrinkRecipeDefinition[] likedDrinks = { };

    [Tooltip("Drink recipes this date dislikes.")]
    public DrinkRecipeDefinition[] dislikedDrinks = { };

    [Header("Outfit")]
    [Tooltip("Outfit style tags this date likes.")]
    public string[] likedOutfitTags = { };

    [Tooltip("Outfit style tags this date dislikes.")]
    public string[] dislikedOutfitTags = { };

    [Header("Clutter")]
    [Tooltip("How much floor clutter this date tolerates (1 = doesn't care, 0 = hates it).")]
    [Range(0f, 1f)]
    public float clutterTolerance = 0.5f;

    [Header("Personality")]
    [Tooltip("Multiplier on reaction strength. >1 = expressive, <1 = reserved.")]
    public float reactionStrength = 1f;
}
