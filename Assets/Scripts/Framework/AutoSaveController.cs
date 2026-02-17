using UnityEngine;

/// <summary>
/// Triggers auto-saves at end of day, end of date, and on application quit.
/// Gathers state from all active registries into IrisSaveData and writes via SaveManager.
/// </summary>
public class AutoSaveController : MonoBehaviour
{
    public static AutoSaveController Instance { get; private set; }

    /// <summary>After RestoreFromSave, holds the day phase to restore to.</summary>
    public int RestoredDayPhase { get; private set; } = -1;

    /// <summary>After RestoreFromSave, holds the saved day number.</summary>
    public int RestoredDay { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AutoSaveController] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit()
    {
        PerformSave("application_quit");
    }

    /// <summary>Trigger a save with a reason tag for debugging.</summary>
    public void PerformSave(string reason = "manual")
    {
        var data = GatherSaveData();
        SaveManager.SaveGame(data);
        Debug.Log($"[AutoSaveController] Auto-saved slot {SaveManager.ActiveSlot} ({reason}).");
    }

    private IrisSaveData GatherSaveData()
    {
        return new IrisSaveData
        {
            playerName = PlayerData.PlayerName,
            currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1,
            currentHour = GameClock.Instance != null ? GameClock.Instance.CurrentHour : 8f,
            dayPhase = DayPhaseManager.Instance != null
                ? (int)DayPhaseManager.Instance.CurrentPhase
                : 0,
            dateHistory = DateHistory.GetAllForSave(),
            itemDisplayStates = ItemStateRegistry.GetAllForSave()
        };
    }

    /// <summary>Load saved game data and restore all registries.</summary>
    public void RestoreFromSave()
    {
        var data = SaveManager.LoadGame();
        if (data == null)
        {
            Debug.Log("[AutoSaveController] No save data found.");
            RestoredDayPhase = -1;
            return;
        }

        PlayerData.PlayerName = data.playerName;
        GameClock.Instance?.RestoreFromSave(data.currentDay, data.currentHour);
        DateHistory.LoadFrom(data.dateHistory);
        ItemStateRegistry.LoadFrom(data.itemDisplayStates);

        RestoredDayPhase = data.dayPhase;
        RestoredDay = data.currentDay;

        Debug.Log($"[AutoSaveController] Restored slot {SaveManager.ActiveSlot} — " +
                  $"Day {data.currentDay}, phase {(DayPhaseManager.DayPhase)data.dayPhase}, " +
                  $"{data.dateHistory?.Count ?? 0} date records.");
    }
}
