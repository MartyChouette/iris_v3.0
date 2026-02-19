using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-scoped singleton for ambient cleaning. Sponge only — click+drag
/// stains to wipe them clean. Raycasts onto <see cref="CleanableSurface"/>.
/// </summary>
[DisallowMultipleComponent]
public class CleaningManager : MonoBehaviour
{
    public static CleaningManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Main camera for raycasts.")]
    [SerializeField] private Camera _mainCamera;

    [Tooltip("All cleanable surfaces in the scene.")]
    [SerializeField] private CleanableSurface[] _surfaces;

    [Tooltip("HUD display.")]
    [SerializeField] private CleaningHUD _hud;

    [Header("Visuals")]
    [Tooltip("Sponge visual that follows the mouse on surfaces.")]
    [SerializeField] private Transform _spongeVisual;

    [Header("Settings")]
    [Tooltip("UV-space radius for wipe brush.")]
    [SerializeField] private float _wipeRadius = 0.06f;

    [Tooltip("Layer mask for raycasting onto cleanable surfaces.")]
    [SerializeField] private LayerMask _cleanableLayer;

    [Tooltip("Offset above surface for tool visual placement.")]
    [SerializeField] private float _surfaceOffset = 0.005f;

    [Header("Audio")]
    public AudioClip wipeSFX;
    public AudioClip allCleanSFX;

    // Input
    private InputAction _mousePosition;
    private InputAction _mouseClick;

    /// <summary>Fired once on the rising edge of a wipe (mouse press on a stain).</summary>
    public static event System.Action OnWipeStarted;

    // State
    private CleanableSurface _hoveredSurface;
    private float _sfxCooldown;
    private bool _allCleanFired;
    private bool _wasPressingLastFrame;

    private const float SFX_COOLDOWN = 0.15f;

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>The surface currently under the cursor, or null.</summary>
    public CleanableSurface HoveredSurface => _hoveredSurface;

    /// <summary>All surfaces in the scene.</summary>
    public CleanableSurface[] Surfaces => _surfaces;

    /// <summary>Replace the surfaces array (used by ApartmentStainSpawner).</summary>
    public void SetSurfaces(CleanableSurface[] surfaces) => _surfaces = surfaces;

    /// <summary>Average clean percent for surfaces in a specific area.</summary>
    public float GetAreaCleanPercent(ApartmentArea area)
    {
        if (_surfaces == null || _surfaces.Length == 0) return 1f;
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < _surfaces.Length; i++)
        {
            if (_surfaces[i].Area == area)
            {
                sum += _surfaces[i].CleanPercent;
                count++;
            }
        }
        return count == 0 ? 1f : sum / count;
    }

    /// <summary>Average clean percent across all surfaces.</summary>
    public float OverallCleanPercent
    {
        get
        {
            if (_surfaces == null || _surfaces.Length == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < _surfaces.Length; i++)
                sum += _surfaces[i].CleanPercent;
            return sum / _surfaces.Length;
        }
    }

    /// <summary>True when all surfaces are >= 95% clean.</summary>
    public bool AllClean
    {
        get
        {
            if (_surfaces == null) return true;
            for (int i = 0; i < _surfaces.Length; i++)
                if (!_surfaces[i].IsFullyClean) return false;
            return true;
        }
    }

    // ── Singleton lifecycle ─────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        _mousePosition = new InputAction("CleanPointer", InputActionType.Value, "<Mouse>/position");
        _mouseClick = new InputAction("CleanClick", InputActionType.Button, "<Mouse>/leftButton");

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        _mousePosition.Enable();
        _mouseClick.Enable();
    }

    void OnDisable()
    {
        _mousePosition.Disable();
        _mouseClick.Disable();
    }

    // ── Update ──────────────────────────────────────────────────────

    void Update()
    {
        if (_mainCamera == null) return;

        // Block cleaning outside interaction phases (name entry, newspaper, date end)
        if (DayPhaseManager.Instance != null && !DayPhaseManager.Instance.IsInteractionPhase)
        {
            SetSpongeVisual(Vector3.zero, false);
            _hoveredSurface = null;
            return;
        }

        // Only allow cleaning interaction while Browsing the apartment
        if (ApartmentManager.Instance != null
            && ApartmentManager.Instance.CurrentState != ApartmentManager.State.Browsing)
        {
            SetSpongeVisual(Vector3.zero, false);
            _hoveredSurface = null;
            return;
        }

        if (ObjectGrabber.IsHoldingObject)
        {
            SetSpongeVisual(Vector3.zero, false);
            _hoveredSurface = null;
            return;
        }

        _sfxCooldown -= Time.deltaTime;

        Vector2 pointer = _mousePosition.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(pointer);

        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, 100f, _cleanableLayer);

        if (hit)
        {
            _hoveredSurface = hitInfo.collider.GetComponentInParent<CleanableSurface>();

            Vector3 toolPos = hitInfo.point + hitInfo.normal * _surfaceOffset;
            SetSpongeVisual(toolPos, true);

            if (_mouseClick.IsPressed() && _hoveredSurface != null)
            {
                if (!_wasPressingLastFrame)
                    OnWipeStarted?.Invoke();

                Vector2 uv = hitInfo.textureCoord;
                _hoveredSurface.Wipe(uv, _wipeRadius);
                PlaySFX(wipeSFX);
            }
        }
        else
        {
            _hoveredSurface = null;
            SetSpongeVisual(Vector3.zero, false);
        }

        _wasPressingLastFrame = _mouseClick.IsPressed();

        // Check all-clean
        if (!_allCleanFired && AllClean)
        {
            _allCleanFired = true;
            Debug.Log("[CleaningManager] All surfaces clean!");
            if (AudioManager.Instance != null && allCleanSFX != null)
                AudioManager.Instance.PlaySFX(allCleanSFX);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private bool _lastSpongeVisible;

    private void SetSpongeVisual(Vector3 position, bool visible)
    {
        if (_spongeVisual == null) return;

        if (visible != _lastSpongeVisible)
        {
            _spongeVisual.gameObject.SetActive(visible);
            _lastSpongeVisible = visible;
        }
        if (visible) _spongeVisual.position = position;
    }

    private void PlaySFX(AudioClip clip)
    {
        if (_sfxCooldown > 0f) return;
        if (AudioManager.Instance != null && clip != null)
        {
            AudioManager.Instance.PlaySFX(clip);
            _sfxCooldown = SFX_COOLDOWN;
        }
    }
}
