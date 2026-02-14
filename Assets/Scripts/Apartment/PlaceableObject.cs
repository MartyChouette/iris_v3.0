using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PlaceableObject : MonoBehaviour
{
    public enum State { Resting, Held, Placed }

    [Header("Visual Feedback")]
    [Tooltip("Color multiplier applied to the material when held.")]
    [SerializeField] private float heldBrightness = 1.4f;

    [Header("Respawn")]
    [Tooltip("Y position below which the object teleports back to its last valid position.")]
    [SerializeField] private float fallThresholdY = -5f;

    [Tooltip("Seconds after falling below threshold before respawn (prevents flicker).")]
    [SerializeField] private float respawnDelay = 0.5f;

    [Header("Wall Mount")]
    [Tooltip("If true, this object can be placed on vertical (wall) surfaces.")]
    [SerializeField] private bool canWallMount;

    [Tooltip("Random rotation range (degrees) applied when spawned on a wall.")]
    [SerializeField] private float crookedAngleRange = 12f;

    public State CurrentState { get; private set; } = State.Resting;
    public bool CanWallMount => canWallMount;
    public PlacementSurface LastPlacedSurface => _lastPlacedSurface;

    private Renderer _renderer;
    private Material _instanceMat;
    private Color _originalColor;
    private int _originalRenderQueue;
    private int _originalZTest;

    private Vector3 _lastValidPosition;
    private Quaternion _lastValidRotation;
    private PlacementSurface _lastPlacedSurface;
    private Rigidbody _rb;
    private float _fallTimer;

    private Coroutine _validationCoroutine;
    private Coroutine _flashCoroutine;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.sharedMaterial != null)
        {
            _instanceMat = new Material(_renderer.sharedMaterial);
            _renderer.material = _instanceMat;
            _originalColor = _instanceMat.color;
            _originalRenderQueue = _instanceMat.renderQueue;
            _originalZTest = _instanceMat.HasProperty("_ZTest")
                ? _instanceMat.GetInt("_ZTest")
                : (int)CompareFunction.LessEqual;
        }

        _lastValidPosition = transform.position;
        _lastValidRotation = transform.rotation;
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (CurrentState == State.Held) return;

        if (transform.position.y < fallThresholdY)
        {
            _fallTimer += Time.deltaTime;
            if (_fallTimer >= respawnDelay)
            {
                Respawn();
                _fallTimer = 0f;
            }
        }
        else
        {
            _fallTimer = 0f;
        }
    }

    private void Respawn()
    {
        transform.position = _lastValidPosition;
        transform.rotation = _lastValidRotation;
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // Wall objects respawn kinematic
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
        Debug.Log($"[PlaceableObject] {name} respawned at {_lastValidPosition}.");
    }

    private void OnDestroy()
    {
        if (_instanceMat != null)
            Destroy(_instanceMat);
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

        // Detach from wall if needed
        if (_rb != null)
            _rb.isKinematic = false;

        // X-ray outline: draw on top of everything while held
        if (_instanceMat != null)
        {
            _instanceMat.color = _originalColor * heldBrightness;
            _instanceMat.renderQueue = 4000;
            if (_instanceMat.HasProperty("_ZTest"))
                _instanceMat.SetInt("_ZTest", (int)CompareFunction.Always);
        }

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

        transform.position = position;
        transform.rotation = rotation;

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
            }
        }

        // Start placement validation for horizontal surfaces
        if (surface != null && !surface.IsVertical)
        {
            if (_validationCoroutine != null) StopCoroutine(_validationCoroutine);
            _validationCoroutine = StartCoroutine(PlacementValidation());
        }

        Debug.Log($"[PlaceableObject] {name} placed at {position} (grid={gridSnapped}, wall={surface != null && surface.IsVertical}).");
    }

    /// <summary>
    /// Called by ObjectGrabber if placement is cancelled (e.g. area exit).
    /// </summary>
    public void OnDropped()
    {
        CurrentState = State.Resting;
        RestoreMaterial();
        Debug.Log($"[PlaceableObject] {name} dropped.");
    }

    // ── Wall alignment ────────────────────────────────────────────────

    /// <summary>
    /// Orient this object flat against a wall. Called each frame by ObjectGrabber while held over a wall.
    /// </summary>
    public void AlignToWall(Vector3 wallNormal, float rotationAngle)
    {
        transform.rotation = Quaternion.LookRotation(-wallNormal, Vector3.up)
            * Quaternion.AngleAxis(rotationAngle, Vector3.forward);
    }

    /// <summary>
    /// Apply a random crooked rotation. Called once at spawn by the scene builder.
    /// </summary>
    public void ApplyCrookedOffset(Vector3 wallNormal)
    {
        float angle = Random.Range(-crookedAngleRange, crookedAngleRange);
        transform.rotation *= Quaternion.AngleAxis(angle, -wallNormal);
    }

    // ── Safety: Placement validation timer ────────────────────────────

    private IEnumerator PlacementValidation()
    {
        yield return new WaitForSeconds(1.5f);

        if (CurrentState != State.Placed || _lastPlacedSurface == null)
            yield break;

        // Check if still within surface bounds or close to last valid position
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
        if (_instanceMat == null) return;
        _instanceMat.color = _originalColor;
        _instanceMat.renderQueue = _originalRenderQueue;
        if (_instanceMat.HasProperty("_ZTest"))
            _instanceMat.SetInt("_ZTest", _originalZTest);
    }
}
