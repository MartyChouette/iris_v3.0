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

    private const int PriorityStation = 30;
    private const int PriorityInactive = 0;

    public StationType Type => stationType;

    /// <summary>
    /// Returns the station's manager cast to IStationManager, or null.
    /// </summary>
    public IStationManager Manager =>
        stationManager != null ? stationManager as IStationManager : null;

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
