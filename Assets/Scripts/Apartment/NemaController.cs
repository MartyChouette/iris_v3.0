using UnityEngine;

/// <summary>
/// Scene-scoped singleton that positions Nema's visible model in the apartment.
/// Teleports between predefined spots based on current area and date phase.
///
/// Wire the Transform fields in the Inspector to empty GameObjects marking
/// each position. Nema faces the camera at each spot.
///
/// Positions:
///   - Per area (browsing): where Nema stands when the player browses each area
///   - Date phases: entrance, kitchen, couch (mirrors date NPC positions)
///   - Newspaper: where Nema stands while reading the newspaper
/// </summary>
public class NemaController : MonoBehaviour
{
    public static NemaController Instance { get; private set; }

    [Header("Model")]
    [Tooltip("Root transform of Nema's visual model (moved by this controller).")]
    [SerializeField] private Transform _model;

    [Header("Browsing Positions (per area index)")]
    [Tooltip("Where Nema stands in each apartment area. Index-matched to ApartmentManager.areas[].")]
    [SerializeField] private Transform[] _areaPositions;

    [Header("Date Positions")]
    [Tooltip("Where Nema stands during entrance judgments (Phase 1).")]
    [SerializeField] private Transform _entrancePosition;

    [Tooltip("Where Nema stands during kitchen/drink phase (Phase 2).")]
    [SerializeField] private Transform _kitchenPosition;

    [Tooltip("Where Nema sits during couch/reveal phase (Phase 3).")]
    [SerializeField] private Transform _couchPosition;

    [Header("Newspaper Position")]
    [Tooltip("Where Nema stands while reading the newspaper (morning phase).")]
    [SerializeField] private Transform _newspaperPosition;

    [Header("Settings")]
    [Tooltip("If true, Nema always faces the main camera.")]
    [SerializeField] private bool _faceCamera = true;

    [Tooltip("Smooth rotation speed (degrees/sec). 0 = instant snap.")]
    [SerializeField] private float _rotationSpeed = 360f;

    private Camera _cachedCamera;
    private Transform _currentTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Subscribe to area changes
        if (ApartmentManager.Instance != null)
            ApartmentManager.Instance.OnAreaChanged += OnAreaChanged;

        // Subscribe to phase changes
        if (DayPhaseManager.Instance != null)
            DayPhaseManager.Instance.OnPhaseChanged.AddListener(OnPhaseChanged);

        // Start at current area position
        if (ApartmentManager.Instance != null)
            OnAreaChanged(ApartmentManager.Instance.CurrentAreaIndex);
    }

    private void LateUpdate()
    {
        if (_model == null) return;

        // Face camera
        if (_faceCamera)
        {
            if (_cachedCamera == null)
                _cachedCamera = Camera.main;
            if (_cachedCamera != null)
            {
                Vector3 toCamera = _cachedCamera.transform.position - _model.position;
                toCamera.y = 0f; // only rotate on Y axis
                if (toCamera.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toCamera);
                    if (_rotationSpeed <= 0f)
                        _model.rotation = targetRot;
                    else
                        _model.rotation = Quaternion.RotateTowards(
                            _model.rotation, targetRot, _rotationSpeed * Time.deltaTime);
                }
            }
        }
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>Teleport Nema to a specific Transform position.</summary>
    public void WarpTo(Transform target)
    {
        if (_model == null || target == null) return;
        _currentTarget = target;
        _model.position = target.position;
        // Apply target's rotation as initial facing (LateUpdate overrides if faceCamera is on)
        _model.rotation = target.rotation;
    }

    /// <summary>Teleport Nema to a world position.</summary>
    public void WarpTo(Vector3 position)
    {
        if (_model == null) return;
        _model.position = position;
    }

    /// <summary>Show or hide Nema's model.</summary>
    public void SetVisible(bool visible)
    {
        if (_model != null)
            _model.gameObject.SetActive(visible);
    }

    // ── Event handlers ──────────────────────────────────────────

    private void OnAreaChanged(int areaIndex)
    {
        // Only move for area changes during browsing phases (not during date)
        if (DayPhaseManager.Instance != null)
        {
            var phase = DayPhaseManager.Instance.CurrentPhase;
            if (phase == DayPhaseManager.DayPhase.DateInProgress
                || phase == DayPhaseManager.DayPhase.FlowerTrimming)
                return;
        }

        if (_areaPositions != null && areaIndex >= 0 && areaIndex < _areaPositions.Length
            && _areaPositions[areaIndex] != null)
        {
            WarpTo(_areaPositions[areaIndex]);
        }
    }

    private void OnPhaseChanged(int phaseInt)
    {
        var phase = (DayPhaseManager.DayPhase)phaseInt;
        switch (phase)
        {
            case DayPhaseManager.DayPhase.Morning:
                if (_newspaperPosition != null)
                    WarpTo(_newspaperPosition);
                break;

            case DayPhaseManager.DayPhase.Exploration:
            case DayPhaseManager.DayPhase.Evening:
                // Return to current area position
                if (ApartmentManager.Instance != null)
                    OnAreaChanged(ApartmentManager.Instance.CurrentAreaIndex);
                break;

            case DayPhaseManager.DayPhase.FlowerTrimming:
                // Hide during flower trimming (separate scene)
                SetVisible(false);
                break;
        }
    }

    /// <summary>Called by DateSessionManager during date phase transitions.</summary>
    public void MoveToDatePhase(DateSessionManager.DatePhase datePhase)
    {
        switch (datePhase)
        {
            case DateSessionManager.DatePhase.Arrival:
                if (_entrancePosition != null) WarpTo(_entrancePosition);
                break;
            case DateSessionManager.DatePhase.BackgroundJudging:
                if (_kitchenPosition != null) WarpTo(_kitchenPosition);
                break;
            case DateSessionManager.DatePhase.Reveal:
                if (_couchPosition != null) WarpTo(_couchPosition);
                break;
        }
    }
}
