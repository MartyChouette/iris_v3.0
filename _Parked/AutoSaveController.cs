using UnityEngine;

/// <summary>
/// Triggers auto-saves at end of day, end of date, and on application quit.
/// Gathers state from all registries into IrisSaveData and writes via SaveManager.
/// </summary>
public class AutoSaveController : MonoBehaviour
{
    public static AutoSaveController Instance { get; private set; }

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
        Debug.Log($"[AutoSaveController] Auto-saved ({reason}).");
    }

    private IrisSaveData GatherSaveData()
    {
        var data = new IrisSaveData
        {
            gameMode = MainMenuManager.ActiveGameMode != null
                ? MainMenuManager.ActiveGameMode.modeName
                : "",
            playerName = PlayerData.PlayerName,
            currentDay = GameClock.Instance != null ? GameClock.Instance.CurrentDay : 1,
            currentHour = GameClock.Instance != null ? GameClock.Instance.CurrentHour : 8f,
            dateHistory = DateHistory.GetAllForSave(),
            learnedPreferences = LearnedPreferenceRegistry.GetAllForSave(),
            itemDisplayStates = ItemStateRegistry.GetAllForSave(),
            foundCollectibles = CollectibleRegistry.GetAllForSave(),
            savedClippings = ClippingRegistry.GetAllForSave(),
            plantHealthStates = PlantHealthTracker.Instance != null
                ? PlantHealthTracker.Instance.GetAllForSave()
                : new System.Collections.Generic.List<PlantHealthRecord>(),
            currentWeatherState = WeatherSystem.Instance != null
                ? WeatherSystem.Instance.GetStateForSave()
                : 0,
            magnetPositions = FridgeMagnetManager.Instance != null
                ? FridgeMagnetManager.Instance.GetMagnetPositions()
                : new System.Collections.Generic.List<MagnetPositionRecord>()
        };

        return data;
    }

    /// <summary>Load saved game data and restore all registries.</summary>
    public void RestoreFromSave()
    {
        var data = SaveManager.LoadGame();
        if (data == null)
        {
            Debug.Log("[AutoSaveController] No save data found.");
            return;
        }

        PlayerData.PlayerName = data.playerName;
        DateHistory.LoadFrom(data.dateHistory);
        LearnedPreferenceRegistry.LoadFrom(data.learnedPreferences);
        ItemStateRegistry.LoadFrom(data.itemDisplayStates);
        CollectibleRegistry.LoadFrom(data.foundCollectibles);
        ClippingRegistry.LoadFrom(data.savedClippings);

        if (PlantHealthTracker.Instance != null)
            PlantHealthTracker.Instance.LoadFrom(data.plantHealthStates);
        if (WeatherSystem.Instance != null)
            WeatherSystem.Instance.LoadFromSave(data.currentWeatherState);
        if (FridgeMagnetManager.Instance != null)
            FridgeMagnetManager.Instance.RestoreMagnetPositions(data.magnetPositions);

        Debug.Log($"[AutoSaveController] Restored save — Day {data.currentDay}, {data.dateHistory?.Count ?? 0} date records.");
    }
}
