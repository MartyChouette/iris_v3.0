using System;
using System.Collections.Generic;

/// <summary>
/// Comprehensive record of everything that happened during a single date.
/// Replaces the thin DateHistoryEntry with full reaction logs, drink data,
/// environment snapshots, and aftermath mess generation data.
/// </summary>
[Serializable]
public class RichDateHistoryEntry
{
    // ── Identity ──────────────────────────────────────────────────
    public string characterId;
    public string characterName;
    public int day;
    public float gameHourStarted;
    public float gameHourEnded;

    // ── Outcome ───────────────────────────────────────────────────
    public float finalAffection;
    public string grade;

    // ── All reactions that fired ──────────────────────────────────
    public List<ReactionRecord> reactions = new List<ReactionRecord>();

    // ── Drink ─────────────────────────────────────────────────────
    public string drinkServedId;
    public int drinkScore;
    public string drinkReaction;

    // ── Environment snapshot ──────────────────────────────────────
    public float moodAtArrival;
    public string activePerfumeId;
    public string activeRecordId;
    public string outfitId;

    // ── Entrance judgments ────────────────────────────────────────
    public string outfitReaction;
    public string moodReaction;
    public string cleanlinessReaction;

    // ── Apartment state ──────────────────────────────────────────
    public float cleanlinessPercent;
    public List<string> itemsOnDisplay = new List<string>();
    public List<string> reactableTagsInvestigated = new List<string>();

    // ── Aftermath (consumed next morning) ────────────────────────
    public List<AftermathStainRecord> generatedStains = new List<AftermathStainRecord>();
}

/// <summary>
/// A single reaction event during a date.
/// </summary>
[Serializable]
public class ReactionRecord
{
    public string tagName;
    public string[] tags;
    public string reactionType;
    public float gameHour;
}

/// <summary>
/// Describes a stain to be spawned the morning after a date.
/// </summary>
[Serializable]
public class AftermathStainRecord
{
    public string spillDefinitionId;
    public int slotIndex;
    public string cause;
}
