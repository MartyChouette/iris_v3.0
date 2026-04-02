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
#pragma warning disable 0414
    [Tooltip("Color multiplier applied to the material when held.")]
    [SerializeField] private float heldBrightness = 1.4f;
#pragma warning restore 0414

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

    [Tooltip("Grid size multiplier for this item (0.5 = half grid, fits 2 per cell). 0 or 1 = use default.")]
    [SerializeField, Range(0f, 2f)] private float _gridSizeMultiplier = 1f;

    /// <summary>Multiplier applied to grid size for this item. Books use 0.5 to fit 2 per cell.</summary>
    public float GridSizeMultiplier => _gridSizeMultiplier <= 0f ? 1f : _gridSizeMultiplier;

    [Tooltip("Random rotation range (degrees) applied when spawned on a wall.")]
    [SerializeField] private float crookedAngleRange = 12f;

    [Header("Dishelved Detection")]
    [Tooltip("If true, this item counts as messy when tilted (books, magazines, papers).")]
    [SerializeField] private bool _canBeDishelved;

    [Tooltip("Angle threshold (degrees) — tilted beyond this from upright = dishelved.")]
    [SerializeField] private float _dishevelAngle = 25f;

    [Tooltip("Captured disheveled rotation. Use context menu 'Capture Disheveled Pose' to set.")]
    [SerializeField] private Quaternion _disheveledRotation = Quaternion.identity;

    [Tooltip("True when a disheveled rotation has been captured.")]
    [SerializeField] private bool _hasDisheveledPose;

    [Tooltip("If true, starts the scene in the disheveled pose.")]
    [SerializeField] private bool _startDishelved;

    [Header("Audio Overrides")]
    [Tooltip("Pickup sound override. If set, plays instead of ObjectGrabber's default.")]
    [SerializeField] private AudioClip _pickupSFXOverride;

    [Tooltip("Place sound override. If set, plays instead of ObjectGrabber's default.")]
    [SerializeField] private AudioClip _placeSFXOverride;

    [Header("Home Position")]
#pragma warning disable 0414
    [Tooltip("If true, captures spawn position as home on Awake (when _homePosition is unset).")]
    [SerializeField] private bool _useSpawnAsHome;
