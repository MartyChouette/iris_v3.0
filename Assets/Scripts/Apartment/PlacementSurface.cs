using UnityEngine;

/// <summary>
/// Component placed on tables, shelves, and walls to define valid placement bounds.
/// ObjectGrabber raycasts against the auto-generated trigger collider on the Surfaces layer
/// and uses ProjectOntoSurface / SnapToGrid for positioning.
/// </summary>
public class PlacementSurface : MonoBehaviour
{
    public enum SurfaceAxis { Up, Forward }

    public struct SurfaceHitResult
    {
        public Vector3 worldPosition;
        public Vector3 surfaceNormal;
        public bool isVertical;
        public PlacementSurface surface;
    }

    [Header("Surface Bounds")]
    [Tooltip("Local-space bounds of the placement area (center + extents).")]
    [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));

    [Header("Orientation")]
    [Tooltip("Up for tables/shelves (normal = transform.up), Forward for walls (normal = transform.forward).")]
    [SerializeField] private SurfaceAxis normalAxis = SurfaceAxis.Up;

    [Tooltip("Layer index for the auto-generated raycast trigger.")]
    [SerializeField] private int surfaceLayerIndex = 0;

    /// <summary>World-space surface normal based on orientation axis.</summary>
    public Vector3 SurfaceNormal =>
        normalAxis == SurfaceAxis.Up ? transform.up : transform.forward;

    /// <summary>True if the surface is roughly vertical (wall).</summary>
    public bool IsVertical =>
        Vector3.Dot(SurfaceNormal, Vector3.up) < 0.3f;

    private void Awake()
    {
        BuildTrigger();
    }

    private void BuildTrigger()
    {
        var triggerGO = new GameObject("SurfaceTrigger");
        triggerGO.transform.SetParent(transform, false);
        triggerGO.layer = surfaceLayerIndex;

        var box = triggerGO.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = localBounds.center;

        // Slightly thicken along the normal so the raycast has something to hit
        Vector3 size = localBounds.size;
        if (normalAxis == SurfaceAxis.Up)
            size.y = Mathf.Max(size.y, 0.05f);
        else
            size.z = Mathf.Max(size.z, 0.05f);

        box.size = size;
    }

    // ── Tangent helpers ──────────────────────────────────────────────

    /// <summary>
    /// Returns the two local-space axis indices that are tangent to the surface.
    /// Up → X(0),Z(2); Forward → X(0),Y(1).
    /// </summary>
    private void GetTangentAxes(out int a, out int b, out int normalIdx)
    {
        if (normalAxis == SurfaceAxis.Up)
        {
            a = 0; b = 2; normalIdx = 1; // X, Z tangent; Y normal
        }
        else
        {
            a = 0; b = 1; normalIdx = 2; // X, Y tangent; Z normal
        }
    }

    // ── Projection / Snapping ────────────────────────────────────────

    private const float EdgeMargin = 0.03f;

    /// <summary>
    /// Project a world point onto this surface, clamped within bounds (with edge margin).
    /// Returns the clamped world position at the surface face.
    /// </summary>
    public SurfaceHitResult ProjectOntoSurface(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        GetTangentAxes(out int a, out int b, out int n);

        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;

        // Clamp tangent axes within bounds with edge margin
        float marginA = (max[a] - min[a] > EdgeMargin * 4f) ? EdgeMargin : 0f;
        float marginB = (max[b] - min[b] > EdgeMargin * 4f) ? EdgeMargin : 0f;
        local[a] = Mathf.Clamp(local[a], min[a] + marginA, max[a] - marginA);
        local[b] = Mathf.Clamp(local[b], min[b] + marginB, max[b] - marginB);

        // Set normal axis to surface face (top of bounds for Up, front for Forward)
        local[n] = localBounds.center[n] + localBounds.extents[n];

        return new SurfaceHitResult
        {
            worldPosition = transform.TransformPoint(local),
            surfaceNormal = SurfaceNormal,
            isVertical = IsVertical,
            surface = this
        };
    }

    /// <summary>
    /// Snap a world point to a grid on this surface's tangent plane, clamped within bounds (with edge margin).
    /// </summary>
    public Vector3 SnapToGrid(Vector3 worldPoint, float gridSize)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        GetTangentAxes(out int a, out int b, out int n);

        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;

        float marginA = (max[a] - min[a] > EdgeMargin * 4f) ? EdgeMargin : 0f;
        float marginB = (max[b] - min[b] > EdgeMargin * 4f) ? EdgeMargin : 0f;
        local[a] = Mathf.Clamp(Mathf.Round(local[a] / gridSize) * gridSize, min[a] + marginA, max[a] - marginA);
        local[b] = Mathf.Clamp(Mathf.Round(local[b] / gridSize) * gridSize, min[b] + marginB, max[b] - marginB);
        local[n] = localBounds.center[n] + localBounds.extents[n];

        return transform.TransformPoint(local);
    }

    // ── Containment (axis-agnostic) ──────────────────────────────────

    /// <summary>
    /// Check whether a world-space point falls within this surface's tangent bounds.
    /// </summary>
    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        GetTangentAxes(out int a, out int b, out _);

        return Mathf.Abs(local[a] - localBounds.center[a]) <= localBounds.extents[a]
            && Mathf.Abs(local[b] - localBounds.center[b]) <= localBounds.extents[b];
    }

    /// <summary>
    /// Clamp a world-space point to the nearest valid position on this surface (with edge margin).
    /// </summary>
    public Vector3 ClampToSurface(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        GetTangentAxes(out int a, out int b, out int n);

        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;

        float marginA = (max[a] - min[a] > EdgeMargin * 4f) ? EdgeMargin : 0f;
        float marginB = (max[b] - min[b] > EdgeMargin * 4f) ? EdgeMargin : 0f;
        local[a] = Mathf.Clamp(local[a], min[a] + marginA, max[a] - marginA);
        local[b] = Mathf.Clamp(local[b], min[b] + marginB, max[b] - marginB);
        local[n] = localBounds.center[n] + localBounds.extents[n];

        return transform.TransformPoint(local);
    }

    /// <summary>
    /// The world-space Y of the surface top (for horizontal surfaces).
    /// </summary>
    public float SurfaceY =>
        transform.TransformPoint(new Vector3(0f, localBounds.center.y + localBounds.extents.y, 0f)).y;

    // ── Static utility ─────────────────────────────────────────────────

    /// <summary>
    /// Find the nearest PlacementSurface to a world position.
    /// Optionally skip vertical or horizontal surfaces.
    /// </summary>
    public static PlacementSurface FindNearest(Vector3 worldPos, bool skipVertical = false, bool skipHorizontal = false)
    {
        var all = Object.FindObjectsByType<PlacementSurface>(FindObjectsSortMode.None);
        PlacementSurface best = null;
        float bestDist = float.MaxValue;

        foreach (var surface in all)
        {
            if (surface == null) continue;
            if (skipVertical && surface.IsVertical) continue;
            if (skipHorizontal && !surface.IsVertical) continue;

            Vector3 clamped = surface.ClampToSurface(worldPos);
            float dist = (clamped - worldPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = surface;
            }
        }

        return best;
    }

    // ── Gizmo ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(localBounds.center, localBounds.size);
        Gizmos.DrawWireCube(localBounds.center, localBounds.size);

        // Normal direction arrow
        Gizmos.color = Color.cyan;
        Vector3 center = localBounds.center;
        Vector3 normalDir = normalAxis == SurfaceAxis.Up ? Vector3.up : Vector3.forward;
        Gizmos.DrawRay(center, normalDir * 0.5f);
    }
}
