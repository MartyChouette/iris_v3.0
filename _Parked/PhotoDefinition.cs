using UnityEngine;

/// <summary>
/// ScriptableObject defining a photograph hidden in a book.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Photo Definition")]
public class PhotoDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique photo identifier.")]
    public string photoId;

    [TextArea(2, 4)]
    [Tooltip("Description shown when the photo is found.")]
    public string description = "An old photograph.";

    [Header("Visuals")]
    [Tooltip("Sprite for the photo (optional, for UI display).")]
    public Sprite sprite;
}
