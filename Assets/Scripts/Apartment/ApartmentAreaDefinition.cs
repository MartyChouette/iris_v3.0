using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Apartment Area")]
public class ApartmentAreaDefinition : ScriptableObject
{
    // ──────────────────────────────────────────────────────────────
    // Identity
    // ──────────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Display name for this area (shown in UI).")]
    public string areaName = "Untitled Area";

    [TextArea(2, 4)]
    [Tooltip("Optional description of the area.")]
    public string description;

    // ──────────────────────────────────────────────────────────────
    // Station
    // ──────────────────────────────────────────────────────────────
    [Header("Station")]
    [Tooltip("Which minigame station this area hosts (None = no station).")]
    public StationType stationType = StationType.None;

    // ──────────────────────────────────────────────────────────────
    // Camera (unified browse + interaction view)
    // ──────────────────────────────────────────────────────────────
    [Header("Camera")]
    [Tooltip("World position of the camera for this area.")]
    public Vector3 cameraPosition;

    [Tooltip("World rotation (Euler) of the camera for this area.")]
    public Vector3 cameraRotation;

    [Tooltip("Field of view for this area.")]
    public float cameraFOV = 50f;

    // ──────────────────────────────────────────────────────────────
    // Blend Timing
    // ──────────────────────────────────────────────────────────────
    [Header("Blend Timing")]
    [Tooltip("Duration of the camera blend when cycling between areas.")]
    public float browseBlendDuration = 0.8f;
}
