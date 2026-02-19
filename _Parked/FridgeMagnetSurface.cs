using UnityEngine;

/// <summary>
/// A vertical placement surface on the fridge door exterior for magnets.
/// Attaches to fridge door pivot as a child, bounds-constrained to door dimensions.
/// </summary>
public class FridgeMagnetSurface : MonoBehaviour
{
    [Header("Bounds")]
    [Tooltip("Half-extents of the magnet placement area on the door.")]
    [SerializeField] private Vector2 _halfExtents = new Vector2(0.3f, 0.4f);

    [Tooltip("Layer index for the FridgeMagnets layer.")]
    [SerializeField] private int _magnetLayer = 20;

    /// <summary>Project a world-space point onto the door surface, clamped to bounds.</summary>
    public Vector3 ProjectOntoSurface(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);

        // Clamp to bounds
        local.x = Mathf.Clamp(local.x, -_halfExtents.x, _halfExtents.x);
        local.y = Mathf.Clamp(local.y, -_halfExtents.y, _halfExtents.y);
        local.z = 0f; // flat on surface

        return transform.TransformPoint(local);
    }

    /// <summary>Check if a world point is within the surface bounds.</summary>
    public bool IsWithinBounds(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        return Mathf.Abs(local.x) <= _halfExtents.x &&
               Mathf.Abs(local.y) <= _halfExtents.y;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(_halfExtents.x * 2f, _halfExtents.y * 2f, 0.01f));
    }
}
