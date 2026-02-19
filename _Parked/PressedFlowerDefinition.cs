using UnityEngine;

/// <summary>
/// ScriptableObject defining a pressed flower hidden in a book.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Pressed Flower Definition")]
public class PressedFlowerDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique flower identifier.")]
    public string flowerId;

    [Tooltip("Display name of the pressed flower.")]
    public string flowerName = "Pressed Flower";

    [TextArea(2, 4)]
    [Tooltip("Description shown when the flower is found.")]
    public string description = "A delicate pressed flower.";

    [Header("Visuals")]
    [Tooltip("Sprite for the flower (optional, for UI display).")]
    public Sprite sprite;

    [Header("Meaning")]
    [TextArea(1, 3)]
    [Tooltip("The flower's symbolic meaning (flower language).")]
    public string meaning = "Beauty";
}
