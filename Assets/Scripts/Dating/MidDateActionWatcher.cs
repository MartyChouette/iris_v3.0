using UnityEngine;

/// <summary>
/// Scene-scoped singleton. Subscribes to all interaction events and applies
/// small affection penalties + NPC frown reaction when the player interacts
/// with apartment objects during an active date. 8-second global cooldown
/// prevents spam from rapid actions (e.g. continuous cleaning).
/// </summary>
public class MidDateActionWatcher : MonoBehaviour
{
    public static MidDateActionWatcher Instance { get; private set; }

    [Header("Penalty")]
    [Tooltip("ReactionType magnitude passed to ApplyReaction (0.35 ~= -1.4 affection).")]
    [SerializeField] private float _penaltyMagnitude = 0.35f;

    [Tooltip("Global cooldown in seconds between penalties.")]
    [SerializeField] private float _cooldownDuration = 8f;

    [Header("Audio")]
    [Tooltip("Optional SFX when the NPC notices a mid-date action.")]
    [SerializeField] private AudioClip _caughtSFX;

    private float _lastPenaltyTime = -999f;
    private int _penaltyCount;

    public int PenaltyCount => _penaltyCount;
    public float TimeSinceLastPenalty => Time.time - _lastPenaltyTime;
    public float CooldownRemaining => Mathf.Max(0f, _cooldownDuration - TimeSinceLastPenalty);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MidDateActionWatcher] Duplicate instance destroyed.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        CleaningManager.OnWipeStarted += OnInteraction;
        ObjectGrabber.OnObjectPlaced += OnInteraction;
        RecordSlot.OnRecordChanged += OnInteraction;
        PerfumeBottle.OnPerfumeSprayed += OnInteraction;
    }

    private void OnDisable()
    {
        CleaningManager.OnWipeStarted -= OnInteraction;
        ObjectGrabber.OnObjectPlaced -= OnInteraction;
        RecordSlot.OnRecordChanged -= OnInteraction;
        PerfumeBottle.OnPerfumeSprayed -= OnInteraction;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnInteraction()
    {
        var dsm = DateSessionManager.Instance;
        if (dsm == null || dsm.CurrentState != DateSessionManager.SessionState.DateInProgress)
            return;

        // Cooldown check
        if (Time.time - _lastPenaltyTime < _cooldownDuration)
            return;

        _lastPenaltyTime = Time.time;
        _penaltyCount++;

        // Apply penalty
        dsm.ApplyReaction(ReactionType.Dislike, _penaltyMagnitude);

        // Show frown on NPC
        var reactionUI = dsm.DateCharacter != null
            ? dsm.DateCharacter.GetComponent<DateReactionUI>()
            : null;
        reactionUI?.ShowReaction(ReactionType.Dislike);

        // Audio
        if (_caughtSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_caughtSFX);

        // Debug overlay
        DateDebugOverlay.Instance?.LogReaction($"<color=orange>MID-DATE PENALTY #{_penaltyCount}</color>");

        Debug.Log($"[MidDateActionWatcher] Penalty #{_penaltyCount} applied (cooldown {_cooldownDuration}s).");
    }
}
