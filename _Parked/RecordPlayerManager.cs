using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-scoped singleton managing the record player interaction.
/// 4-state FSM with physical sleeve browsing:
///   Idle → (hover stack) → Browsing → (click sleeve) → Selected → (click turntable) → Playing
/// </summary>
public class RecordPlayerManager : MonoBehaviour, IStationManager
{
    public enum State { Idle, Browsing, Selected, Playing }

    /// <summary>Fired when a record starts playing (new or changed).</summary>
    public static event System.Action OnRecordChanged;

    public static RecordPlayerManager Instance { get; private set; }

    // ──────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Records")]
    [Tooltip("Available records to browse and play.")]
    [SerializeField] private RecordDefinition[] records;

    [Header("Sleeves")]
    [Tooltip("Individual sleeve GameObjects (one per record, built by scene builder).")]
    [SerializeField] private Transform[] _sleeveTransforms;

    [Header("Visuals")]
    [Tooltip("Transform of the record disc visual (rotated during playback).")]
    [SerializeField] private Transform recordVisual;

    [Tooltip("Renderer on the record visual for changing label color.")]
    [SerializeField] private Renderer recordRenderer;

    [Tooltip("Rotation speed in degrees/second while playing.")]
    [SerializeField] private float rotationSpeed = 33.3f;

    [Header("Fan Animation")]
    [Tooltip("How far sleeves spread apart when fanned (world units along local X).")]
    [SerializeField] private float fanSpread = 0.04f;

    [Tooltip("Tilt angle applied to fanned sleeves (degrees around Z).")]
    [SerializeField] private float fanTiltAngle = 5f;

    [Tooltip("Animation speed for fan-out/fan-in (seconds).")]
    [SerializeField] private float fanDuration = 0.3f;

    [Tooltip("How far the hovered sleeve slides forward (local Z).")]
    [SerializeField] private float hoverSlideDistance = 0.03f;

    [Tooltip("How far the selected sleeve pops forward (local Z).")]
    [SerializeField] private float selectedPopDistance = 0.06f;

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

    [Tooltip("Layer mask for individual sleeve colliders.")]
    [SerializeField] private LayerMask _sleeveLayer;

    [Tooltip("Main camera (auto-found if null).")]
    [SerializeField] private Camera _mainCamera;

    // ──────────────────────────────────────────────────────────────
    // Inline InputActions
    // ──────────────────────────────────────────────────────────────
    private InputAction _clickAction;
    private InputAction _cancelAction;
    private InputAction _mousePosition;
    private InputAction _scrollAction;

    // ──────────────────────────────────────────────────────────────
    // Runtime state
    // ──────────────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Idle;
    public bool IsAtIdleState => CurrentState == State.Idle;

    private int _selectedIndex = -1;
    private int _browseHoverIndex = -1;
    private Material _labelMat;

    // Sleeve rest positions (stacked, stored at init)
    private Vector3[] _sleeveRestPositions;
    private Quaternion[] _sleeveRestRotations;
    // Sleeve fan-out target positions
    private Vector3[] _sleeveFanPositions;
    private Quaternion[] _sleeveFanRotations;
    // Sleeve materials for highlight
    private Material[] _sleeveMaterials;
    private Color[] _sleeveOriginalColors;

    // Fan animation state
    private float _fanProgress; // 0 = stacked, 1 = fanned
    private bool _fanningOut;
    private bool _fanningIn;

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

