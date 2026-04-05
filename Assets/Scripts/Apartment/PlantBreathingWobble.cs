using UnityEngine;

/// <summary>
/// Lightweight breathing wobble for apartment living plants.
/// Replaces full physics joints with a cheap sine-wave sway.
/// Rotates the plant slightly on X and Z axes at layered frequencies
/// so each plant feels alive and unique.
/// </summary>
public class PlantBreathingWobble : MonoBehaviour
{
    [Header("Wobble")]
    [Tooltip("Maximum tilt angle in degrees.")]
    [SerializeField] private float _maxAngle = 2f;

    [Tooltip("Primary sway speed.")]
    [SerializeField] private float _speed = 0.8f;

    [Tooltip("Secondary sway speed (creates organic irregularity).")]
    [SerializeField] private float _speed2 = 1.3f;

    private float _phaseX;
    private float _phaseZ;
    private Quaternion _baseRotation;

    private void Start()
    {
        _baseRotation = transform.localRotation;
        // Randomize phase so plants don't sway in sync
        _phaseX = Random.Range(0f, Mathf.PI * 2f);
        _phaseZ = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (AccessibilitySettings.ReduceMotion) return;

        float t = Time.time;
        float x = Mathf.Sin(t * _speed + _phaseX) * 0.6f
                + Mathf.Sin(t * _speed2 + _phaseX) * 0.4f;
        float z = Mathf.Sin(t * _speed * 0.9f + _phaseZ) * 0.6f
                + Mathf.Sin(t * _speed2 * 1.1f + _phaseZ) * 0.4f;

        Quaternion wobble = Quaternion.Euler(x * _maxAngle, 0f, z * _maxAngle);
        transform.localRotation = _baseRotation * wobble;
    }
}
