using UnityEngine;

/// <summary>
/// Defines parameters for a game mode (7 Minutes, 7 Days, 7 Weeks).
/// Each mode SO controls pacing, day count, and demo timer limits.
/// </summary>
[CreateAssetMenu(menuName = "Iris/Game Mode Config")]
public class GameModeConfig : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name shown in the menu (e.g. '7 Minutes').")]
    public string modeName;

    [Tooltip("Short description shown on hover or below the button.")]
    [TextArea(1, 3)]
    public string modeDescription;

    [Header("Calendar")]
    [Tooltip("Total in-game days. 1 for demo, 7 for showcase, 49 for full.")]
    public int totalDays = 7;

    [Header("Demo Timer")]
    [Tooltip("Real-time limit in seconds. 0 = no real-time limit (use day count instead).")]
    public float demoTimeLimitSeconds;

    [Header("Pacing")]
    [Tooltip("Real-world seconds per one game hour.")]
    public float realSecondsPerGameHour = 60f;

    [Tooltip("Preparation phase duration in seconds (hidden from player).")]
    public float prepDuration = 900f;

    [Tooltip("Phase 3 (couch judging) duration in seconds. 0 = use DateSessionManager default.")]
    public float datePhaseDuration;
}
