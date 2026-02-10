using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Mood Machine Profile")]
public class MoodMachineProfile : ScriptableObject
{
    [Header("Directional Light")]
    [Tooltip("Light color across mood range (0 = sunny warm cream, 1 = stormy cold blue-grey).")]
    public Gradient lightColor;

    [Tooltip("Light intensity across mood range (e.g. 1.2 at sunny, 0.4 at stormy).")]
    public AnimationCurve lightIntensity = AnimationCurve.Linear(0f, 1.2f, 1f, 0.4f);

    [Tooltip("Sun pitch angle across mood range (e.g. 50° at sunny, 25° at stormy).")]
    public AnimationCurve lightAngleX = AnimationCurve.Linear(0f, 50f, 1f, 25f);

    [Header("Ambient")]
    [Tooltip("RenderSettings.ambientLight color (sky blue → dark slate).")]
    public Gradient ambientColor;

    [Header("Fog")]
    [Tooltip("RenderSettings.fogColor (light haze → dark grey).")]
    public Gradient fogColor;

    [Tooltip("RenderSettings.fogDensity (0.001 → 0.03).")]
    public AnimationCurve fogDensity = AnimationCurve.Linear(0f, 0.001f, 1f, 0.03f);

    [Header("Effects")]
    [Tooltip("Rain particle emission rate (0 → 200/sec).")]
    public AnimationCurve rainRate = AnimationCurve.Linear(0f, 0f, 1f, 200f);

    [Tooltip("Window material emission strength (0 → 1).")]
    public AnimationCurve windowEmission = AnimationCurve.Linear(0f, 0f, 1f, 1f);
}
