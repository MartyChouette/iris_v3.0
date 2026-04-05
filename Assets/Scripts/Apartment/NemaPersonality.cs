using UnityEngine;

/// <summary>
/// Nema's personality configuration — a spider chart with 5 axes.
/// Player allocates a fixed point budget across traits, shaping how
/// Nema speaks and responds during dates.
/// Persists via save system. Adjusted by clicking Nema in the apartment.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Nema Personality")]
public class NemaPersonality : ScriptableObject
{
    public const int TraitCount = 5;
    public const int PointBudget = 10;
    public const int MaxPerTrait = 5;
    public const int MinPerTrait = 0;

    public enum Trait { Warm, Transparent, Playful, Bold, Romantic }

    /// <summary>Trait labels for UI display. Index matches Trait enum.</summary>
    public static readonly string[] TraitNames = { "Warm", "Transparent", "Playful", "Bold", "Romantic" };

    /// <summary>Opposite-end labels for UI display.</summary>
    public static readonly string[] TraitOpposites = { "Aloof", "Mysterious", "Serious", "Shy", "Practical" };

    [Header("Trait Points (0-5 each, total must equal 10)")]
    [Range(0, 5)] public int warm = 2;
    [Range(0, 5)] public int transparent = 2;
    [Range(0, 5)] public int playful = 2;
    [Range(0, 5)] public int bold = 2;
    [Range(0, 5)] public int romantic = 2;

    /// <summary>Get trait value by index (0-5).</summary>
    public int GetTrait(int index)
    {
        return index switch
        {
            0 => warm,
            1 => transparent,
            2 => playful,
            3 => bold,
            4 => romantic,
            _ => 0
        };
    }

    /// <summary>Set trait value by index. Does NOT enforce budget — caller must handle.</summary>
    public void SetTrait(int index, int value)
    {
        value = Mathf.Clamp(value, MinPerTrait, MaxPerTrait);
        switch (index)
        {
            case 0: warm = value; break;
            case 1: transparent = value; break;
            case 2: playful = value; break;
            case 3: bold = value; break;
            case 4: romantic = value; break;
        }
    }

    /// <summary>Get normalized value (0-1) for a trait.</summary>
    public float GetNormalized(int index) => GetTrait(index) / (float)MaxPerTrait;

    /// <summary>Total points currently allocated.</summary>
    public int TotalAllocated => warm + transparent + playful + bold + romantic;

    /// <summary>Points remaining to allocate.</summary>
    public int PointsRemaining => PointBudget - TotalAllocated;

    /// <summary>Get the dominant trait (highest value). Ties broken by enum order.</summary>
    public Trait DominantTrait
    {
        get
        {
            int max = -1;
            int maxIndex = 0;
            for (int i = 0; i < TraitCount; i++)
            {
                int val = GetTrait(i);
                if (val > max) { max = val; maxIndex = i; }
            }
            return (Trait)maxIndex;
        }
    }
}
