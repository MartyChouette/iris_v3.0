using System;
using System.Collections.Generic;

/// <summary>
/// Unified save data schema for the entire Iris game state.
/// Serialized to iris_savegame.json via SaveManager.
/// Flower session data remains in iris_sessions.json (untouched).
/// </summary>
[Serializable]
public class IrisSaveData
{
    public int saveVersion = 1;

    // ── Game Mode ───────────────────────────────────────────────
    public string gameMode;

    // ── Player ────────────────────────────────────────────────────
    public string playerName;

    // ── Calendar ──────────────────────────────────────────────────
    public int currentDay;
    public float currentHour;

    // ── Date History ──────────────────────────────────────────────
    public List<RichDateHistoryEntry> dateHistory = new List<RichDateHistoryEntry>();

    // ── Learned Preferences ──────────────────────────────────────
    public List<LearnedPreference> learnedPreferences = new List<LearnedPreference>();

    // ── Item Display States ──────────────────────────────────────
    public List<ItemDisplayRecord> itemDisplayStates = new List<ItemDisplayRecord>();

    // ── Collectibles ─────────────────────────────────────────────
    public List<CollectibleRecord> foundCollectibles = new List<CollectibleRecord>();

    // ── Fridge Magnets ───────────────────────────────────────────
    public List<MagnetPositionRecord> magnetPositions = new List<MagnetPositionRecord>();

    // ── Newspaper Clippings ──────────────────────────────────────
    public List<ClippingRecord> savedClippings = new List<ClippingRecord>();

    // ── Plant Health ───────────────────────────────────────────
    public List<PlantHealthRecord> plantHealthStates = new List<PlantHealthRecord>();

    // ── Weather ────────────────────────────────────────────────
    public int currentWeatherState;
}

/// <summary>Serializable record of an item's display state.</summary>
[Serializable]
public class ItemDisplayRecord
{
    public string itemId;
    public int displayState; // maps to ItemStateRegistry.ItemDisplayState
}

/// <summary>Serializable record of a found collectible.</summary>
[Serializable]
public class CollectibleRecord
{
    public string collectibleId;
    public string collectibleType; // "photo", "pressed_flower"
    public int dayFound;
}

/// <summary>Serializable record of a fridge magnet position.</summary>
[Serializable]
public class MagnetPositionRecord
{
    public string magnetId;
    public float posX, posY;
}

/// <summary>Serializable record of a newspaper clipping on the fridge.</summary>
[Serializable]
public class ClippingRecord
{
    public string characterId;
    public string characterName;
    public string adTextSnippet;
    public int day;
    public string grade;
    public float affection;
}

/// <summary>Serializable record of a plant's health state.</summary>
[Serializable]
public class PlantHealthRecord
{
    public string plantId;
    public int tier;          // 0=Healthy, 1=Wilting, 2=Dead
    public int lastWateredDay;
    public int lastQuality;   // 0=Perfect, 1=Normal, 2=Missed
    public int consecutiveMisses;
    public int recoveryProgress;
}
