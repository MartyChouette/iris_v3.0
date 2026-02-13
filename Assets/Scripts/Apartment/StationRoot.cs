using UnityEngine;

/// <summary>
/// Marker component placed on the root GameObject of each apartment station.
/// Handles activating/deactivating the station's manager, HUD, and cameras.
/// </summary>
public class StationRoot : MonoBehaviour
{
    [Header("Station")]
    [Tooltip("Which station type this root represents.")]
    [SerializeField] private StationType stationType;

    [Tooltip("The MonoBehaviour manager for this station (must implement IStationManager).")]
    [SerializeField] private MonoBehaviour stationManager;

    [Tooltip("Optional HUD root to enable/disable with the station.")]
    [SerializeField] private GameObject hudRoot;

    [Tooltip("Optional Cinemachine cameras owned by this station (raised to priority 30 on activate).")]
    [SerializeField] private Unity.Cinemachine.CinemachineCamera[] stationCameras;

    [Tooltip("If non-empty, station is only available during these day phases (int cast of DayPhaseManager.DayPhase). Empty = always available.")]
    [SerializeField] private int[] availableInPhases;

    private const int PriorityStation = 30;
    private const int PriorityInactive = 0;

    public StationType Type => stationType;

    /// <summary>
    /// True if this station has its own Cinemachine cameras.
    /// </summary>
    public bool HasStationCameras =>
        stationCameras != null && stationCameras.Length > 0;

    /// <summary>
    /// Returns the station's manager cast to IStationManager, or null.
    /// </summary>
    public IStationManager Manager =>
        stationManager != null ? stationManager as IStationManager : null;

    /// <summary>
    /// True if the station is available in the current day phase.
    /// Returns true if no phase restrictions are set (always available).
    /// </summary>
    public bool IsAvailableInCurrentPhase()
    {
        if (availableInPhases == null || availableInPhases.Length == 0) return true;
        var phase = DayPhaseManager.Instance != null
            ? DayPhaseManager.Instance.CurrentPhase
            : DayPhaseManager.DayPhase.Exploration;
        for (int i = 0; i < availableInPhases.Length; i++)
            if ((DayPhaseManager.DayPhase)availableInPhases[i] == phase) return true;
        return false;
    }

    /// <summary>
    /// Enable the station manager, show its HUD, and raise camera priorities.
    /// </summary>
    public void Activate()
    {
        if (stationManager != null)
            stationManager.enabled = true;

        if (hudRoot != null)
            hudRoot.SetActive(true);

        if (stationCameras != null)
        {
            foreach (var cam in stationCameras)
            {
                if (cam != null)
                    cam.Priority = PriorityStation;
            }
        }

        Debug.Log($"[StationRoot] Activated station: {stationType}");
    }

    /// <summary>
    /// Enable the station manager and show its HUD but do NOT raise camera priorities.
    /// Used during Exploration phase for Selected-state-only interaction.
    /// </summary>
    public void SoftActivate()
    {
        if (stationManager != null)
            stationManager.enabled = true;

        if (hudRoot != null)
            hudRoot.SetActive(true);

        Debug.Log($"[StationRoot] Soft-activated station: {stationType}");
    }

    /// <summary>
    /// Disable the station manager, hide its HUD, and lower camera priorities.
    /// </summary>
    public void Deactivate()
    {
        if (stationManager != null)
            stationManager.enabled = false;

        if (hudRoot != null)
            hudRoot.SetActive(false);

        if (stationCameras != null)
        {
            foreach (var cam in stationCameras)
            {
                if (cam != null)
                    cam.Priority = PriorityInactive;
            }
        }

        Debug.Log($"[StationRoot] Deactivated station: {stationType}");
    }
}
