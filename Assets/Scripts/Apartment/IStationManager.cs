/// <summary>
/// Interface for minigame managers that live inside apartment stations.
/// ApartmentManager checks IsAtIdleState before allowing Escape to exit.
/// </summary>
public interface IStationManager
{
    /// <summary>
    /// True when the manager is in a state where the player can safely
    /// exit back to the apartment (e.g. browsing, not mid-action).
    /// </summary>
    bool IsAtIdleState { get; }
}
