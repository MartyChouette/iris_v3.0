using UnityEngine;

/// <summary>
/// Pre-planned weather timeline for each day. Multiple weather states at
/// different times of day, smoothly interpolated between them.
/// Days without entries fall back to weighted random.
/// MoodMachine still overrides mood — this controls visuals/audio.
///
/// Example: Day 1 could be Clear@0.25 → Overcast@0.5 → Rainy@0.75
/// The system lerps all NatureBox values between keyframes.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Weather Schedule")]
public class WeatherSchedule : ScriptableObject
{
    [System.Serializable]
    public struct WeatherKeyframe
    {
        [Tooltip("Normalized time of day (0 = midnight, 0.25 = 6am, 0.5 = noon, 0.75 = 6pm).")]
        [Range(0f, 1f)]
        public float time;

        [Tooltip("Weather state at this time.")]
        public WeatherSystem.WeatherState weather;
    }

    [System.Serializable]
    public struct DayTimeline
    {
        [Tooltip("Day number (1-based, matches GameClock.CurrentDay).")]
        public int day;

        [Tooltip("Weather keyframes throughout the day. Must be sorted by time.")]
        public WeatherKeyframe[] keyframes;
    }

    [Tooltip("Weather timelines per day. Days not listed use weighted random.")]
    public DayTimeline[] days;

    /// <summary>
    /// Try to get the planned weather for a given day.
    /// Returns true if a timeline exists (even single-entry).
    /// </summary>
    public bool TryGetTimeline(int day, out DayTimeline timeline)
    {
        if (days != null)
        {
            for (int i = 0; i < days.Length; i++)
            {
                if (days[i].day == day)
                {
                    timeline = days[i];
                    return true;
                }
            }
        }

        timeline = default;
        return false;
    }

    /// <summary>
    /// Evaluate the timeline at a given normalized time of day.
    /// Returns the two surrounding keyframes and the lerp factor between them.
    /// </summary>
    public static void Evaluate(DayTimeline timeline, float timeOfDay,
        out WeatherSystem.WeatherState stateA, out WeatherSystem.WeatherState stateB, out float t)
    {
        var keys = timeline.keyframes;
        if (keys == null || keys.Length == 0)
        {
            stateA = stateB = WeatherSystem.WeatherState.Clear;
            t = 0f;
            return;
        }

        if (keys.Length == 1)
        {
            stateA = stateB = keys[0].weather;
            t = 0f;
            return;
        }

        // Find the two keyframes surrounding timeOfDay
        // If before first keyframe, use first. If after last, use last.
        if (timeOfDay <= keys[0].time)
        {
            stateA = stateB = keys[0].weather;
            t = 0f;
            return;
        }

        if (timeOfDay >= keys[keys.Length - 1].time)
        {
            stateA = stateB = keys[keys.Length - 1].weather;
            t = 0f;
            return;
        }

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (timeOfDay >= keys[i].time && timeOfDay <= keys[i + 1].time)
            {
                stateA = keys[i].weather;
                stateB = keys[i + 1].weather;
                float range = keys[i + 1].time - keys[i].time;
                t = range > 0.001f ? (timeOfDay - keys[i].time) / range : 0f;
                return;
            }
        }

        // Fallback
        stateA = stateB = keys[keys.Length - 1].weather;
        t = 0f;
    }
}
