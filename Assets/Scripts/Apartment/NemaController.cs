using UnityEngine;

/// <summary>
/// Scene-scoped singleton that positions Nema's visible model in the apartment.
/// Teleports between predefined spots based on current area and date phase.
/// Nema idles in cool poses, looks at what the player interacts with,
/// and glances at random things in the room when bored.
///
/// Wire the Transform fields in the Inspector to empty GameObjects marking
/// each position.
///
/// Animation setup:
///   - Animator with "IdleIndex" (int) parameter for per-area pose sets
///   - "LookWeight" (float 0-1) for head IK blend
///   - "Bored" (trigger) for bored glance animations
///   - OnAnimatorIK callback drives head look-at toward _lookTarget
/// </summary>
public class NemaController : MonoBehaviour
{
    public static NemaController Instance { get; private set; }

    [Header("Model")]
    [Tooltip("Root transform of Nema's visual model (moved by this controller).")]
    [SerializeField] private Transform _model;

    [Tooltip("Animator on Nema's model. Optional — pose/look-at features require it.")]
    [SerializeField] private Animator _animator;

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

    [Header("Look-At")]
    [Tooltip("How fast the look-at weight blends in/out (per second).")]
    [SerializeField] private float _lookBlendSpeed = 3f;

    [Tooltip("Maximum head turn angle (degrees) before Nema gives up looking.")]
    [SerializeField] private float _maxLookAngle = 90f;

    [Tooltip("Head bone for manual look-at rotation (used when Animator IK is not available).")]
    [SerializeField] private Transform _headBone;

    [Header("Bored Behavior")]
    [Tooltip("Seconds of no player interaction before Nema gets bored and glances around.")]
    [SerializeField] private float _boredDelay = 6f;

    [Tooltip("How long Nema looks at a random object before picking another or returning to idle.")]
    [SerializeField] private float _boredGlanceDuration = 2.5f;

    [Tooltip("Chance (0-1) of glancing at a random object vs just shifting pose.")]
    [SerializeField, Range(0f, 1f)] private float _glanceChance = 0.7f;

    [Header("Body Facing")]
    [Tooltip("If true, Nema's body subtly rotates toward the look target (not just head).")]
    [SerializeField] private bool _bodyFacesTarget = true;

    [Tooltip("Max degrees the body turns toward the look target.")]
    [SerializeField] private float _maxBodyTurn = 30f;

    [Tooltip("How fast the body rotates toward the look target (degrees/sec).")]
    [SerializeField] private float _bodyTurnSpeed = 60f;

    // ── Runtime state ──────────────────────────────────────────
    private Camera _cachedCamera;
    private Transform _currentTarget;

    // Look-at
    private Vector3 _lookTarget;
    private float _lookWeight;
    private float _targetLookWeight;
    private bool _hasLookTarget;

    // Bored timer
    private float _interactionTimer; // time since last player interaction
    private float _boredGlanceTimer;
    private bool _isBored;
    private Transform _boredTarget;

    // Body facing
    private Quaternion _baseRotation; // rotation from position marker
    private float _currentBodyTurn;

