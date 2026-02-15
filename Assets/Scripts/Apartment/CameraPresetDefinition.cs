using UnityEngine;
using UnityEngine.Rendering;
using Unity.Cinemachine;

namespace Iris.Apartment
{
    [CreateAssetMenu(menuName = "Iris/Camera Preset")]
    public class CameraPresetDefinition : ScriptableObject
    {
        [Header("Preset")]
        [Tooltip("Display label for this preset (e.g. V1, V2, V3).")]
        public string label;

        [Header("Per-Area Configs")]
        [Tooltip("Camera config per area, index-matched to ApartmentManager.areas[].")]
        public AreaCameraConfig[] areaConfigs;
    }

    [System.Serializable]
    public struct AreaCameraConfig
    {
        [Tooltip("Label for inspector readability.")]
        public string areaLabel;

        [Header("Transform")]
        [Tooltip("World position of the camera.")]
        public Vector3 position;

        [Tooltip("World rotation (Euler degrees) of the camera.")]
        public Vector3 rotation;

        [Header("Lens")]
        [Tooltip("Cinemachine lens settings (FOV, near/far clip, dutch, ortho size, physical).")]
        public LensSettings lens;

        [Header("Post-Processing")]
        [Tooltip("URP Volume Profile for this camera angle (color grading, bloom, vignette, DoF).")]
        public VolumeProfile volumeProfile;

        [Header("Light Overrides")]
        [Tooltip("Multiplier applied to the directional light intensity on top of MoodMachine.")]
        public float lightIntensityMultiplier;

        [Tooltip("Tint applied to the directional light color on top of MoodMachine.")]
        public Color lightColorTint;
    }
}
