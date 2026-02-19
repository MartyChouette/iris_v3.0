using UnityEngine;

/// <summary>
/// ScriptableObject defining a fridge magnet's appearance and identity.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Magnet Definition")]
public class MagnetDefinition : ScriptableObject
{
    public enum MagnetShape { Letter, Food, Flower, Star }

    [Header("Identity")]
    [Tooltip("Unique magnet identifier.")]
    public string magnetId;

    [Tooltip("Display name shown on hover.")]
    public string displayName = "Magnet";

    [Header("Appearance")]
    [Tooltip("Color of the magnet.")]
    public Color magnetColor = Color.red;

    [Tooltip("Shape of the magnet.")]
    public MagnetShape magnetShape = MagnetShape.Flower;
}
