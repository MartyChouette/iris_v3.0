using UnityEngine;

/// <summary>
/// ScriptableObject defining conditions that must be met for content to appear.
/// Used to gate newspaper ads, items, and events based on game progression.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Narrative Condition")]
public class NarrativeCondition : ScriptableObject
{
    [Header("Day")]
    [Tooltip("Minimum day number required (0 = no minimum).")]
    public int minDay;

    [Tooltip("Maximum day number allowed (0 = no maximum).")]
    public int maxDay;

    [Header("Dates")]
    [Tooltip("Minimum number of completed dates required.")]
    public int minDatesCompleted;

    [Tooltip("Character IDs (SO asset names) that must have been dated.")]
    public string[] requiredDatedCharacterIds;

    [Tooltip("Minimum best affection score across all dates.")]
    public float minBestAffection;

    [Header("Preferences")]
    [Tooltip("Learned preferences that must exist before this unlocks.")]
    public PreferenceRequirement[] requiredPreferences;
}

/// <summary>
/// A specific learned preference requirement.
/// </summary>
[System.Serializable]
public class PreferenceRequirement
{
    [Tooltip("Character ID (SO asset name).")]
    public string characterId;

    [Tooltip("Tag that must have been learned.")]
    public string tag;
}
