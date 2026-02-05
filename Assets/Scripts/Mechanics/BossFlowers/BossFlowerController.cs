using TMPro;
using UnityEngine;

/// <summary>
/// Boss flower behaviours: snap trap (timed jaw that forces game over),
/// regrowth (stem grows back after cuts), and scroll stem (alt mode where
/// the player scrolls to find the ideal zone on a very tall stem).
/// </summary>
[DisallowMultipleComponent]
public class BossFlowerController : MonoBehaviour
{
    [Header("Snap Trap")]
    [Tooltip("Enable the snap trap behaviour.")]
    public bool enableSnapTrap = true;

    [Tooltip("Seconds before the trap snaps shut.")]
    public float snapChargeTime = 3f;

    [Tooltip("Seconds before the jaw reopens after a snap or cut.")]
    public float snapResetTime = 2f;

    [Tooltip("The visual jaw object that rotates closed.")]
    public Transform snapJaw;

    [Tooltip("Rotation angle when jaw is fully closed (degrees).")]
    public float jawClosedAngle = 60f;

    public AudioClip snapSFX;

    [Header("Regrowth")]
    [Tooltip("Enable stem regrowth after cuts.")]
    public bool enableRegrowth = true;

    [Tooltip("Units per second the stem regrows.")]
    public float regrowthRate = 0.1f;

    [Tooltip("Seconds after a cut before regrowth starts.")]
    public float regrowthDelay = 1f;

    [Tooltip("Maximum length the stem can regrow to (clamped to original).")]
    public float maxRegrowthLength;

    [Header("Scroll Stem (Alt Mode)")]
    [Tooltip("Enable scroll stem mode (mutually exclusive with snap/regrowth for simplicity).")]
    public bool enableScrollStem;

    [Tooltip("Visible viewport height for the stem.")]
    public float stemDisplayHeight = 2f;

    [Tooltip("Normalized zone range where the ideal cut is located (min..max).")]
    public Vector2 idealCutZone = new Vector2(0.3f, 0.5f);

    [Tooltip("Scroll speed (units per second).")]
    public float scrollSpeed = 0.5f;

    [Header("Core")]
    public FlowerStemRuntime stem;
    public FlowerSessionController session;
    public FlowerGameBrain brain;
    public CuttingPlaneController planeController;

    [Tooltip("Display name of the boss.")]
    public string bossName = "Venus Maw";

    public TMP_Text bossNameLabel;

    // Snap trap state
    private float _snapTimer;
    private bool _snapCoolingDown;
    private float _snapCooldownTimer;
    private Quaternion _jawOpenRotation;

    // Regrowth state
    private float _originalStemLength;
    private Vector3 _originalTipLocalPos;
    private float _regrowthWaitTimer;
    private bool _waitingToRegrow;
    private float _lastStemLength;
    private bool _sessionEnded;

    // Scroll state
    private float _scrollOffset;

    void Start()
    {
        if (snapJaw != null)
            _jawOpenRotation = snapJaw.localRotation;

        if (stem != null)
        {
            _originalStemLength = stem.CurrentLength;
            maxRegrowthLength = maxRegrowthLength <= 0f ? _originalStemLength : maxRegrowthLength;
            if (stem.StemTip != null)
                _originalTipLocalPos = stem.StemTip.localPosition;
            _lastStemLength = _originalStemLength;
        }

        if (bossNameLabel != null)
            bossNameLabel.text = bossName;

        if (session != null)
            session.OnResult.AddListener(OnSessionResult);
    }

    void OnDestroy()
    {
        if (session != null)
            session.OnResult.RemoveListener(OnSessionResult);
    }

    private void OnSessionResult(FlowerGameBrain.EvaluationResult result, int score, int days)
    {
        _sessionEnded = true;
    }

    void Update()
    {
        if (_sessionEnded) return;

        if (enableSnapTrap && !enableScrollStem)
            UpdateSnapTrap();

        if (enableRegrowth && !enableScrollStem)
            UpdateRegrowth();

        if (enableScrollStem)
            UpdateScrollStem();
    }

    // ═══════════════════════════════════════════════════════════
    // Snap Trap
    // ═══════════════════════════════════════════════════════════

