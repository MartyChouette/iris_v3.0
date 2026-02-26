using System;

/// <summary>
/// Serializable data container for playtest feedback submissions.
/// Player-entered fields + auto-gathered telemetry.
/// </summary>
[Serializable]
public class FeedbackPayload
{
    // ── Player input ──
    public int enjoymentRating;       // 1-5 stars
    public int grabFeelRating;        // 1-5: picking up / putting down objects
    public int dateFeelRating;        // 1-5: how the date felt
    public int flowerFeelRating;      // 1-5: flower trimming scene
    public string feedbackPositive;
    public string feedbackNegative;
    public string bugReport;

    // ── Auto-telemetry (gathered on form open) ──
    public string sessionId;
    public string timestamp;          // ISO 8601
    public string buildVersion;
    public float playTimeSeconds;
    public int currentDay;
    public string currentPhase;
    public float tidiness;
    public float mood;
    public int dateCount;
    public string currentDateCharacter;
    public float currentAffection;
    public string systemInfo;
    public string screenResolution;
    public string accessibilityNotes;
}
