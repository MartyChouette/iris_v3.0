using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PlaceableObject : MonoBehaviour
{
    // ── Static registry (avoids FindObjectsByType) ──────────────────
    private static readonly List<PlaceableObject> s_all = new();
    public static IReadOnlyList<PlaceableObject> All => s_all;

    public enum State { Resting, Held, Placed }

    [Header("Visual Feedback")]
    [Tooltip("Color multiplier applied to the material when held.")]
    [SerializeField] private float heldBrightness = 1.4f;

    [Header("Respawn")]
    [Tooltip("Seconds after leaving world bounds before recovery (prevents flicker).")]
    [SerializeField] private float respawnDelay = 0.5f;

    [Header("Item Classification")]
    [Tooltip("What kind of apartment item this is (for tidiness scoring).")]
    [SerializeField] private ItemCategory _itemCategory = ItemCategory.General;

    [Tooltip("Name of the DropZone this item belongs to (empty = no home zone).")]
    [SerializeField] private string _homeZoneName = "";

    [Tooltip("Secondary zone name (e.g. 'CoffeeTable'). Item counts as home on either zone.")]
    [SerializeField] private string _altHomeZoneName = "";

    [Tooltip("Short description shown when picked up (leave empty to use object name).")]
    [SerializeField] private string _itemDescription = "";

    [Header("Surface Restrictions")]
    [Tooltip("If true, this object can be placed on vertical (wall) surfaces.")]
    [SerializeField] private bool canWallMount;

    [Tooltip("If true, this object can ONLY be placed on walls (rejects tables/shelves).")]
    [SerializeField] private bool wallOnly;

    [Tooltip("Random rotation range (degrees) applied when spawned on a wall.")]
    [SerializeField] private float crookedAngleRange = 12f;

    [Header("Dishelved Detection")]
    [Tooltip("If true, this item counts as messy when tilted (books, magazines, papers).")]
    [SerializeField] private bool _canBeDishelved;

    [Tooltip("Angle threshold (degrees) — tilted beyond this from upright = dishelved.")]
    [SerializeField] private float _dishevelAngle = 25f;

    [Header("Home Position")]
    [Tooltip("If true, captures spawn position as home on Awake (when _homePosition is unset).")]
    [SerializeField] private bool _useSpawnAsHome;

    [Tooltip("World-space home position. Set manually or auto-captured via _useSpawnAsHome.")]
    [SerializeField] private Vector3 _homePosition;

    [Tooltip("Distance threshold — object counts as 'at home' when within this range.")]
    [SerializeField] private float _homeTolerance = 0.2f;

    // ── Static world bounds (set by ApartmentManager) ─────────────────
    private static Bounds s_worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    private static bool s_boundsSet;

    /// <summary>Set the world bounding box. Objects outside this are recovered to the nearest surface.</summary>
    public static void SetWorldBounds(Bounds bounds) { s_worldBounds = bounds; s_boundsSet = true; }

    public State CurrentState { get; private set; } = State.Resting;
    public ItemCategory Category => _itemCategory;
    public string HomeZoneName => _homeZoneName;
    public string AltHomeZoneName => _altHomeZoneName;
    public string ItemDescription => !string.IsNullOrEmpty(_itemDescription) ? _itemDescription : name;
    private bool _isAtHomeOverride;
    public bool IsAtHome
    {
        get => _isAtHomeOverride || IsNearHome;
        set => _isAtHomeOverride = value;
    }

    /// <summary>
    /// True when this object is near its home position (within tolerance).
    /// Excludes held objects and requires a non-zero home position.
    /// </summary>
    public bool IsNearHome => _homePosition != Vector3.zero
        && CurrentState != State.Held
        && Vector3.Distance(transform.position, _homePosition) <= _homeTolerance;
    public bool CanWallMount => canWallMount;
    public bool WallOnly => wallOnly;
    public bool CanBeDishelved => _canBeDishelved;
    public PlacementSurface LastPlacedSurface => _lastPlacedSurface;

    /// <summary>
    /// Assign the surface this item is sitting on. Used by DrawerController
    /// to claim editor-placed items that didn't go through OnPlaced().
    /// </summary>
    public void SetLastPlacedSurface(PlacementSurface surface) => _lastPlacedSurface = surface;

    /// <summary>
    /// Configure home settings at runtime (e.g. from BookItem).
    /// Captures current position as home if useSpawnAsHome is true and no home is set yet.
    /// </summary>
    public void ConfigureHome(string homeZone = null, string altHomeZone = null, bool useSpawnAsHome = false)
    {
        if (homeZone != null) _homeZoneName = homeZone;
        if (altHomeZone != null) _altHomeZoneName = altHomeZone;
        if (useSpawnAsHome && _homePosition == Vector3.zero)
            _homePosition = transform.position;
    }

    /// <summary>
    /// True when this item is tilted beyond its angle threshold.
    /// Used by ObjectGrabber for click-to-straighten.
    /// </summary>
    public bool IsTilted
    {
        get
        {
            if (!_canBeDishelved) return false;
            if (CurrentState == State.Held) return false;
            if (canWallMount) return false;
            float angle = Vector3.Angle(transform.up, Vector3.up);
            return angle > _dishevelAngle;
        }
    }

    /// <summary>
    /// True when this item is messy / out of place. Includes:
    /// - Trash items (always disheveled until disposed)
    /// - Items not at home (need to be returned)
    /// - Tilted items (crooked books, magazines)
    /// </summary>
    public bool IsDishelved
    {
        get
        {
            if (CurrentState == State.Held) return false;
            if (_itemCategory == ItemCategory.Trash) return true;
            if (!IsAtHome && _homePosition != Vector3.zero) return true;
            return IsTilted;
        }
    }

    /// <summary>
    /// Straighten a tilted item in-place (zeroes X/Z rotation, keeps Y).
    /// Returns true if the item was tilted and got straightened.
    /// </summary>
    public bool Straighten()
    {
        if (!IsTilted) return false;
        float yAngle = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
        if (_rb != null) _rb.angularVelocity = Vector3.zero;
        Debug.Log($"[PlaceableObject] {name} straightened.");
        return true;
    }

    /// <summary>
    /// True when the object is resting on the floor (not held, not on a surface/DropZone).
    /// Items on PlacementSurfaces or in DropZones are NOT considered clutter.
    /// </summary>
    public bool IsOnFloor => CurrentState != State.Held
                          && transform.position.y < 0.15f
                          && _lastPlacedSurface == null;

    private Renderer _renderer;
    private Material _instanceMat;
    private Color _originalColor;

    // Silhouette overlay (see-through when occluded)
    private Material _silhouetteMat;
    private GameObject _silhouetteGO;

    private Vector3 _lastValidPosition;
    private Quaternion _lastValidRotation;
    private PlacementSurface _lastPlacedSurface;
    private Rigidbody _rb;
    private float _fallTimer;

    private Collider[] _colliders;
    private Coroutine _validationCoroutine;
    private Coroutine _flashCoroutine;

    private void OnEnable() => s_all.Add(this);
    private void OnDisable() => s_all.Remove(this);

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.sharedMaterial != null)
        {
            _instanceMat = new Material(_renderer.sharedMaterial);
            _renderer.material = _instanceMat;
            _originalColor = _instanceMat.color;
        }

        // Build silhouette material once (reused each pickup)
        var silShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (silShader == null) silShader = Shader.Find("Sprites/Default");
        if (silShader != null)
        {
            _silhouetteMat = new Material(silShader);
            _silhouetteMat.SetFloat("_Surface", 1f); // Transparent
            _silhouetteMat.SetFloat("_Blend", 0f);   // Alpha
            _silhouetteMat.SetInt("_ZTest", (int)CompareFunction.Greater);
            _silhouetteMat.renderQueue = 3100;
        }

        _lastValidPosition = transform.position;
        _lastValidRotation = transform.rotation;
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponents<Collider>();

        // Auto-capture spawn position as home
        if (_useSpawnAsHome && _homePosition == Vector3.zero)
            _homePosition = transform.position;
    }

    private void Update()
    {
        if (CurrentState == State.Held) return;

        if (!s_worldBounds.Contains(transform.position))
        {
            _fallTimer += Time.deltaTime;
            if (_fallTimer >= respawnDelay)
            {
                RecoverToNearestSurface();
                _fallTimer = 0f;
            }
        }
        else
        {
            _fallTimer = 0f;
        }
    }

    private void RecoverToNearestSurface()
    {
        // If last valid position is ALSO out of bounds, try to find any surface.
        // Otherwise we'd loop forever respawning to an invalid spot.
        Vector3 target = _lastValidPosition;
        if (!s_worldBounds.Contains(target))
        {
            var nearest = FindNearestSurface(s_worldBounds.center);
            if (nearest != null)
            {
                var hit = nearest.ProjectOntoSurface(s_worldBounds.center);
                target = hit.worldPosition + hit.surfaceNormal * 0.1f;
            }
            else
            {
                // No surface found — park at world center and go kinematic to prevent falling
                target = s_worldBounds.center;
            }
            _lastValidPosition = target;
            Debug.LogWarning($"[PlaceableObject] {name} had invalid recovery pos — placed at {target}.");
        }

        transform.position = target;
        transform.rotation = _lastValidRotation;
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            if (_lastPlacedSurface != null && _lastPlacedSurface.IsVertical)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }
            else
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
            }
        }
        CurrentState = State.Resting;
        StartFlash();
        Debug.Log($"[PlaceableObject] {name} out of bounds — respawned at {target}.");
    }

    private void OnDestroy()
    {
        if (_instanceMat != null) Destroy(_instanceMat);
        if (_silhouetteMat != null) Destroy(_silhouetteMat);
        if (_silhouetteGO != null) Destroy(_silhouetteGO);
    }

    // ── Grabbed / Released ────────────────────────────────────────────

    /// <summary>
    /// Called by ObjectGrabber when this object is picked up.
    /// </summary>
    public void OnPickedUp()
    {
        CurrentState = State.Held;
        _lastValidPosition = transform.position;
        _lastValidRotation = transform.rotation;
        IsAtHome = false;

        // If we were stored in a drawer, notify the drawer
        var drawer = GetComponentInParent<DrawerController>();
        if (drawer != null)
            drawer.RemoveStoredItem(this);

        if (_rb != null)
            _rb.isKinematic = false;

        // Disable colliders so held object doesn't block raycasts or knock items
        SetCollidersEnabled(false);

        // Brightness boost on main material
        if (_instanceMat != null)
            _instanceMat.color = _originalColor * heldBrightness;

        // Spawn silhouette child mesh (renders only where occluded by furniture)
        BuildSilhouette();

        if (_validationCoroutine != null)
        {
            StopCoroutine(_validationCoroutine);
            _validationCoroutine = null;
        }

        Debug.Log($"[PlaceableObject] {name} picked up.");
    }

    /// <summary>
    /// Called by ObjectGrabber when this object is placed on a surface.
    /// </summary>
    public void OnPlaced(PlacementSurface surface, bool gridSnapped, Vector3 position, Quaternion rotation)
    {
        CurrentState = State.Placed;
        _lastValidPosition = position;
        _lastValidRotation = rotation;
        _lastPlacedSurface = surface;

        // Check if placed on a matching DropZone (home or alt zone)
        if (surface != null)
        {
            var zone = surface.GetComponent<DropZone>();
            if (zone != null)
            {
                if ((!string.IsNullOrEmpty(_homeZoneName) && zone.ZoneName == _homeZoneName)
                    || (!string.IsNullOrEmpty(_altHomeZoneName) && zone.ZoneName == _altHomeZoneName))
                    IsAtHome = true;
            }
        }

        transform.position = position;
        transform.rotation = rotation;

        SetCollidersEnabled(true);
        RestoreMaterial();

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            if (surface != null && surface.IsVertical)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }
            else
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                // Freeze X/Z rotation to prevent toppling; Y rotation preserved for scroll-rotate
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        if (surface != null && !surface.IsVertical)
        {
            if (_validationCoroutine != null) StopCoroutine(_validationCoroutine);
            _validationCoroutine = StartCoroutine(PlacementValidation());
        }

        Debug.Log($"[PlaceableObject] {name} placed at {position} (grid={gridSnapped}, wall={surface != null && surface.IsVertical}).");
    }

    /// <summary>
    /// Called by ObjectGrabber if placement is cancelled (e.g. area exit without a surface).
    /// </summary>
    public void OnDropped()
    {
        CurrentState = State.Resting;
        SetCollidersEnabled(true);
        RestoreMaterial();

        // Check if we ended up somewhere the player can't see
        StartCoroutine(CheckVisibilityAfterDrop());

        Debug.Log($"[PlaceableObject] {name} dropped.");
    }

    // ── Collider toggle ──────────────────────────────────────────────

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null) return;
        foreach (var col in _colliders)
        {
            if (col != null)
                col.enabled = enabled;
        }
    }

    // ── Wall alignment ────────────────────────────────────────────────

    public void AlignToWall(Vector3 wallNormal, float rotationAngle)
    {
        transform.rotation = Quaternion.LookRotation(wallNormal, Vector3.up)
            * Quaternion.AngleAxis(rotationAngle, Vector3.forward);
    }

    public void ApplyCrookedOffset(Vector3 wallNormal)
    {
        float angle = Random.Range(-crookedAngleRange, crookedAngleRange);
        transform.rotation *= Quaternion.AngleAxis(angle, -wallNormal);
    }

    // ── Silhouette overlay (see-through furniture) ────────────────────

    private void BuildSilhouette()
    {
        if (_silhouetteMat == null) return;

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        // Tint: washed-out version of object color at 35% opacity
        _silhouetteMat.color = new Color(
            _originalColor.r * 0.5f + 0.5f,
            _originalColor.g * 0.5f + 0.5f,
            _originalColor.b * 0.5f + 0.5f,
            0.35f);

        _silhouetteGO = new GameObject("Silhouette");
        _silhouetteGO.transform.SetParent(transform, false);

        var silMF = _silhouetteGO.AddComponent<MeshFilter>();
        silMF.sharedMesh = mf.sharedMesh;

        var silRend = _silhouetteGO.AddComponent<MeshRenderer>();
        silRend.sharedMaterial = _silhouetteMat;
        silRend.shadowCastingMode = ShadowCastingMode.Off;
        silRend.receiveShadows = false;
    }

    private void DestroySilhouette()
    {
        if (_silhouetteGO != null)
        {
            Destroy(_silhouetteGO);
            _silhouetteGO = null;
        }
    }

    // ── Safety: drop visibility check ─────────────────────────────────

    private IEnumerator CheckVisibilityAfterDrop()
    {
        yield return new WaitForSeconds(2f);

        if (CurrentState != State.Resting) yield break;

        var cam = Camera.main;
        if (cam == null) yield break;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        bool inView = vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;

        if (!inView)
        {
            // Place on nearest surface instead of raw respawn
            var nearest = FindNearestSurface(transform.position);
            if (nearest != null)
            {
                var hit = nearest.ProjectOntoSurface(transform.position);
                float halfY = 0f;
                var col = GetComponent<Collider>();
                if (col != null)
                    halfY = Mathf.Abs(Vector3.Dot(col.bounds.extents, hit.surfaceNormal.normalized));

                Vector3 pos = hit.worldPosition + hit.surfaceNormal * halfY;
                Quaternion rot = nearest.IsVertical
                    ? Quaternion.LookRotation(hit.surfaceNormal, Vector3.up)
                    : transform.rotation;

                Debug.Log($"[PlaceableObject] {name} out of view after drop — placed on {nearest.name}.");
                OnPlaced(nearest, false, pos, rot);
            }
            else
            {
                Debug.Log($"[PlaceableObject] {name} out of view, no surface found — recovering.");
                RecoverToNearestSurface();
            }
        }
    }

    private PlacementSurface FindNearestSurface(Vector3 worldPos)
    {
        return PlacementSurface.FindNearest(worldPos,
            skipVertical: !canWallMount,
            skipHorizontal: wallOnly);
    }

    // ── Safety: Placement validation timer ────────────────────────────

    private IEnumerator PlacementValidation()
    {
        yield return new WaitForSeconds(1.5f);

        if (CurrentState != State.Placed || _lastPlacedSurface == null)
            yield break;

        bool onSurface = _lastPlacedSurface.ContainsWorldPoint(transform.position);
        bool nearValid = Vector3.Distance(transform.position, _lastValidPosition) < 0.5f;

        if (!onSurface && !nearValid)
        {
            Debug.Log($"[PlaceableObject] {name} drifted from surface — snapping back.");
            transform.position = _lastValidPosition;
            transform.rotation = _lastValidRotation;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            StartFlash();
        }

        _validationCoroutine = null;
    }

    // ── Visual feedback flash ─────────────────────────────────────────

    private void StartFlash()
    {
        if (_instanceMat == null) return;
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        Color bright = _originalColor * 2f;
        for (int i = 0; i < 2; i++)
        {
            _instanceMat.color = bright;
            yield return new WaitForSeconds(0.1f);
            _instanceMat.color = _originalColor;
            yield return new WaitForSeconds(0.1f);
        }
        _flashCoroutine = null;
    }

    // ── Material restore ──────────────────────────────────────────────

    private void RestoreMaterial()
    {
        if (_instanceMat != null)
            _instanceMat.color = _originalColor;

        DestroySilhouette();
    }
}
