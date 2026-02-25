using UnityEngine;

/// <summary>
/// Turntable/record player receiver. Accepts a PlaceableObject with RecordItem,
/// manages playback via AudioManager, feeds MoodMachine, and toggles ReactableTag.
/// Place on the turntable GameObject alongside a ReactableTag.
/// Scene-scoped singleton (one turntable per apartment).
/// </summary>
public class RecordSlot : MonoBehaviour
{
    public static RecordSlot Instance { get; private set; }

    /// <summary>Fired when a record starts playing (for MidDateActionWatcher).</summary>
    public static event System.Action OnRecordChanged;

    [Header("Visuals")]
    [Tooltip("Transform of the disc visual (rotated during playback).")]
    [SerializeField] private Transform _discVisual;

    [Tooltip("Renderer on the disc visual for changing label color.")]
    [SerializeField] private Renderer _discRenderer;

    [Tooltip("Rotation speed in degrees/second while playing.")]
    [SerializeField] private float _rotationSpeed = 33.3f;

    [Header("Record Placement")]
    [Tooltip("Where the record snaps to when placed on the turntable.")]
    [SerializeField] private Transform _recordSnapPoint;

    [Header("Audio")]
    [Tooltip("SFX played when a record starts playing.")]
    [SerializeField] private AudioClip _playSFX;

    [Tooltip("SFX played when a record is ejected/stopped.")]
    [SerializeField] private AudioClip _stopSFX;

    private PlaceableObject _loadedRecord;
    private RecordItem _loadedRecordItem;
    private Material _labelMat;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public RecordDefinition CurrentRecord => _loadedRecordItem != null ? _loadedRecordItem.Definition : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RecordSlot] Duplicate instance destroyed.");
            Destroy(this);
            return;
        }
        Instance = this;

        if (_discRenderer != null)
        {
            _labelMat = new Material(_discRenderer.sharedMaterial);
            _discRenderer.material = _labelMat;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_labelMat != null)
            Object.Destroy(_labelMat);
    }

    private void Update()
    {
        if (_isPlaying && _discVisual != null)
            _discVisual.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.Self);
    }

    /// <summary>
    /// Attempts to accept a held PlaceableObject as a record.
    /// Returns true if the record was accepted (has RecordItem component).
    /// </summary>
    public bool TryAcceptRecord(PlaceableObject held)
    {
        if (held == null) return false;

        var recordItem = held.GetComponent<RecordItem>();
        if (recordItem == null || recordItem.Definition == null) return false;

        // Eject current record if one is loaded
        if (_loadedRecord != null)
            EjectRecord();

        // Load the new record
        _loadedRecord = held;
        _loadedRecordItem = recordItem;

        // Disable physics and snap to turntable
        var rb = held.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable colliders so the record doesn't interfere with raycasts
        foreach (var col in held.GetComponents<Collider>())
            col.enabled = false;

        // Snap position
        if (_recordSnapPoint != null)
        {
            held.transform.position = _recordSnapPoint.position;
            held.transform.rotation = _recordSnapPoint.rotation;
        }
        else
        {
            held.transform.position = transform.position + Vector3.up * 0.02f;
            held.transform.rotation = Quaternion.identity;
        }

        held.transform.SetParent(transform);

        StartPlayback();
        return true;
    }

    /// <summary>
    /// Ejects the current record, re-enabling its physics and PlaceableObject state.
    /// </summary>
    public void EjectRecord()
    {
        if (_loadedRecord == null) return;

        StopPlayback();

        // Re-enable physics
        _loadedRecord.transform.SetParent(null);

        var rb = _loadedRecord.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Re-enable colliders
        foreach (var col in _loadedRecord.GetComponents<Collider>())
            col.enabled = true;

        _loadedRecord.OnDropped();

        Debug.Log($"[RecordSlot] Ejected '{_loadedRecordItem?.Definition?.title}'.");

        _loadedRecord = null;
        _loadedRecordItem = null;
    }

    /// <summary>
    /// Toggles playback on/off. Called by clicking the turntable while a record is loaded.
    /// </summary>
    public void TogglePlayback()
    {
        if (_loadedRecord == null) return;

        if (_isPlaying)
            StopPlayback();
        else
            StartPlayback();
    }

    /// <summary>
    /// Stops playback without ejecting the record. Called by GameClock / DayPhaseManager
    /// during phase transitions.
    /// </summary>
    public void Stop()
    {
        if (_isPlaying)
            StopPlayback();
    }

    private void StartPlayback()
    {
        if (_loadedRecordItem == null || _loadedRecordItem.Definition == null) return;

        _isPlaying = true;
        var def = _loadedRecordItem.Definition;

        // Apply label color
        if (_labelMat != null)
            _labelMat.color = def.labelColor;

        // Play music
        if (def.musicClip != null)
            AudioManager.Instance?.PlayMusic(def.musicClip, def.volume);

        // Feed mood machine
        MoodMachine.Instance?.SetSource("Music", def.moodValue);

        // Activate reactable tag
        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = true;

        AudioManager.Instance?.PlaySFX(_playSFX);
        OnRecordChanged?.Invoke();

        Debug.Log($"[RecordSlot] Playing: {def.title} by {def.artist}");
    }

    private void StopPlayback()
    {
        _isPlaying = false;

        AudioManager.Instance?.StopMusic();
        MoodMachine.Instance?.RemoveSource("Music");

        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = false;

        AudioManager.Instance?.PlaySFX(_stopSFX);

        Debug.Log("[RecordSlot] Stopped playback.");
    }
}
