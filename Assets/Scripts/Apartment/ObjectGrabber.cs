using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectGrabber : MonoBehaviour
{
    [Header("Physics")]
    [Tooltip("Spring strength pulling the object toward the cursor.")]
    [SerializeField] private float grabSpring = 120f;

    [Tooltip("Damper opposing overshoot.")]
    [SerializeField] private float grabDamper = 18f;

    [Tooltip("Maximum acceleration applied to the grabbed object.")]
    [SerializeField] private float maxAccel = 60f;

    [Tooltip("Maximum speed cap to prevent tunneling.")]
    [SerializeField] private float maxSpeed = 12f;

    [Header("Grid Snap")]
    [Tooltip("Grid cell size in world units (X/Z).")]
    [SerializeField] private float gridSize = 0.5f;

    [Header("Raycast")]
    [Tooltip("Layer mask for placeable objects.")]
    [SerializeField] private LayerMask placeableLayer = ~0;

    [Tooltip("Camera used for raycasting. Auto-finds MainCamera if null.")]
    [SerializeField] private Camera cam;

    [Header("Placement Surfaces")]
    [Tooltip("Valid surfaces for placing objects. If empty, no surface clamping is applied.")]
    [SerializeField] private PlacementSurface[] surfaces;

    // Inline InputActions (same pattern as MarkerController)
    private InputAction _mousePosition;
    private InputAction _mouseClick;
    private InputAction _gridToggle;

    private bool _isEnabled;
    private bool _gridSnap;

    private PlaceableObject _held;
    private Rigidbody _heldRb;
    private Vector3 _grabTarget;

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        _mousePosition = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");
        _mouseClick = new InputAction("MouseClick", InputActionType.Button, "<Mouse>/leftButton");
        _gridToggle = new InputAction("GridToggle", InputActionType.Button, "<Keyboard>/g");
    }

    private void OnEnable()
    {
        _mousePosition.Enable();
        _mouseClick.Enable();
        _gridToggle.Enable();
    }

    private void OnDisable()
    {
        _mousePosition.Disable();
        _mouseClick.Disable();
        _gridToggle.Disable();
    }

    private void Update()
    {
        if (!_isEnabled) return;

        // Toggle grid snap
        if (_gridToggle.WasPressedThisFrame())
        {
            _gridSnap = !_gridSnap;
            Debug.Log($"[ObjectGrabber] Grid snap: {(_gridSnap ? "ON" : "OFF")}");
        }

        if (_mouseClick.WasPressedThisFrame())
        {
            if (_held == null)
                TryPickUp();
            else
                Place();
        }

        // Update grab target while holding
        if (_held != null)
            UpdateGrabTarget();
    }

    private void FixedUpdate()
    {
        if (_held == null || _heldRb == null) return;

        // Spring-damper pull (same math as GrabPull)
        Vector3 toTarget = _grabTarget - _heldRb.worldCenterOfMass;
        Vector3 accel = toTarget * grabSpring - _heldRb.linearVelocity * grabDamper;

        if (accel.sqrMagnitude > maxAccel * maxAccel)
            accel = accel.normalized * maxAccel;

        _heldRb.AddForce(accel, ForceMode.Acceleration);

        // Speed cap
        if (_heldRb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            _heldRb.linearVelocity = _heldRb.linearVelocity.normalized * maxSpeed;

        // Validate position against surfaces while holding
        if (surfaces != null && surfaces.Length > 0)
            _held.ValidatePosition(surfaces);
    }

    private void TryPickUp()
    {
        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, placeableLayer))
            return;

        var placeable = hit.collider.GetComponent<PlaceableObject>();
        if (placeable == null)
            placeable = hit.collider.GetComponentInParent<PlaceableObject>();

        if (placeable == null || placeable.CurrentState == PlaceableObject.State.Held)
            return;

        _held = placeable;
        _heldRb = placeable.GetComponent<Rigidbody>();
        _heldRb.useGravity = false;
        _grabTarget = _heldRb.worldCenterOfMass;

        placeable.OnPickedUp();
    }

    private void Place()
    {
        if (_held == null) return;

        Vector3 pos = _heldRb.position;

        if (_gridSnap)
        {
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
            _heldRb.position = pos;
        }

        // Clamp to nearest surface if surfaces are configured
        if (surfaces != null && surfaces.Length > 0)
        {
            if (!IsOnAnySurface(pos))
            {
                pos = ClampToNearestSurface(pos);
                _heldRb.position = pos;
            }
        }

        // Re-enable gravity so object settles on surface
        _heldRb.useGravity = true;
        _heldRb.linearVelocity = Vector3.zero;

        _held.OnPlaced(_gridSnap, pos);
        _held = null;
        _heldRb = null;
    }

    private void UpdateGrabTarget()
    {
        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        // Project cursor onto plane perpendicular to camera forward (same as GrabPull)
        var plane = new Plane(-cam.transform.forward, _heldRb.worldCenterOfMass);
        if (plane.Raycast(ray, out float enter))
            _grabTarget = ray.GetPoint(enter);
    }

    private bool IsOnAnySurface(Vector3 worldPos)
    {
        foreach (var surface in surfaces)
        {
            if (surface != null && surface.ContainsWorldPoint(worldPos))
                return true;
        }
        return false;
    }

    private Vector3 ClampToNearestSurface(Vector3 worldPos)
    {
        float bestDist = float.MaxValue;
        Vector3 bestPos = worldPos;

        foreach (var surface in surfaces)
        {
            if (surface == null) continue;
            Vector3 clamped = surface.ClampToSurface(worldPos);
            float dist = (clamped - worldPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestPos = clamped;
            }
        }

        return bestPos;
    }

    /// <summary>
    /// Enable or disable grabbing. Called by ApartmentManager during state transitions.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;

        if (!enabled && _held != null)
        {
            // Drop the held object
            _heldRb.useGravity = true;
            _heldRb.linearVelocity = Vector3.zero;
            _held.OnDropped();
            _held = null;
            _heldRb = null;
        }
    }
}
