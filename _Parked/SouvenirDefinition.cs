using UnityEngine;

/// <summary>
/// ScriptableObject defining a physical souvenir left behind by a date NPC.
/// Discovered by the player the morning after a date.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Souvenir Definition")]
public class SouvenirDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique souvenir identifier.")]
    public string souvenirId;

    [Tooltip("Display name shown when discovered.")]
    public string displayName = "Souvenir";

    [TextArea(2, 4)]
    [Tooltip("Description of the souvenir.")]
    public string description = "A memento from the date.";

    [Header("Appearance")]
    [Tooltip("Color of the souvenir object.")]
    public Color souvenirColor = Color.white;

    [Header("Behavior")]
    [Tooltip("Can this souvenir be picked up and placed on surfaces?")]
    public bool isPlaceable = true;
}
