using UnityEngine;

public enum CookiePattern
{
    ColorCircles,
    MirrorGrid,
    Pinpoints,
    Prism
}

[CreateAssetMenu(menuName = "Iris/Disco Bulb Definition")]
public class DiscoBulbDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name of the bulb.")]
    public string bulbName = "Disco Bulb";

    [Tooltip("Reaction tags applied to the bulb item's ReactableTag.")]
    public string[] reactionTags = new[] { "light", "disco" };

    [Header("Cookie Pattern")]
    [Tooltip("Which procedural cookie pattern to generate.")]
    public CookiePattern pattern = CookiePattern.ColorCircles;

    [Tooltip("Resolution of the generated cookie texture (64-512).")]
    [Range(64, 512)]
    public int cookieResolution = 256;

    [Header("Light")]
    [Tooltip("Base color of the spotlight.")]
    public Color lightColor = Color.white;

    [Tooltip("When true, light color cycles through the gradient over time.")]
    public bool cycleColors;

    [Tooltip("Color gradient for cycling (used when cycleColors is true).")]
    public Gradient colorGradient;

    [Tooltip("Speed of color cycling in cycles per second.")]
    [Range(0.1f, 5f)]
    public float colorCycleSpeed = 0.5f;

    [Tooltip("Rotation speed of the projection in degrees per second.")]
    [Range(0.5f, 20f)]
    public float rotationSpeed = 3f;

    [Tooltip("Spotlight intensity.")]
    [Range(0.5f, 8f)]
    public float lightIntensity = 3f;

    [Tooltip("Spotlight cone angle in degrees.")]
    [Range(15f, 120f)]
    public float spotAngle = 90f;

    [Header("Mood Machine")]
    [Tooltip("Value fed to MoodMachine 'DiscoBall' source (0 = sunny, 1 = stormy).")]
    [Range(0f, 1f)]
    public float moodValue = 0.6f;
}
