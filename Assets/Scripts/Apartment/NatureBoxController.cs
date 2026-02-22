using UnityEngine;

/// <summary>
/// Drives the NatureBox shader from GameClock (time of day) and WeatherSystem (weather effects).
/// Single bridge between game state and all shader properties — smooth lerps all values per frame.
///
/// Setup: Create a Cube, add this component (auto-scales + removes collider),
/// assign a material with the Iris/NatureBox shader.
/// </summary>
public class NatureBoxController : MonoBehaviour
{
    public static NatureBoxController Instance { get; private set; }

    [Header("Override")]
    [Tooltip("Time of day when GameClock is absent (0 = midnight, 0.5 = noon).")]
    [Range(0f, 1f)]
    [SerializeField] private float _manualTimeOfDay = 0.5f;

    [Tooltip("Auto-cycle time for preview when no GameClock is present.")]
    [SerializeField] private bool _animate;

    [Tooltip("Full day-night cycles per minute.")]
    [SerializeField] private float _animateSpeed = 0.5f;

    [Header("Box")]
    [Tooltip("Scale of the environment box. Must be large enough to enclose the level.")]
    [SerializeField] private float _boxScale = 200f;

    [Header("Transition")]
    [Tooltip("How fast weather properties lerp to their targets (units/sec).")]
    [SerializeField] private float _weatherLerpSpeed = 0.8f;

    // ── Base defaults ────────────────────────────────────────────────
    private const float BaseCloudDensity = 0.45f;
    private const float BaseHorizonFog = 0.35f;

    // ── Shader property IDs ──────────────────────────────────────────
    private static readonly int TimeOfDayId      = Shader.PropertyToID("_TimeOfDay");
    private static readonly int CloudDensityId   = Shader.PropertyToID("_CloudDensity");
    private static readonly int HorizonFogId     = Shader.PropertyToID("_HorizonFog");
    private static readonly int RainIntensityId  = Shader.PropertyToID("_RainIntensity");
    private static readonly int SnowIntensityId  = Shader.PropertyToID("_SnowIntensity");
    private static readonly int LeafIntensityId  = Shader.PropertyToID("_LeafIntensity");
    private static readonly int OvercastDarkenId = Shader.PropertyToID("_OvercastDarken");
    private static readonly int SnowCapId        = Shader.PropertyToID("_SnowCapIntensity");

    // ── Target values (set by WeatherSystem) ─────────────────────────
    private float _targetRain;
    private float _targetSnow;
    private float _targetLeaves;
    private float _targetOvercast;
    private float _targetSnowCap;
    private float _targetCloudDensity;
    private float _targetHorizonFog;

    // ── Current (lerped) values ──────────────────────────────────────
    private float _curRain;
    private float _curSnow;
    private float _curLeaves;
    private float _curOvercast;
    private float _curSnowCap;
    private float _curCloudDensity;
    private float _curHorizonFog;

    private Renderer _renderer;
    private Material _matInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NatureBoxController] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _matInstance = _renderer.material;

        // Initialize current values to base defaults
        _curCloudDensity = BaseCloudDensity;
        _curHorizonFog = BaseHorizonFog;
        _targetCloudDensity = BaseCloudDensity;
        _targetHorizonFog = BaseHorizonFog;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_matInstance != null)
            Destroy(_matInstance);
    }

    private void Update()
    {
        if (_matInstance == null) return;

        // ── Time of day ──
        float t;
        if (GameClock.Instance != null)
        {
            t = GameClock.Instance.NormalizedTimeOfDay;
        }
        else if (_animate)
        {
            _manualTimeOfDay = Mathf.Repeat(
                _manualTimeOfDay + Time.deltaTime * _animateSpeed / 60f, 1f);
            t = _manualTimeOfDay;
        }
        else
        {
            t = _manualTimeOfDay;
        }

        _matInstance.SetFloat(TimeOfDayId, t);

        // ── Smooth-lerp weather properties ──
        float step = _weatherLerpSpeed * Time.deltaTime;

        _curRain         = Mathf.MoveTowards(_curRain,         _targetRain,         step);
        _curSnow         = Mathf.MoveTowards(_curSnow,         _targetSnow,         step);
        _curLeaves       = Mathf.MoveTowards(_curLeaves,       _targetLeaves,       step);
        _curOvercast     = Mathf.MoveTowards(_curOvercast,     _targetOvercast,     step);
        _curSnowCap      = Mathf.MoveTowards(_curSnowCap,      _targetSnowCap,      step);
        _curCloudDensity = Mathf.MoveTowards(_curCloudDensity, _targetCloudDensity, step);
        _curHorizonFog   = Mathf.MoveTowards(_curHorizonFog,   _targetHorizonFog,   step);

        // ── ReduceMotion: zero out animated effects ──
        bool reduceMotion = AccessibilitySettings.ReduceMotion;
        float rain   = reduceMotion ? 0f : _curRain;
        float snow   = reduceMotion ? 0f : _curSnow;
        float leaves = reduceMotion ? 0f : _curLeaves;

        _matInstance.SetFloat(RainIntensityId,  rain);
        _matInstance.SetFloat(SnowIntensityId,  snow);
        _matInstance.SetFloat(LeafIntensityId,  leaves);
        _matInstance.SetFloat(OvercastDarkenId, _curOvercast);
        _matInstance.SetFloat(SnowCapId,        _curSnowCap);
        _matInstance.SetFloat(CloudDensityId,   _curCloudDensity);
        _matInstance.SetFloat(HorizonFogId,     _curHorizonFog);
    }

    /// <summary>
    /// Set all weather targets. Values lerp smoothly per frame.
    /// Called by WeatherSystem when weather state changes.
    /// </summary>
    public void SetWeatherTargets(float rain, float snow, float leaves,
                                   float overcast, float snowCap,
                                   float cloudDensity, float horizonFog)
    {
        _targetRain         = rain;
        _targetSnow         = snow;
        _targetLeaves       = leaves;
        _targetOvercast     = overcast;
        _targetSnowCap      = snowCap;
        _targetCloudDensity = cloudDensity;
        _targetHorizonFog   = horizonFog;
    }

    /// <summary>
    /// Override time of day manually (disables GameClock driving).
    /// Used by test controllers for direct slider control.
    /// </summary>
    public void SetManualTime(float normalizedTime)
    {
        _manualTimeOfDay = Mathf.Repeat(normalizedTime, 1f);
        // Disable GameClock so it doesn't fight with the manual value
        if (GameClock.Instance != null)
            GameClock.Instance.enabled = false;
    }

    /// <summary>Called when component is added in editor — auto-configures the box.</summary>
    private void Reset()
    {
        transform.localScale = Vector3.one * _boxScale;

        #if UNITY_EDITOR
        var col = GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        #endif
    }
}
