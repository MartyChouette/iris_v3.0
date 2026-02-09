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

    [Header("HUD")]
    [Tooltip("RecordPlayerHUD component.")]
    [SerializeField] private RecordPlayerHUD hud;

    // ──────────────────────────────────────────────────────────────
    // Inline InputActions
    // ──────────────────────────────────────────────────────────────
    private InputAction _navLeftAction;
    private InputAction _navRightAction;
    private InputAction _playStopAction;

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

        _navLeftAction = new InputAction("NavLeft", InputActionType.Button);
        _navLeftAction.AddBinding("<Keyboard>/a");
        _navLeftAction.AddBinding("<Keyboard>/leftArrow");

        _navRightAction = new InputAction("NavRight", InputActionType.Button);
        _navRightAction.AddBinding("<Keyboard>/d");
        _navRightAction.AddBinding("<Keyboard>/rightArrow");

        _playStopAction = new InputAction("PlayStop", InputActionType.Button);
        _playStopAction.AddBinding("<Keyboard>/enter");
        _playStopAction.AddBinding("<Keyboard>/space");
    }

    private void OnEnable()
    {
        _navLeftAction.Enable();
        _navRightAction.Enable();
        _playStopAction.Enable();
    }

    private void OnDisable()
    {
        _navLeftAction.Disable();
        _navRightAction.Disable();
        _playStopAction.Disable();

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

        if (_navLeftAction.WasPressedThisFrame())
            CycleRecord(-1);
        else if (_navRightAction.WasPressedThisFrame())
            CycleRecord(1);

        if (_playStopAction.WasPressedThisFrame())
            StartPlayback();
    }

    private void CycleRecord(int direction)
    {
        _currentRecordIndex = (_currentRecordIndex + direction + records.Length) % records.Length;
        ApplyRecordVisual();
        Debug.Log($"[RecordPlayerManager] Browsing: {records[_currentRecordIndex].title}");
    }

    // ──────────────────────────────────────────────────────────────
    // Playing
    // ──────────────────────────────────────────────────────────────

    private void HandlePlayingInput()
    {
        if (_playStopAction.WasPressedThisFrame())
            StopPlayback();
    }

    private void StartPlayback()
    {
        if (records == null || records.Length == 0) return;

        var record = records[_currentRecordIndex];
        CurrentState = State.Playing;

        if (record.musicClip != null)
        {
            audioSource.clip = record.musicClip;
            audioSource.volume = record.volume;
            audioSource.Play();
        }

        UpdateHUD();
        Debug.Log($"[RecordPlayerManager] Playing: {record.title} by {record.artist}");
    }

    private void StopPlayback()
    {
        CurrentState = State.Browsing;
        audioSource.Stop();
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
