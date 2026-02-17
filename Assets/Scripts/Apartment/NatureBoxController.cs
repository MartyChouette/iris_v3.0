using UnityEngine;

/// <summary>
/// Drives the NatureBox shader's _TimeOfDay property from GameClock.
/// Attach to a cube scaled to encompass the level — the shader uses Cull Front
/// so only inside faces render, creating a procedural sky/mountain/tree environment.
///
/// Setup: Create a Cube, add this component (auto-scales + removes collider),
/// assign a material with the Iris/NatureBox shader.
/// </summary>
public class NatureBoxController : MonoBehaviour
{
    [Header("Override")]
    [Tooltip("Time of day when GameClock is absent (0 = midnight, 0.5 = noon).")]
    [Range(0f, 1f)]
    [SerializeField] private float _manualTimeOfDay = 0.5f;

    [Tooltip("Auto-cycle time for preview when no GameClock is present.")]
    [SerializeField] private bool _animate;

    [Tooltip("Full day-night cycles per minute.")]
    [SerializeField] private float _animateSpeed = 0.5f;

    [Header("Box")]
    [Tooltip("Scale of the environment box. Must be large enough to enclose the level.")]
    [SerializeField] private float _boxScale = 200f;

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private static readonly int TimeOfDayId = Shader.PropertyToID("_TimeOfDay");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_renderer == null) return;

        float t;
        if (GameClock.Instance != null)
        {
            t = GameClock.Instance.NormalizedTimeOfDay;
        }
        else if (_animate)
        {
            _manualTimeOfDay = Mathf.Repeat(
                _manualTimeOfDay + Time.deltaTime * _animateSpeed / 60f, 1f);
            t = _manualTimeOfDay;
        }
        else
        {
            t = _manualTimeOfDay;
        }

        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(TimeOfDayId, t);
        _renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>Called when component is added in editor — auto-configures the box.</summary>
    private void Reset()
    {
        transform.localScale = Vector3.one * _boxScale;

        #if UNITY_EDITOR
        var col = GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        #endif
    }
}
