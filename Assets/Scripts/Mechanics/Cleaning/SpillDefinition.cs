using UnityEngine;

/// <summary>
/// Defines a type of spill or dirt for the cleaning prototype.
/// Stubbornness controls how much spray is needed before wiping is effective.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Spill Definition")]
public class SpillDefinition : ScriptableObject
{
    public enum SpillType { Dirt, Liquid, Mixed }

    [Header("Type")]
    [Tooltip("Category of spill — affects HUD hints.")]
    public SpillType spillType;

    [Header("Appearance")]
    [Tooltip("Base colour of the spill/dirt.")]
    public Color spillColor = new Color(0.35f, 0.20f, 0.08f);

    [Tooltip("Fraction of texture radius covered by the splatter (0-1).")]
    [Range(0f, 1f)]
    public float coverage = 0.35f;

    [Tooltip("Seed for Perlin noise. Change for different splatter shapes.")]
    public int seed = 42;

    [Tooltip("Texture resolution (square).")]
    public int textureSize = 256;

    [Header("Behavior")]
    [Tooltip("0 = wipes instantly, 1 = must spray first. Controls dry-wipe effectiveness.")]
    [Range(0f, 1f)]
    public float stubbornness;

    [Tooltip("Display name shown in HUD.")]
    public string displayName = "Spill";
}
