using UnityEngine;

/// <summary>
/// Component placed on table tops and shelves to define valid placement bounds.
/// ObjectGrabber uses these to constrain held objects and clamp placement.
/// </summary>
public class PlacementSurface : MonoBehaviour
{
    [Header("Surface Bounds")]
    [Tooltip("Local-space bounds of the placement area (center + extents).")]
    [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));

    /// <summary>
    /// Check whether a world-space point falls within this surface's bounds.
    /// Only checks X/Z (horizontal); Y is ignored for containment.
    /// </summary>
    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        // Check X and Z within extents; Y is flexible (above surface is fine)
        return Mathf.Abs(local.x - localBounds.center.x) <= localBounds.extents.x
            && Mathf.Abs(local.z - localBounds.center.z) <= localBounds.extents.z;
    }

    /// <summary>
    /// Clamp a world-space point to the nearest valid position on this surface.
    /// Returns the clamped world position at the surface's Y height.
    /// </summary>
    public Vector3 ClampToSurface(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);

        local.x = Mathf.Clamp(local.x,
            localBounds.center.x - localBounds.extents.x,
            localBounds.center.x + localBounds.extents.x);

        local.z = Mathf.Clamp(local.z,
            localBounds.center.z - localBounds.extents.z,
            localBounds.center.z + localBounds.extents.z);

        // Place on surface top
        local.y = localBounds.center.y + localBounds.extents.y;

        return transform.TransformPoint(local);
    }

    /// <summary>
    /// The world-space Y of the surface top (for snapping objects onto it).
    /// </summary>
    public float SurfaceY =>
        transform.TransformPoint(new Vector3(0f, localBounds.center.y + localBounds.extents.y, 0f)).y;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(localBounds.center, localBounds.size);
        Gizmos.DrawWireCube(localBounds.center, localBounds.size);
    }
}
