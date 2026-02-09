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
    // Spline Dolly (browse camera)
    // ──────────────────────────────────────────────────────────────
    [Header("Spline Dolly")]
    [Tooltip("Normalized position (0-1) on the apartment dolly spline for browse view.")]
    [Range(0f, 1f)]
    public float splinePosition;

    // ──────────────────────────────────────────────────────────────
    // Look-At Target (browse camera focus point)
    // ──────────────────────────────────────────────────────────────
    [Header("Look-At Target")]
    [Tooltip("World position the browse camera should look at when at this area. Place at the main point of interest.")]
    public Vector3 lookAtPosition;

    // ──────────────────────────────────────────────────────────────
    // Selected Camera (close-up for interaction)
    // ──────────────────────────────────────────────────────────────
    [Header("Selected Camera")]
    [Tooltip("World position of the camera when this area is selected.")]
    public Vector3 selectedPosition;

    [Tooltip("World rotation (Euler) of the camera when this area is selected.")]
    public Vector3 selectedRotation;

    [Tooltip("Field of view when this area is selected.")]
    public float selectedFOV = 50f;

    // ──────────────────────────────────────────────────────────────
    // Blend Timing
    // ──────────────────────────────────────────────────────────────
    [Header("Blend Timing")]
    [Tooltip("Duration of the camera blend when cycling between areas in browse mode.")]
    public float browseBlendDuration = 0.8f;

    [Tooltip("Duration of the camera blend when entering/exiting selected mode.")]
    public float selectBlendDuration = 0.5f;
}