    private void UpdateSnapTrap()
    {
        if (_snapCoolingDown)
        {
            _snapCooldownTimer += Time.deltaTime;
            if (_snapCooldownTimer >= snapResetTime)
            {
                _snapCoolingDown = false;
                _snapTimer = 0f;
            }
            // Lerp jaw open during cooldown
            if (snapJaw != null)
            {
                float openT = _snapCooldownTimer / snapResetTime;
                snapJaw.localRotation = Quaternion.Lerp(
                    _jawOpenRotation * Quaternion.Euler(jawClosedAngle, 0f, 0f),
                    _jawOpenRotation,
                    openT);
            }
            return;
        }

        _snapTimer += Time.deltaTime;

        // Animate jaw closing
        if (snapJaw != null)
        {
            float closeT = Mathf.Clamp01(_snapTimer / snapChargeTime);
            snapJaw.localRotation = Quaternion.Lerp(
                _jawOpenRotation,
                _jawOpenRotation * Quaternion.Euler(jawClosedAngle, 0f, 0f),
                closeT);
        }

        // Detect if player cut (stem length changed)
        if (stem != null)
        {
            float currentLen = stem.CurrentLength;
            if (currentLen < _lastStemLength - 0.01f)
            {
                // Player cut — reset trap
                ResetSnapTrap();
                _lastStemLength = currentLen;
                return;
            }
            _lastStemLength = currentLen;
        }

        // Snap!
        if (_snapTimer >= snapChargeTime)
        {
            // Check if scissors are near stem
            bool scissorsNear = IsToolNearStem();
            if (scissorsNear)
            {
                if (AudioManager.Instance != null && snapSFX != null)
                    AudioManager.Instance.PlaySFX(snapSFX);

                if (session != null)
                    session.ForceGameOver("Snapped!");

                Debug.Log("[BossFlowerController] SNAP! Game over.");
            }
            ResetSnapTrap();
        }
    }

    private void ResetSnapTrap()
    {
        _snapCoolingDown = true;
        _snapCooldownTimer = 0f;
        _snapTimer = 0f;
    }

    private bool IsToolNearStem()
    {
        if (planeController == null || stem == null) return false;

        Transform planeTf = planeController.planePoseRootOverride != null
            ? planeController.planePoseRootOverride
            : planeController.transform;

        Vector3 closest = stem.GetClosestPointOnStem(planeTf.position);
        float dist = Vector3.Distance(planeTf.position, closest);
        return dist < 0.5f;
    }

    // ═══════════════════════════════════════════════════════════
    // Regrowth
    // ═══════════════════════════════════════════════════════════

    private void UpdateRegrowth()
    {
        if (stem == null || stem.StemTip == null) return;

        float currentLen = stem.CurrentLength;

        // Detect new cut
        if (currentLen < _lastStemLength - 0.01f && !_waitingToRegrow)
        {
            _waitingToRegrow = true;
            _regrowthWaitTimer = 0f;
        }
        _lastStemLength = currentLen;

        if (!_waitingToRegrow) return;

        // Wait for regrowth delay
        _regrowthWaitTimer += Time.deltaTime;
        if (_regrowthWaitTimer < regrowthDelay) return;

        // Regrow toward original
        if (currentLen < maxRegrowthLength)
        {
            Vector3 direction = (stem.StemTip.position - stem.StemAnchor.position).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = -Vector3.up;

            stem.StemTip.position += direction * regrowthRate * Time.deltaTime;

            // Check if regrowth is complete
            if (stem.CurrentLength >= maxRegrowthLength)
            {
                _waitingToRegrow = false;
            }
        }
        else
        {
            _waitingToRegrow = false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Scroll Stem
    // ═══════════════════════════════════════════════════════════

    private void UpdateScrollStem()
    {
        float scroll = Input.mouseScrollDelta.y;
        _scrollOffset += scroll * scrollSpeed * Time.deltaTime;
        _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, Mathf.Max(0f, _originalStemLength - stemDisplayHeight));

        // Move the flower root to simulate scrolling
        if (stem != null)
        {
            stem.transform.localPosition = new Vector3(
                stem.transform.localPosition.x,
                -_scrollOffset,
                stem.transform.localPosition.z);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Public accessors for HUD
    // ═══════════════════════════════════════════════════════════

    /// <summary>Time remaining before the snap trap fires.</summary>
    public float SnapTimeRemaining => _snapCoolingDown ? snapChargeTime : Mathf.Max(0f, snapChargeTime - _snapTimer);

    /// <summary>True if regrowth is currently active.</summary>
    public bool IsRegrowing => _waitingToRegrow && _regrowthWaitTimer >= regrowthDelay;
}
