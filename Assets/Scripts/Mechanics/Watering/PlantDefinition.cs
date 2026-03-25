using UnityEngine;

/// <summary>
/// Defines a potted plant's watering characteristics.
/// Each soil type absorbs water differently — the player judges
/// moisture by soil color alone, aiming for the perfect shade.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Plant Definition")]
public class PlantDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name shown in HUD.")]
    public string plantName = "Plant";

    [Tooltip("Flavour text shown when browsing.")]
    [TextArea(1, 3)]
    public string description = "";

    [Header("Appearance")]
    [Tooltip("Tint for the pot mesh.")]
    public Color potColor = new Color(0.72f, 0.45f, 0.20f);

    [Tooltip("Stem/leaf tint for the plant visual.")]
    public Color plantColor = new Color(0.2f, 0.6f, 0.2f);

    [Header("Soil Colors")]
    [Tooltip("Soil when bone dry (moisture = 0).")]
    public Color soilDry = new Color(0.65f, 0.50f, 0.32f);

    [Tooltip("Soil at perfect moisture — the color the player is aiming for.")]
    public Color soilPerfect = new Color(0.35f, 0.25f, 0.15f);

    [Tooltip("Soil when waterlogged (moisture = 1). Muddy, too dark.")]
    public Color soilWaterlogged = new Color(0.12f, 0.08f, 0.05f);

    [Header("Watering")]
    [Tooltip("The ideal soil moisture level (0-1). Player aims for this by watching color.")]
    [Range(0f, 1f)]
    public float perfectMoisture = 0.55f;

    [Tooltip("How close to perfectMoisture counts as 'good' (± this value).")]
    [Range(0.01f, 0.3f)]
    public float moistureTolerance = 0.1f;

    [Tooltip("Water units per second added to the pool while pouring.")]
    public float pourRate = 0.25f;

    [Tooltip("How fast the soil soaks up pooled water (units/sec). Low = slow spongy soil, high = fast sandy soil.")]
    public float absorptionRate = 0.15f;

    [Tooltip("Max pooled water that can sit on top before overflowing.")]
    [Range(0.1f, 1f)]
    public float maxPool = 0.5f;

    [Tooltip("How quickly pooled water drains away if it overflows (units/sec).")]
    public float overflowDrainRate = 0.3f;

    [Header("Oscillating Target")]
    [Tooltip("Oscillations per second for the moving target line.")]
    public float targetOscSpeed = 0.5f;

    [Tooltip("How far the target swings above/below perfectMoisture.")]
    public float targetOscAmplitude = 0.08f;

    [Header("Scoring")]
    [Tooltip("Maximum possible score for this plant.")]
    public int baseScore = 100;
}
