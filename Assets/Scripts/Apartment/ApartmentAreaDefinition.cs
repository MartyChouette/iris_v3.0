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
    // Browse Camera (overview vantage)
    // ──────────────────────────────────────────────────────────────
    [Header("Browse Camera")]
    [Tooltip("World position of the camera when browsing this area.")]
    public Vector3 browsePosition;

    [Tooltip("World rotation (Euler) of the camera when browsing this area.")]
    public Vector3 browseRotation;

    [Tooltip("Field of view when browsing this area.")]
    public float browseFOV = 60f;

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