#pragma warning restore 0414

    [Tooltip("World-space home position. Set manually or auto-captured via _useSpawnAsHome.")]
    [SerializeField] private Vector3 _homePosition;

    [Tooltip("Distance threshold — object counts as 'at home' when within this range.")]
    [SerializeField] private float _homeTolerance = 0.2f;

    [Tooltip("World-space home rotation. Auto-captured alongside home position.")]
    [SerializeField] private Quaternion _homeRotation = Quaternion.identity;

    // ── Static world bounds (set by ApartmentManager) ─────────────────
    private static Bounds s_worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

    /// <summary>Set the world bounding box. Objects outside this are recovered to the nearest surface.</summary>
    public static void SetWorldBounds(Bounds bounds) { s_worldBounds = bounds; }

    public State CurrentState { get; private set; } = State.Resting;
    public ItemCategory Category => _itemCategory;
    public string HomeZoneName => _homeZoneName;
    public string AltHomeZoneName => _altHomeZoneName;
    public string ItemDescription => !string.IsNullOrEmpty(_itemDescription) ? _itemDescription : name;
    public AudioClip PickupSFXOverride => _pickupSFXOverride;
    public AudioClip PlaceSFXOverride => _placeSFXOverride;
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
    public Vector3 HomePosition => _homePosition;
    public Quaternion HomeRotation => _homeRotation;
    public float HomeTolerance => _homeTolerance;
    public bool HasHome => _homePosition != Vector3.zero;

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
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }
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
    /// Straighten a tilted item — snaps to home rotation if captured, otherwise zeroes X/Z.
    /// Returns true if the item was tilted and got straightened.
    /// </summary>
    public bool Straighten()
    {
        if (!IsTilted) return false;

        if (_homeRotation != Quaternion.identity)
            transform.rotation = _homeRotation;
        else
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (_rb != null) _rb.angularVelocity = Vector3.zero;
        Debug.Log($"[PlaceableObject] {name} straightened.");
        return true;
    }

    /// <summary>
    /// Apply the captured disheveled pose. Does nothing if no pose was captured.
    /// Call from mess spawners or DailyMessSpawner to scatter items.
    /// </summary>
    public void Dishevel()
    {
        if (!_canBeDishelved || !_hasDisheveledPose) return;

        transform.rotation = _disheveledRotation;

        if (_rb != null)
        {
            _rb.rotation = _disheveledRotation;
            _rb.angularVelocity = Vector3.zero;
            _rb.linearVelocity = Vector3.zero;
        }

        ApplyGlitch();
        Debug.Log($"[PlaceableObject] {name} disheveled.");
    }

    private void ApplyGlitch()
    {
        if (_instanceMat == null) return;
        if (_isGlitched) return;

        if (!s_glitchShaderCached)
        {
            s_glitchShaderCached = true;
            s_glitchShader = Shader.Find("Iris/PSXLitGlitch");
        }
        if (s_glitchShader == null) return;

        _originalShader = _instanceMat.shader;
        _instanceMat.shader = s_glitchShader;
        _instanceMat.SetFloat("_GlitchIntensity", 0.4f);
        _isGlitched = true;
    }

    private void RemoveGlitch()
    {
        if (!_isGlitched || _instanceMat == null || _originalShader == null) return;

        _instanceMat.shader = _originalShader;
        _isGlitched = false;
    }

    /// <summary>Capture current rotation as the disheveled pose (rotation only, position untouched).</summary>
    [ContextMenu("Capture Disheveled Rotation")]
    private void CaptureDisheveledPose()
    {
        _disheveledRotation = transform.rotation;
        _hasDisheveledPose = true;
        Debug.Log($"[PlaceableObject] {name} disheveled rotation captured: {transform.eulerAngles}");
    }

    /// <summary>Capture current rotation as the home/normal rotation (rotation only, position untouched).</summary>
    [ContextMenu("Capture Home Rotation")]
    private void CaptureHomeRotation()
    {
        _homeRotation = transform.rotation;
        Debug.Log($"[PlaceableObject] {name} home rotation captured: {transform.eulerAngles}");
    }

    /// <summary>Capture current position + rotation as the full home pose.</summary>
    [ContextMenu("Capture Home Position + Rotation")]
    private void CaptureNormalPose()
    {
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
        Debug.Log($"[PlaceableObject] {name} home pose captured: pos={_homePosition}, rot={transform.eulerAngles}");
    }

    /// <summary>Clear the home position and rotation. Object will have no home.</summary>
    [ContextMenu("Clear Home Position")]
    private void ClearHomePose()
    {
        _homePosition = Vector3.zero;
        _homeRotation = Quaternion.identity;
        _useSpawnAsHome = false;
        Debug.Log($"[PlaceableObject] {name} home position cleared.");
    }

    /// <summary>Clear the captured disheveled pose (reverts to procedural fallback).</summary>
    [ContextMenu("Clear Disheveled Rotation")]
    private void ClearDisheveledPose()
    {
        _hasDisheveledPose = false;
        _disheveledRotation = Quaternion.identity;
        Debug.Log($"[PlaceableObject] {name} disheveled rotation cleared.");
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
    private Coroutine _snapBounceCoroutine;

    private void OnEnable() => s_all.Add(this);
    private void OnDisable() => s_all.Remove(this);

    // Cached shader lookups (avoid per-object Shader.Find)
    private static Shader s_silhouetteShader;
    private static bool s_silhouetteShaderCached;
    private static Shader s_glitchShader;
    private static bool s_glitchShaderCached;
    private Shader _originalShader;
    private bool _isGlitched;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
        // No instance material — shared material stays untouched.
        // _instanceMat and _originalColor kept for compatibility but not used for color changes.

        _lastValidPosition = transform.position;
        _lastValidRotation = transform.rotation;
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponents<Collider>();

        // Ensure every placeable has an InteractableHighlight for hover/proximity effects
        // (skip plants — they have their own watering interaction)
        // InteractableHighlight auto-add disabled — cursor system handles feedback
        // Component still works for static registry (cursor detection) if manually added
        // if (GetComponent<InteractableHighlight>() == null && GetComponent<WaterablePlant>() == null)
        //     gameObject.AddComponent<InteractableHighlight>();

        // Auto-capture spawn position/rotation as home whenever unset
        if (_homePosition == Vector3.zero)
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }
    }

    private void Start()
    {
        if (_startDishelved)
            Dishevel();
    }

    /// <summary>
    /// Lazy-init silhouette material and mesh on first pickup (not Awake).
    /// Avoids 2 material allocations + Shader.Find + GO creation per object at scene load.
    /// </summary>
    public void EnsureSilhouette()
    {
        if (_silhouetteMat != null) return;

        if (!s_silhouetteShaderCached)
        {
            s_silhouetteShaderCached = true;
            s_silhouetteShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s_silhouetteShader == null) s_silhouetteShader = Shader.Find("Sprites/Default");
        }

        if (s_silhouetteShader != null)
        {
            _silhouetteMat = new Material(s_silhouetteShader);
            _silhouetteMat.SetFloat("_Surface", 1f);
            _silhouetteMat.SetFloat("_Blend", 0f);
            _silhouetteMat.SetInt("_ZTest", (int)CompareFunction.Greater);
            _silhouetteMat.renderQueue = 3100;
        }

        BuildSilhouette();
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
        RemoveGlitch();

        // Lazy-init silhouette on first pickup (deferred from scene load)
        EnsureSilhouette();

        // Also silhouette all paired/stacked children
        foreach (Transform child in transform)
        {
            var childPlaceable = child.GetComponent<PlaceableObject>();
            if (childPlaceable != null)
                childPlaceable.EnsureSilhouette();
        }

        // If we were stored in a drawer, notify the drawer
        var drawer = GetComponentInParent<DrawerController>();
        if (drawer != null)
            drawer.RemoveStoredItem(this);

        if (_rb != null)
            _rb.isKinematic = false;

        // Auto-straighten to home rotation on pickup (if home was captured)
        if (_homeRotation != Quaternion.identity)
        {
            transform.rotation = _homeRotation;
            if (_rb != null) _rb.angularVelocity = Vector3.zero;
            Debug.Log($"[PlaceableObject] {name} straightened to home rotation on pickup.");
        }
        else
        {
            // No home rotation — just zero X/Z, keep Y
            Vector3 euler = transform.eulerAngles;
            float xTilt = Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f));
            float zTilt = Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f));
            if (xTilt > 1f || zTilt > 1f)
            {
                transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
                if (_rb != null) _rb.angularVelocity = Vector3.zero;
                Debug.Log($"[PlaceableObject] {name} auto-straightened on pickup.");
            }
        }

        // Disable colliders so held object doesn't block raycasts or knock items
        SetCollidersEnabled(false);

        // Held-item feedback handled by grab cursor — no material color change

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
        DestroySilhouette();

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

        // Satisfying scale bounce on placement
        if (_snapBounceCoroutine != null) StopCoroutine(_snapBounceCoroutine);
        _snapBounceCoroutine = StartCoroutine(PlacementSnapBounce());
    }

    /// <summary>
    /// Called by ObjectGrabber if placement is cancelled (e.g. area exit without a surface).
    /// </summary>
    public void OnDropped()
    {
        CurrentState = State.Resting;
        SetCollidersEnabled(true);
        RestoreMaterial();
        DestroySilhouette();

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
        if (_silhouetteGO != null) return; // Already built

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

    /// <summary>Remove the silhouette overlay on this item and all children. Called on place, drop, and pair.</summary>
    public void ForceDestroySilhouette()
    {
        DestroySilhouette();

        // Also destroy silhouettes on paired/stacked children
        foreach (Transform child in transform)
        {
            var childPlaceable = child.GetComponent<PlaceableObject>();
            if (childPlaceable != null)
                childPlaceable.ForceDestroySilhouette();
        }
    }

    private void DestroySilhouette()
    {
        if (_silhouetteGO != null)
        {
            Destroy(_silhouetteGO);
            _silhouetteGO = null;
        }
        if (_silhouetteMat != null)
        {
            Destroy(_silhouetteMat);
            _silhouetteMat = null;
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

    // ── Placement snap bounce ─────────────────────────────────────────

    private IEnumerator PlacementSnapBounce()
    {
        if (AccessibilitySettings.ReduceMotion)
        {
            _snapBounceCoroutine = null;
            yield break;
        }

        Vector3 baseScale = transform.localScale;
        Vector3 basePos = transform.position;

        // Squish down (0.04s)
        yield return ScaleLerp(baseScale, baseScale * 0.92f, basePos, basePos + Vector3.down * 0.01f, 0.04f);
        // Bounce up (0.06s)
        yield return ScaleLerp(baseScale * 0.92f, baseScale * 1.04f, basePos + Vector3.down * 0.01f, basePos, 0.06f);
        // Settle (0.04s)
        yield return ScaleLerp(baseScale * 1.04f, baseScale, basePos, basePos, 0.04f);

        transform.localScale = baseScale;
        transform.position = basePos;
        _snapBounceCoroutine = null;
    }

    private IEnumerator ScaleLerp(Vector3 fromScale, Vector3 toScale, Vector3 fromPos, Vector3 toPos, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(fromScale, toScale, t);
            transform.position = Vector3.Lerp(fromPos, toPos, t);
            yield return null;
        }
    }

    // ── Material restore ──────────────────────────────────────────────

    private void RestoreMaterial()
    {
        // No-op: brightness boost removed, material stays at _originalColor always
    }

    /// <summary>Public version for external systems (PairableItem) to force color restore.</summary>
    public void ForceRestoreMaterial()
    {
        RestoreMaterial();
    }

    /// <summary>Set instance material color directly (for PairableItem pulse). Returns false if no instance material.</summary>
    public bool SetInstanceColor(Color color)
    {
        if (_instanceMat == null) return false;
        _instanceMat.color = color;
        return true;
    }

    /// <summary>The original base color captured in Awake.</summary>
    public Color OriginalColor => _originalColor;

    /// <summary>
    /// Override the instance material's shader and properties from a source material.
    /// Call AFTER Awake (which creates _instanceMat) and BEFORE InteractableHighlight
    /// caches base materials. Preserves the material reference so pickup/place still works.
    /// </summary>
    public void ApplyMaterialOverride(Material source, Color color)
    {
        if (source == null) return;

        if (_instanceMat == null)
        {
            // Awake hasn't run yet — set on the renderer directly
            _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;
            _instanceMat = new Material(source);
            _instanceMat.color = color;
            _renderer.material = _instanceMat;
        }
        else
        {
            _instanceMat.shader = source.shader;
            _instanceMat.CopyPropertiesFromMaterial(source);
            _instanceMat.color = color;
        }
        _originalColor = color;
    }
}
