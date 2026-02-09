using UnityEngine;

/// <summary>
/// Marker component placed on each clickable plant pot in the apartment scene.
/// WateringManager raycasts against these to start pouring.
/// </summary>
public class WaterablePlant : MonoBehaviour
{
    [Tooltip("Which plant definition this pot uses.")]
    public PlantDefinition definition;
}
