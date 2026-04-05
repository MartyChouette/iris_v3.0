using UnityEngine;

/// <summary>
/// Static dialogue table for Nema's reactions to weather changes.
/// 30-second cooldown prevents spamming during timeline interpolation.
/// </summary>
public static class NemaWeatherDialogue
{
    private const float Cooldown = 30f;
    private static float s_lastCommentTime = -100f;

    private static readonly string[][] s_lines =
    {
        // Clear
        new[] { "What a beautiful day...", "The sun feels so nice.", "Perfect weather for a date.", "I love clear skies like this." },
        // Overcast
        new[] { "Feels cozy with those clouds.", "I like this moody sky.", "Hmm, hope it doesn't rain.", "Overcast days have a certain charm." },
        // Rainy
        new[] { "I love the sound of rain.", "Rainy days are the best for staying in.", "Good thing we're inside.", "Rain makes everything feel so intimate." },
        // Stormy
        new[] { "Wow, listen to that storm!", "A little dramatic out there...", "Storms make everything feel exciting.", "I hope the power doesn't go out..." },
        // Snowy
        new[] { "Snow! It's so pretty.", "I want to go outside and play in it.", "Everything looks magical.", "Hot cocoa weather..." },
        // FallingLeaves
        new[] { "Autumn vibes... I love it.", "Look at those leaves.", "This is my favorite time of year.", "The colors are gorgeous." },
    };

    public static void ReactToWeather(WeatherSystem.WeatherState state)
    {
        if (Time.time - s_lastCommentTime < Cooldown) return;
        if (DialoguePortraitBox.Instance == null) return;

        int idx = (int)state;
        if (idx < 0 || idx >= s_lines.Length) return;

        var pool = s_lines[idx];
        string line = pool[Random.Range(0, pool.Length)];
        DialoguePortraitBox.Instance.Say(line, 3f);
        s_lastCommentTime = Time.time;
    }
}
