using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Camera Preset")]
public class CameraPresetDefinition : ScriptableObject
{
    [Header("Preset")]
    [Tooltip("Display label for this preset (e.g. V1, V2, V3).")]
    public string label;

    [Tooltip("True for orthographic projection, false for perspective.")]
    public bool orthographic;

    [Header("Per-Area Configs")]
    [Tooltip("Camera config per area, index-matched to ApartmentManager.areas[].")]
    public AreaCameraConfig[] areaConfigs;
}

[System.Serializable]
public struct AreaCameraConfig
{
    [Tooltip("Label for inspector readability.")]
    public string areaLabel;

    [Tooltip("World position of the camera.")]
    public Vector3 position;

    [Tooltip("World rotation (Euler degrees) of the camera.")]
    public Vector3 rotation;

    [Tooltip("Field of view if perspective, ortho size if orthographic.")]
    public float fovOrOrthoSize;
}
