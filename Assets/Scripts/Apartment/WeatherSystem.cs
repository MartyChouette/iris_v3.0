using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-scoped singleton managing daily weather.
/// Weighted random weather per day, feeds MoodMachine + NatureBox shader.
/// F3 cycles through weather states for debug.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    public enum WeatherState { Clear, Overcast, Rainy, Stormy, Snowy, FallingLeaves }

    [Header("Weather Weights (relative, not %)")]
    [Tooltip("Chance weight for clear weather.")]
    [SerializeField] private float _clearWeight = 35f;
    [Tooltip("Chance weight for overcast weather.")]
    [SerializeField] private float _overcastWeight = 25f;
    [Tooltip("Chance weight for rainy weather.")]
    [SerializeField] private float _rainyWeight = 15f;
    [Tooltip("Chance weight for stormy weather.")]
    [SerializeField] private float _stormyWeight = 8f;
    [Tooltip("Chance weight for snowy weather.")]
    [SerializeField] private float _snowyWeight = 10f;
    [Tooltip("Chance weight for falling leaves weather.")]
    [SerializeField] private float _fallingLeavesWeight = 7f;

    [Header("Mood Values (fed to MoodMachine)")]
    [SerializeField] private float _clearMood = 0.1f;
    [SerializeField] private float _overcastMood = 0.3f;
    [SerializeField] private float _rainyMood = 0.6f;
    [SerializeField] private float _stormyMood = 0.9f;
    [SerializeField] private float _snowyMood = 0.4f;
    [SerializeField] private float _fallingLeavesMood = 0.25f;

    [Header("Rain Intensity")]
    [SerializeField] private float _clearRain = 0f;
    [SerializeField] private float _overcastRain = 0f;
    [SerializeField] private float _rainyRain = 0.6f;
    [SerializeField] private float _stormyRain = 1f;

    [Header("Audio")]
    [Tooltip("Ambient loop for rain.")]
    [SerializeField] private AudioClip _rainAmbience;
    [Tooltip("Ambient loop for storms.")]
    [SerializeField] private AudioClip _stormAmbience;

    public WeatherState CurrentWeather { get; private set; } = WeatherState.Clear;
    public float CurrentRainIntensity { get; private set; }
    public float CurrentSnowIntensity { get; private set; }
    public float CurrentLeafIntensity { get; private set; }
    public float CurrentOvercast { get; private set; }

    private InputAction _debugCycleAction;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WeatherSystem] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // F3 debug key
        _debugCycleAction = new InputAction("DebugCycleWeather",
            UnityEngine.InputSystem.InputActionType.Button,
            "<Keyboard>/f3");
    }

    private void OnEnable()
    {
        _debugCycleAction?.Enable();
    }

    private void OnDisable()
    {
        _debugCycleAction?.Disable();
    }

    private void Start()
    {
        // Generate initial weather from current day
        int day = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1;
        GenerateWeather(day);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (MoodMachine.Instance != null)
                MoodMachine.Instance.RemoveSource("Weather");
            Instance = null;
        }
        _debugCycleAction?.Dispose();
    }

    private void Update()
    {
        // F3 debug cycling
        if (_debugCycleAction != null && _debugCycleAction.WasPressedThisFrame())
        {
            int next = ((int)CurrentWeather + 1) % 6;
            ForceWeather((WeatherState)next);
        }
    }

    /// <summary>Generate weather for a given day using weighted random.</summary>
    public void GenerateWeather(int day)
    {
        float total = _clearWeight + _overcastWeight + _rainyWeight
                    + _stormyWeight + _snowyWeight + _fallingLeavesWeight;
        float roll = Random.Range(0f, total);

        float cumulative = _clearWeight;
        if (roll < cumulative)
        { SetWeather(WeatherState.Clear); goto done; }

        cumulative += _overcastWeight;
        if (roll < cumulative)
        { SetWeather(WeatherState.Overcast); goto done; }

        cumulative += _rainyWeight;
        if (roll < cumulative)
        { SetWeather(WeatherState.Rainy); goto done; }

        cumulative += _stormyWeight;
        if (roll < cumulative)
        { SetWeather(WeatherState.Stormy); goto done; }

        cumulative += _snowyWeight;
        if (roll < cumulative)
        { SetWeather(WeatherState.Snowy); goto done; }

        SetWeather(WeatherState.FallingLeaves);

        done:
        Debug.Log($"[WeatherSystem] Day {day} weather: {CurrentWeather}");
    }

    /// <summary>Force a specific weather state (debug/testing).</summary>
    public void ForceWeather(WeatherState state)
    {
        SetWeather(state);
        Debug.Log($"[WeatherSystem] Forced weather: {state}");
    }

    private void SetWeather(WeatherState state)
    {
        CurrentWeather = state;

        // ── Mood ──
        float mood = state switch
        {
            WeatherState.Clear         => _clearMood,
            WeatherState.Overcast      => _overcastMood,
            WeatherState.Rainy         => _rainyMood,
            WeatherState.Stormy        => _stormyMood,
            WeatherState.Snowy         => _snowyMood,
            WeatherState.FallingLeaves => _fallingLeavesMood,
            _ => _clearMood
        };

        // ── Per-state intensities ──
        CurrentRainIntensity = 0f;
        CurrentSnowIntensity = 0f;
        CurrentLeafIntensity = 0f;
        CurrentOvercast = 0f;

        float cloudDensity = 0.45f; // base
        float horizonFog = 0.35f;   // base
        float snowCap = 0f;

        switch (state)
        {
            case WeatherState.Clear:
                CurrentRainIntensity = _clearRain;
                cloudDensity = 0.3f;
                horizonFog = 0.25f;
                break;

            case WeatherState.Overcast:
                CurrentRainIntensity = _overcastRain;
                CurrentOvercast = 0.5f;
                cloudDensity = 0.75f;
                horizonFog = 0.45f;
                break;

            case WeatherState.Rainy:
                CurrentRainIntensity = _rainyRain;
                CurrentOvercast = 0.4f;
                cloudDensity = 0.8f;
                horizonFog = 0.5f;
                break;

            case WeatherState.Stormy:
                CurrentRainIntensity = _stormyRain;
                CurrentOvercast = 0.8f;
                cloudDensity = 0.95f;
                horizonFog = 0.6f;
                break;

            case WeatherState.Snowy:
                CurrentSnowIntensity = 0.8f;
                CurrentOvercast = 0.3f;
                cloudDensity = 0.65f;
                horizonFog = 0.4f;
                snowCap = 0.9f;
                break;

            case WeatherState.FallingLeaves:
                CurrentLeafIntensity = 0.7f;
                cloudDensity = 0.4f;
                horizonFog = 0.3f;
                break;
        }

        // Feed MoodMachine
        if (MoodMachine.Instance != null)
            MoodMachine.Instance.SetSource("Weather", mood);

        // Push to NatureBox shader
        PushToNatureBox(cloudDensity, horizonFog, snowCap);
    }

    private void PushToNatureBox(float cloudDensity, float horizonFog, float snowCap)
    {
        if (NatureBoxController.Instance == null) return;

        NatureBoxController.Instance.SetWeatherTargets(
            CurrentRainIntensity,
            CurrentSnowIntensity,
            CurrentLeafIntensity,
            CurrentOvercast,
            snowCap,
            cloudDensity,
            horizonFog
        );
    }

    // ── Persistence ───────────────────────────────────────────────

    public int GetStateForSave() => (int)CurrentWeather;

    public void LoadFromSave(int state)
    {
        if (state >= 0 && state <= 5)
            SetWeather((WeatherState)state);
    }
}
