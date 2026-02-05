using UnityEngine;

/// <summary>
/// A slot on the target flower where a grafted part can be placed.
/// Shows a visual indicator (color) for empty vs occupied state.
/// </summary>
[DisallowMultipleComponent]
public class GraftableSlot : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("What kind of part this slot accepts.")]
    public FlowerPartKind acceptedKind = FlowerPartKind.Leaf;

    [Header("Runtime")]
    [Tooltip("Whether a part has been placed here.")]
    public bool isOccupied;

    [Tooltip("The part currently in this slot (if any).")]
    public FlowerPartRuntime occupant;

    [Header("Visuals")]
    [Tooltip("Color shown when the slot is empty and available.")]
    public Color emptyColor = new Color(0.3f, 0.9f, 0.4f, 0.5f);

    [Tooltip("Color shown when the slot is filled.")]
    public Color occupiedColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

    private Renderer _renderer;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        UpdateVisual();
    }

    /// <summary>Update the slot's visual indicator based on occupancy.</summary>
    public void UpdateVisual()
    {
        if (_renderer == null) return;
        if (_renderer.material != null)
            _renderer.material.color = isOccupied ? occupiedColor : emptyColor;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? occupiedColor : emptyColor;
        Gizmos.DrawWireSphere(transform.position, 0.03f);
    }
}