        _clickAction = new InputAction("RecordClick", InputActionType.Button, "<Mouse>/leftButton");
        _cancelAction = new InputAction("RecordCancel", InputActionType.Button, "<Mouse>/rightButton");
        _mousePosition = new InputAction("RecordPointer", InputActionType.Value, "<Mouse>/position");
        _scrollAction = new InputAction("RecordScroll", InputActionType.Value, "<Mouse>/scroll");

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        CacheSleeveData();
    }

    private void OnEnable()
    {
        _clickAction.Enable();
        _cancelAction.Enable();
        _mousePosition.Enable();
        _scrollAction.Enable();
    }

    private void OnDisable()
    {
        _clickAction.Disable();
        _cancelAction.Disable();
        _mousePosition.Disable();
        _scrollAction.Disable();

        if (CurrentState == State.Playing)
            StopPlayback();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_labelMat != null) Destroy(_labelMat);
        CleanupSleeveMaterials();
    }

    private void Update()
    {
        if (DayPhaseManager.Instance == null || !DayPhaseManager.Instance.IsInteractionPhase)
        {
            if (CurrentState == State.Playing)
                StopPlayback();
            return;
        }

        if (ObjectGrabber.IsHoldingObject) return;

        // Fan animation
        UpdateFanAnimation();

        switch (CurrentState)
        {
            case State.Idle:
                HandleIdleInput();
                break;
            case State.Browsing:
                HandleBrowsingInput();
                break;
            case State.Selected:
                HandleSelectedInput();
                break;
            case State.Playing:
                HandlePlayingInput();
                RotateRecord();
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Sleeve Data
    // ──────────────────────────────────────────────────────────────

    private void CacheSleeveData()
    {
        if (_sleeveTransforms == null || _sleeveTransforms.Length == 0) return;

        int count = _sleeveTransforms.Length;
        _sleeveRestPositions = new Vector3[count];
        _sleeveRestRotations = new Quaternion[count];
        _sleeveFanPositions = new Vector3[count];
        _sleeveFanRotations = new Quaternion[count];
        _sleeveMaterials = new Material[count];
        _sleeveOriginalColors = new Color[count];

        for (int i = 0; i < count; i++)
        {
            if (_sleeveTransforms[i] == null) continue;

            _sleeveRestPositions[i] = _sleeveTransforms[i].localPosition;
            _sleeveRestRotations[i] = _sleeveTransforms[i].localRotation;

            // Fan-out: spread along local X, slight Z tilt
            float centerOffset = (i - (count - 1) * 0.5f);
            _sleeveFanPositions[i] = _sleeveRestPositions[i]
                + new Vector3(centerOffset * fanSpread, 0f, 0f);
            _sleeveFanRotations[i] = _sleeveRestRotations[i]
                * Quaternion.Euler(0f, 0f, centerOffset * fanTiltAngle);

            // Cache material for highlight
            var rend = _sleeveTransforms[i].GetComponent<Renderer>();
            if (rend != null)
            {
                _sleeveMaterials[i] = rend.material; // instance material
                _sleeveOriginalColors[i] = _sleeveMaterials[i].color;
            }
        }
    }

    private void CleanupSleeveMaterials()
    {
        if (_sleeveMaterials == null) return;
        foreach (var mat in _sleeveMaterials)
        {
            if (mat != null) Destroy(mat);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Fan Animation
    // ──────────────────────────────────────────────────────────────

    private void StartFanOut()
    {
        _fanningOut = true;
        _fanningIn = false;
    }

    private void StartFanIn()
    {
        _fanningIn = true;
        _fanningOut = false;
    }

    private void UpdateFanAnimation()
    {
        if (_sleeveTransforms == null) return;

        if (_fanningOut)
        {
            _fanProgress = Mathf.MoveTowards(_fanProgress, 1f, Time.deltaTime / fanDuration);
            if (_fanProgress >= 1f) _fanningOut = false;
        }
        else if (_fanningIn)
        {
            _fanProgress = Mathf.MoveTowards(_fanProgress, 0f, Time.deltaTime / fanDuration);
            if (_fanProgress <= 0f) _fanningIn = false;
        }

        for (int i = 0; i < _sleeveTransforms.Length; i++)
        {
            if (_sleeveTransforms[i] == null) continue;

            Vector3 targetPos = Vector3.Lerp(_sleeveRestPositions[i], _sleeveFanPositions[i], _fanProgress);
            Quaternion targetRot = Quaternion.Slerp(_sleeveRestRotations[i], _sleeveFanRotations[i], _fanProgress);

            // Hover slide: push hovered sleeve forward
            if (CurrentState == State.Browsing && i == _browseHoverIndex)
                targetPos += _sleeveTransforms[i].parent != null
                    ? _sleeveTransforms[i].parent.InverseTransformDirection(Vector3.forward) * hoverSlideDistance
                    : Vector3.forward * hoverSlideDistance;

            // Selected pop: push selected sleeve further forward
            if (CurrentState == State.Selected && i == _selectedIndex)
                targetPos += _sleeveTransforms[i].parent != null
                    ? _sleeveTransforms[i].parent.InverseTransformDirection(Vector3.forward) * selectedPopDistance
                    : Vector3.forward * selectedPopDistance;

            _sleeveTransforms[i].localPosition = Vector3.Lerp(
                _sleeveTransforms[i].localPosition, targetPos, Time.deltaTime * 12f);
            _sleeveTransforms[i].localRotation = Quaternion.Slerp(
                _sleeveTransforms[i].localRotation, targetRot, Time.deltaTime * 12f);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Idle
    // ──────────────────────────────────────────────────────────────

    private void HandleIdleInput()
    {
        if (_mainCamera == null) return;

        Vector2 mousePos = _mousePosition.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        // Hover over vinyl stack or any sleeve → enter Browsing
        if (Physics.Raycast(ray, out _, 50f, _vinylStackLayer | _sleeveLayer))
        {
            EnterBrowsing();
            return;
        }

        // Click turntable while playing → stop
        if (_clickAction.WasPressedThisFrame() && Physics.Raycast(ray, out _, 50f, _turntableLayer))
        {
            // Nothing to do in Idle with turntable click
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Browsing
    // ──────────────────────────────────────────────────────────────

    private void EnterBrowsing()
    {
        CurrentState = State.Browsing;
        _browseHoverIndex = -1;
        _selectedIndex = -1;
        ShowHUDCanvas();
        StartFanOut();
        Debug.Log("[RecordPlayerManager] Entered Browsing.");
    }

    private void HandleBrowsingInput()
    {
        if (_mainCamera == null || records == null || records.Length == 0) return;

        Vector2 mousePos = _mousePosition.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        // Check if mouse is still over stack/sleeve area
        bool overStack = Physics.Raycast(ray, out _, 50f, _vinylStackLayer);
        int hoveredSleeve = RaycastSleeve(ray);

        if (!overStack && hoveredSleeve < 0)
        {
            // Mouse left the browsing area → return to Idle
            ExitBrowsing();
            return;
        }

        // Update hover highlight
        SetHoveredSleeve(hoveredSleeve);

        // Scroll to browse through records
        Vector2 scroll = _scrollAction.ReadValue<Vector2>();
        if (Mathf.Abs(scroll.y) > 0.1f)
        {
            int dir = scroll.y > 0 ? 1 : -1;
            CycleBrowseHover(dir);
        }

        // Click sleeve → select it
        if (_clickAction.WasPressedThisFrame() && _browseHoverIndex >= 0)
        {
            SelectRecord(_browseHoverIndex);
            return;
        }

        // RMB → cancel browsing
        if (_cancelAction.WasPressedThisFrame())
        {
            ExitBrowsing();
            return;
        }

        // Update HUD with hovered record info
        UpdateBrowseHUD();
    }

    private void ExitBrowsing()
    {
        ClearSleeveHighlights();
        _browseHoverIndex = -1;
        StartFanIn();
        CurrentState = State.Idle;
        if (hud != null) hud.UpdateDisplay("", "", false);
        Debug.Log("[RecordPlayerManager] Exited Browsing → Idle.");
    }

    private void CycleBrowseHover(int direction)
    {
        if (_sleeveTransforms == null || _sleeveTransforms.Length == 0) return;

        int count = Mathf.Min(records.Length, _sleeveTransforms.Length);
        if (_browseHoverIndex < 0)
            _browseHoverIndex = direction > 0 ? 0 : count - 1;
        else
            _browseHoverIndex = (_browseHoverIndex + direction + count) % count;

        SetHoveredSleeve(_browseHoverIndex);

        if (browseSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(browseSFX);
    }

    private void SetHoveredSleeve(int index)
    {
        if (index == _browseHoverIndex) return;

        // Clear previous highlight
        ClearSleeveHighlights();

        _browseHoverIndex = index;

        // Apply highlight to new hover
        if (index >= 0 && _sleeveMaterials != null && index < _sleeveMaterials.Length
            && _sleeveMaterials[index] != null)
        {
            _sleeveMaterials[index].color = _sleeveOriginalColors[index] * 1.4f;
        }
    }

    private void ClearSleeveHighlights()
    {
        if (_sleeveMaterials == null) return;
        for (int i = 0; i < _sleeveMaterials.Length; i++)
        {
            if (_sleeveMaterials[i] != null)
                _sleeveMaterials[i].color = _sleeveOriginalColors[i];
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Selected
    // ──────────────────────────────────────────────────────────────

    private void SelectRecord(int index)
    {
        _selectedIndex = Mathf.Clamp(index, 0, records.Length - 1);
        CurrentState = State.Selected;

        // Apply label color to disc preview
        ApplyRecordVisual();

        if (browseSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(browseSFX);

        UpdateSelectedHUD();
        Debug.Log($"[RecordPlayerManager] Selected: {records[_selectedIndex].title}");
    }

    private void HandleSelectedInput()
    {
        if (_mainCamera == null) return;

        Vector2 mousePos = _mousePosition.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        // RMB → deselect, back to Browsing
        if (_cancelAction.WasPressedThisFrame())
        {
            _selectedIndex = -1;
            CurrentState = State.Browsing;
            _browseHoverIndex = -1;
            UpdateBrowseHUD();
            Debug.Log("[RecordPlayerManager] Deselected → Browsing.");
            return;
        }

        if (!_clickAction.WasPressedThisFrame()) return;

        // Click turntable → place and play
        if (Physics.Raycast(ray, out _, 50f, _turntableLayer))
        {
            StartPlayback();
            return;
        }

        // Click a different sleeve → switch selection
        int clicked = RaycastSleeve(ray);
        if (clicked >= 0 && clicked != _selectedIndex)
        {
            SelectRecord(clicked);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Playing
    // ──────────────────────────────────────────────────────────────

    private void HandlePlayingInput()
    {
        if (_mainCamera == null || !_clickAction.WasPressedThisFrame()) return;

        Vector2 mousePos = _mousePosition.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        // Click turntable → stop, return to Idle
        if (Physics.Raycast(ray, out _, 50f, _turntableLayer))
        {
            StopPlayback();
            return;
        }

        // Click stack/sleeve → stop and go to Browsing
        if (Physics.Raycast(ray, out _, 50f, _vinylStackLayer | _sleeveLayer))
        {
            StopPlayback();
            EnterBrowsing();
        }
    }

    private void StartPlayback()
    {
        if (records == null || records.Length == 0) return;
        if (_selectedIndex < 0) _selectedIndex = 0;
        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, records.Length - 1);

        ShowHUDCanvas();
        var record = records[_selectedIndex];
        CurrentState = State.Playing;

        // Fan sleeves back in
        StartFanIn();
        ClearSleeveHighlights();

        if (record.musicClip != null)
        {
            audioSource.clip = record.musicClip;
            audioSource.volume = record.volume;
            audioSource.Play();
        }

        ApplyRecordVisual();
        MoodMachine.Instance?.SetSource("Music", record.moodValue);

        if (playSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(playSFX);

        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = true;

        OnRecordChanged?.Invoke();

        UpdatePlayingHUD();
        Debug.Log($"[RecordPlayerManager] Playing: {record.title} by {record.artist}");
    }

    public void StopPlayback()
    {
        CurrentState = State.Idle;
        _selectedIndex = -1;
        _browseHoverIndex = -1;
        audioSource.Stop();

        MoodMachine.Instance?.RemoveSource("Music");

        var reactable = GetComponent<ReactableTag>();
        if (reactable != null) reactable.IsActive = false;

        ClearSleeveHighlights();

        if (hud != null)
            hud.UpdateDisplay("", "", false);

        Debug.Log("[RecordPlayerManager] Stopped playback.");
    }

    private void RotateRecord()
    {
        if (recordVisual != null)
            recordVisual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }

    // ──────────────────────────────────────────────────────────────
    // Raycast helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>Returns the index of the sleeve hit by the ray, or -1.</summary>
    private int RaycastSleeve(Ray ray)
    {
        if (_sleeveTransforms == null || _sleeveLayer == 0) return -1;

        if (!Physics.Raycast(ray, out RaycastHit hit, 50f, _sleeveLayer)) return -1;

        for (int i = 0; i < _sleeveTransforms.Length; i++)
        {
            if (_sleeveTransforms[i] == null) continue;
            if (hit.collider.transform == _sleeveTransforms[i]
                || hit.collider.transform.IsChildOf(_sleeveTransforms[i]))
                return i;
        }

        return -1;
    }

    // ──────────────────────────────────────────────────────────────
    // Visuals
    // ──────────────────────────────────────────────────────────────

    private void ApplyRecordVisual()
    {
        if (records == null || records.Length == 0 || _selectedIndex < 0) return;

        var record = records[_selectedIndex];
        if (_labelMat != null)
            _labelMat.color = record.labelColor;
    }

    private void ShowHUDCanvas()
    {
        if (_hudCanvas != null && !_hudCanvas.gameObject.activeSelf)
            _hudCanvas.gameObject.SetActive(true);
    }

    // ──────────────────────────────────────────────────────────────
    // HUD Updates
    // ──────────────────────────────────────────────────────────────

    private void UpdateBrowseHUD()
    {
        if (hud == null) return;

        if (_browseHoverIndex >= 0 && _browseHoverIndex < records.Length)
        {
            var record = records[_browseHoverIndex];
            hud.ShowBrowseMode(record.title);
        }
        else
        {
            hud.ShowBrowseMode("");
        }
    }

    private void UpdateSelectedHUD()
    {
        if (hud == null || _selectedIndex < 0) return;

        var record = records[_selectedIndex];
        string moodDesc = record.moodValue < 0.3f ? "Sunny" :
                          record.moodValue < 0.6f ? "Mellow" : "Stormy";
        hud.ShowSelectedMode(record.title, record.artist, moodDesc);
    }

    private void UpdatePlayingHUD()
    {
        if (hud == null || _selectedIndex < 0) return;

        var record = records[_selectedIndex];
        hud.ShowPlayingMode(record.title, record.artist);
    }
}