    // Animator hashes
    private static readonly int H_IdleIndex = Animator.StringToHash("IdleIndex");
    private static readonly int H_LookWeight = Animator.StringToHash("LookWeight");
    private static readonly int H_Bored = Animator.StringToHash("Bored");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_animator == null && _model != null)
            _animator = _model.GetComponentInChildren<Animator>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (ApartmentManager.Instance != null)
            ApartmentManager.Instance.OnAreaChanged += OnAreaChanged;

        if (DayPhaseManager.Instance != null)
            DayPhaseManager.Instance.OnPhaseChanged.AddListener(OnPhaseChanged);

        // Start at current area position
        if (ApartmentManager.Instance != null)
            OnAreaChanged(ApartmentManager.Instance.CurrentAreaIndex);
    }

    private void Update()
    {
        if (_model == null) return;

        UpdateLookTarget();
        UpdateBoredTimer();
        UpdateBodyFacing();

        // Sync animator parameters
        if (_animator != null)
        {
            _animator.SetFloat(H_LookWeight, _lookWeight);
        }
    }

    private void LateUpdate()
    {
        if (_model == null) return;

        // Manual head look-at (when no Animator IK)
        if (_headBone != null && _lookWeight > 0.01f && _hasLookTarget)
        {
            Vector3 toTarget = _lookTarget - _headBone.position;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toTarget);
                _headBone.rotation = Quaternion.Slerp(_headBone.rotation, lookRot, _lookWeight * 0.7f);
            }
        }
    }

    // ── Animator IK callback (if Animator has IK pass enabled) ──

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;

        if (_hasLookTarget && _lookWeight > 0.01f)
        {
            _animator.SetLookAtPosition(_lookTarget);
            _animator.SetLookAtWeight(_lookWeight, 0.3f, 0.6f, 0.8f, 0.5f);
            // body weight, head weight, eyes weight, clamp weight
        }
        else
        {
            _animator.SetLookAtWeight(0f);
        }
    }

    // ── Look-at system ─────────────────────────────────────────

    private void UpdateLookTarget()
    {
        // Priority 1: Player is holding something — look at it
        if (ObjectGrabber.IsHoldingObject && ObjectGrabber.HeldObject != null)
        {
            SetLookTarget(ObjectGrabber.HeldObject.transform.position);
            _interactionTimer = 0f; // reset bored timer
            _isBored = false;
            return;
        }

        // Priority 2: Player is hovering something (check ApartmentManager highlight)
        if (ApartmentManager.Instance != null)
        {
            var hovered = ApartmentManager.Instance.HoveredHighlight;
            if (hovered != null)
            {
                SetLookTarget(hovered.transform.position);
                _interactionTimer = 0f;
                _isBored = false;
                return;
            }
        }

        // Priority 3: Bored — look at random thing
        if (_isBored && _boredTarget != null)
        {
            SetLookTarget(_boredTarget.position);
            return;
        }

        // Nothing interesting — clear look target
        ClearLookTarget();
    }

    private void SetLookTarget(Vector3 worldPos)
    {
        if (_model == null) return;

        // Check angle — don't look behind Nema
        Vector3 toTarget = worldPos - _model.position;
        toTarget.y = 0f;
        float angle = Vector3.Angle(_model.forward, toTarget);
        if (angle > _maxLookAngle)
        {
            ClearLookTarget();
            return;
        }

        _lookTarget = worldPos;
        _hasLookTarget = true;
        _targetLookWeight = 1f;
        _lookWeight = Mathf.MoveTowards(_lookWeight, _targetLookWeight, _lookBlendSpeed * Time.deltaTime);
    }

    private void ClearLookTarget()
    {
        _targetLookWeight = 0f;
        _lookWeight = Mathf.MoveTowards(_lookWeight, 0f, _lookBlendSpeed * Time.deltaTime);
        if (_lookWeight < 0.01f)
            _hasLookTarget = false;
    }

    // ── Bored behavior ─────────────────────────────────────────

    private void UpdateBoredTimer()
    {
        if (_isBored)
        {
            _boredGlanceTimer -= Time.deltaTime;
            if (_boredGlanceTimer <= 0f)
            {
                // Done glancing — either pick a new target or stop being bored
                if (Random.value < _glanceChance)
                    StartBoredGlance();
                else
                    _isBored = false;
            }
            return;
        }

        _interactionTimer += Time.deltaTime;
        if (_interactionTimer >= _boredDelay)
        {
            StartBoredGlance();
        }
    }

    private void StartBoredGlance()
    {
        // Pick a random active ReactableTag in the scene
        var candidates = ReactableTag.All;
        if (candidates.Count == 0)
        {
            _isBored = false;
            return;
        }

        // Try a few times to find one that's in front of Nema
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var pick = candidates[Random.Range(0, candidates.Count)];
            if (pick == null || !pick.IsActive) continue;

            Vector3 toItem = pick.transform.position - _model.position;
            toItem.y = 0f;
            if (Vector3.Angle(_model.forward, toItem) <= _maxLookAngle)
            {
                _boredTarget = pick.transform;
                _isBored = true;
                _boredGlanceTimer = _boredGlanceDuration + Random.Range(-0.5f, 0.5f);

                // Fire animator trigger for a pose shift
                if (_animator != null)
                    _animator.SetTrigger(H_Bored);

                return;
            }
        }

        // Couldn't find a valid target — just shift pose without looking
        _isBored = false;
        if (_animator != null)
            _animator.SetTrigger(H_Bored);
    }

    // ── Body facing ────────────────────────────────────────────

    private void UpdateBodyFacing()
    {
        if (!_bodyFacesTarget || _model == null) return;

        float targetTurn = 0f;

        if (_hasLookTarget && _lookWeight > 0.1f)
        {
            Vector3 toTarget = _lookTarget - _model.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                float signedAngle = Vector3.SignedAngle(_baseRotation * Vector3.forward, toTarget, Vector3.up);
                targetTurn = Mathf.Clamp(signedAngle, -_maxBodyTurn, _maxBodyTurn) * _lookWeight;
            }
        }

        _currentBodyTurn = Mathf.MoveTowards(_currentBodyTurn, targetTurn, _bodyTurnSpeed * Time.deltaTime);
        _model.rotation = _baseRotation * Quaternion.Euler(0f, _currentBodyTurn, 0f);
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>Teleport Nema to a specific Transform position.</summary>
    public void WarpTo(Transform target)
    {
        if (_model == null || target == null) return;
        _currentTarget = target;
        _model.position = target.position;
        _baseRotation = target.rotation;
        _model.rotation = target.rotation;
        _currentBodyTurn = 0f;

        // Reset look/bored state on teleport
        ClearLookTarget();
        _isBored = false;
        _interactionTimer = 0f;
        _boredTarget = null;
    }

    /// <summary>Teleport Nema to a world position.</summary>
    public void WarpTo(Vector3 position)
    {
        if (_model == null) return;
        _model.position = position;
        _baseRotation = _model.rotation;
        _currentBodyTurn = 0f;
    }

    /// <summary>Show or hide Nema's model.</summary>
    public void SetVisible(bool visible)
    {
        if (_model != null)
            _model.gameObject.SetActive(visible);
    }

    /// <summary>Notify Nema that the player just did something interesting (resets bored timer).</summary>
    public void NotifyInteraction()
    {
        _interactionTimer = 0f;
        _isBored = false;
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

            // Set idle pose for this area
            if (_animator != null)
                _animator.SetInteger(H_IdleIndex, areaIndex);
        }
    }

    private void OnPhaseChanged(int phaseInt)
    {
        var phase = (DayPhaseManager.DayPhase)phaseInt;
        switch (phase)
        {
            case DayPhaseManager.DayPhase.Morning:
                SetVisible(true);
                if (_newspaperPosition != null)
                    WarpTo(_newspaperPosition);
                break;

            case DayPhaseManager.DayPhase.Exploration:
            case DayPhaseManager.DayPhase.Evening:
                SetVisible(true);
                if (ApartmentManager.Instance != null)
                    OnAreaChanged(ApartmentManager.Instance.CurrentAreaIndex);
                break;

            case DayPhaseManager.DayPhase.FlowerTrimming:
                SetVisible(false);
                break;

            case DayPhaseManager.DayPhase.DateInProgress:
                SetVisible(true);
                break;
        }
    }

    /// <summary>Called by DateSessionManager during date phase transitions.</summary>
    public void MoveToDatePhase(DateSessionManager.DatePhase datePhase)
    {
        SetVisible(true);
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
