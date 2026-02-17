using System;
using System.Collections.Generic;

/// <summary>
/// Unified save data schema for one Iris game slot.
/// Serialized to iris_save_{slot}.json via SaveManager.
/// </summary>
[Serializable]
public class IrisSaveData
{
    public int saveVersion = 1;

    // ── Player ────────────────────────────────────────────────────
    public string playerName;

    // ── Calendar ──────────────────────────────────────────────────
    public int currentDay;
    public float currentHour;
    public int dayPhase; // maps to DayPhaseManager.DayPhase

    // ── Date History ──────────────────────────────────────────────
    public List<DateHistory.DateHistoryEntry> dateHistory = new List<DateHistory.DateHistoryEntry>();

    // ── Item Display States ──────────────────────────────────────
    public List<ItemDisplayRecord> itemDisplayStates = new List<ItemDisplayRecord>();
}

/// <summary>Serializable record of an item's display state.</summary>
[Serializable]
public class ItemDisplayRecord
{
    public string itemId;
    public int displayState; // maps to ItemStateRegistry.ItemDisplayState
}
