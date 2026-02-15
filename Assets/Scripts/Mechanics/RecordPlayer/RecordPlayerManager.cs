using UnityEngine;
using UnityEngine.InputSystem;

public class RecordPlayerManager : MonoBehaviour, IStationManager
{
    public enum State { Browsing, Playing }

    public static RecordPlayerManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Records")]
    [Tooltip("Available records to browse and play.")]
    [SerializeField] private RecordDefinition[] records;

    [Header("Visuals")]
    [Tooltip("Transform of the record disc visual (rotated during playback).")]
    [SerializeField] private Transform recordVisual;

    [Tooltip("Renderer on the record visual for changing label color.")]
    [SerializeField] private Renderer recordRenderer;

    [Tooltip("Rotation speed in degrees/second while playing.")]
    [SerializeField] private float rotationSpeed = 33.3f;

    [Header("Audio")]
    [Tooltip("AudioSource for music playback. Auto-added if null.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("SFX played when browsing/cycling to a new record.")]
    [SerializeField] private AudioClip browseSFX;

    [Tooltip("SFX played when starting playback (changing song).")]
    [SerializeField] private AudioClip playSFX;

    [Header("HUD")]
    [Tooltip("RecordPlayerHUD component.")]
    [SerializeField] private RecordPlayerHUD hud;

    [Tooltip("HUD canvas — hidden until the player interacts.")]
    [SerializeField] private Canvas _hudCanvas;

    [Header("Click Interaction")]
    [Tooltip("Layer mask for the vinyl stack (click to browse).")]
    [SerializeField] private LayerMask _vinylStackLayer;

    [Tooltip("Layer mask for the turntable/disc (click to play/stop).")]
    [SerializeField] private LayerMask _turntableLayer;

    [Tooltip("Main camera (auto-found if null).")]
    [SerializeField] private Camera _mainCamera;

    // ──────────────────────────────────────────────────────────────
    // Inline InputActions
    // ──────────────────────────────────────────────────────────────
    private InputAction _playStopAction;
    private InputAction _clickAction;
    private InputAction _mousePosition;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Browsing;
    public bool IsAtIdleState => CurrentState == State.Browsing;

    private int _currentRecordIndex;
    private Material _labelMat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RecordPlayerManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;

        if (recordRenderer != null)
        {
            _labelMat = new Material(recordRenderer.sharedMaterial);
            recordRenderer.material = _labelMat;
        }

        _playStopAction = new InputAction("PlayStop", InputActionType.Button);
        _playStopAction.AddBinding("<Keyboard>/space");

        _clickAction = new InputAction("RecordClick", InputActionType.Button, "<Mouse>/leftButton");
        _mousePosition = new InputAction("RecordPointer", InputActionType.Value, "<Mouse>/position");

        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        _playStopAction.Enable();
        _clickAction.Enable();
        _mousePosition.Enable();
    }

    private void OnDisable()
    {
        _playStopAction.Disable();
        _clickAction.Disable();
        _mousePosition.Disable();

        // Stop playback when disabled
        if (CurrentState == State.Playing)
            StopPlayback();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_labelMat != null) Destroy(_labelMat);
    }

    private void Update()
    {
        if (DayPhaseManager.Instance == null || !DayPhaseManager.Instance.IsInteractionPhase)
        {
            if (CurrentState == State.Playing)
                StopPlayback();
            return;
        }

        switch (CurrentState)
        {
            case State.Browsing:
                HandleBrowsingInput();
                break;

            case State.Playing:
                HandlePlayingInput();
                RotateRecord();
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Browsing
    // ──────────────────────────────────────────────────────────────

    private void HandleBrowsingInput()
    {
        if (records == null || records.Length == 0) return;

        if (_playStopAction.WasPressedThisFrame())
            StartPlayback();

        HandleClickInput();
    }

    private void CycleRecord(int direction)
    {
        _currentRecordIndex = (_currentRecordIndex + direction + records.Length) % records.Length;
        ApplyRecordVisual();

        if (browseSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(browseSFX);

        Debug.Log($"[RecordPlayerManager] Browsing: {records[_currentRecordIndex].title}");
    }

    // ──────────────────────────────────────────────────────────────
    // Playing
    // ──────────────────────────────────────────────────────────────

    private void HandlePlayingInput()
    {
        if (_playStopAction.WasPressedThisFrame())
            StopPlayback();

        HandleClickInput();
    }

    private void HandleClickInput()
    {
        if (_mainCamera == null || !_clickAction.WasPressedThisFrame()) return;

        Vector2 mousePos = _mousePosition.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        // Click vinyl stack → cycle to next record
        if (Physics.Raycast(ray, out _, 50f, _vinylStackLayer))
        {
            if (CurrentState == State.Playing)
                StopPlayback();
            ShowHUDCanvas();
            CycleRecord(1);
            return;
        }

        // Click turntable → toggle play/stop
        if (Physics.Raycast(ray, out _, 50f, _turntableLayer))
        {
            if (CurrentState == State.Playing)
                StopPlayback();
            else
                StartPlayback();
        }
    }

    private void ShowHUDCanvas()
    {
        if (_hudCanvas != null && !_hudCanvas.gameObject.activeSelf)
            _hudCanvas.gameObject.SetActive(true);
    }

    private void StartPlayback()
    {
        if (records == null || records.Length == 0) return;
        _currentRecordIndex = Mathf.Clamp(_currentRecordIndex, 0, records.Length - 1);

        ShowHUDCanvas();
        var record = records[_currentRecordIndex];
        CurrentState = State.Playing;

        if (record.musicClip != null)
        {
            audioSource.clip = record.musicClip;
            audioSource.volume = record.volume;
            audioSource.Play();
        }

        MoodMachine.Instance?.SetSource("Music", record.moodValue);

        if (playSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(playSFX);

        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = true;

        UpdateHUD();
        Debug.Log($"[RecordPlayerManager] Playing: {record.title} by {record.artist}");
    }

    private void StopPlayback()
    {
        CurrentState = State.Browsing;
        audioSource.Stop();

        MoodMachine.Instance?.RemoveSource("Music");

        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = false;

        UpdateHUD();
        Debug.Log("[RecordPlayerManager] Stopped playback.");
    }

    private void RotateRecord()
    {
        if (recordVisual != null)
            recordVisual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }

    // ──────────────────────────────────────────────────────────────
    // Visuals
    // ──────────────────────────────────────────────────────────────

    private void ApplyRecordVisual()
    {
        if (records == null || records.Length == 0) return;

        var record = records[_currentRecordIndex];
        if (_labelMat != null)
            _labelMat.color = record.labelColor;

        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (hud == null || records == null || records.Length == 0) return;
        var record = records[_currentRecordIndex];
        hud.UpdateDisplay(record.title, record.artist, CurrentState == State.Playing);
    }
}
