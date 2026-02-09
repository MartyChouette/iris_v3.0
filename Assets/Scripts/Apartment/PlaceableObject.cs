using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PlaceableObject : MonoBehaviour
{
    public enum State { Resting, Held, Placed }

    [Header("Visual Feedback")]
    [Tooltip("Color multiplier applied to the material when held.")]
    [SerializeField] private float heldBrightness = 1.4f;

    public State CurrentState { get; private set; } = State.Resting;

    private Renderer _renderer;
    private Material _instanceMat;
    private Color _originalColor;

    private Vector3 _lastValidPosition;
    private PlacementSurface _currentSurface;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.sharedMaterial != null)
        {
            // Create an instance so we don't modify shared materials
            _instanceMat = new Material(_renderer.sharedMaterial);
            _renderer.material = _instanceMat;
            _originalColor = _instanceMat.color;
        }

        _lastValidPosition = transform.position;
    }

    private void OnDestroy()
    {
        if (_instanceMat != null)
            Destroy(_instanceMat);
    }

    /// <summary>
    /// Called by ObjectGrabber when this object is picked up.
    /// </summary>
    public void OnPickedUp()
    {
        CurrentState = State.Held;
        _lastValidPosition = transform.position;

        if (_instanceMat != null)
            _instanceMat.color = _originalColor * heldBrightness;

        Debug.Log($"[PlaceableObject] {name} picked up.");
    }

    /// <summary>
    /// Called by ObjectGrabber when this object is placed.
    /// </summary>
    public void OnPlaced(bool gridSnapped, Vector3 position)
    {
        CurrentState = State.Placed;
        _lastValidPosition = position;

        if (_instanceMat != null)
            _instanceMat.color = _originalColor;

        Debug.Log($"[PlaceableObject] {name} placed at {position} (grid={gridSnapped}).");
    }

    /// <summary>
    /// Called by ObjectGrabber if placement is cancelled (e.g. area exit).
    /// </summary>
    public void OnDropped()
    {
        CurrentState = State.Resting;

        if (_instanceMat != null)
            _instanceMat.color = _originalColor;

        Debug.Log($"[PlaceableObject] {name} dropped.");
    }

    /// <summary>
    /// Check if the object is within any placement surface bounds.
    /// If not, snap back to last valid position.
    /// Called by ObjectGrabber in FixedUpdate while holding.
    /// </summary>
    public void ValidatePosition(PlacementSurface[] surfaces)
    {
        if (surfaces == null || surfaces.Length == 0) return;

        Vector3 pos = transform.position;
        foreach (var surface in surfaces)
        {
            if (surface != null && surface.ContainsWorldPoint(pos))
            {
                _lastValidPosition = pos;
                _currentSurface = surface;
                return;
            }
        }

        // Not on any surface — don't snap while held (let ObjectGrabber clamp on Place)
    }
}
