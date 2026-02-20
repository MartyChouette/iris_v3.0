using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-scoped singleton for ambient cleaning. Sponge only — click+drag
/// stains to wipe them clean. Raycasts onto <see cref="CleanableSurface"/>.
/// Skips already-clean stains, plays sparkle VFX + SFX on completion,
/// and deactivates stain quads after cleaning.
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

    [Tooltip("SFX played when an individual stain is fully cleaned.")]
    [SerializeField] private AudioClip _stainCompleteSFX;

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

        Debug.Log($"[CleaningManager] Awake — camera={(_mainCamera != null ? _mainCamera.name : "NULL")}, " +
                  $"surfaces={(_surfaces != null ? _surfaces.Length : 0)}, " +
                  $"sponge={(_spongeVisual != null ? "OK" : "NULL")}, " +
                  $"layer={_cleanableLayer.value}");
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
        // Retry Camera.main every frame if reference was lost (e.g. scene rebuild)
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
            Debug.Log("[CleaningManager] Re-acquired main camera.");
        }

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

            // Skip already-clean stains — no sponge, no wipe
            if (_hoveredSurface != null && _hoveredSurface.IsFullyClean)
            {
                _hoveredSurface = null;
                SetSpongeVisual(Vector3.zero, false);
            }
            else
            {
                Vector3 toolPos = hitInfo.point + hitInfo.normal * _surfaceOffset;
                SetSpongeVisual(toolPos, true);

                if (_mouseClick.IsPressed() && _hoveredSurface != null)
                {
                    if (!_wasPressingLastFrame)
                        OnWipeStarted?.Invoke();

                    bool wasClean = _hoveredSurface.IsFullyClean;
                    Vector2 uv = hitInfo.textureCoord;
                    _hoveredSurface.Wipe(uv, _wipeRadius);
                    PlaySFX(wipeSFX);

                    // Per-stain completion: sparkle + SFX + deactivate
                    if (!wasClean && _hoveredSurface.IsFullyClean)
                    {
                        OnStainCompleted(_hoveredSurface);
                    }
                }
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

    // ── Stain completion ────────────────────────────────────────────

    private void OnStainCompleted(CleanableSurface surface)
    {
        Vector3 pos = surface.transform.position;

        // SFX
        if (AudioManager.Instance != null && _stainCompleteSFX != null)
            AudioManager.Instance.PlaySFX(_stainCompleteSFX);

        // Sparkle VFX
        SpawnSparkle(pos);

        // Deactivate stain quad after sparkle finishes
        StartCoroutine(DeactivateAfterDelay(surface.gameObject, 0.5f));
    }

    private void SpawnSparkle(Vector3 position)
    {
        var go = new GameObject("StainSparkle");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.4f;
        main.startSpeed = 0.3f;
        main.startSize = 0.02f;
        main.startColor = new Color(1f, 0.95f, 0.8f, 1f);
        main.gravityModifier = -0.3f;
        main.maxParticles = 12;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 1f); // Additive
            mat.color = new Color(1f, 0.95f, 0.8f, 1f);
            renderer.material = mat;
        }
    }

    private IEnumerator DeactivateAfterDelay(GameObject stainGO, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (stainGO != null)
            stainGO.SetActive(false);
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
