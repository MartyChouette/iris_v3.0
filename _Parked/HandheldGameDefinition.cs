using UnityEngine;

/// <summary>
/// ScriptableObject defining a handheld game cartridge.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Handheld Game Definition")]
public class HandheldGameDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique game identifier.")]
    public string gameId;

    [Tooltip("Display name of the game.")]
    public string gameName = "Flower Catcher";

    [TextArea(2, 4)]
    [Tooltip("Description of the game.")]
    public string description = "Catch falling petals!";

    [Header("Gameplay")]
    [Tooltip("Difficulty level (1-5).")]
    [Range(1, 5)]
    public int difficultyLevel = 2;

    [Tooltip("MoodMachine value while playing (0-1).")]
    [Range(0f, 1f)]
    public float moodValue = 0.3f;
}
