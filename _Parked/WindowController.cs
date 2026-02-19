using UnityEngine;

/// <summary>
/// Per-window component that drives ParallaxWindow shader properties.
/// Reads MoodMachine emission each frame and accepts rain intensity from WeatherSystem.
/// </summary>
public class WindowController : MonoBehaviour
{
    [Tooltip("Renderer with the Iris/ParallaxWindow material.")]
    [SerializeField] private Renderer _windowRenderer;

    [Tooltip("Virtual depth behind the window (parallax strength).")]
    [SerializeField] private float _roomDepth = 2f;

    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionProp = Shader.PropertyToID("_WindowEmission");
    private static readonly int RainProp = Shader.PropertyToID("_RainIntensity");
    private static readonly int DepthProp = Shader.PropertyToID("_RoomDepth");

    public Renderer WindowRenderer => _windowRenderer;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        // Push initial depth
        if (_windowRenderer != null)
        {
            _mpb.SetFloat(DepthProp, _roomDepth);
            _windowRenderer.SetPropertyBlock(_mpb);
        }
    }

    private void Update()
    {
        if (_windowRenderer == null || _mpb == null) return;

        // Read window emission from MoodMachine
        float emission = 0f;
        if (MoodMachine.Instance != null)
            emission = MoodMachine.Instance.WindowEmission;

        _windowRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(EmissionProp, emission);
        _mpb.SetFloat(DepthProp, _roomDepth);
        _windowRenderer.SetPropertyBlock(_mpb);
    }

    /// <summary>Set rain intensity on the window shader (called by WeatherSystem).</summary>
    public void SetRainIntensity(float intensity)
    {
        if (_windowRenderer == null || _mpb == null) return;

        _windowRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(RainProp, intensity);
        _windowRenderer.SetPropertyBlock(_mpb);
    }
}
