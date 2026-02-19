using UnityEngine;

/// <summary>
/// Scene-scoped singleton managing daily weather.
/// Weighted random weather per day, feeds MoodMachine + rain to windows.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    public enum WeatherState { Clear, Overcast, Rainy, Stormy }

    [Header("Weather Weights (relative, not %)")]
    [Tooltip("Chance weight for clear weather.")]
    [SerializeField] private float _clearWeight = 40f;
    [Tooltip("Chance weight for overcast weather.")]
    [SerializeField] private float _overcastWeight = 30f;
    [Tooltip("Chance weight for rainy weather.")]
    [SerializeField] private float _rainyWeight = 20f;
    [Tooltip("Chance weight for stormy weather.")]
    [SerializeField] private float _stormyWeight = 10f;

    [Header("Mood Values (fed to MoodMachine)")]
    [Tooltip("MoodMachine source value for clear weather.")]
    [SerializeField] private float _clearMood = 0.1f;
    [Tooltip("MoodMachine source value for overcast weather.")]
    [SerializeField] private float _overcastMood = 0.3f;
    [Tooltip("MoodMachine source value for rainy weather.")]
    [SerializeField] private float _rainyMood = 0.6f;
    [Tooltip("MoodMachine source value for stormy weather.")]
    [SerializeField] private float _stormyMood = 0.9f;

    [Header("Rain Intensity")]
    [Tooltip("Rain intensity for clear weather.")]
    [SerializeField] private float _clearRain = 0f;
    [Tooltip("Rain intensity for overcast weather.")]
    [SerializeField] private float _overcastRain = 0f;
    [Tooltip("Rain intensity for rainy weather.")]
    [SerializeField] private float _rainyRain = 0.5f;
    [Tooltip("Rain intensity for stormy weather.")]
    [SerializeField] private float _stormyRain = 1f;

    [Header("Audio")]
    [Tooltip("Ambient loop for rain.")]
    [SerializeField] private AudioClip _rainAmbience;
    [Tooltip("Ambient loop for storms.")]
    [SerializeField] private AudioClip _stormAmbience;

    [Header("References")]
    [Tooltip("Window controllers to push rain intensity to.")]
    [SerializeField] private WindowController[] _windows;

    public WeatherState CurrentWeather { get; private set; } = WeatherState.Clear;
    public float CurrentRainIntensity { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WeatherSystem] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // Clean up MoodMachine source
            if (MoodMachine.Instance != null)
                MoodMachine.Instance.RemoveSource("Weather");
            Instance = null;
        }
    }

    /// <summary>Generate weather for a given day using weighted random.</summary>
    public void GenerateWeather(int day)
    {
        float total = _clearWeight + _overcastWeight + _rainyWeight + _stormyWeight;
        float roll = Random.Range(0f, total);

        if (roll < _clearWeight)
            SetWeather(WeatherState.Clear);
        else if (roll < _clearWeight + _overcastWeight)
            SetWeather(WeatherState.Overcast);
        else if (roll < _clearWeight + _overcastWeight + _rainyWeight)
            SetWeather(WeatherState.Rainy);
        else
            SetWeather(WeatherState.Stormy);

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

        float mood = state switch
        {
            WeatherState.Clear => _clearMood,
            WeatherState.Overcast => _overcastMood,
            WeatherState.Rainy => _rainyMood,
            WeatherState.Stormy => _stormyMood,
            _ => _clearMood
        };

        CurrentRainIntensity = state switch
        {
            WeatherState.Clear => _clearRain,
            WeatherState.Overcast => _overcastRain,
            WeatherState.Rainy => _rainyRain,
            WeatherState.Stormy => _stormyRain,
            _ => 0f
        };

        // Feed MoodMachine
        if (MoodMachine.Instance != null)
            MoodMachine.Instance.SetSource("Weather", mood);

        // Push rain to windows
        PushRainToWindows();
    }

    private void PushRainToWindows()
    {
        if (_windows == null) return;
        for (int i = 0; i < _windows.Length; i++)
        {
            if (_windows[i] != null)
                _windows[i].SetRainIntensity(CurrentRainIntensity);
        }
    }

    // ── Persistence ───────────────────────────────────────────────

    public int GetStateForSave() => (int)CurrentWeather;

    public void LoadFromSave(int state)
    {
        SetWeather((WeatherState)state);
    }
}
