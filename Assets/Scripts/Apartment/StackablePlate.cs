using UnityEngine;

/// <summary>
/// Stackable plate component. Plates stack via Unity parenting — placing one
/// on top of another parents it and snaps to the correct height offset.
/// Attach alongside PlaceableObject + InteractableHighlight + Rigidbody + Collider.
/// </summary>
public class StackablePlate : MonoBehaviour
{
    [Header("Stacking")]
    [Tooltip("Y offset per stacked plate.")]
    [SerializeField] private float _plateThickness = 0.03f;

    [Tooltip("Downward raycast distance to detect plate below after placement.")]
    [SerializeField] private float _stackDetectDistance = 0.15f;

    [Tooltip("Layer mask for plate detection raycast.")]
    [SerializeField] private LayerMask _plateLayer = ~0;

    /// <summary>The plate directly below this one in a stack (null if free/bottom).</summary>
    public StackablePlate ParentPlate { get; private set; }

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Called before ObjectGrabber picks this plate up.
    /// Detaches from parent stack, re-enables physics. Children above stay attached.
    /// </summary>
    public void PrepareForGrab()
    {
        if (ParentPlate != null)
        {
            transform.SetParent(null);
            ParentPlate = null;
        }

        if (_rb != null)
            _rb.isKinematic = false;
    }

    /// <summary>
    /// Called after ObjectGrabber places this plate on a surface.
    /// Raycasts down to find a plate below and joins the stack if found.
    /// </summary>
    public void TryJoinStack()
    {
        // Raycast down from center of this plate
        Vector3 origin = transform.position + Vector3.up * 0.01f;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                _stackDetectDistance, _plateLayer))
            return;

        // Skip self
        if (hit.collider.gameObject == gameObject) return;

        var belowPlate = hit.collider.GetComponent<StackablePlate>();
        if (belowPlate == null)
            belowPlate = hit.collider.GetComponentInParent<StackablePlate>();
        if (belowPlate == null) return;

        // Find topmost plate in the stack
        var topmost = FindTopmost(belowPlate);

        // Parent to the topmost plate
        transform.SetParent(topmost.transform);
        transform.localPosition = new Vector3(0f, _plateThickness, 0f);
        transform.localRotation = Quaternion.identity;
        ParentPlate = topmost;

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[StackablePlate] {name} stacked on {topmost.name}");
    }

    private static StackablePlate FindTopmost(StackablePlate plate)
    {
        // Walk up children to find the highest stacked plate
        var current = plate;
        foreach (Transform child in plate.transform)
        {
            var childPlate = child.GetComponent<StackablePlate>();
            if (childPlate != null)
                return FindTopmost(childPlate);
        }
        return current;
    }
}
