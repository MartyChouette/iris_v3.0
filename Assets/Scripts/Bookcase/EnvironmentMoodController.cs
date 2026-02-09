using System.Collections;
using UnityEngine;

/// <summary>
/// Holds references to the directional light and ambient particle system.
/// Perfume sprays call ApplyPerfumeMood() to lerp lighting color/intensity.
/// </summary>
public class EnvironmentMoodController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Directional light whose color/intensity is modified by perfume.")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Optional ambient particle system to tint with perfume mood.")]
    [SerializeField] private ParticleSystem moodParticles;

    [Header("Settings")]
    [Tooltip("Duration of the mood color/intensity lerp.")]
    [SerializeField] private float lerpDuration = 2f;

    /// <summary>The currently active perfume definition, or null if none.</summary>
    public PerfumeDefinition ActivePerfume { get; private set; }

    private Color _defaultLightColor;
    private float _defaultLightIntensity;
    private Coroutine _lerpCoroutine;

    private void Awake()
    {
        if (directionalLight != null)
        {
            _defaultLightColor = directionalLight.color;
            _defaultLightIntensity = directionalLight.intensity;
        }
    }

    public void SetDirectionalLight(Light light)
    {
        directionalLight = light;
        if (light != null)
        {
            _defaultLightColor = light.color;
            _defaultLightIntensity = light.intensity;
        }
    }

    public void SetMoodParticles(ParticleSystem ps) => moodParticles = ps;

    /// <summary>
    /// Lerp directional light and mood particles to this perfume's settings.
    /// </summary>
    public void ApplyPerfumeMood(PerfumeDefinition def)
    {
        if (def == null || directionalLight == null) return;
        if (ActivePerfume == def) return;

        ActivePerfume = def;

        if (_lerpCoroutine != null)
            StopCoroutine(_lerpCoroutine);

        _lerpCoroutine = StartCoroutine(LerpMood(def.lightingColor, def.lightIntensity, def.moodParticleColor));
    }

    /// <summary>
    /// Reset lighting to defaults (no active perfume).
    /// </summary>
    public void ClearMood()
    {
        ActivePerfume = null;

        if (_lerpCoroutine != null)
            StopCoroutine(_lerpCoroutine);

        if (directionalLight != null)
            _lerpCoroutine = StartCoroutine(LerpMood(_defaultLightColor, _defaultLightIntensity, Color.clear));
    }

    private IEnumerator LerpMood(Color targetColor, float targetIntensity, Color particleColor)
    {
        Color startColor = directionalLight.color;
        float startIntensity = directionalLight.intensity;
        float elapsed = 0f;

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lerpDuration;

            directionalLight.color = Color.Lerp(startColor, targetColor, t);
            directionalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);

            yield return null;
        }

        directionalLight.color = targetColor;
        directionalLight.intensity = targetIntensity;

        // Tint mood particles
        if (moodParticles != null)
        {
            var main = moodParticles.main;
            main.startColor = particleColor;
        }

        _lerpCoroutine = null;
    }
}
