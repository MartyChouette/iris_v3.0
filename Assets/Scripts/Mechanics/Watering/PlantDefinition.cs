using UnityEngine;

/// <summary>
/// Defines a single potted plant for the watering prototype.
/// Controls water needs, foam behaviour, and scoring thresholds.
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

    [Tooltip("Soil colour when dry.")]
    public Color dryColor = new Color(0.55f, 0.40f, 0.25f);

    [Tooltip("Soil colour when fully saturated.")]
    public Color wetColor = new Color(0.30f, 0.22f, 0.12f);

    [Tooltip("Bubbly foam tint when dirt foams up.")]
    public Color foamColor = new Color(0.50f, 0.38f, 0.22f, 0.7f);

    [Tooltip("Stem/leaf tint for the plant visual on the shelf.")]
    public Color plantColor = new Color(0.2f, 0.6f, 0.2f);

    [Header("Watering")]
    [Tooltip("Target fill level (0-1). The fill line sits here.")]
    [Range(0f, 1f)]
    public float idealWaterLevel = 0.7f;

    [Tooltip("Tolerance around ideal level for a perfect score.")]
    [Range(0.01f, 0.2f)]
    public float waterTolerance = 0.08f;

    [Tooltip("Water units per second while pouring.")]
    public float pourRate = 0.12f;

    [Tooltip("Foam rises this many times faster than water (dirt bubbles up).")]
    public float foamRateMultiplier = 2.0f;

    [Tooltip("Units/sec foam collapses toward water level when not pouring.")]
    public float foamSettleRate = 0.25f;

    [Tooltip("Units/sec water absorbs into soil (reduces visible foam).")]
    public float absorptionRate = 0.05f;

    [Header("Scoring")]
    [Tooltip("Maximum possible score for this plant.")]
    public int baseScore = 100;
}
