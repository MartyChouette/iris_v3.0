using System.Collections.Generic;
using UnityEngine;

public class MoodMachine : MonoBehaviour
{
    public static MoodMachine Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Profile")]
    [Tooltip("ScriptableObject defining how the scene looks at each mood value.")]
    [SerializeField] private MoodMachineProfile profile;

    [Header("References")]
    [Tooltip("Directional light to drive color, intensity, and angle.")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Optional rain particle system. Emission rate driven by profile.")]
    [SerializeField] private ParticleSystem rainParticles;

    [Header("Settings")]
    [Tooltip("Speed of mood lerp (units per second). 0.5 ≈ 2 seconds for full traverse.")]
    [SerializeField] private float lerpSpeed = 0.5f;

    // ──────────────────────────────────────────────────────────────
    // Public read-only
    // ──────────────────────────────────────────────────────────────
    public float Mood => _currentMood;
    public IReadOnlyDictionary<string, float> Sources => _sources;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    private readonly Dictionary<string, float> _sources = new Dictionary<string, float>();
    private float _currentMood;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MoodMachine] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────────────────────────────────────────────────────────
    // Source API
    // ──────────────────────────────────────────────────────────────

    public void SetSource(string key, float value)
    {
        _sources[key] = Mathf.Clamp01(value);
    }

    public void RemoveSource(string key)
    {
        _sources.Remove(key);
    }

    // ──────────────────────────────────────────────────────────────
    // Update
    // ──────────────────────────────────────────────────────────────

    private void Update()
    {
        float target = ComputeTarget();
        _currentMood = Mathf.MoveTowards(_currentMood, target, lerpSpeed * Time.deltaTime);
        ApplyMood(_currentMood);
    }

    private float ComputeTarget()
    {
        if (_sources.Count == 0) return 0f;

        float sum = 0f;
        foreach (var kv in _sources)
            sum += kv.Value;

        return Mathf.Clamp01(sum / _sources.Count);
    }

    private void ApplyMood(float t)
    {
        if (profile == null) return;

        // Directional light
        if (directionalLight != null)
        {
            directionalLight.color = profile.lightColor.Evaluate(t);
            directionalLight.intensity = profile.lightIntensity.Evaluate(t);

            Vector3 euler = directionalLight.transform.eulerAngles;
            euler.x = profile.lightAngleX.Evaluate(t);
            directionalLight.transform.eulerAngles = euler;
        }

        // Ambient + Fog
        RenderSettings.ambientLight = profile.ambientColor.Evaluate(t);
        RenderSettings.fogColor = profile.fogColor.Evaluate(t);
        RenderSettings.fogDensity = profile.fogDensity.Evaluate(t);

        // Rain particles
        if (rainParticles != null)
        {
            var emission = rainParticles.emission;
            emission.rateOverTime = profile.rainRate.Evaluate(t);
        }
    }
}
