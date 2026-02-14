using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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
    [SerializeField] private float gridSize = 0.1f;

    [Header("Scroll Behavior")]
    [Tooltip("Degrees rotated per scroll tick (MMB + scroll).")]
    [SerializeField] private float rotationStep = 15f;

    [Tooltip("World units of depth change per scroll tick.")]
    [SerializeField] private float depthStep = 0.3f;

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
    private InputAction _middleClickAction;
    private InputAction _gridToggle;
    private InputAction _scrollAction;

    private bool _isEnabled;
    private bool _gridSnap;

    private PlaceableObject _held;
    private Rigidbody _heldRb;
    private Vector3 _grabTarget;
    private float _grabDepth;

    // Shadow preview
    private GameObject _shadowGO;
    private MeshRenderer _shadowRenderer;
    private Material _shadowMat;
    private static readonly Color s_shadowValid = new Color(0.2f, 0.8f, 0.3f, 0.5f);
    private static readonly Color s_shadowInvalid = new Color(0.8f, 0.2f, 0.2f, 0.5f);

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        _mousePosition = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");
        _mouseClick = new InputAction("MouseClick", InputActionType.Button, "<Mouse>/leftButton");
        _middleClickAction = new InputAction("MiddleClick", InputActionType.Button, "<Mouse>/middleButton");
        _gridToggle = new InputAction("GridToggle", InputActionType.Button, "<Keyboard>/g");
        _scrollAction = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");

        BuildShadow();
    }

    private void OnEnable()
    {
        _mousePosition.Enable();
        _mouseClick.Enable();
        _middleClickAction.Enable();
        _gridToggle.Enable();
        _scrollAction.Enable();
    }

    private void OnDisable()
    {
        _mousePosition.Disable();
        _mouseClick.Disable();
        _middleClickAction.Disable();
        _gridToggle.Disable();
        _scrollAction.Disable();
    }

    private void OnDestroy()
    {
        if (_shadowMat != null) Destroy(_shadowMat);
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

        // Update grab target, scroll input, and shadow while holding
        if (_held != null)
        {
            UpdateGrabTarget();
            UpdateScrollInput();
            UpdateShadow();
        }
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
        _grabDepth = Vector3.Dot(_heldRb.worldCenterOfMass - cam.transform.position, cam.transform.forward);

        placeable.OnPickedUp();
        ShowShadow(true);
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
        ShowShadow(false);
    }

    private void UpdateGrabTarget()
    {
        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        // Project cursor onto plane at _grabDepth distance along camera forward
        Vector3 planePoint = cam.transform.position + cam.transform.forward * _grabDepth;
        var plane = new Plane(-cam.transform.forward, planePoint);
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

    private void UpdateScrollInput()
    {
        float scroll = _scrollAction.ReadValue<float>();
        if (Mathf.Abs(scroll) < 0.01f) return;

        if (_middleClickAction.IsPressed())
        {
            // MMB + scroll → rotate
            _held.transform.Rotate(Vector3.up, rotationStep * Mathf.Sign(scroll), Space.World);
        }
        else
        {
            // Scroll alone → adjust grab depth
            _grabDepth += Mathf.Sign(scroll) * depthStep;
            _grabDepth = Mathf.Max(0.5f, _grabDepth); // don't let objects go behind camera
        }
    }

    // ── Shadow preview ──────────────────────────────────────────────────

    private void BuildShadow()
    {
        _shadowGO = new GameObject("PlacementShadow");
        _shadowGO.transform.SetParent(transform);

        var mf = _shadowGO.AddComponent<MeshFilter>();
        _shadowRenderer = _shadowGO.AddComponent<MeshRenderer>();

        // Procedural quad
        var mesh = new Mesh { name = "ShadowQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mf.sharedMesh = mesh;

        // Procedural soft-circle texture (reuse SapDecalPool pattern)
        int texSize = 32;
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = texSize * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float a = Mathf.Clamp01((radius - dist) / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        // URP Particles/Unlit shader with fallback chain
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        _shadowMat = new Material(shader);
        _shadowMat.mainTexture = tex;
        _shadowMat.color = s_shadowValid;
        _shadowMat.SetFloat("_Surface", 1f); // Transparent
        _shadowMat.SetFloat("_Blend", 0f);   // Alpha
        _shadowMat.renderQueue = 3000;
        _shadowRenderer.sharedMaterial = _shadowMat;
        _shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _shadowRenderer.receiveShadows = false;

        _shadowGO.SetActive(false);
    }

    private void ShowShadow(bool show)
    {
        if (_shadowGO != null)
            _shadowGO.SetActive(show);
    }

    private void UpdateShadow()
    {
        if (_shadowGO == null || _heldRb == null) return;

        Vector3 objPos = _heldRb.position;

        // Raycast down from held object to find surface
        float surfaceY = objPos.y - 1f; // fallback if no hit
        if (Physics.Raycast(objPos, Vector3.down, out RaycastHit hit, 50f))
            surfaceY = hit.point.y;

        float targetX = objPos.x;
        float targetZ = objPos.z;

        if (_gridSnap)
        {
            targetX = Mathf.Round(targetX / gridSize) * gridSize;
            targetZ = Mathf.Round(targetZ / gridSize) * gridSize;
        }

        // Check if shadow position is on a valid surface
        Vector3 shadowPos = new Vector3(targetX, surfaceY + 0.01f, targetZ);
        bool onSurface = IsOnAnySurface(shadowPos);
        _shadowMat.color = onSurface ? s_shadowValid : s_shadowInvalid;

        _shadowGO.transform.position = shadowPos;
        _shadowGO.transform.rotation = Quaternion.identity;

        // Scale to match held object's footprint
        var col = _held.GetComponent<Collider>();
        if (col != null)
        {
            Vector3 ext = col.bounds.extents;
            float diameter = Mathf.Max(ext.x, ext.z) * 2f;
            _shadowGO.transform.localScale = new Vector3(diameter, 1f, diameter);
        }
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
            ShowShadow(false);
        }
    }
}
